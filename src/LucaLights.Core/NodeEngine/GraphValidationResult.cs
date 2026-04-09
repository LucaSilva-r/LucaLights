namespace LucaLights.Core.NodeEngine;

public sealed record GraphValidationResult(
    bool IsValid,
    IReadOnlyList<GraphDiagnostic> Diagnostics);
