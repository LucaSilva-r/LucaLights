using System.Text.Json.Serialization;

namespace LucaLights.Core.NodeEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GraphDiagnosticSeverity
{
    Info,
    Warning,
    Error
}
