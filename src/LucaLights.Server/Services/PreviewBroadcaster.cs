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
            CreateEvent("preview.snapshot", CapturePreview(null))
        };

        return _hub.AcceptAsync(socket, initialMessages, cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lightingManager.FrameRendered += HandleFrameRendered;
        _lightingManager.OutputCleared += HandleOutputCleared;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _lightingManager.FrameRendered -= HandleFrameRendered;
        _lightingManager.OutputCleared -= HandleOutputCleared;

        return Task.CompletedTask;
    }

    private void HandleFrameRendered(LightingFrameContext frameContext)
    {
        if (frameContext.FrameIndex % SampleEveryFrame != 0)
        {
            return;
        }

        _ = _hub.BroadcastAsync(CreateEvent("preview.frame", CapturePreview(frameContext))).AsTask();
    }

    private void HandleOutputCleared()
    {
        _ = _hub.BroadcastAsync(CreateEvent("preview.cleared", CapturePreview(null))).AsTask();
    }

    private object CapturePreview(LightingFrameContext? frameContext)
    {
        lock (_lightingManager.SyncRoot)
        {
            return new
            {
                frameIndex = frameContext?.FrameIndex,
                totalElapsedMs = frameContext?.TotalElapsed.TotalMilliseconds,
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
                        colors = segment.Leds
                            .Take(MaxPreviewLedsPerSegment)
                            .Select(color => new[] { color.R, color.G, color.B })
                            .ToArray()
                    }).ToArray()
                }).ToArray()
            };
        }
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
