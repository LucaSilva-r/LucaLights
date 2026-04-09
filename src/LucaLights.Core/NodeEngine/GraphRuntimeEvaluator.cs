using System.Text.Json.Nodes;
using LucaLights.Core.Engine;
using LucaLights.Core.Models;

namespace LucaLights.Core.NodeEngine;

public sealed class GraphRuntimeEvaluator
{
    private readonly NodeGraphCompiler _nodeGraphCompiler;
    private readonly INodeTypeCatalog _nodeTypeCatalog;

    public GraphRuntimeEvaluator(
        NodeGraphCompiler nodeGraphCompiler,
        INodeTypeCatalog nodeTypeCatalog)
    {
        _nodeGraphCompiler = nodeGraphCompiler;
        _nodeTypeCatalog = nodeTypeCatalog;
    }

    public PreparedGraph? Prepare(NodeGraph? graph)
    {
        if (graph is null)
        {
            return null;
        }

        var compiled = _nodeGraphCompiler.Compile(graph);
        var nodeTypesByNodeId = new Dictionary<string, NodeTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        var inputConnectionsByNodeId = new Dictionary<string, Dictionary<string, RuntimeConnectionSource>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in compiled.Graph.Nodes)
        {
            if (_nodeTypeCatalog.TryGetNodeType(node.TypeId, out var nodeType) && nodeType is not null)
            {
                nodeTypesByNodeId[node.Id] = nodeType;
            }
        }

        foreach (var connection in compiled.Graph.Connections)
        {
            if (!inputConnectionsByNodeId.TryGetValue(connection.TargetNodeId, out var targetInputs))
            {
                targetInputs = new Dictionary<string, RuntimeConnectionSource>(StringComparer.OrdinalIgnoreCase);
                inputConnectionsByNodeId[connection.TargetNodeId] = targetInputs;
            }

            targetInputs[connection.TargetPortId] = new RuntimeConnectionSource(
                connection.SourceNodeId,
                connection.SourcePortId);
        }

