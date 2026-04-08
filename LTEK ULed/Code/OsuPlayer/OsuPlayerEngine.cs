using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Threading;

namespace LTEK_ULed.Code.OsuPlayer;

public class OsuPlayerEngine : IDisposable
{
    private readonly TosuClient _tosu = new();
    private List<ManiaHitObject> _hitObjects = new();
    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _interpolationTimer = new();
    private readonly Stopwatch _uiUpdateTimer = new();
    private int _keyCount;
    private string _currentChecksum = string.Empty;
    private double _lastTosuTimeMs;
    private bool _isGameplay;
    private bool _isPaused;

    public static OsuPlayerEngine? Instance { get; private set; }
    public bool IsGameplay => _isGameplay;
    public bool IsConnected => _tosu.IsConnected;

    public event Action<string>? StatusChanged;
    public event Action<string>? NowPlayingChanged;
    public event Action<double, double>? PositionChanged; // currentMs, totalMs
    public event Action<bool>? ConnectionChanged;

    public void Start()
    {
        Instance = this;
        _tosu.DataReceived += OnTosuData;
        _tosu.ConnectionChanged += connected =>
        {
            ConnectionChanged?.Invoke(connected);
            if (!connected)
            {
                StopGameplay();
                Dispatcher.UIThread.Post(() => StatusChanged?.Invoke("Disconnected from tosu"));
            }
            else
            {
                Dispatcher.UIThread.Post(() => StatusChanged?.Invoke("Connected to tosu"));
            }
        };
        _tosu.Start();
    }

