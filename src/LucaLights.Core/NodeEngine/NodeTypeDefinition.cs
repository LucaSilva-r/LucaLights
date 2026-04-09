namespace LucaLights.Core.NodeEngine;

public sealed record NodeTypeDefinition(
    string TypeId,
    string DisplayName,
    string Category,
    string Description,
    IReadOnlyList<NodePortDefinition> Inputs,
    IReadOnlyList<NodePortDefinition> Outputs,
    IReadOnlyList<NodePropertyDefinition> Properties);
