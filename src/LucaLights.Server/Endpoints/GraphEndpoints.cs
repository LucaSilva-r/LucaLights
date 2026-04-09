using LucaLights.Core.Configuration;
using LucaLights.Core.Engine;
using LucaLights.Core.NodeEngine;
using LucaLights.Core.Models;
using LucaLights.Server.Graph;
using LucaLights.Server.Services;

namespace LucaLights.Server.Endpoints;

public static class GraphEndpoints
{
    public static IEndpointRouteBuilder MapGraphEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/graph", GetGraph);
        endpoints.MapPut("/api/graph", ReplaceGraph);
        endpoints.MapPost("/api/graph/validate", ValidateGraph);

        return endpoints;
    }

    private static IResult GetGraph(
        Settings settings,
        LightingManager lightingManager,
        NodeGraphCompiler graphCompiler)
    {
        lock (lightingManager.SyncRoot)
        {
            var compiled = graphCompiler.Compile(settings.Graph);
            return Results.Ok(CreateGraphResponse(compiled));
        }
    }

    private static IResult ReplaceGraph(
        SvelteFlowGraphDocument graph,
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

        var nodeGraph = SvelteFlowGraphAdapter.ToNodeGraph(graph);
        var compiled = graphCompiler.Compile(nodeGraph);

        lock (lightingManager.SyncRoot)
        {
            settings.Graph = compiled.Graph;
            SaveDirty(settings, configManager, eventBroadcaster, "graph.replaced");
        }

        return Results.Ok(CreateGraphResponse(compiled));
    }

    private static IResult ValidateGraph(
        SvelteFlowGraphDocument graph,
        NodeGraphCompiler graphCompiler)
    {
        if (graph is null)
        {
            return Results.BadRequest("Graph body is required.");
        }

        var nodeGraph = SvelteFlowGraphAdapter.ToNodeGraph(graph);
        return Results.Ok(CreateGraphResponse(graphCompiler.Compile(nodeGraph)));
    }

    private static object CreateGraphResponse(CompiledNodeGraph compiled)
    {
        return new
        {
            graph = SvelteFlowGraphAdapter.FromNodeGraph(compiled.Graph),
            validation = compiled.Validation,
            evaluationOrder = compiled.EvaluationOrder.Select(node => node.Id).ToArray()
        };
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
