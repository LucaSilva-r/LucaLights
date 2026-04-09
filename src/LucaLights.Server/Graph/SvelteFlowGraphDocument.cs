using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LucaLights.Core.Models;

namespace LucaLights.Server.Graph;

public sealed class SvelteFlowGraphDocument
{
    [JsonPropertyName("nodes")]
    public List<SvelteFlowNode> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<SvelteFlowEdge> Edges { get; set; } = [];

    [JsonPropertyName("viewport")]
    public SvelteFlowViewport Viewport { get; set; } = new();
}

public sealed class SvelteFlowNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public SvelteFlowPosition Position { get; set; } = new();

    [JsonPropertyName("data")]
    public SvelteFlowNodeData Data { get; set; } = new();
}

public sealed class SvelteFlowNodeData
{
    [JsonPropertyName("properties")]
    public JsonObject Properties { get; set; } = new();
}

public sealed class SvelteFlowPosition
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}

public sealed class SvelteFlowEdge
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("sourceHandle")]
    public string SourceHandle { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("targetHandle")]
    public string TargetHandle { get; set; } = string.Empty;
}

public sealed class SvelteFlowViewport
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("zoom")]
    public float Zoom { get; set; } = 1;
}

public static class SvelteFlowGraphAdapter
{
    public static NodeGraph ToNodeGraph(SvelteFlowGraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new NodeGraph
        {
            Nodes = (document.Nodes ?? []).Select(node => new NodeInstance
            {
                Id = node.Id,
                TypeId = node.Type,
                Properties = CloneJsonObject(node.Data?.Properties),
                X = node.Position?.X ?? 0,
                Y = node.Position?.Y ?? 0
            }).ToList(),
            Connections = (document.Edges ?? []).Select(edge => new Connection
            {
                Id = edge.Id,
                SourceNodeId = edge.Source,
                SourcePortId = edge.SourceHandle,
                TargetNodeId = edge.Target,
                TargetPortId = edge.TargetHandle
            }).ToList(),
            Viewport = new GraphViewport
            {
                X = document.Viewport?.X ?? 0,
                Y = document.Viewport?.Y ?? 0,
                Zoom = document.Viewport?.Zoom ?? 1
            }
        };
    }

    public static SvelteFlowGraphDocument FromNodeGraph(NodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        return new SvelteFlowGraphDocument
        {
            Nodes = (graph.Nodes ?? []).Select(node => new SvelteFlowNode
            {
                Id = node.Id,
                Type = node.TypeId,
                Position = new SvelteFlowPosition
                {
                    X = node.X,
                    Y = node.Y
                },
                Data = new SvelteFlowNodeData
                {
                    Properties = CloneJsonObject(node.Properties)
                }
            }).ToList(),
            Edges = (graph.Connections ?? []).Select(connection => new SvelteFlowEdge
            {
                Id = connection.Id,
                Source = connection.SourceNodeId,
                SourceHandle = connection.SourcePortId,
                Target = connection.TargetNodeId,
                TargetHandle = connection.TargetPortId
            }).ToList(),
            Viewport = new SvelteFlowViewport
            {
                X = graph.Viewport?.X ?? 0,
                Y = graph.Viewport?.Y ?? 0,
                Zoom = graph.Viewport?.Zoom ?? 1
            }
        };
    }

    private static JsonObject CloneJsonObject(JsonObject? value)
    {
        return value?.DeepClone() as JsonObject ?? new JsonObject();
    }
}
