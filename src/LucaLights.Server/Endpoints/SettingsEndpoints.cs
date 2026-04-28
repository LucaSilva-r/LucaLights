using LucaLights.Core.Configuration;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.GameInput.Modules;
using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;
using CoreColor = LucaLights.Core.Color;
using LucaLights.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LucaLights.Server.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/settings", GetSettings);
        endpoints.MapPut("/api/settings", ReplaceSettings);
        endpoints.MapGet("/api/room-layout", GetRoomLayout);
        endpoints.MapPut("/api/room-layout", UpdateRoomLayout);

        endpoints.MapGet("/api/devices", ListDevices);
        endpoints.MapPost("/api/devices", CreateDevice);
        endpoints.MapGet("/api/devices/{deviceId}", GetDevice);
        endpoints.MapPut("/api/devices/{deviceId}", UpdateDevice);
        endpoints.MapDelete("/api/devices/{deviceId}", DeleteDevice);

        endpoints.MapGet("/api/devices/{deviceId}/segments", ListSegments);
        endpoints.MapPost("/api/devices/{deviceId}/segments", CreateSegment);
        endpoints.MapGet("/api/devices/{deviceId}/segments/{segmentId}", GetSegment);
        endpoints.MapPut("/api/devices/{deviceId}/segments/{segmentId}", UpdateSegment);
        endpoints.MapDelete("/api/devices/{deviceId}/segments/{segmentId}", DeleteSegment);

        endpoints.MapPost("/api/layout-preview", SetLayoutPreview);
        endpoints.MapPost("/api/layout-preview/batch", SetLayoutPreviewBatch);
        endpoints.MapDelete("/api/layout-preview", ClearLayoutPreview);

        return endpoints;
    }

    private static IResult GetSettings(
        Settings settings,
        ConfigManager configManager,
        LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            return Results.Ok(new SettingsResponse(configManager.SettingsPath, settings));
        }
    }

    private static async Task<IResult> ReplaceSettings(
        [FromBody] Settings replacement,
        Settings settings,
        ConfigManager configManager,
        LightingManager lightingManager,
        RuntimeEventBroadcaster eventBroadcaster,
        GameInputManager inputManager,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (replacement is null)
        {
            return Results.BadRequest("Settings body is required.");
        }

        NormalizeSettings(replacement);

        var activeInputModuleId = replacement.ActiveInputModuleId;
        var logger = loggerFactory.CreateLogger("SettingsEndpoints");

        lock (lightingManager.SyncRoot)
        {
            foreach (var device in settings.Devices)
            {
                device.Dispose();
            }

            settings.Devices = replacement.Devices;
            settings.Graph = replacement.Graph;
            settings.RoomLayout = replacement.RoomLayout;
            settings.ActiveInputModuleId = activeInputModuleId;
            settings.InputModuleSettings = replacement.InputModuleSettings;

            // Re-register modules with the new settings so that their configuration is updated.
            inputManager.RegisterModule(ITGManiaInputModule.CreateFromSettings(
                settings,
                message => logger.LogInformation("{Message}", message)));
            inputManager.RegisterModule(OsuInputModule.CreateFromSettings(
                settings,
                message => logger.LogInformation("{Message}", message)));

            SaveDirty(settings, configManager, eventBroadcaster, "settings.replaced");
        }

        // Always call SetActiveModuleAsync to ensure the module is re-started if the instance changed
        // (which it just did because we re-registered it).
        await inputManager.SetActiveModuleAsync(activeInputModuleId, cancellationToken);

        return Results.Ok(new SettingsResponse(configManager.SettingsPath, settings));
    }

    private static IResult ListDevices(Settings settings, LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            return Results.Ok(settings.Devices.ToArray());
        }
    }

    private static IResult GetRoomLayout(Settings settings, LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            settings.RoomLayout.Normalize(settings.Devices);
            return Results.Ok(settings.RoomLayout);
        }
    }

    private static IResult UpdateRoomLayout(
        RoomLayout roomLayout,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        if (roomLayout is null)
        {
            return Results.BadRequest("Room layout body is required.");
        }

        lock (lightingManager.SyncRoot)
        {
            roomLayout.Normalize(settings.Devices);
            settings.RoomLayout = roomLayout;
            SaveDirty(settings, configManager, eventBroadcaster, "room-layout.updated");
        }

        return Results.Ok(settings.RoomLayout);
    }

    private static IResult CreateDevice(
        Device device,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        if (device is null)
        {
            return Results.BadRequest("Device body is required.");
        }

        NormalizeDevice(device);

        lock (lightingManager.SyncRoot)
        {
            if (FindDeviceIndex(settings, device.Id) >= 0)
            {
                return Results.Conflict($"Device id already exists: {device.Id}");
            }

            settings.Devices.Add(device);
            SaveDirty(settings, configManager, eventBroadcaster, "device.created");
        }

        return Results.Created($"/api/devices/{Uri.EscapeDataString(device.Id)}", device);
    }

    private static IResult GetDevice(string deviceId, Settings settings, LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            var index = FindDeviceIndex(settings, deviceId);
            return index < 0
                ? Results.NotFound()
                : Results.Ok(settings.Devices[index]);
        }
    }

    private static IResult UpdateDevice(
        string deviceId,
        Device device,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        if (device is null)
        {
            return Results.BadRequest("Device body is required.");
        }

        if (!string.IsNullOrWhiteSpace(device.Id)
            && !string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Device id in the body must match the route id.");
        }

        device.Id = deviceId;
        NormalizeDevice(device);

        lock (lightingManager.SyncRoot)
        {
            var index = FindDeviceIndex(settings, deviceId);
            if (index < 0)
            {
                return Results.NotFound();
            }

            settings.Devices[index].Dispose();
            settings.Devices[index] = device;
            SaveDirty(settings, configManager, eventBroadcaster, "device.updated");
        }

        return Results.Ok(device);
    }

    private static IResult DeleteDevice(
        string deviceId,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            var index = FindDeviceIndex(settings, deviceId);
            if (index < 0)
            {
                return Results.NotFound();
            }

            settings.Devices[index].Dispose();
            settings.Devices.RemoveAt(index);
            SaveDirty(settings, configManager, eventBroadcaster, "device.deleted");
        }

        return Results.NoContent();
    }

    private static IResult ListSegments(
        string deviceId,
        Settings settings,
        LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            var device = FindDevice(settings, deviceId);
            return device is null
                ? Results.NotFound()
                : Results.Ok(device.Segments.ToArray());
        }
    }

    private static IResult CreateSegment(
        string deviceId,
        Segment segment,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        if (segment is null)
        {
            return Results.BadRequest("Segment body is required.");
        }

        NormalizeSegment(segment);

        lock (lightingManager.SyncRoot)
        {
            var device = FindDevice(settings, deviceId);
            if (device is null)
            {
                return Results.NotFound();
            }

            if (FindSegmentIndex(device, segment.Id) >= 0)
            {
                return Results.Conflict($"Segment id already exists: {segment.Id}");
            }

            device.Segments.Add(segment);
            SaveDirty(settings, configManager, eventBroadcaster, "segment.created");
        }

        return Results.Created(
            $"/api/devices/{Uri.EscapeDataString(deviceId)}/segments/{Uri.EscapeDataString(segment.Id)}",
            segment);
    }

    private static IResult GetSegment(
        string deviceId,
        string segmentId,
        Settings settings,
        LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            var device = FindDevice(settings, deviceId);
            if (device is null)
            {
                return Results.NotFound();
            }

            var segmentIndex = FindSegmentIndex(device, segmentId);
            return segmentIndex < 0
                ? Results.NotFound()
                : Results.Ok(device.Segments[segmentIndex]);
        }
    }

    private static IResult UpdateSegment(
        string deviceId,
        string segmentId,
        Segment segment,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        if (segment is null)
        {
            return Results.BadRequest("Segment body is required.");
        }

        if (!string.IsNullOrWhiteSpace(segment.Id)
            && !string.Equals(segment.Id, segmentId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Segment id in the body must match the route id.");
        }

        segment.Id = segmentId;
        NormalizeSegment(segment);

        lock (lightingManager.SyncRoot)
        {
            var device = FindDevice(settings, deviceId);
            if (device is null)
            {
                return Results.NotFound();
            }

            var segmentIndex = FindSegmentIndex(device, segmentId);
            if (segmentIndex < 0)
            {
                return Results.NotFound();
            }

            device.Segments[segmentIndex] = segment;
            SaveDirty(settings, configManager, eventBroadcaster, "segment.updated");
        }

        return Results.Ok(segment);
    }

    private static IResult DeleteSegment(
        string deviceId,
        string segmentId,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            var device = FindDevice(settings, deviceId);
            if (device is null)
            {
                return Results.NotFound();
            }

            var segmentIndex = FindSegmentIndex(device, segmentId);
            if (segmentIndex < 0)
            {
                return Results.NotFound();
            }

            device.Segments.RemoveAt(segmentIndex);
            SaveDirty(settings, configManager, eventBroadcaster, "segment.deleted");
        }

        return Results.NoContent();
    }

    private static IResult SetLayoutPreview(
        LayoutPreviewRequest request,
        Settings settings,
        LightingManager lightingManager)
    {
        if (request is null)
        {
            return Results.BadRequest("Preview body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.SegmentId))
        {
            return Results.BadRequest("Preview target is required.");
        }

        if (request.Colors is null)
        {
            return Results.BadRequest("Preview colors are required.");
        }

        lock (lightingManager.SyncRoot)
        {
            var device = FindDevice(settings, request.DeviceId);
            if (device is null)
            {
                return Results.NotFound();
            }

            var segmentIndex = FindSegmentIndex(device, request.SegmentId);
            if (segmentIndex < 0)
            {
                return Results.NotFound();
            }
        }

        var colors = request.Colors
            .Select(color => CoreColor.FromRgb(ClampByte(color.R), ClampByte(color.G), ClampByte(color.B)))
            .ToArray();

        lightingManager.SetLayoutPreviewFrame(request.DeviceId, request.SegmentId, colors);

        return Results.NoContent();
    }

    private static IResult SetLayoutPreviewBatch(
        LayoutPreviewBatchRequest request,
        Settings settings,
        LightingManager lightingManager)
    {
        if (request?.Segments is null)
        {
            return Results.BadRequest("Preview segments are required.");
        }

        var frames = new List<LightingManager.LayoutPreviewSegmentFrame>();
        lock (lightingManager.SyncRoot)
        {
            foreach (var segmentRequest in request.Segments)
            {
                if (segmentRequest.Colors is null
                    || string.IsNullOrWhiteSpace(segmentRequest.DeviceId)
                    || string.IsNullOrWhiteSpace(segmentRequest.SegmentId))
                {
                    return Results.BadRequest("Every preview segment requires a device id, segment id, and colors.");
                }

                var device = FindDevice(settings, segmentRequest.DeviceId);
                if (device is null || FindSegmentIndex(device, segmentRequest.SegmentId) < 0)
                {
                    return Results.NotFound();
                }

                frames.Add(new LightingManager.LayoutPreviewSegmentFrame(
                    segmentRequest.DeviceId,
                    segmentRequest.SegmentId,
                    segmentRequest.Colors
                        .Select(color => CoreColor.FromRgb(ClampByte(color.R), ClampByte(color.G), ClampByte(color.B)))
                        .ToArray()));
            }
        }

        lightingManager.SetLayoutPreviewFrames(frames);

        return Results.NoContent();
    }

    private static IResult ClearLayoutPreview(LightingManager lightingManager)
    {
        lightingManager.ClearLayoutPreviewFrame();
        return Results.NoContent();
    }

    private static void SaveDirty(
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        string reason)
    {
        settings.Normalize();
        settings.MarkDirty();
        configManager.Save(settings);
        _ = eventBroadcaster.PublishSettingsChangedAsync(settings, reason).AsTask();
    }

    private static void NormalizeSettings(Settings settings)
    {
        settings.Normalize();
        settings.RoomLayout ??= new RoomLayout();
        settings.RoomLayout.Normalize(settings.Devices);
        NodeGraphCompiler.NormalizeGraph(settings.Graph);

        foreach (var device in settings.Devices)
        {
            NormalizeDevice(device);
        }
    }

    private static void NormalizeDevice(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.Id))
        {
            device.Id = Guid.NewGuid().ToString("N");
        }

        device.Segments ??= [];

        foreach (var segment in device.Segments)
        {
            NormalizeSegment(segment);
        }
    }

    private static void NormalizeSegment(Segment segment)
    {
        if (string.IsNullOrWhiteSpace(segment.Id))
        {
            segment.Id = Guid.NewGuid().ToString("N");
        }

        segment.GroupIds ??= [];
        segment.Length = segment.Length;
        segment.NormalizeLayout();
    }

    private static Device? FindDevice(Settings settings, string deviceId)
    {
        var index = FindDeviceIndex(settings, deviceId);
        return index < 0 ? null : settings.Devices[index];
    }

    private static int FindDeviceIndex(Settings settings, string deviceId)
    {
        return settings.Devices.FindIndex(
            device => string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase));
    }

    private static int FindSegmentIndex(Device device, string segmentId)
    {
        return device.Segments.FindIndex(
            segment => string.Equals(segment.Id, segmentId, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record SettingsResponse(string SettingsPath, Settings Settings);

    private sealed record LayoutPreviewRequest(
        string DeviceId,
        string SegmentId,
        IReadOnlyList<LayoutPreviewColor> Colors);

    private sealed record LayoutPreviewBatchRequest(
        IReadOnlyList<LayoutPreviewRequest> Segments);

    private sealed record LayoutPreviewColor(int R, int G, int B);

    private static byte ClampByte(int value) => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
}
