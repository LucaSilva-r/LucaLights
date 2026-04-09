using LucaLights.Core.Configuration;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;
using LucaLights.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LucaLights.Server.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/settings", GetSettings);
        endpoints.MapPut("/api/settings", ReplaceSettings);

        endpoints.MapPut("/api/settings/active-effect", SetActiveEffect);

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

        endpoints.MapGet("/api/effects", ListEffects);
        endpoints.MapPost("/api/effects", CreateEffect);
        endpoints.MapGet("/api/effects/{effectId}", GetEffect);
        endpoints.MapPut("/api/effects/{effectId}", UpdateEffect);
        endpoints.MapDelete("/api/effects/{effectId}", DeleteEffect);

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
        Settings replacement,
        Settings settings,
        ConfigManager configManager,
        LightingManager lightingManager,
        RuntimeEventBroadcaster eventBroadcaster,
        GameInputManager inputManager,
        CancellationToken cancellationToken)
    {
        if (replacement is null)
        {
            return Results.BadRequest("Settings body is required.");
        }

        NormalizeSettings(replacement);

        var activeInputModuleId = replacement.ActiveInputModuleId;
        var activeEffectId = replacement.ActiveEffectId;
        var activeInputModuleChanged = false;

        lock (lightingManager.SyncRoot)
        {
            activeInputModuleChanged = !string.Equals(
                settings.ActiveInputModuleId,
                activeInputModuleId,
                StringComparison.OrdinalIgnoreCase);

            foreach (var device in settings.Devices)
            {
                device.Dispose();
            }

            settings.Devices = replacement.Devices;
            settings.Effects = replacement.Effects;
            settings.ActiveEffectId = activeEffectId;
            settings.ActiveInputModuleId = activeInputModuleId;
            settings.InputModuleSettings = replacement.InputModuleSettings;

            SaveDirty(settings, configManager, eventBroadcaster, "settings.replaced");
        }

        if (activeInputModuleChanged)
        {
            await inputManager.SetActiveModuleAsync(activeInputModuleId, cancellationToken);
        }

        return Results.Ok(new SettingsResponse(configManager.SettingsPath, settings));
    }

    private static IResult SetActiveEffect(
        ActiveEffectRequest request,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        if (request is null)
        {
            return Results.BadRequest("Request body is required.");
        }

        lock (lightingManager.SyncRoot)
        {
            if (!string.IsNullOrEmpty(request.ActiveEffectId)
                && FindEffectIndex(settings, request.ActiveEffectId) < 0)
            {
                return Results.NotFound($"Effect not found: {request.ActiveEffectId}");
            }

            settings.ActiveEffectId = string.IsNullOrEmpty(request.ActiveEffectId) ? null : request.ActiveEffectId;
            SaveDirty(settings, configManager, eventBroadcaster, "activeEffect.changed");
        }

        return Results.Ok(new { activeEffectId = settings.ActiveEffectId });
    }

    private static IResult ListDevices(Settings settings, LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            return Results.Ok(settings.Devices.ToArray());
        }
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

    private static IResult ListEffects(Settings settings, LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            return Results.Ok(settings.Effects.ToArray());
        }
    }

    private static IResult CreateEffect(
        Effect effect,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        if (effect is null)
        {
            return Results.BadRequest("Effect body is required.");
        }

        NormalizeEffect(effect);

        lock (lightingManager.SyncRoot)
        {
            if (FindEffectIndex(settings, effect.Id) >= 0)
            {
                return Results.Conflict($"Effect id already exists: {effect.Id}");
            }

            settings.Effects.Add(effect);
            SaveDirty(settings, configManager, eventBroadcaster, "effect.created");
        }

        return Results.Created($"/api/effects/{Uri.EscapeDataString(effect.Id)}", effect);
    }

    private static IResult GetEffect(string effectId, Settings settings, LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            var index = FindEffectIndex(settings, effectId);
            return index < 0
                ? Results.NotFound()
                : Results.Ok(settings.Effects[index]);
        }
    }

    private static IResult UpdateEffect(
        string effectId,
        Effect effect,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        if (effect is null)
        {
            return Results.BadRequest("Effect body is required.");
        }

        if (!string.IsNullOrWhiteSpace(effect.Id)
            && !string.Equals(effect.Id, effectId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Effect id in the body must match the route id.");
        }

        effect.Id = effectId;
        NormalizeEffect(effect);

        lock (lightingManager.SyncRoot)
        {
            var index = FindEffectIndex(settings, effectId);
            if (index < 0)
            {
                return Results.NotFound();
            }

            settings.Effects[index] = effect;
            SaveDirty(settings, configManager, eventBroadcaster, "effect.updated");
        }

        return Results.Ok(effect);
    }

    private static IResult DeleteEffect(
        string effectId,
        Settings settings,
        ConfigManager configManager,
        RuntimeEventBroadcaster eventBroadcaster,
        LightingManager lightingManager)
    {
        lock (lightingManager.SyncRoot)
        {
            var index = FindEffectIndex(settings, effectId);
            if (index < 0)
            {
                return Results.NotFound();
            }

            settings.Effects.RemoveAt(index);
            SaveDirty(settings, configManager, eventBroadcaster, "effect.deleted");
        }

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

        foreach (var device in settings.Devices)
        {
            NormalizeDevice(device);
        }

        foreach (var effect in settings.Effects)
        {
            NormalizeEffect(effect);
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
    }

    private static void NormalizeEffect(Effect effect)
    {
        if (string.IsNullOrWhiteSpace(effect.Id))
        {
            effect.Id = Guid.NewGuid().ToString("N");
        }

        effect.Graph ??= new NodeGraph();
        NodeGraphCompiler.NormalizeGraph(effect.Graph);
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

    private static int FindEffectIndex(Settings settings, string effectId)
    {
        return settings.Effects.FindIndex(
            effect => string.Equals(effect.Id, effectId, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record SettingsResponse(string SettingsPath, Settings Settings);

    private sealed record ActiveEffectRequest(string? ActiveEffectId);
}
