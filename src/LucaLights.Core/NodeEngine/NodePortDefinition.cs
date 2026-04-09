namespace LucaLights.Core.NodeEngine;

public sealed record NodePortDefinition(
    string Id,
    string Label,
    NodeValueType ValueType,
    NodePortDirection Direction,
    string Description,
    bool AllowMultipleConnections = false);
