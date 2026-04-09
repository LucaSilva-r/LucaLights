namespace LucaLights.Core.NodeEngine;

public sealed record GraphDiagnostic(
    GraphDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? NodeId = null,
    string? ConnectionId = null,
    string? PortId = null);