        return new PreparedGraph(
            compiled,
            nodeTypesByNodeId,
            inputConnectionsByNodeId);
    }

    public void Render(
        Settings settings,
        PreparedGraph? preparedEffect,
        LightingFrameContext frameContext)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (preparedEffect is null || !preparedEffect.CanRender)
        {
            return;
        }

        var outputs = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in preparedEffect.CompiledGraph.EvaluationOrder)
        {
            switch (node.TypeId)
            {
                case "constant.color":
                    outputs[BuildOutputKey(node.Id, "color")] = RuntimeValue.FromColor(ReadColor(node.Properties));
                    break;

                case "constant.float":
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(ReadFloat(node.Properties, "value"));
                    break;

                case "constant.bool":
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromBool(ReadBool(node.Properties, "value"));
                    break;

                case "input.bool":
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromBool(
                        frameContext.InputSnapshot.GetBool(ReadString(node.Properties, "key", "input")));
                    break;

                case "input.float":
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(
                        frameContext.InputSnapshot.GetFloat(ReadString(node.Properties, "key", "input")));
                    break;

                case "input.color":
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromColor(
                        frameContext.InputSnapshot.GetColor(ReadString(node.Properties, "key", "input"), Color.Black));
                    break;

                case "logic.select-color":
                {
                    var condition = GetInputBool(preparedEffect, outputs, node.Id, "condition");
                    var selectedColor = condition
                        ? GetInputColor(preparedEffect, outputs, node.Id, "trueColor")
                        : GetInputColor(preparedEffect, outputs, node.Id, "falseColor");
                    outputs[BuildOutputKey(node.Id, "color")] = RuntimeValue.FromColor(selectedColor);
                    break;
                }

                case "output.segment-color":
                    ApplySegmentColor(
                        settings,
                        node,
                        GetInputColor(preparedEffect, outputs, node.Id, "color"));
                    break;
            }
        }
    }

    private static bool GetInputBool(
        PreparedGraph preparedEffect,
        Dictionary<string, RuntimeValue> outputs,
        string nodeId,
        string portId,
        bool defaultValue = false)
    {
        return TryGetInputValue(preparedEffect, outputs, nodeId, portId, out var value)
            && value.Type == NodeValueType.Bool
            ? value.BoolValue
            : defaultValue;
    }

    private static Color GetInputColor(
        PreparedGraph preparedEffect,
        Dictionary<string, RuntimeValue> outputs,
        string nodeId,
        string portId,
        Color? defaultValue = null)
    {
        return TryGetInputValue(preparedEffect, outputs, nodeId, portId, out var value)
            && value.Type == NodeValueType.Color
            ? value.ColorValue
            : defaultValue ?? Color.Black;
    }

    private static bool TryGetInputValue(
        PreparedGraph preparedEffect,
        Dictionary<string, RuntimeValue> outputs,
        string nodeId,
        string portId,
        out RuntimeValue value)
    {
        value = default;

        if (!preparedEffect.TryGetInputSource(nodeId, portId, out var source))
        {
            return false;
        }

        return outputs.TryGetValue(BuildOutputKey(source.SourceNodeId, source.SourcePortId), out value);
    }

    private static string BuildOutputKey(string nodeId, string portId)
    {
        return $"{nodeId}:{portId}";
    }

    private static void ApplySegmentColor(Settings settings, NodeInstance node, Color color)
    {
        var deviceIds = ParseStringSet(ReadString(node.Properties, "deviceIds", string.Empty));
        var segmentIds = ParseStringSet(ReadString(node.Properties, "segmentIds", string.Empty));
        var groupIds = ParseIntSet(ReadString(node.Properties, "groupIds", string.Empty));

        foreach (var device in settings.Devices)
        {
            if (deviceIds.Count > 0 && !deviceIds.Contains(device.Id))
            {
                continue;
            }

            foreach (var segment in device.Segments)
            {
                if (segmentIds.Count > 0 && !segmentIds.Contains(segment.Id))
                {
                    continue;
                }

                if (groupIds.Count > 0 && !segment.GroupIds.Any(groupIds.Contains))
                {
                    continue;
                }

                FillSegment(segment, color);
            }
        }
    }

    private static void FillSegment(Segment segment, Color color)
    {
        for (var i = 0; i < segment.Leds.Length; i++)
        {
            segment.Leds[i] = color;
        }
    }

    private static Color ReadColor(JsonObject properties)
    {
        return Color.FromRgb(
            ReadByte(properties, "r", 255),
            ReadByte(properties, "g", 255),
            ReadByte(properties, "b", 255));
    }

    private static byte ReadByte(JsonObject properties, string key, byte defaultValue)
    {
        return (byte)Math.Clamp(ReadFloat(properties, key, defaultValue), byte.MinValue, byte.MaxValue);
    }

    private static float ReadFloat(JsonObject properties, string key, float defaultValue = 0f)
    {
        var node = properties[key];
        if (node is null)
        {
            return defaultValue;
        }

        if (node is JsonValue floatNode && floatNode.TryGetValue<float>(out var floatValue))
        {
            return floatValue;
        }

        if (node is JsonValue intNode && intNode.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        if (node is JsonValue doubleNode && doubleNode.TryGetValue<double>(out var doubleValue))
        {
            return (float)doubleValue;
        }

        if (node is JsonValue stringNode
            && stringNode.TryGetValue<string>(out var stringValue)
            && float.TryParse(stringValue, out var parsedFloat))
        {
            return parsedFloat;
        }

        return defaultValue;
    }

    private static bool ReadBool(JsonObject properties, string key, bool defaultValue = false)
    {
        var node = properties[key];
        if (node is null)
        {
            return defaultValue;
        }

        if (node is JsonValue boolNode && boolNode.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        if (node is JsonValue stringNode
            && stringNode.TryGetValue<string>(out var stringValue)
            && bool.TryParse(stringValue, out var parsedBool))
        {
            return parsedBool;
        }

        return defaultValue;
    }

    private static string ReadString(JsonObject properties, string key, string defaultValue)
    {
        var node = properties[key];
        if (node is JsonValue stringNode && stringNode.TryGetValue<string>(out var value))
        {
            return value?.Trim() ?? defaultValue;
        }

        return defaultValue;
    }

    private static HashSet<string> ParseStringSet(string value)
    {
        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<int> ParseIntSet(string value)
    {
        var values = new HashSet<int>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out var parsed))
            {
                values.Add(parsed);
            }
        }

        return values;
    }
}

public sealed record PreparedGraph(
    CompiledNodeGraph CompiledGraph,
    IReadOnlyDictionary<string, NodeTypeDefinition> NodeTypesByNodeId,
    IReadOnlyDictionary<string, Dictionary<string, RuntimeConnectionSource>> InputConnectionsByNodeId)
{
    public bool CanRender => CompiledGraph.Validation.IsValid && CompiledGraph.EvaluationOrder.Count > 0;

    public bool TryGetInputSource(string nodeId, string portId, out RuntimeConnectionSource source)
    {
        source = default;

        return InputConnectionsByNodeId.TryGetValue(nodeId, out var inputs)
            && inputs.TryGetValue(portId, out source);
    }
}

public readonly record struct RuntimeConnectionSource(
    string SourceNodeId,
    string SourcePortId);

public readonly record struct RuntimeValue(
    NodeValueType Type,
    bool BoolValue,
    float FloatValue,
    Color ColorValue)
{
    public static RuntimeValue FromBool(bool value)
    {
        return new RuntimeValue(NodeValueType.Bool, value, default, default);
    }

    public static RuntimeValue FromFloat(float value)
    {
        return new RuntimeValue(NodeValueType.Float, default, value, default);
    }

    public static RuntimeValue FromColor(Color value)
    {
        return new RuntimeValue(NodeValueType.Color, default, default, value);
    }
}
