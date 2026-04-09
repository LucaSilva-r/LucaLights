using LucaLights.Core.Models;

namespace LucaLights.Core.NodeEngine;

public sealed record CompiledNodeGraph(
    NodeGraph Graph,
    IReadOnlyList<NodeInstance> EvaluationOrder,
    GraphValidationResult Validation);
