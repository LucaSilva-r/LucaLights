using System.Net.WebSockets;
using LucaLights.Core.Engine;
using LucaLights.Core.Models;

namespace LucaLights.Server.Services;

public sealed class PreviewBroadcaster : IHostedService
{
    private const int SampleEveryFrame = 3;
    private const int MaxPreviewLedsPerSegment = 128;

    private readonly Settings _settings;
    private readonly LightingManager _lightingManager;
    private readonly WebSocketJsonHub _hub = new();

    public PreviewBroadcaster(Settings settings, LightingManager lightingManager)
    {
        _settings = settings;
        _lightingManager = lightingManager;
    }

    public int ClientCount => _hub.ClientCount;

    public Task AcceptClientAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var initialMessages = new[]
        {
            CreateEvent("preview.connected", new { clientCount = ClientCount + 1 }),
            CreateEvent("preview.topology", CaptureTopology())
        };

        return _hub.AcceptAsync(socket, initialMessages, cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lightingManager.FrameRendered += HandleFrameRendered;
        _lightingManager.OutputCleared += HandleOutputCleared;
        _lightingManager.SettingsApplied += HandleSettingsApplied;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _lightingManager.FrameRendered -= HandleFrameRendered;
        _lightingManager.OutputCleared -= HandleOutputCleared;
        _lightingManager.SettingsApplied -= HandleSettingsApplied;

        return Task.CompletedTask;
    }

    private void HandleFrameRendered(LightingFrameContext frameContext)
    {
        if (ClientCount == 0 || frameContext.FrameIndex % SampleEveryFrame != 0)
        {
            return;
        }

        _ = _hub.BroadcastBinaryAsync(CaptureFrame()).AsTask();
    }

    private void HandleOutputCleared()
    {
        if (ClientCount == 0)
        {
            return;
        }

        _ = _hub.BroadcastBinaryAsync(CaptureFrame()).AsTask();
    }

    private void HandleSettingsApplied()
    {
        if (ClientCount == 0)
        {
            return;
        }

        _ = _hub.BroadcastAsync(CreateEvent("preview.topology", CaptureTopology())).AsTask();
    }

    private object CaptureTopology()
    {
        lock (_lightingManager.SyncRoot)
        {
            var sampledLedOffset = 0;

            return new
            {
                totalLedCount = _settings.Devices.Sum(device => device.Segments.Sum(segment => segment.Length)),
                maxPreviewLedsPerSegment = MaxPreviewLedsPerSegment,
                devices = _settings.Devices.Select(device => new
                {
                    device.Id,
                    device.Name,
                    device.Ip,
                    protocol = device.TransportType.ToString(),
                    ledCount = device.Segments.Sum(segment => segment.Length),
                    segments = device.Segments.Select(segment => new
                    {
                        segment.Id,
                        segment.Name,
                        segment.Length,
                        sampledLedOffset,
                        sampledLedCount = AdvanceSampledLedOffset(ref sampledLedOffset, segment)
                    }).ToArray()
                }).ToArray()
            };
        }
    }

    private byte[] CaptureFrame()
    {
        lock (_lightingManager.SyncRoot)
        {
            var sampledLedCount = _settings.Devices.Sum(device =>
                device.Segments.Sum(segment => Math.Min(segment.Leds.Length, MaxPreviewLedsPerSegment)));
            var frame = new byte[sampledLedCount * 3];
            var offset = 0;

            foreach (var device in _settings.Devices)
            {
                foreach (var segment in device.Segments)
                {
                    var count = Math.Min(segment.Leds.Length, MaxPreviewLedsPerSegment);
                    for (var i = 0; i < count; i++)
                    {
                        var color = segment.Leds[i];
                        frame[offset++] = color.R;
                        frame[offset++] = color.G;
                        frame[offset++] = color.B;
                    }
                }
            }

            return frame;
        }
    }

    private static int AdvanceSampledLedOffset(ref int sampledLedOffset, Segment segment)
    {
        var sampledLedCount = Math.Min(segment.Leds.Length, MaxPreviewLedsPerSegment);
        sampledLedOffset += sampledLedCount;
        return sampledLedCount;
    }

    private static object CreateEvent(string type, object payload)
    {
        return new
        {
            type,
            timestampUtc = DateTimeOffset.UtcNow,
            payload
        };
    }
}
