using System.Diagnostics;
using System.Globalization;

namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule
{
    // Timing state (updated by v2 and precise WebSocket handlers)
    internal double _lastPreciseTimeMs = 0;
    private  double _lastV2TimeMs      = 0;
    private readonly Stopwatch _interpolationTimer = new();

    // Note engine state
    private volatile bool _noteEnginePaused = false;
    private readonly object _noteEngineLock  = new();
    private CancellationTokenSource? _noteEngineCts  = null;
    private Task?                    _noteEngineTask = null;

    private List<OsuHitObject> _hitObjects     = [];
    private string             _currentChecksum = string.Empty;
    private int                _currentMode     = -1;
    private int                _currentKeyCount = 4;

    // Called by v2 WebSocket after updating _latestV2
    private void OnV2DataReceived(TosuV2Data data)
    {
        // Sync timing
        lock (_syncRoot)
        {
            _lastV2TimeMs = data.Beatmap.Time.Live;
            _interpolationTimer.Restart();
        }

        _noteEnginePaused = data.Game.Paused;

        var isPlaying  = data.State.Number == TosuStateNumber.Playing;
        var hasBeatmap = !string.IsNullOrEmpty(data.Beatmap.Checksum);

        if (isPlaying && hasBeatmap)
        {
            if (data.Beatmap.Checksum != _currentChecksum)
                LoadBeatmap(data);

            StartNoteEngine();
        }
        else if (!isPlaying)
        {
            StopNoteEngine();
            lock (_syncRoot)
            {
                Array.Clear(_columnBools);
                _noteActive = false;
                _taikoDon   = false;
                _taikoKat   = false;
            }
        }
    }

    private void LoadBeatmap(TosuV2Data data)
    {
        var songs    = data.Folders.Songs;
        var beatmap  = data.DirectPath.BeatmapFile;

        if (string.IsNullOrEmpty(songs) || string.IsNullOrEmpty(beatmap))
        {
            _log?.Invoke("osu: cannot resolve beatmap path — folders not provided by tosu.");
            lock (_syncRoot) { _hitObjects = []; }
            _currentChecksum = data.Beatmap.Checksum;
            return;
        }

        var fullPath = Path.Combine(songs, beatmap);
        if (!File.Exists(fullPath))
        {
            _log?.Invoke($"osu: beatmap file not found: {fullPath}");
            lock (_syncRoot) { _hitObjects = []; }
            _currentChecksum = data.Beatmap.Checksum;
            return;
        }

        try
        {
            var mode      = data.Beatmap.Mode.Number;
            var keyCount  = (int)data.Beatmap.Stats.Cs.Original;
            var objects   = ParseOsuFile(fullPath, mode, keyCount);

            lock (_syncRoot)
            {
                _hitObjects      = objects;
                _currentMode     = mode;
                _currentKeyCount = keyCount;
            }

            _currentChecksum = data.Beatmap.Checksum;
            _log?.Invoke($"osu: loaded {objects.Count} hit objects from {Path.GetFileName(fullPath)}.");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"osu: failed to parse beatmap: {ex.Message}");
            lock (_syncRoot) { _hitObjects = []; }
            _currentChecksum = data.Beatmap.Checksum;
        }
    }

    private void StartNoteEngine()
    {
        lock (_noteEngineLock)
        {
            if (_noteEngineCts is not null) return; // already running
            var cts = new CancellationTokenSource();
            _noteEngineCts  = cts;
            _noteEngineTask = Task.Run(() => NoteEngineLoop(cts.Token), CancellationToken.None);
        }
    }

    private void StopNoteEngine()
    {
        CancellationTokenSource? cts;
        lock (_noteEngineLock)
        {
            cts             = _noteEngineCts;
            _noteEngineCts  = null;
            _noteEngineTask = null;
        }
        cts?.Cancel();
        cts?.Dispose();
    }

    private void NoteEngineLoop(CancellationToken ct)
    {
        var    publishTimer  = Stopwatch.StartNew();
        int    windowStart   = 0;
        double previousMs    = 0;

        // Track previous state to avoid redundant publishes
        bool prevNoteActive  = false;
        bool prevDon         = false;
        bool prevKat         = false;
        bool prevCircle      = false;
        bool prevSlider      = false;
        bool prevSpin        = false;
        bool prevCatch       = false;
        var  prevCols        = new bool[MaxManiaColumns];

        while (!ct.IsCancellationRequested)
        {
            if (_noteEnginePaused)
            {
                Thread.Sleep(10);
                continue;
            }

            double lastV2Time;
            long   elapsed;
            lock (_syncRoot)
            {
                lastV2Time = _lastV2TimeMs;
                elapsed    = _interpolationTimer.ElapsedMilliseconds;
            }

            var currentMs = lastV2Time + elapsed;

            // Time jumped backward — song restarted or seeked
            if (currentMs < previousMs - 500)
                windowStart = 0;
            previousMs = currentMs;

            List<OsuHitObject> hitObjects;
            int mode;
            lock (_syncRoot)
            {
                hitObjects = _hitObjects;
                mode       = _currentMode;
            }

            // Advance window past expired objects
            while (windowStart < hitObjects.Count && hitObjects[windowStart].EndTimeMs < currentMs - 20)
                windowStart++;

            // Build active state
            var  cols           = new bool[MaxManiaColumns];
            bool noteActive     = false;
            bool don            = false;
            bool kat            = false;
            bool standardCircle = false;
            bool standardSlider = false;
            bool standardSpin   = false;
            bool catchFruit     = false;

            for (var i = windowStart; i < hitObjects.Count; i++)
            {
                var obj = hitObjects[i];
                if (obj.StartTimeMs > currentMs) break;
                if (obj.EndTimeMs   < currentMs) continue;

                noteActive = true;

                switch (obj.Type)
                {
                    case OsuHitObjectType.TaikoDon: don = true; break;
                    case OsuHitObjectType.TaikoKat: kat = true; break;
                    case OsuHitObjectType.Circle:
                        if (mode == TosuModeNumber.Standard) standardCircle = true;
                        else if (mode == TosuModeNumber.Mania && obj.Column < MaxManiaColumns) cols[obj.Column] = true;
                        break;
                    case OsuHitObjectType.Slider:   standardSlider = true; break;
                    case OsuHitObjectType.Spinner:  standardSpin = true;   break;
                    case OsuHitObjectType.Hold:
                        if (mode == TosuModeNumber.Mania && obj.Column < MaxManiaColumns) cols[obj.Column] = true;
                        break;
                    case OsuHitObjectType.CatchFruit: catchFruit = true; break;
                }
            }

            // Check if state actually changed
            var changed = noteActive != prevNoteActive || don != prevDon || kat != prevKat
                          || standardCircle != prevCircle || standardSlider != prevSlider
                          || standardSpin != prevSpin || catchFruit != prevCatch
                          || !cols.SequenceEqual(prevCols);

            if (changed || publishTimer.ElapsedMilliseconds >= 16) // cap at ~60fps when unchanged
            {
                lock (_syncRoot)
                {
                    _noteActive      = noteActive;
                    _taikoDon        = don;
                    _taikoKat        = kat;
                    _standardCircle  = standardCircle;
                    _standardSlider  = standardSlider;
                    _standardSpinner = standardSpin;
                    _catchFruit      = catchFruit;
                    Array.Copy(cols, _columnBools, MaxManiaColumns);
                }

                if (changed) PublishCurrentSnapshot();

                Array.Copy(cols, prevCols, MaxManiaColumns);
                prevNoteActive = noteActive;
                prevDon        = don;
                prevKat        = kat;
                prevCircle     = standardCircle;
                prevSlider     = standardSlider;
                prevSpin       = standardSpin;
                prevCatch      = catchFruit;
                publishTimer.Restart();
            }

            Thread.Sleep(1);
        }
    }

    // ---------------------------------------------------------------------------
    // .osu file parser — handles all four game modes
    // ---------------------------------------------------------------------------

    private static List<OsuHitObject> ParseOsuFile(string path, int mode, int keyCountHint)
    {
        var    lines        = File.ReadAllLines(path);
        var    result       = new List<OsuHitObject>();
        string? section     = null;
        var    keyCount     = keyCountHint > 0 ? keyCountHint : 4;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1];
                continue;
            }

            switch (section)
            {
                case "Difficulty":
                    if (line.StartsWith("CircleSize:", StringComparison.Ordinal))
                    {
                        var val = line["CircleSize:".Length..].Trim();
                        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var cs))
                            keyCount = (int)cs;
                    }
                    break;

                case "HitObjects":
                    var obj = ParseHitObject(line, mode, keyCount);
                    if (obj is not null) result.Add(obj);
                    break;
            }
        }

        result.Sort((a, b) =>
        {
            var cmp = a.StartTimeMs.CompareTo(b.StartTimeMs);
            return cmp != 0 ? cmp : a.Column.CompareTo(b.Column);
        });
        return result;
    }

    private static OsuHitObject? ParseHitObject(string line, int mode, int keyCount)
    {
        var parts = line.Split(',');
        if (parts.Length < 5) return null;

        if (!int.TryParse(parts[0].Trim(), out var x))         return null;
        if (!int.TryParse(parts[2].Trim(), out var startTime)) return null;
        if (!int.TryParse(parts[3].Trim(), out var typeFlags)) return null;
        if (!int.TryParse(parts[4].Trim(), out var hitSound))  return null;

        const int MinTap = 80;
        var isHold    = (typeFlags & 128) != 0;
        var isSlider  = (typeFlags & 2)   != 0;
        var isSpinner = (typeFlags & 8)   != 0;
        var endTime   = startTime + MinTap;

        if (isHold && parts.Length >= 6)
        {
            var endParts = parts[5].Split(':');
            if (int.TryParse(endParts[0].Trim(), out var parsed) && parsed > startTime)
                endTime = parsed;
        }

        return mode switch
        {
            TosuModeNumber.Mania => new OsuHitObject
            {
                Column      = Math.Clamp((int)Math.Floor((double)x * keyCount / 512.0), 0, keyCount - 1),
                StartTimeMs = startTime,
                EndTimeMs   = endTime,
                IsHold      = isHold,
                Type        = isHold ? OsuHitObjectType.Hold : OsuHitObjectType.Circle
            },
            TosuModeNumber.Taiko => new OsuHitObject
            {
                Column      = 0,
                StartTimeMs = startTime,
                EndTimeMs   = endTime,
                Type        = IsKatHitSound(hitSound) ? OsuHitObjectType.TaikoKat : OsuHitObjectType.TaikoDon
            },
            TosuModeNumber.Catch => new OsuHitObject
            {
                Column      = 0,
                StartTimeMs = startTime,
                EndTimeMs   = endTime,
                Type        = OsuHitObjectType.CatchFruit
            },
            _ => new OsuHitObject   // Standard
            {
                Column      = 0,
                StartTimeMs = startTime,
                EndTimeMs   = isSpinner && parts.Length >= 6
                              && int.TryParse(parts[5].Split(':')[0].Trim(), out var spinEnd) && spinEnd > startTime
                              ? spinEnd : endTime,
                IsHold      = isSlider,
                Type        = isSpinner ? OsuHitObjectType.Spinner
                            : isSlider  ? OsuHitObjectType.Slider
                            : OsuHitObjectType.Circle
            }
        };
    }

    // Taiko: whistle (bit 1) or clap (bit 3) = kat; normal (bit 0) or finish (bit 2) = don
    private static bool IsKatHitSound(int hitSound) => (hitSound & 2) != 0 || (hitSound & 8) != 0;
}
