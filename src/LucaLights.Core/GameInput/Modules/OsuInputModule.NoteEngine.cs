using System.Diagnostics;
using System.Globalization;

namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule
{
    // Timing state (updated by v2 and precise WebSocket handlers)
    internal double _lastPreciseTimeMs = 0;
    private  double _lastV2TimeMs      = 0;
    private  bool   _hasV2TimeSample   = false;
    private  string _lastV2TimeChecksum = string.Empty;
    private  bool   _musicPlaying      = false;
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
        // tosu keeps reporting the last beatmap after osu! quits (state = Exit); treat that
        // as "no beatmap" so the note engine stops instead of looping on stale data.
        var checksum    = data.Beatmap.Checksum ?? string.Empty;
        var liveTimeMs  = data.Beatmap.Time.Live;
        var gameRunning = data.State.Number != TosuStateNumber.Exit;
        var hasBeatmap  = gameRunning && !string.IsNullOrEmpty(checksum);

        if (hasBeatmap)
        {
            lock (_syncRoot)
            {
                var canCompareTime = _hasV2TimeSample
                                     && string.Equals(_lastV2TimeChecksum, checksum, StringComparison.Ordinal);
                var beatmapTimeChanged = canCompareTime && liveTimeMs != _lastV2TimeMs;

                _musicPlaying       = beatmapTimeChanged;
                _noteEnginePaused   = !beatmapTimeChanged;
                _lastV2TimeMs       = liveTimeMs;
                _lastV2TimeChecksum = checksum;
                _hasV2TimeSample    = true;

                if (beatmapTimeChanged)
                    _interpolationTimer.Restart();
                else
                    _interpolationTimer.Reset();
            }

            if (data.Beatmap.Checksum != _currentChecksum)
                LoadBeatmap(data);

            StartNoteEngine();
        }
        else
        {
            StopNoteEngine();
            lock (_syncRoot)
            {
                Array.Clear(_columnLevel);
                Array.Clear(_columnPulsePending);
                _noteActive     = false;
                _taikoDon       = false;
                _taikoKat       = false;
                _standardCircle = false;
                _standardSlider = false;
                _standardSpinner= false;
                _taikoDrumroll  = false;
                _taikoDenden    = false;
                _catchFruit     = false;
                _currentMode    = -1;
                _currentChecksum = string.Empty;
                ResetV2TimingStateUnsafe();
            }
            StopPaletteExtraction();
        }
    }

    // Must be called under _syncRoot.
    private void ResetV2TimingStateUnsafe()
    {
        _lastV2TimeMs       = 0;
        _hasV2TimeSample    = false;
        _lastV2TimeChecksum = string.Empty;
        _musicPlaying       = false;
        _noteEnginePaused   = true;
        _interpolationTimer.Reset();
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

        StartPaletteExtraction(data.Beatmap.Checksum, songs, data.DirectPath.BeatmapBackground);
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
        double lastScanMs    = double.NegativeInfinity;

        // Track previous level-state to avoid redundant publishes (pulses publish on every onset).
        bool prevNoteActive = false;
        bool prevSlider     = false;
        bool prevSpin       = false;
        bool prevDrumroll   = false;
        bool prevDenden     = false;
        var  prevCols       = new bool[MaxManiaColumns];

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_noteEnginePaused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                double baseTime;
                long   elapsed;
                lock (_syncRoot)
                {
                    // v2's Beatmap.Time.Live is the right clock here: during gameplay it
                    // tracks audio time, and during song-select it ticks from 0 per map
                    baseTime = _lastV2TimeMs;
                    elapsed  = _interpolationTimer.ElapsedMilliseconds;
                }

                var currentMs = baseTime + elapsed;

                List<OsuHitObject> hitObjects;
                int mode;
                lock (_syncRoot)
                {
                    hitObjects = _hitObjects;
                    mode       = _currentMode;
                }

                // On a large backward jump (song restart), treat everything as fresh.
                if (currentMs < lastScanMs - 500) lastScanMs = double.NegativeInfinity;

                // Level state — only long notes populate these.
                var  levelCols      = new bool[MaxManiaColumns];
                bool noteActive     = false;
                bool standardSlider = false;
                bool standardSpin   = false;
                bool taikoDrumroll  = false;
                bool taikoDenden    = false;

                // Pulse onsets this tick — short notes whose StartTimeMs is in (lastScanMs, currentMs].
                bool donOnset    = false;
                bool katOnset    = false;
                bool circleOnset = false;
                bool fruitOnset  = false;
                var  colOnset    = new bool[MaxManiaColumns];
                var  onsetLo     = lastScanMs;

                for (var i = 0; i < hitObjects.Count; i++)
                {
                    var obj = hitObjects[i];
                    if (obj.StartTimeMs > currentMs) break;

                    var isOnset = obj.StartTimeMs > onsetLo && obj.StartTimeMs <= currentMs;
                    var isLive  = obj.EndTimeMs  >= currentMs;

                    if (isLive) noteActive = true;

                    switch (obj.Type)
                    {
                        case OsuHitObjectType.TaikoDon:   if (isOnset) donOnset    = true; break;
                        case OsuHitObjectType.TaikoKat:   if (isOnset) katOnset    = true; break;
                        case OsuHitObjectType.CatchFruit: if (isOnset) fruitOnset  = true; break;
                        case OsuHitObjectType.Circle:
                            if (mode == TosuModeNumber.Standard) { if (isOnset) circleOnset = true; }
                            else if (mode == TosuModeNumber.Mania && obj.Column < MaxManiaColumns)
                            { if (isOnset) colOnset[obj.Column] = true; }
                            break;
                        case OsuHitObjectType.Slider:        if (isLive) standardSlider = true; break;
                        case OsuHitObjectType.Spinner:       if (isLive) standardSpin   = true; break;
                        case OsuHitObjectType.TaikoDrumroll: if (isLive) taikoDrumroll  = true; break;
                        case OsuHitObjectType.TaikoDenden:   if (isLive) taikoDenden    = true; break;
                        case OsuHitObjectType.Hold:
                            if (isLive && mode == TosuModeNumber.Mania && obj.Column < MaxManiaColumns)
                                levelCols[obj.Column] = true;
                            break;
                    }
                }

                lastScanMs = currentMs;

                var anyColOnset = false;
                for (var i = 0; i < MaxManiaColumns; i++) if (colOnset[i]) { anyColOnset = true; break; }

                var changed = noteActive != prevNoteActive
                              || standardSlider != prevSlider || standardSpin != prevSpin
                              || taikoDrumroll != prevDrumroll || taikoDenden != prevDenden
                              || donOnset || katOnset || circleOnset || fruitOnset || anyColOnset
                              || !levelCols.SequenceEqual(prevCols);

                if (changed || publishTimer.ElapsedMilliseconds >= 16) // ~60fps minimum
                {
                    InputSnapshot snapshot;
                    lock (_syncRoot)
                    {
                        _noteActive      = noteActive;
                        _standardSlider  = standardSlider;
                        _standardSpinner = standardSpin;
                        _taikoDrumroll   = taikoDrumroll;
                        _taikoDenden     = taikoDenden;

                        // Latch onset pulses — cleared by AcknowledgePulses after the renderer reads them.
                        if (donOnset)    _taikoDon       = true;
                        if (katOnset)    _taikoKat       = true;
                        if (circleOnset) _standardCircle = true;
                        if (fruitOnset)  _catchFruit     = true;

                        Array.Copy(levelCols, _columnLevel, MaxManiaColumns);
                        for (var i = 0; i < MaxManiaColumns; i++)
                            if (colOnset[i]) _columnPulsePending[i] = true;

                        snapshot        = BuildSnapshot();
                        _latestSnapshot = snapshot;
                    }

                    _snapshotDispatch.Writer.TryWrite(snapshot);

                    Array.Copy(levelCols, prevCols, MaxManiaColumns);
                    prevNoteActive = noteActive;
                    prevSlider     = standardSlider;
                    prevSpin       = standardSpin;
                    prevDrumroll   = taikoDrumroll;
                    prevDenden     = taikoDenden;
                    publishTimer.Restart();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log?.Invoke($"osu: note engine loop error: {ex.Message}");
            }

            Thread.Sleep(4);
        }
    }


    // ---------------------------------------------------------------------------
    // .osu file parser — handles all four game modes
    // ---------------------------------------------------------------------------

    private static List<OsuHitObject> ParseOsuFile(string path, int mode, int keyCountHint)
    {
        var     lines            = File.ReadAllLines(path);
        var     result           = new List<OsuHitObject>();
        string? section          = null;
        var     keyCount         = keyCountHint > 0 ? keyCountHint : 4;
        var     sliderMultiplier = 1.4;           // .osu default
        var     beatLength       = 500.0;         // fallback ~120 bpm
        var     gotBeatLength    = false;

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
                    else if (line.StartsWith("SliderMultiplier:", StringComparison.Ordinal))
                    {
                        var val = line["SliderMultiplier:".Length..].Trim();
                        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var sm))
                            sliderMultiplier = sm;
                    }
                    break;

                case "TimingPoints":
                    // First uninherited timing point: positive beatLength = ms per beat.
                    if (!gotBeatLength)
                    {
                        var tp = line.Split(',');
                        if (tp.Length >= 2 &&
                            double.TryParse(tp[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var bl)
                            && bl > 0)
                        {
                            beatLength    = bl;
                            gotBeatLength = true;
                        }
                    }
                    break;

                case "HitObjects":
                    var obj = ParseHitObject(line, mode, keyCount, sliderMultiplier, beatLength);
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

    private static OsuHitObject? ParseHitObject(string line, int mode, int keyCount,
        double sliderMultiplier, double beatLength)
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

        // Spinner end time lives in parts[5] (osu!std + taiko denden).
        int spinnerEnd = endTime;
        if (isSpinner && parts.Length >= 6
            && int.TryParse(parts[5].Split(':')[0].Trim(), out var se) && se > startTime)
            spinnerEnd = se;

        // Slider duration for taiko drumrolls: slides * length / (100 * SliderMultiplier) * beatLength ms.
        // Ignores inherited velocity changes (approximation — good enough for lighting).
        int sliderEnd = endTime;
        if (isSlider && parts.Length >= 8
            && int.TryParse(parts[6].Trim(), out var slides) && slides > 0
            && double.TryParse(parts[7].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var length)
            && length > 0 && sliderMultiplier > 0 && beatLength > 0)
        {
            var durMs = slides * length / (100.0 * sliderMultiplier) * beatLength;
            sliderEnd = startTime + (int)Math.Round(durMs);
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
                EndTimeMs   = isSpinner ? spinnerEnd : isSlider ? sliderEnd : endTime,
                Type        = isSpinner ? OsuHitObjectType.TaikoDenden
                            : isSlider  ? OsuHitObjectType.TaikoDrumroll
                            : IsKatHitSound(hitSound) ? OsuHitObjectType.TaikoKat
                                                      : OsuHitObjectType.TaikoDon
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
                EndTimeMs   = isSpinner ? spinnerEnd : endTime,
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
