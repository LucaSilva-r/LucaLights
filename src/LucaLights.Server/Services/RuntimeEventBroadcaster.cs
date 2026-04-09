using System.Net.WebSockets;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;

namespace LucaLights.Server.Services;

public sealed class RuntimeEventBroadcaster
{
    private readonly WebSocketJsonHub _hub = new();

    public int ClientCount => _hub.ClientCount;

    public Task AcceptClientAsync(
        WebSocket socket,
        GameInputManager inputManager,
        CancellationToken cancellationToken)
    {
        var initialMessages = new[]
        {
            CreateEvent("events.connected", new
            {
                activeModuleId = inputManager.ActiveModuleId,
                clientCount = ClientCount + 1
            }),
            CreateEvent("input.snapshot", inputManager.LatestSnapshot)
        };

        return _hub.AcceptAsync(socket, initialMessages, cancellationToken);
    }

    public ValueTask PublishInputSnapshotAsync(InputSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        return PublishAsync("input.snapshot", snapshot, cancellationToken);
    }

    public ValueTask PublishModuleChangedAsync(string? moduleId, CancellationToken cancellationToken = default)
    {
        return PublishAsync("input.moduleChanged", new { activeModuleId = moduleId }, cancellationToken);
    }

    public ValueTask PublishSettingsChangedAsync(
        Settings settings,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return PublishAsync("settings.changed", CreateSettingsSummary(settings, reason), cancellationToken);
    }

    public ValueTask PublishSystemEventAsync(
        string reason,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        return PublishAsync("system.event", new { reason, details }, cancellationToken);
    }

    public ValueTask PublishAsync(string type, object payload, CancellationToken cancellationToken = default)
    {
        return _hub.BroadcastAsync(CreateEvent(type, payload), cancellationToken);
    }

    private static RuntimeEventEnvelope CreateEvent(string type, object payload)
    {
        return new RuntimeEventEnvelope(type, DateTimeOffset.UtcNow, payload);
    }

    private static object CreateSettingsSummary(Settings settings, string reason)
    {
        return new
        {
            reason,
            devices = settings.Devices.Count,
            effects = settings.Effects.Count,
            activeInputModuleId = settings.ActiveInputModuleId,
            dirty = settings.Dirty
        };
    }

    private sealed record RuntimeEventEnvelope(
        string Type,
        DateTimeOffset TimestampUtc,
        object Payload);
}
