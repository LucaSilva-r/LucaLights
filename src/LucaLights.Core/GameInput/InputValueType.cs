using System.Text.Json.Serialization;

namespace LucaLights.Core.GameInput;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InputValueType
{
    Bool,
    Float,
    Color,
    String
}
