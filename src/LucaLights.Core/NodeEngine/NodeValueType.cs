using System.Text.Json.Serialization;

namespace LucaLights.Core.NodeEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NodeValueType
{
    Bool,
    Float,
    Color,
    String,
    Trigger
}
