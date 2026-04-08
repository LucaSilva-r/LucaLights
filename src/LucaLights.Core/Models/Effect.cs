using System.Text.Json.Serialization;

namespace LucaLights.Core.Models;

public sealed class Effect
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = "New Effect";

    [JsonPropertyName("graph")]
    public NodeGraph Graph { get; set; } = new();
}
