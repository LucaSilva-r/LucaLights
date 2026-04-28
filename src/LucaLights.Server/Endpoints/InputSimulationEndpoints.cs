using LucaLights.Core;
using LucaLights.Core.GameInput;
using LucaLights.Server.Services;

namespace LucaLights.Server.Endpoints;

public static class InputSimulationEndpoints
{
    public static IEndpointRouteBuilder MapInputSimulationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/input-simulation", GetSimulation);
        endpoints.MapPut("/api/input-simulation", SetSimulation);
        endpoints.MapPut("/api/input-simulation/bool/{key}", SetBool);
        endpoints.MapPost("/api/input-simulation/pulse/{key}", TriggerPulse);
        endpoints.MapPut("/api/input-simulation/float/{key}", SetFloat);
        endpoints.MapPut("/api/input-simulation/color/{key}", SetColor);
        return endpoints;
    }

    private static IResult GetSimulation(GameInputManager inputManager)
    {
        return Results.Ok(inputManager.GetSimulationState());
    }

    private static async Task<IResult> SetSimulation(
        GameInputManager inputManager,
        RuntimeEventBroadcaster broadcaster,
        InputSimulationModeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleId))
        {
            return Results.BadRequest("Module id is required.");
        }

        if (!inputManager.SetSimulationEnabled(
                request.ModuleId,
                request.Enabled,
                out var snapshot,
                out var activeModuleId))
        {
            return Results.NotFound($"Input module '{request.ModuleId}' was not found.");
        }

        await broadcaster.PublishModuleChangedAsync(activeModuleId);
        if (snapshot is not null)
        {
            await broadcaster.PublishInputSnapshotAsync(snapshot);
        }

        return Results.Ok(new
        {
            simulation = inputManager.GetSimulationState(),
            snapshot
        });
    }

    private static async Task<IResult> SetBool(
        string key,
        GameInputManager inputManager,
        RuntimeEventBroadcaster broadcaster,
        BoolValueRequest request)
    {
        if (!inputManager.SetSimulationBool(key, request.Value, out var snapshot) || snapshot is null)
        {
            return Results.BadRequest("Input simulation is not active.");
        }

        await broadcaster.PublishInputSnapshotAsync(snapshot);
        return Results.Ok(snapshot);
    }

    private static async Task<IResult> TriggerPulse(
        string key,
        GameInputManager inputManager,
        RuntimeEventBroadcaster broadcaster)
    {
        if (!inputManager.TriggerSimulationPulse(key, out var snapshot) || snapshot is null)
        {
            return Results.BadRequest("Input simulation is not active.");
        }

        await broadcaster.PublishInputSnapshotAsync(snapshot);
        return Results.Ok(snapshot);
    }

    private static async Task<IResult> SetFloat(
        string key,
        GameInputManager inputManager,
        RuntimeEventBroadcaster broadcaster,
        FloatValueRequest request)
    {
        if (!inputManager.SetSimulationFloat(key, request.Value, out var snapshot) || snapshot is null)
        {
            return Results.BadRequest("Input simulation is not active.");
        }

        await broadcaster.PublishInputSnapshotAsync(snapshot);
        return Results.Ok(snapshot);
    }

    private static async Task<IResult> SetColor(
        string key,
        GameInputManager inputManager,
        RuntimeEventBroadcaster broadcaster,
        ColorValueRequest request)
    {
        var color = Color.FromRgb(request.R, request.G, request.B);
        if (!inputManager.SetSimulationColor(key, color, out var snapshot) || snapshot is null)
        {
            return Results.BadRequest("Input simulation is not active.");
        }

        await broadcaster.PublishInputSnapshotAsync(snapshot);
        return Results.Ok(snapshot);
    }

    private sealed record InputSimulationModeRequest(string ModuleId, bool Enabled);

    private sealed record BoolValueRequest(bool Value);

    private sealed record FloatValueRequest(float Value);

    private sealed record ColorValueRequest(byte R, byte G, byte B);
}
