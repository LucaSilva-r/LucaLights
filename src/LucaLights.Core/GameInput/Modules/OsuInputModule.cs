using System.Threading.Channels;
using LucaLights.Core.Models;

namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule : IGameInputModule, IDisposable
{
    public const string ModuleIdValue  = "osu";
    public const int    MaxManiaColumns = 18;

    private static readonly Lazy<InputDefinition> DefinitionLazy = new(BuildDefinition);
    private static readonly TimeSpan ProcessPollInterval = TimeSpan.FromSeconds(1);

    // Config
    internal readonly string          _tosuUrl;
    internal readonly bool            _autoManageProcess;
    internal readonly Action<string>? _log;

    // Lifecycle
    private readonly object               _syncRoot       = new();
    private          CancellationTokenSource? _run        = null;
    private          Task?                _backgroundTask = null;
    private          bool                 _disposed       = false;
    private          long                 _sequence       = 0;
    private          volatile bool        _osuProcessRunning = false;

    // Snapshot dispatch channel — WebSocket/note-engine threads write here instead of invoking
    // SnapshotUpdated directly, so they can never be blocked by a slow event handler.
    private readonly Channel<InputSnapshot> _snapshotDispatch = Channel.CreateBounded<InputSnapshot>(
        new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    // Snapshot state — all protected by _syncRoot
    private InputSnapshot _latestSnapshot = InputSnapshot.Empty;
    private bool          _v2Connected    = false;
    private TosuV2Data?   _latestV2       = null;

    // Note engine output — updated by NoteEngine partial, read by BuildSnapshot
    // Mania columns: level reflects active holds; pulse pending latches on circle onsets.
    internal readonly bool[] _columnLevel        = new bool[MaxManiaColumns];
    internal readonly bool[] _columnPulsePending = new bool[MaxManiaColumns];
    internal          bool   _noteActive      = false;
    internal          bool   _taikoDon        = false;
    internal          bool   _taikoKat        = false;
    internal          bool   _taikoDrumroll   = false;
    internal          bool   _taikoDenden     = false;

    internal          bool   _standardCircle  = false;
    internal          bool   _standardSlider  = false;
    internal          bool   _standardSpinner = false;
    internal          bool   _catchFruit      = false;

    // Key press state — updated by precise WebSocket
    internal bool _k1Pressed = false;
    internal bool _k2Pressed = false;
    internal bool _m1Pressed = false;
    internal bool _m2Pressed = false;

    // Key hit state (pulse) — updated by precise WebSocket based on counter change
    internal int            _k1Count   = -1;
    internal int            _k2Count   = -1;
    internal int            _m1Count   = -1;
    internal int            _m2Count   = -1;
    // Pulse flags — set on counter change, cleared when consumed by BuildSnapshot.
    internal bool _k1HitPending;
    internal bool _k2HitPending;
    internal bool _m1HitPending;
    internal bool _m2HitPending;

    public OsuInputModule(string tosuUrl, bool autoManageProcess = true, Action<string>? log = null)
    {
        _tosuUrl           = tosuUrl.TrimEnd('/');
        _autoManageProcess = autoManageProcess;
        _log               = log;

        if (_autoManageProcess)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => StopTosuProcess();
        }
    }

    public string ModuleId    => ModuleIdValue;
    public string DisplayName => "osu!";

    public event Action<InputSnapshot>? SnapshotUpdated;

    public static OsuInputModule CreateFromSettings(Settings settings, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var ms  = settings.GetOrCreateInputModuleSettings(ModuleIdValue);
        var url = ms["tosuUrl"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url)) url = "ws://127.0.0.1:24050";
        var autoManage = ms["autoManage"]?.GetValue<bool>() ?? true;
        return new OsuInputModule(url, autoManage, log);
    }

    public InputDefinition GetDefinition() => DefinitionLazy.Value;

    public InputSnapshot GetLatestSnapshot()
    {
        lock (_syncRoot) return _latestSnapshot;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        lock (_syncRoot)
        {
            if (_backgroundTask is not null) return Task.CompletedTask;
            _run            = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(() => RunAll(_run.Token), CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        Task?                    task;
        CancellationTokenSource? run;
        lock (_syncRoot)
        {
            task            = _backgroundTask;
            run             = _run;
            _backgroundTask = null;
            _run            = null;
        }

        if (task is null) return;

        run?.Cancel();
        StopNoteEngine();
        StopTosuProcess();

        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (TimeoutException) { _log?.Invoke("osu: stop timed out."); }
        catch (Exception ex)    { _log?.Invoke($"osu: stop error: {ex.Message}"); }
        finally
        {
            run?.Dispose();
            PublishDisconnectedSnapshot();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _tosuStartupLock.Dispose();
        _disposed = true;
    }

    private async Task RunAll(CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(
                RunV2LoopAsync(ct),
                RunPreciseLoopAsync(ct),
                DispatchSnapshotsAsync(ct));
        }
        finally
        {
            _snapshotDispatch.Writer.TryComplete();
            if (_autoManageProcess)
                StopTosuProcess();
        }
    }

    private async Task DispatchSnapshotsAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var snapshot in _snapshotDispatch.Reader.ReadAllAsync(ct))
                SnapshotUpdated?.Invoke(snapshot);
        }
        catch (OperationCanceledException) { }
    }

    // Called from any background task after updating shared state.
    // Acquires _syncRoot, builds snapshot, stores it, then enqueues for dispatch.
    // Returns immediately — never blocks on event handlers.
    internal void PublishCurrentSnapshot()
    {
        InputSnapshot snapshot;
        lock (_syncRoot)
        {
            snapshot        = BuildSnapshot();
            _latestSnapshot = snapshot;
        }
        _snapshotDispatch.Writer.TryWrite(snapshot);
    }

    // Renderer signals a frame has consumed the current snapshot. Clear the pulse latches
    // and refresh _latestSnapshot so a later sample without an intervening publish cannot
    // re-deliver the same pulse.
    public void AcknowledgePulses(InputSnapshot consumed)
    {
        static bool Observed(InputSnapshot s, string key) =>
            s.BoolValues.TryGetValue(key, out var v) && v;

        lock (_syncRoot)
        {
            bool clearedAny = false;
            if (_k1HitPending  && Observed(consumed, "raw.osu.keys.k1_hit")) { _k1HitPending  = false; clearedAny = true; }
            if (_k2HitPending  && Observed(consumed, "raw.osu.keys.k2_hit")) { _k2HitPending  = false; clearedAny = true; }
            if (_m1HitPending  && Observed(consumed, "raw.osu.keys.m1_hit")) { _m1HitPending  = false; clearedAny = true; }
            if (_m2HitPending  && Observed(consumed, "raw.osu.keys.m2_hit")) { _m2HitPending  = false; clearedAny = true; }

            // Tap-type note pulses — latched one-frame signals driven by NoteEngine onsets.
            // Slider/spinner/hold remain level (not touched here).
            if (_taikoDon       && Observed(consumed, "raw.osu.taiko.don"))        { _taikoDon       = false; clearedAny = true; }
            if (_taikoKat       && Observed(consumed, "raw.osu.taiko.kat"))        { _taikoKat       = false; clearedAny = true; }
            if (_standardCircle && Observed(consumed, "raw.osu.standard.circle")) { _standardCircle = false; clearedAny = true; }
            if (_catchFruit     && Observed(consumed, "raw.osu.catch.fruit"))     { _catchFruit     = false; clearedAny = true; }

            for (var i = 0; i < MaxManiaColumns; i++)
            {
                if (_columnPulsePending[i] && Observed(consumed, $"raw.osu.mania.col_{i}"))
                {
                    _columnPulsePending[i] = false;
                    clearedAny = true;
                }
            }

            if (clearedAny)
                _latestSnapshot = BuildSnapshot();
        }
    }

    private void PublishDisconnectedSnapshot()
    {
        InputSnapshot snap;
        lock (_syncRoot)
        {
            snap = new InputSnapshot
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Sequence     = ++_sequence,
                IsConnected  = false,
                IsActive     = false,
                BoolValues   = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["raw.osu.connected"] = false
                },
                FloatValues  = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                Metadata     = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            _latestSnapshot = snap;
        }
        SnapshotUpdated?.Invoke(snap);
    }

    // Must be called under _syncRoot lock.
    private InputSnapshot BuildSnapshot()
    {
        var v2        = _latestV2;
        var connected = _v2Connected;
        var stateNum  = v2?.State.Number ?? -1;
        var playing   = stateNum == TosuStateNumber.Playing;
        var inMenu    = stateNum == TosuStateNumber.MainMenu;
        var inSelect  = stateNum == TosuStateNumber.SelectPlay || stateNum == TosuStateNumber.SelectEdit;
        var inResults = stateNum == TosuStateNumber.Ranking;
        // Mode is driven by the beatmap loaded in the note engine, so effects follow the
        // currently previewed beatmap (e.g. while browsing song select).
        var mode      = _currentMode;
        // Pulse flags stay latched until AcknowledgePulses() is called by the renderer.
        var k1Hit = _k1HitPending;
        var k2Hit = _k2HitPending;
        var m1Hit = _m1HitPending;
        var m2Hit = _m2HitPending;

        var bools = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw.osu.connected"]        = connected,
            ["raw.osu.playing"]          = playing,
            ["raw.osu.state.menu"]       = inMenu,
            ["raw.osu.state.song_select"]= inSelect,
            ["raw.osu.state.results"]    = inResults,
            ["raw.osu.paused"]           = v2?.Game.Paused ?? false,
            ["raw.osu.failed"]           = v2?.Play.Failed ?? false,
            ["raw.osu.is_kiai"]          = v2?.Beatmap.IsKiai ?? false,
            ["raw.osu.is_break"]         = v2?.Beatmap.IsBreak ?? false,
            ["raw.osu.keys.k1"]          = _k1Pressed,
            ["raw.osu.keys.k2"]          = _k2Pressed,
            ["raw.osu.keys.m1"]          = _m1Pressed,
            ["raw.osu.keys.m2"]          = _m2Pressed,
            ["raw.osu.keys.k1_hit"]      = k1Hit,
            ["raw.osu.keys.k2_hit"]      = k2Hit,
            ["raw.osu.keys.m1_hit"]      = m1Hit,
            ["raw.osu.keys.m2_hit"]      = m2Hit,
            ["raw.osu.note_active"]      = _noteActive,
            ["raw.osu.taiko.don"]        = _taikoDon,
            ["raw.osu.taiko.kat"]        = _taikoKat,
            ["raw.osu.taiko.drumroll"]   = _taikoDrumroll,
            ["raw.osu.taiko.denden"]     = _taikoDenden,
            ["raw.osu.standard.circle"]  = _standardCircle,
            ["raw.osu.standard.slider"]  = _standardSlider,
            ["raw.osu.standard.spinner"] = _standardSpinner,
            ["raw.osu.catch.fruit"]      = _catchFruit,
            ["raw.osu.mode.standard"]    = mode == TosuModeNumber.Standard,
            ["raw.osu.mode.taiko"]       = mode == TosuModeNumber.Taiko,
            ["raw.osu.mode.catch"]       = mode == TosuModeNumber.Catch,
            ["raw.osu.mode.mania"]       = mode == TosuModeNumber.Mania,
        };

        // Mode-specific user inputs
        if (mode == TosuModeNumber.Standard)
        {
            bools["raw.osu.standard.user.k1"] = _k1Pressed;
            bools["raw.osu.standard.user.k2"] = _k2Pressed;
            bools["raw.osu.standard.user.m1"] = _m1Pressed;
            bools["raw.osu.standard.user.m2"] = _m2Pressed;
        }
        else if (mode == TosuModeNumber.Taiko)
        {
            bools["raw.osu.taiko.user.k1"] = k1Hit;
            bools["raw.osu.taiko.user.k2"] = k2Hit;
            bools["raw.osu.taiko.user.m1"] = m1Hit;
            bools["raw.osu.taiko.user.m2"] = m2Hit;
            // Descriptive aliases for Taiko
            bools["raw.osu.taiko.user.left_kat"]  = k1Hit;
            bools["raw.osu.taiko.user.left_don"]  = k2Hit;
            bools["raw.osu.taiko.user.right_don"] = m1Hit;
            bools["raw.osu.taiko.user.right_kat"] = m2Hit;
        }
        else if (mode == TosuModeNumber.Catch)
        {
            bools["raw.osu.catch.user.k1"] = _k1Pressed;
            bools["raw.osu.catch.user.k2"] = _k2Pressed;
            bools["raw.osu.catch.user.m1"] = _m1Pressed;
            bools["raw.osu.catch.user.m2"] = _m2Pressed;
        }
        else if (mode == TosuModeNumber.Mania)
        {
            bools["raw.osu.mania.user.k1"] = _k1Pressed;
            bools["raw.osu.mania.user.k2"] = _k2Pressed;
            bools["raw.osu.mania.user.m1"] = _m1Pressed;
            bools["raw.osu.mania.user.m2"] = _m2Pressed;
        }

        for (var i = 0; i < MaxManiaColumns; i++)
            bools[$"raw.osu.mania.col_{i}"] = _columnLevel[i] || _columnPulsePending[i];

        var timeMs  = v2?.Beatmap.Time.Live      ?? 0;
        var totalMs = v2?.Beatmap.Time.Mp3Length ?? 1;

        var floats = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw.osu.hp"]            = v2?.Play.HealthBar.Normal  ?? 0f,
            ["raw.osu.hp_smooth"]     = v2?.Play.HealthBar.Smooth  ?? 0f,
            ["raw.osu.combo"]         = (float)(v2?.Play.Combo.Current ?? 0),
            ["raw.osu.combo_max"]     = (float)(v2?.Play.Combo.Max     ?? 0),
            ["raw.osu.accuracy"]      = v2?.Play.Accuracy          ?? 0f,
            ["raw.osu.score"]         = (float)(v2?.Play.Score     ?? 0),
            ["raw.osu.pp.current"]    = v2?.Play.Pp.Current        ?? 0f,
            ["raw.osu.pp.fc"]         = v2?.Play.Pp.Fc             ?? 0f,
            ["raw.osu.progress"]      = totalMs > 0 ? Math.Clamp((float)timeMs / totalMs, 0f, 1f) : 0f,
            ["raw.osu.stars"]         = v2?.Beatmap.Stats.Stars.Live ?? 0f,
            ["raw.osu.bpm"]           = v2?.Beatmap.Stats.Bpm.Realtime ?? 0f,
            ["raw.osu.unstable_rate"] = v2?.Play.UnstableRate      ?? 0f,
        };

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw.osu.beatmap_checksum"] = v2?.Beatmap.Checksum ?? string.Empty,
            ["raw.osu.mode_id"]          = mode.ToString(),
            ["raw.osu.debug.v2_state"]   = v2?.State.Number.ToString() ?? "none",
            ["raw.osu.debug.note_count"] = _hitObjects.Count.ToString()
        };

        if (v2 != null && !string.IsNullOrEmpty(v2.Beatmap.Checksum))
        {
            var modeSuffix = v2.Beatmap.Mode.Number == TosuModeNumber.Mania
                ? $" ({(int)v2.Beatmap.Stats.Cs.Original}K)" : string.Empty;
            metadata["raw.osu.now_playing"] =
                $"{v2.Beatmap.Artist} - {v2.Beatmap.Title} [{v2.Beatmap.Version}]{modeSuffix}";
        }

        // Module is active if connected AND (tosu says playing OR there is any current activity)
        var hasAnyActivity = _noteActive || k1Hit || k2Hit || m1Hit || m2Hit || _k1Pressed || _k2Pressed || _m1Pressed || _m2Pressed;

        return new InputSnapshot
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Sequence     = ++_sequence,
            IsConnected  = connected,
            IsActive     = connected && (playing || hasAnyActivity),
            BoolValues   = bools,
            FloatValues  = floats,
            Metadata     = metadata
        };
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    // ---------------------------------------------------------------------------
    // Channel definition
    // ---------------------------------------------------------------------------

    private static InputDefinition BuildDefinition()
    {
        var def = new InputDefinition { ModuleId = ModuleIdValue, DisplayName = "osu!" };

        // System
        def.Channels.Add(Bool("raw.osu.connected",  "Connected", "System", "Raw / System", "True while osu! is running and tosu v2 data is flowing."));
        def.Channels.Add(Bool("raw.osu.playing",    "Playing",   "System", "Raw / System", "True while actively in gameplay (state = playing)."));
        def.Channels.Add(Bool("raw.osu.paused",     "Paused",    "System", "Raw / System", "True while the game is paused."));
        def.Channels.Add(Bool("raw.osu.failed",     "Failed",    "System", "Raw / System", "True after the player fails."));
        def.Channels.Add(Bool("raw.osu.state.menu",        "Main Menu",   "System", "Raw / System", "True while in the osu! main menu."));
        def.Channels.Add(Bool("raw.osu.state.song_select", "Song Select", "System", "Raw / System", "True while in the song select screen."));
        def.Channels.Add(Bool("raw.osu.state.results",     "Results",     "System", "Raw / System", "True while on the results/ranking screen."));

        // Beatmap state
        def.Channels.Add(Bool("raw.osu.is_kiai",  "Kiai",  "Beatmap", "Raw / Beatmap", "True during kiai (chorus/hype) sections."));
        def.Channels.Add(Bool("raw.osu.is_break", "Break", "Beatmap", "Raw / Beatmap", "True during break sections."));

        // Generic Key presses (Always active)
        def.Channels.Add(Bool("raw.osu.keys.k1", "K1", "Keys (Generic)", "Raw / Keys", "Keyboard key 1 is pressed."));
        def.Channels.Add(Bool("raw.osu.keys.k2", "K2", "Keys (Generic)", "Raw / Keys", "Keyboard key 2 is pressed."));
        def.Channels.Add(Bool("raw.osu.keys.m1", "M1", "Keys (Generic)", "Raw / Keys", "Mouse button 1 is pressed."));
        def.Channels.Add(Bool("raw.osu.keys.m2", "M2", "Keys (Generic)", "Raw / Keys", "Mouse button 2 is pressed."));

        def.Channels.Add(Bool("raw.osu.keys.k1_hit", "K1 Hit", "Keys (Generic)", "Raw / Keys", "Pulse triggered when Keyboard key 1 is hit."));
        def.Channels.Add(Bool("raw.osu.keys.k2_hit", "K2 Hit", "Keys (Generic)", "Raw / Keys", "Pulse triggered when Keyboard key 2 is hit."));
        def.Channels.Add(Bool("raw.osu.keys.m1_hit", "M1 Hit", "Keys (Generic)", "Raw / Keys", "Pulse triggered when Mouse button 1 is hit."));
        def.Channels.Add(Bool("raw.osu.keys.m2_hit", "M2 Hit", "Keys (Generic)", "Raw / Keys", "Pulse triggered when Mouse button 2 is hit."));

        // Mode-specific User Inputs
        def.Channels.Add(Bool("raw.osu.standard.user.k1", "Standard K1", "Standard Keys", "Raw / Keys", "Standard K1 (only in Standard mode)."));
        def.Channels.Add(Bool("raw.osu.standard.user.k2", "Standard K2", "Standard Keys", "Raw / Keys", "Standard K2 (only in Standard mode)."));

        def.Channels.Add(Bool("raw.osu.taiko.user.left_kat",  "Taiko Left Kat",  "Taiko Keys", "Raw / Keys", "Taiko Left Rim (only in Taiko mode)."));
        def.Channels.Add(Bool("raw.osu.taiko.user.left_don",  "Taiko Left Don",  "Taiko Keys", "Raw / Keys", "Taiko Left Centre (only in Taiko mode)."));
        def.Channels.Add(Bool("raw.osu.taiko.user.right_don", "Taiko Right Don", "Taiko Keys", "Raw / Keys", "Taiko Right Centre (only in Taiko mode)."));
        def.Channels.Add(Bool("raw.osu.taiko.user.right_kat", "Taiko Right Kat", "Taiko Keys", "Raw / Keys", "Taiko Right Rim (only in Taiko mode)."));

        def.Channels.Add(Bool("raw.osu.mania.user.k1", "Mania K1", "Mania Keys", "Raw / Keys", "Mania K1 (only in Mania mode)."));
        def.Channels.Add(Bool("raw.osu.mania.user.k2", "Mania K2", "Mania Keys", "Raw / Keys", "Mania K2 (only in Mania mode)."));
        def.Channels.Add(Bool("raw.osu.mania.user.m1", "Mania M1", "Mania Keys", "Raw / Keys", "Mania M1 (only in Mania mode)."));
        def.Channels.Add(Bool("raw.osu.mania.user.m2", "Mania M2", "Mania Keys", "Raw / Keys", "Mania M2 (only in Mania mode)."));

        def.Channels.Add(Bool("raw.osu.catch.user.k1", "Catch K1", "Catch Keys", "Raw / Keys", "Catch K1 (only in Catch mode)."));
        def.Channels.Add(Bool("raw.osu.catch.user.k2", "Catch K2", "Catch Keys", "Raw / Keys", "Catch K2 (only in Catch mode)."));

        // Notes (from .osu file timing engine)
        def.Channels.Add(Bool("raw.osu.note_active", "Note Active", "Notes", "Raw / Notes", "True when any hit object is active at the current song time."));
        def.Channels.Add(Bool("raw.osu.taiko.don",      "Taiko Don",      "Notes", "Raw / Notes", "One-frame pulse on each don (centre/red) note onset."));
        def.Channels.Add(Bool("raw.osu.taiko.kat",      "Taiko Kat",      "Notes", "Raw / Notes", "One-frame pulse on each kat (rim/blue) note onset."));
        def.Channels.Add(Bool("raw.osu.taiko.drumroll", "Taiko Drumroll", "Notes", "Raw / Notes", "True while a drumroll (yellow slider) is active."));
        def.Channels.Add(Bool("raw.osu.taiko.denden",   "Taiko Denden",   "Notes", "Raw / Notes", "True while a denden (spinner) is active."));

        // Standard
        def.Channels.Add(Bool("raw.osu.standard.circle",  "Standard Circle",  "Notes", "Raw / Notes", "One-frame pulse on each standard hit circle onset."));
        def.Channels.Add(Bool("raw.osu.standard.slider",  "Standard Slider",  "Notes", "Raw / Notes", "True when a standard slider is active."));
        def.Channels.Add(Bool("raw.osu.standard.spinner", "Standard Spinner", "Notes", "Raw / Notes", "True when a standard spinner is active."));

        // Catch
        def.Channels.Add(Bool("raw.osu.catch.fruit", "Catch Fruit", "Notes", "Raw / Notes", "One-frame pulse on each catch fruit/droplet onset."));

        // Mania columns
        for (var i = 0; i < MaxManiaColumns; i++)
            def.Channels.Add(Bool($"raw.osu.mania.col_{i}", $"Mania Col {i}", "Mania Columns",
                "Raw / Notes", $"Pulse on circle onset in mania column {i}; level while a hold in column {i} is active."));

        // Mode
        def.Channels.Add(Bool("raw.osu.mode.standard", "Standard", "Mode", "Raw / Mode", "True when playing osu!standard."));
        def.Channels.Add(Bool("raw.osu.mode.taiko",    "Taiko",    "Mode", "Raw / Mode", "True when playing osu!taiko."));
        def.Channels.Add(Bool("raw.osu.mode.catch",    "Catch",    "Mode", "Raw / Mode", "True when playing osu!catch."));
        def.Channels.Add(Bool("raw.osu.mode.mania",    "Mania",    "Mode", "Raw / Mode", "True when playing osu!mania."));

        // Gameplay floats
        def.Channels.Add(Float("raw.osu.hp",            "HP",            "Gameplay", "Raw / Gameplay", "Health bar value (0–1).",              0f, 0f, 1f));
        def.Channels.Add(Float("raw.osu.hp_smooth",     "HP Smooth",     "Gameplay", "Raw / Gameplay", "Smoothed health bar value (0–1).",      0f, 0f, 1f));
        def.Channels.Add(Float("raw.osu.combo",         "Combo",         "Gameplay", "Raw / Gameplay", "Current combo count.",                  0f, 0f, null));
        def.Channels.Add(Float("raw.osu.combo_max",     "Combo Max",     "Gameplay", "Raw / Gameplay", "Max combo this play.",                  0f, 0f, null));
        def.Channels.Add(Float("raw.osu.accuracy",      "Accuracy",      "Gameplay", "Raw / Gameplay", "Current accuracy (0–100).",            100f, 0f, 100f));
        def.Channels.Add(Float("raw.osu.score",         "Score",         "Gameplay", "Raw / Gameplay", "Current score.",                        0f, 0f, null));
        def.Channels.Add(Float("raw.osu.pp.current",    "PP Current",    "Gameplay", "Raw / Gameplay", "Live performance points.",              0f, 0f, null));
        def.Channels.Add(Float("raw.osu.pp.fc",         "PP FC",         "Gameplay", "Raw / Gameplay", "Full combo PP estimate.",               0f, 0f, null));
        def.Channels.Add(Float("raw.osu.progress",      "Progress",      "Gameplay", "Raw / Gameplay", "Song progress (0–1).",                  0f, 0f, 1f));
        def.Channels.Add(Float("raw.osu.stars",         "Stars",         "Gameplay", "Raw / Gameplay", "Live star rating.",                     0f, 0f, null));
        def.Channels.Add(Float("raw.osu.bpm",           "BPM",           "Gameplay", "Raw / Gameplay", "Current realtime BPM.",                 0f, 0f, null));
        def.Channels.Add(Float("raw.osu.unstable_rate", "Unstable Rate", "Gameplay", "Raw / Gameplay", "Unstable rate (lower = more consistent).", 0f, 0f, null));

        return def;
    }

    private static InputChannelDefinition Bool(string key, string label, string group, string category, string desc)
        => new() { Key = key, Label = label, Group = group, ValueType = InputValueType.Bool, Category = category, Description = desc };

    private static InputChannelDefinition Float(string key, string label, string group, string category, string desc,
        float? def = null, float? min = null, float? max = null)
        => new() { Key = key, Label = label, Group = group, ValueType = InputValueType.Float, Category = category,
                   Description = desc, DefaultFloatValue = def, MinFloatValue = min, MaxFloatValue = max };
}
