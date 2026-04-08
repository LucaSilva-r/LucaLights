using System.Diagnostics;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;

namespace LucaLights.Core.Engine;

public sealed class LightingManager : IDisposable
{
    private readonly Settings _settings;
    private readonly ILightingRenderer _renderer;
    private readonly LightingManagerOptions _options;
    private readonly Func<bool> _shouldSendOutput;
    private readonly GameInputManager? _gameInputManager;
    private readonly Func<bool> _isRenderingActiveFallback;
    private readonly Action<string>? _log;
    private readonly object _syncRoot = new();
    private readonly Stopwatch _globalTimer = new();

    private CancellationTokenSource? _run;
    private Thread? _thread;
    private bool _disposed;
    private bool _cleared = true;
    private long _frameIndex;
    private TimeSpan _previousElapsed;

    public LightingManager(
        Settings settings,
        ILightingRenderer renderer,
        LightingManagerOptions? options = null,
        GameInputManager? gameInputManager = null,
        Func<bool>? isRenderingActive = null,
        Func<bool>? shouldSendOutput = null,
        Action<string>? log = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _options = options ?? new LightingManagerOptions();
        _gameInputManager = gameInputManager;
        _isRenderingActiveFallback = isRenderingActive ?? (() => true);
        _shouldSendOutput = shouldSendOutput ?? (() => true);
        _log = log;
    }

    public event Action<LightingFrameContext>? FrameRendered;

    public event Action? OutputCleared;

    public event Action? SettingsApplied;

    public object SyncRoot => _syncRoot;

    public bool IsRunning => _thread?.IsAlive == true;

    public void Start()
    {
        ThrowIfDisposed();
        Stop();

        lock (_syncRoot)
        {
            ApplySettingsUnsafe();
            _cleared = true;
            _frameIndex = 0;
            _previousElapsed = TimeSpan.Zero;
        }

        _globalTimer.Restart();
        _run = new CancellationTokenSource();
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "LucaLights.Core.LightingManager"
        };
        _thread.Start();
    }

    public void Stop()
    {
        _run?.Cancel();
        _thread?.Join();
        _thread = null;
        _run?.Dispose();
        _run = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private void RunLoop()
    {
        var token = _run?.Token ?? CancellationToken.None;
        var targetFrameTimeMs = Math.Max(1, 1000 / Math.Max(1, _options.TargetFps));
        var sw = new Stopwatch();

        _log?.Invoke("LightingManager started.");

        while (!token.IsCancellationRequested)
        {
            sw.Restart();
            var inputSnapshot = _gameInputManager?.LatestSnapshot ?? InputSnapshot.Empty;

            if (!IsRenderingActive(inputSnapshot))
            {
                if (!_cleared && _options.ClearOutputWhenInactive)
                {
                    var shouldNotifyCleared = false;
                    lock (_syncRoot)
                    {
                        shouldNotifyCleared = ClearOutputsUnsafe();
                    }

                    if (shouldNotifyCleared)
                    {
                        OutputCleared?.Invoke();
                    }
                }

                Thread.Sleep(1);
                continue;
            }

            LightingFrameContext frameContext;
            var shouldRaiseSettingsApplied = false;

            lock (_syncRoot)
            {
                if (_settings.Dirty)
                {
                    ApplySettingsUnsafe();
                    shouldRaiseSettingsApplied = true;
                }

                ClearSegmentBuffersUnsafe();

                var elapsed = _globalTimer.Elapsed;
                var delta = elapsed - _previousElapsed;
                _previousElapsed = elapsed;
                frameContext = new LightingFrameContext(++_frameIndex, elapsed, delta, inputSnapshot);

                _renderer.Render(_settings, frameContext);

                if (_shouldSendOutput())
                {
                    SendDevicesUnsafe();
                }

                _cleared = false;
            }

            if (shouldRaiseSettingsApplied)
            {
                SettingsApplied?.Invoke();
            }

            FrameRendered?.Invoke(frameContext);

            var remaining = targetFrameTimeMs - (int)sw.ElapsedMilliseconds;
            if (remaining > 1)
            {
                Thread.Sleep(remaining - 1);
            }

            while (sw.ElapsedMilliseconds < targetFrameTimeMs && !token.IsCancellationRequested)
            {
                Thread.SpinWait(64);
            }
        }

        _log?.Invoke("LightingManager stopped.");
    }

    private void ApplySettingsUnsafe()
    {
        foreach (var device in _settings.Devices)
        {
            device.Recalculate();
        }

        _renderer.Prepare(_settings);
        _settings.ClearDirty();
    }

    private bool ClearOutputsUnsafe()
    {
        if (_cleared)
        {
            return false;
        }

        ClearSegmentBuffersUnsafe();
        _renderer.Clear(_settings);

        if (_shouldSendOutput())
        {
            SendDevicesUnsafe();
        }

        _cleared = true;
        return true;
    }

    private void ClearSegmentBuffersUnsafe()
    {
        foreach (var device in _settings.Devices)
        {
            foreach (var segment in device.Segments)
            {
                Array.Clear(segment.Leds);
            }
        }
    }

    private void SendDevicesUnsafe()
    {
        foreach (var device in _settings.Devices)
        {
            device.Send();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private bool IsRenderingActive(InputSnapshot inputSnapshot)
    {
        if (_gameInputManager is not null)
        {
            return inputSnapshot.IsActive;
        }

        return _isRenderingActiveFallback();
    }
}
