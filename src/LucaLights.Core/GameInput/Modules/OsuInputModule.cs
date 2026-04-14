using LucaLights.Core.Models;

namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule : IGameInputModule, IDisposable
{
    public const string ModuleIdValue  = "osu";
    public const int    MaxManiaColumns = 18;

    private static readonly Lazy<InputDefinition> DefinitionLazy = new(BuildDefinition);

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

    // Snapshot state — all protected by _syncRoot
    private InputSnapshot _latestSnapshot = InputSnapshot.Empty;
    private bool          _v2Connected    = false;
    private TosuV2Data?   _latestV2       = null;

    // Note engine output — updated by NoteEngine partial, read by BuildSnapshot
    internal readonly bool[] _columnBools     = new bool[MaxManiaColumns];
    internal          bool   _noteActive      = false;
    internal          bool   _taikoDon        = false;
    internal          bool   _taikoKat        = false;
    internal          bool   _standardCircle  = false;
    internal          bool   _standardSlider  = false;
    internal          bool   _standardSpinner = false;
    internal          bool   _catchFruit      = false;

    // Key press state — updated by precise WebSocket
    internal bool _k1Pressed = false;
    internal bool _k2Pressed = false;
    internal bool _m1Pressed = false;
    internal bool _m2Pressed = false;

    public OsuInputModule(string tosuUrl, bool autoManageProcess = true, Action<string>? log = null)
    {
        _tosuUrl           = tosuUrl.TrimEnd('/');
        _autoManageProcess = autoManageProcess;
        _log               = log;
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
        _disposed = true;
    }

    private async Task RunAll(CancellationToken ct)
    {
        if (_autoManageProcess)
            await EnsureTosuRunningAsync(ct);

        await Task.WhenAll(
            RunV2LoopAsync(ct),
            RunPreciseLoopAsync(ct));
    }

    // Called from any background task after updating shared state.
    // Acquires _syncRoot, builds snapshot, stores it, then fires event outside lock.
    internal void PublishCurrentSnapshot()
    {
        InputSnapshot snapshot;
        lock (_syncRoot)
        {
            snapshot       = BuildSnapshot();
            _latestSnapshot = snapshot;
        }
        SnapshotUpdated?.Invoke(snapshot);
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
        var playing   = v2?.State.Number == TosuStateNumber.Playing;
        var mode      = v2?.Beatmap.Mode.Number ?? -1;

        var bools = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw.osu.connected"]        = connected,
            ["raw.osu.playing"]          = playing,
            ["raw.osu.paused"]           = v2?.Game.Paused ?? false,
            ["raw.osu.failed"]           = v2?.Play.Failed ?? false,
            ["raw.osu.is_kiai"]          = v2?.Beatmap.IsKiai ?? false,
            ["raw.osu.is_break"]         = v2?.Beatmap.IsBreak ?? false,
            ["raw.osu.keys.k1"]          = _k1Pressed,
            ["raw.osu.keys.k2"]          = _k2Pressed,
            ["raw.osu.keys.m1"]          = _m1Pressed,
            ["raw.osu.keys.m2"]          = _m2Pressed,
            ["raw.osu.note_active"]      = _noteActive,
            ["raw.osu.taiko.don"]        = _taikoDon,
            ["raw.osu.taiko.kat"]        = _taikoKat,
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
            bools["raw.osu.taiko.user.k1"] = _k1Pressed;
            bools["raw.osu.taiko.user.k2"] = _k2Pressed;
            bools["raw.osu.taiko.user.m1"] = _m1Pressed;
            bools["raw.osu.taiko.user.m2"] = _m2Pressed;
            // Descriptive aliases for Taiko
            bools["raw.osu.taiko.user.left_kat"]  = _k1Pressed;
            bools["raw.osu.taiko.user.left_don"]  = _k2Pressed;
            bools["raw.osu.taiko.user.right_don"] = _m1Pressed;
            bools["raw.osu.taiko.user.right_kat"] = _m2Pressed;
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
            bools[$"raw.osu.mania.col_{i}"] = _columnBools[i];

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
            ["raw.osu.beatmap_checksum"] = v2?.Beatmap.Checksum ?? string.Empty
        };

        if (v2 != null && !string.IsNullOrEmpty(v2.Beatmap.Checksum))
        {
            var modeSuffix = v2.Beatmap.Mode.Number == TosuModeNumber.Mania
                ? $" ({(int)v2.Beatmap.Stats.Cs.Original}K)" : string.Empty;
            metadata["raw.osu.now_playing"] =
                $"{v2.Beatmap.Artist} - {v2.Beatmap.Title} [{v2.Beatmap.Version}]{modeSuffix}";
        }

        return new InputSnapshot
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Sequence     = ++_sequence,
            IsConnected  = connected,
            IsActive     = connected && playing,
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
        def.Channels.Add(Bool("raw.osu.connected",  "Connected", "System", "Raw / System", "True while tosu WebSocket is connected."));
        def.Channels.Add(Bool("raw.osu.playing",    "Playing",   "System", "Raw / System", "True while actively in gameplay (state = playing)."));
        def.Channels.Add(Bool("raw.osu.paused",     "Paused",    "System", "Raw / System", "True while the game is paused."));
        def.Channels.Add(Bool("raw.osu.failed",     "Failed",    "System", "Raw / System", "True after the player fails."));

        // Beatmap state
        def.Channels.Add(Bool("raw.osu.is_kiai",  "Kiai",  "Beatmap", "Raw / Beatmap", "True during kiai (chorus/hype) sections."));
        def.Channels.Add(Bool("raw.osu.is_break", "Break", "Beatmap", "Raw / Beatmap", "True during break sections."));

        // Generic Key presses (Always active)
        def.Channels.Add(Bool("raw.osu.keys.k1", "K1", "Keys (Generic)", "Raw / Keys", "Keyboard key 1 is pressed."));
        def.Channels.Add(Bool("raw.osu.keys.k2", "K2", "Keys (Generic)", "Raw / Keys", "Keyboard key 2 is pressed."));
        def.Channels.Add(Bool("raw.osu.keys.m1", "M1", "Keys (Generic)", "Raw / Keys", "Mouse button 1 is pressed."));
        def.Channels.Add(Bool("raw.osu.keys.m2", "M2", "Keys (Generic)", "Raw / Keys", "Mouse button 2 is pressed."));

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
        def.Channels.Add(Bool("raw.osu.taiko.don",   "Taiko Don",   "Notes", "Raw / Notes", "True when a don (centre/red) note is active."));
        def.Channels.Add(Bool("raw.osu.taiko.kat",   "Taiko Kat",   "Notes", "Raw / Notes", "True when a kat (rim/blue) note is active."));

        // Standard
        def.Channels.Add(Bool("raw.osu.standard.circle",  "Standard Circle",  "Notes", "Raw / Notes", "True when a standard hit circle is active."));
        def.Channels.Add(Bool("raw.osu.standard.slider",  "Standard Slider",  "Notes", "Raw / Notes", "True when a standard slider is active."));
        def.Channels.Add(Bool("raw.osu.standard.spinner", "Standard Spinner", "Notes", "Raw / Notes", "True when a standard spinner is active."));

        // Catch
        def.Channels.Add(Bool("raw.osu.catch.fruit", "Catch Fruit", "Notes", "Raw / Notes", "True when a catch fruit (or droplet) is active."));

        // Mania columns
        for (var i = 0; i < MaxManiaColumns; i++)
            def.Channels.Add(Bool($"raw.osu.mania.col_{i}", $"Mania Col {i}", "Mania Columns",
                "Raw / Notes", $"True when mania column {i} has an active note."));

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

