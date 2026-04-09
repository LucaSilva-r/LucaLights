using System.Text.Json.Serialization;

namespace LucaLights.Core.NodeEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NodePortDirection
{
    Input,
    Output
}
