using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;
using LucaLights.Server.Services;

namespace LucaLights.Server.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/node-types", GetNodeTypes);
        endpoints.MapPost("/api/system/restart-engine", RestartEngine);
        endpoints.MapPost("/api/system/shutdown", Shutdown);

        return endpoints;
    }

    private static IResult GetNodeTypes(INodeTypeCatalog nodeTypeCatalog)
    {
        return Results.Ok(new
        {
            schemaVersion = 1,
            nodeTypes = nodeTypeCatalog.GetNodeTypes()
        });
    }

    private static async Task<IResult> RestartEngine(
        Settings settings,
        GameInputManager inputManager,
        LightingManager lightingManager,
        RuntimeEventBroadcaster eventBroadcaster,
        CancellationToken cancellationToken)
    {
        lightingManager.Stop();
        await inputManager.StopAsync(cancellationToken);

        settings.Normalize();
        await inputManager.StartAsync(settings, cancellationToken);
        lightingManager.Start();

        var result = new
        {
            status = "restarted",
            lightingRunning = lightingManager.IsRunning,
            activeInputModuleId = inputManager.ActiveModuleId
        };

        await eventBroadcaster.PublishSystemEventAsync("engine.restarted", result, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Shutdown(
        IHostApplicationLifetime lifetime,
        RuntimeEventBroadcaster eventBroadcaster,
        CancellationToken cancellationToken)
    {
        await eventBroadcaster.PublishSystemEventAsync("system.shutdownRequested", cancellationToken: cancellationToken);
        lifetime.StopApplication();
        return Results.Accepted("/api/system/shutdown", new { status = "shutdown_requested" });
    }
}