    private void OnTosuData(TosuData data)
    {
        bool isMania = data.Beatmap.Mode.Number == 3;
        bool paused = data.Game.Paused;
        var checksum = data.Beatmap.Checksum;
        bool hasBeatmap = !string.IsNullOrEmpty(checksum);

        // Resync stopwatch from tosu's live time
        var tosuTimeMs = data.Beatmap.Time.Live;
        _lastTosuTimeMs = tosuTimeMs;
        _interpolationTimer.Restart();

        // Update pause state
        _isPaused = paused;

        // UI position update
        var totalMs = (double)data.Beatmap.Time.Mp3Length;
        Dispatcher.UIThread.Post(() => PositionChanged?.Invoke(tosuTimeMs, totalMs));

        if (hasBeatmap && isMania && !paused)
        {
            // Beatmap changed?
            if (checksum != _currentChecksum)
            {
                StopGameplay();
                _currentChecksum = checksum;
                _keyCount = (int)data.Beatmap.Stats.Cs.Original;

                var osuFilePath = ResolveOsuFilePath(data);
                if (osuFilePath != null && File.Exists(osuFilePath))
                {
                    try
                    {
                        _hitObjects = OsuFileParser.Parse(osuFilePath);
                        Debug.WriteLine($"[OsuEngine] Loaded {_hitObjects.Count} hit objects from {osuFilePath}");

                        var nowPlaying = $"{data.Beatmap.Artist} - {data.Beatmap.Title} [{data.Beatmap.Version}] ({_keyCount}K)";
                        Dispatcher.UIThread.Post(() =>
                        {
                            NowPlayingChanged?.Invoke(nowPlaying);
                            StatusChanged?.Invoke($"Playing ({_hitObjects.Count} notes)");
                        });

                        StartGameplay();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OsuEngine] Failed to parse .osu: {ex.Message}");
                        Dispatcher.UIThread.Post(() => StatusChanged?.Invoke($"Parse error: {ex.Message}"));
                    }
                }
                else
                {
                    Debug.WriteLine($"[OsuEngine] .osu file not found: {osuFilePath}");
                    Dispatcher.UIThread.Post(() => StatusChanged?.Invoke("Beatmap file not found"));
                }
            }

            if (!_isGameplay && _hitObjects.Count > 0)
                StartGameplay();
        }
        else if (_isGameplay && (!hasBeatmap || !isMania))
        {
            StopGameplay();
            _currentChecksum = string.Empty;
            Dispatcher.UIThread.Post(() =>
            {
                NowPlayingChanged?.Invoke(string.Empty);
                StatusChanged?.Invoke("Connected to tosu");
            });
        }
        else if (_isGameplay && paused)
        {
            // Clear lights while paused
            lock (GameState.gameState.state)
            {
                GameState.gameState.state.gameButton = GameButton.NONE;
                GameState.gameState.state.cabinetLight = CabinetLight.NONE;
            }
        }
    }

    private string? ResolveOsuFilePath(TosuData data)
    {
        var songsFolder = data.Folders.Songs;
        var beatmapFile = data.DirectPath.BeatmapFile;

        if (string.IsNullOrEmpty(songsFolder) || string.IsNullOrEmpty(beatmapFile))
            return null;

        return Path.Combine(songsFolder, beatmapFile);
    }

    private void StartGameplay()
    {
        if (_isGameplay) return;
        _isGameplay = true;

        GameState.gameState.SetConnectionStatus(true);
        lock (GameState.gameState.state)
        {
            GameState.gameState.state.lightsMode = LightsMode.LIGHTSMODE_GAMEPLAY;
        }

        _cts = new CancellationTokenSource();
        _uiUpdateTimer.Restart();

        _thread = new Thread(() => TimingLoop(_cts.Token))
        {
            IsBackground = true,
            Name = "OsuPlayerEngine"
        };
        _thread.Start();
    }

    private void StopGameplay()
    {
        if (!_isGameplay) return;
        _isGameplay = false;

        _cts?.Cancel();
        _thread?.Join(500);
        _thread = null;
        _cts?.Dispose();
        _cts = null;

        lock (GameState.gameState.state)
        {
            GameState.gameState.state.gameButton = GameButton.NONE;
            GameState.gameState.state.cabinetLight = CabinetLight.NONE;
        }
        GameState.gameState.SetConnectionStatus(false);
    }

    private void TimingLoop(CancellationToken ct)
    {
        int windowStart = 0;
        double previousMs = 0;

        while (!ct.IsCancellationRequested)
        {
            if (_isPaused)
            {
                Thread.Sleep(10);
                continue;
            }

            // Interpolated time: last tosu timestamp + elapsed since that update
            var currentMs = _lastTosuTimeMs + _interpolationTimer.ElapsedMilliseconds;

            // Detect backward time jump (song restarted / entered gameplay)
            if (currentMs < previousMs - 500)
            {
                windowStart = 0;
                Debug.WriteLine($"[OsuEngine] Time jumped backward ({previousMs:F0} -> {currentMs:F0}), reset window");
            }
            previousMs = currentMs;

            // Advance window start past fully expired hit objects
            while (windowStart < _hitObjects.Count && _hitObjects[windowStart].EndTimeMs < currentMs - 20)
                windowStart++;

            // Build flags from active hit objects
            GameButton buttons = GameButton.NONE;
            CabinetLight lights = CabinetLight.NONE;
            for (int i = windowStart; i < _hitObjects.Count; i++)
            {
                var obj = _hitObjects[i];
                if (obj.StartTimeMs > currentMs) break;

                if (obj.EndTimeMs >= currentMs)
                {
                    ApplyColumnMapping(obj.Column, ref buttons, ref lights);
                }
            }

            lock (GameState.gameState.state)
            {
                GameState.gameState.state.gameButton = buttons;
                GameState.gameState.state.cabinetLight = lights;
            }

            Thread.Sleep(1);
        }
    }

    private void ApplyColumnMapping(int column, ref GameButton buttons, ref CabinetLight lights)
    {
        var allMappings = Settings.Instance?.OsuColumnMappings;
        if (allMappings != null && allMappings.TryGetValue(_keyCount, out var mappings))
        {
            var mapping = mappings.FirstOrDefault(m => m.Column == column);
            if (mapping != null && (mapping.GameButtons != 0 || mapping.CabinetLights != 0))
            {
                buttons |= (GameButton)mapping.GameButtons;
                lights |= (CabinetLight)mapping.CabinetLights;
                return;
            }
        }

        // Default: map to CUSTOM_01-CUSTOM_19
        if (column >= 0 && column <= 18)
            buttons |= (GameButton)(4096 << column);
    }

    public void Dispose()
    {
        StopGameplay();
        _tosu.Dispose();
    }
}
