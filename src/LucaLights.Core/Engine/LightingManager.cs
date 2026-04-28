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
    private readonly Action<string>? _log;
    private readonly object _syncRoot = new();
    private readonly Stopwatch _globalTimer = new();

    private CancellationTokenSource? _run;
    private Thread? _thread;
    private LayoutPreviewFrame? _layoutPreviewFrame;
    private bool _disposed;
    private bool _cleared = true;
    private long _frameIndex;
    private TimeSpan _previousElapsed;

    public LightingManager(
        Settings settings,
        ILightingRenderer renderer,
        LightingManagerOptions? options = null,
        GameInputManager? gameInputManager = null,
        Func<bool>? shouldSendOutput = null,
        Action<string>? log = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _options = options ?? new LightingManagerOptions();
        _gameInputManager = gameInputManager;
        _shouldSendOutput = shouldSendOutput ?? (() => true);
        _log = log;
    }

    public event Action<LightingFrameContext>? FrameRendered;

    public event Action? OutputCleared;

    public event Action? SettingsApplied;

    public object SyncRoot => _syncRoot;

    public bool IsRunning => _thread?.IsAlive == true;

    public bool IsLayoutPreviewActive
    {
        get
        {
            lock (_syncRoot)
            {
                return _layoutPreviewFrame is not null;
            }
        }
    }

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

            if (TryRenderLayoutPreviewFrame(inputSnapshot, out var previewFrameContext))
            {
                FrameRendered?.Invoke(previewFrameContext);
                SleepUntilNextFrame(sw, targetFrameTimeMs, token);
                continue;
            }

            if (!IsInputConnected(inputSnapshot))
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

            // Clear any pulse/edge-triggered inputs now that this frame has consumed them.
            _gameInputManager?.AcknowledgePulses(inputSnapshot);

            SleepUntilNextFrame(sw, targetFrameTimeMs, token);
        }

        _log?.Invoke("LightingManager stopped.");
    }

    public void SetLayoutPreviewFrame(string deviceId, string segmentId, IReadOnlyList<Color> colors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentId);
        ArgumentNullException.ThrowIfNull(colors);

        SetLayoutPreviewFrames([new LayoutPreviewSegmentFrame(deviceId, segmentId, colors.ToArray())]);
    }

    public void SetLayoutPreviewFrames(IReadOnlyList<LayoutPreviewSegmentFrame> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        lock (_syncRoot)
        {
            _layoutPreviewFrame = new LayoutPreviewFrame(
                segments
                    .Where(segment => !string.IsNullOrWhiteSpace(segment.DeviceId)
                        && !string.IsNullOrWhiteSpace(segment.SegmentId))
                    .Select(segment => segment with { Colors = segment.Colors.ToArray() })
                    .ToArray());
        }
    }

    public void ClearLayoutPreviewFrame()
    {
        lock (_syncRoot)
        {
            if (_layoutPreviewFrame is null)
            {
                return;
            }

            _layoutPreviewFrame = null;
            ClearSegmentBuffersUnsafe();

            if (_shouldSendOutput())
            {
                SendDevicesUnsafe();
            }

            _cleared = true;
        }
    }

    private bool TryRenderLayoutPreviewFrame(InputSnapshot inputSnapshot, out LightingFrameContext frameContext)
    {
        frameContext = default!;

        lock (_syncRoot)
        {
            if (_layoutPreviewFrame is not { } previewFrame)
            {
                return false;
            }

            if (_settings.Dirty)
            {
                ApplySettingsUnsafe();
                SettingsApplied?.Invoke();
            }

            ClearSegmentBuffersUnsafe();
            ApplyLayoutPreviewFrameUnsafe(previewFrame);

            var elapsed = _globalTimer.Elapsed;
            var delta = elapsed - _previousElapsed;
            _previousElapsed = elapsed;
            frameContext = new LightingFrameContext(++_frameIndex, elapsed, delta, inputSnapshot);

            if (_shouldSendOutput())
            {
                SendDevicesUnsafe();
            }

            _cleared = false;
            return true;
        }
    }

    private void ApplyLayoutPreviewFrameUnsafe(LayoutPreviewFrame previewFrame)
    {
        foreach (var previewSegment in previewFrame.Segments)
        {
            var device = _settings.Devices.FirstOrDefault(
                candidate => string.Equals(candidate.Id, previewSegment.DeviceId, StringComparison.OrdinalIgnoreCase));
            var segment = device?.Segments.FirstOrDefault(
                candidate => string.Equals(candidate.Id, previewSegment.SegmentId, StringComparison.OrdinalIgnoreCase));

            if (segment is null)
            {
                continue;
            }

            var count = Math.Min(segment.Leds.Length, previewSegment.Colors.Length);
            Array.Copy(previewSegment.Colors, segment.Leds, count);
        }
    }

    private static void SleepUntilNextFrame(Stopwatch sw, int targetFrameTimeMs, CancellationToken token)
    {
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

    private bool IsInputConnected(InputSnapshot inputSnapshot)
    {
        if (_gameInputManager is null)
        {
            return true;
        }

        return inputSnapshot.IsConnected;
    }

    public readonly record struct LayoutPreviewSegmentFrame(string DeviceId, string SegmentId, Color[] Colors);

    private sealed record LayoutPreviewFrame(LayoutPreviewSegmentFrame[] Segments);
}
