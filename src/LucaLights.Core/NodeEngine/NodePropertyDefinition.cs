using System.Text.Json.Nodes;

namespace LucaLights.Core.NodeEngine;

public sealed record NodePropertyDefinition(
    string Key,
    string Label,
    NodeValueType ValueType,
    string Description,
    JsonNode? DefaultValue = null,
    float? MinFloatValue = null,
    float? MaxFloatValue = null);
