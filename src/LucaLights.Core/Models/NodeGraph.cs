using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LucaLights.Core.Models;

public sealed class NodeGraph
{
    [JsonPropertyName("nodes")]
    public List<NodeInstance> Nodes { get; set; } = [];

    [JsonPropertyName("connections")]
    public List<Connection> Connections { get; set; } = [];
}

public sealed class NodeInstance
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("typeId")]
    public string TypeId { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public JsonObject Properties { get; set; } = new();

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}

public sealed class Connection
{
    [JsonPropertyName("sourceNodeId")]
    public string SourceNodeId { get; set; } = string.Empty;

    [JsonPropertyName("sourcePortId")]
    public string SourcePortId { get; set; } = string.Empty;

    [JsonPropertyName("targetNodeId")]
    public string TargetNodeId { get; set; } = string.Empty;

    [JsonPropertyName("targetPortId")]
    public string TargetPortId { get; set; } = string.Empty;
}
