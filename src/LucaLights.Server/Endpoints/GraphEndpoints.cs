using LucaLights.Core.Configuration;
using LucaLights.Core.Engine;
using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;
using LucaLights.Server.Services;

namespace LucaLights.Server.Endpoints;

public static class GraphEndpoints
{
    public static IEndpointRouteBuilder MapGraphEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/effects/{effectId}/graph", GetGraph);
        endpoints.MapPut("/api/effects/{effectId}/graph", ReplaceGraph);
        endpoints.MapPost("/api/effects/{effectId}/graph/validate", ValidateGraph);

        return endpoints;
    }

    private static IResult GetGraph(
        string effectId,
        Settings settings,
        LightingManager lightingManager,
        NodeGraphCompiler graphCompiler)
    {
        lock (lightingManager.SyncRoot)
        {
            var effect = FindEffect(settings, effectId);
            if (effect is null)
            {
                return Results.NotFound();
            }

            var compiled = graphCompiler.Compile(effect.Graph);
            return Results.Ok(CreateGraphResponse(compiled));
        }
    }

    private static IResult ReplaceGraph(
        string effectId,
        NodeGraph graph,
        Settings settings,
        ConfigManager configManager,
        LightingManager lightingManager,
        NodeGraphCompiler graphCompiler,
        RuntimeEventBroadcaster eventBroadcaster)
    {
        if (graph is null)
        {
            return Results.BadRequest("Graph body is required.");
        }

        var compiled = graphCompiler.Compile(graph);

        lock (lightingManager.SyncRoot)
        {
            var effect = FindEffect(settings, effectId);
            if (effect is null)
            {
                return Results.NotFound();
            }

            effect.Graph = graph;
            SaveDirty(settings, configManager, eventBroadcaster, "effect.graph.replaced");
        }

        return Results.Ok(CreateGraphResponse(compiled));
    }

    private static IResult ValidateGraph(
        string effectId,
        NodeGraph graph,
        Settings settings,
        LightingManager lightingManager,
        NodeGraphCompiler graphCompiler)
    {
        if (graph is null)
        {
            return Results.BadRequest("Graph body is required.");
        }

        lock (lightingManager.SyncRoot)
        {
            if (FindEffect(settings, effectId) is null)
            {
                return Results.NotFound();
            }
        }

        return Results.Ok(CreateGraphResponse(graphCompiler.Compile(graph)));
    }

    private static object CreateGraphResponse(CompiledNodeGraph compiled)
    {
        return new
        {
            graph = compiled.Graph,
            validation = compiled.Validation,
            evaluationOrder = compiled.EvaluationOrder.Select(node => node.Id).ToArray()
        };
    }

    private static Effect? FindEffect(Settings settings, string effectId)
    {
        return settings.Effects.FirstOrDefault(
            effect => string.Equals(effect.Id, effectId, StringComparison.OrdinalIgnoreCase));
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
}
