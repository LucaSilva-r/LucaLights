using System.Text.Json.Nodes;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
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
        var totalSeconds = (float)frameContext.TotalElapsed.TotalSeconds;

        foreach (var node in preparedEffect.CompiledGraph.EvaluationOrder)
        {
            switch (node.TypeId)
            {
                case "annotation.comment":
                    break;

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
                        ReadMergedBool(frameContext.InputSnapshot, node.Properties));
                    break;

                case "input.float":
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(
                        ReadMergedFloat(frameContext.InputSnapshot, node.Properties));
                    break;

                case "input.color":
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromColor(
                        ReadMergedColor(frameContext.InputSnapshot, node.Properties));
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

                case "logic.select-float":
                {
                    var condition = GetInputBool(preparedEffect, outputs, node.Id, "condition");
                    var value = condition
                        ? GetInputFloat(preparedEffect, outputs, node.Id, "true", ReadFloat(node.Properties, "true", 1f))
                        : GetInputFloat(preparedEffect, outputs, node.Id, "false", ReadFloat(node.Properties, "false", 0f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(value);
                    break;
                }

                case "logic.not":
                {
                    var value = GetInputBool(preparedEffect, outputs, node.Id, "value");
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromBool(!value);
                    break;
                }

                case "logic.and":
                {
                    var a = GetInputBool(preparedEffect, outputs, node.Id, "a");
                    var b = GetInputBool(preparedEffect, outputs, node.Id, "b");
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromBool(a && b);
                    break;
                }

                case "logic.or":
                {
                    var a = GetInputBool(preparedEffect, outputs, node.Id, "a");
                    var b = GetInputBool(preparedEffect, outputs, node.Id, "b");
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromBool(a || b);
                    break;
                }

                case "logic.compare":
                {
                    var a = GetInputFloat(preparedEffect, outputs, node.Id, "a", ReadFloat(node.Properties, "a", 0f));
                    var b = GetInputFloat(preparedEffect, outputs, node.Id, "b", ReadFloat(node.Properties, "b", 0.5f));
                    var mode = ReadString(node.Properties, "mode", "greater");
                    var result = mode switch
                    {
                        "less" => a < b,
                        "equal" => MathF.Abs(a - b) < 0.0001f,
                        _ => a > b
                    };
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromBool(result);
                    break;
                }

                case "logic.mix-color":
                {
                    var colorA = GetInputColor(preparedEffect, outputs, node.Id, "a");
                    var colorB = GetInputColor(preparedEffect, outputs, node.Id, "b");
                    var factor = GetInputFloat(
                        preparedEffect,
                        outputs,
                        node.Id,
                        "factor",
                        ReadFloat(node.Properties, "factor", 0.5f));
                    outputs[BuildOutputKey(node.Id, "color")] = RuntimeValue.FromColor(
                        MixColors(colorA, colorB, factor));
                    break;
                }

                case "math.add":
                {
                    var a = GetInputFloat(preparedEffect, outputs, node.Id, "a", ReadFloat(node.Properties, "a", 0f));
                    var b = GetInputFloat(preparedEffect, outputs, node.Id, "b", ReadFloat(node.Properties, "b", 0f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(a + b);
                    break;
                }

                case "math.multiply":
                {
                    var a = GetInputFloat(preparedEffect, outputs, node.Id, "a", ReadFloat(node.Properties, "a", 1f));
                    var b = GetInputFloat(preparedEffect, outputs, node.Id, "b", ReadFloat(node.Properties, "b", 1f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(a * b);
                    break;
                }

                case "math.clamp":
                {
                    var value = GetInputFloat(preparedEffect, outputs, node.Id, "value", ReadFloat(node.Properties, "value", 0f));
                    var min = GetInputFloat(preparedEffect, outputs, node.Id, "min", ReadFloat(node.Properties, "min", 0f));
                    var max = GetInputFloat(preparedEffect, outputs, node.Id, "max", ReadFloat(node.Properties, "max", 1f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(Math.Clamp(value, min, max));
                    break;
                }

                case "math.remap":
                {
                    var value = GetInputFloat(preparedEffect, outputs, node.Id, "value", ReadFloat(node.Properties, "value", 0f));
                    var inMin = ReadFloat(node.Properties, "inMin", 0f);
                    var inMax = ReadFloat(node.Properties, "inMax", 1f);
                    var outMin = ReadFloat(node.Properties, "outMin", 0f);
                    var outMax = ReadFloat(node.Properties, "outMax", 1f);
                    var inRange = inMax - inMin;
                    var t = inRange == 0f ? 0f : (value - inMin) / inRange;
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(outMin + (t * (outMax - outMin)));
                    break;
                }

                case "math.modulo":
                {
                    var value = GetInputFloat(preparedEffect, outputs, node.Id, "value", ReadFloat(node.Properties, "value", 0f));
                    var divisor = GetInputFloat(preparedEffect, outputs, node.Id, "divisor", ReadFloat(node.Properties, "divisor", 1f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(
                        divisor == 0f ? 0f : value % divisor);
                    break;
                }

                case "math.abs":
                {
                    var value = GetInputFloat(preparedEffect, outputs, node.Id, "value", ReadFloat(node.Properties, "value", 0f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(MathF.Abs(value));
                    break;
                }

                case "math.step":
                {
                    var value = GetInputFloat(preparedEffect, outputs, node.Id, "value", ReadFloat(node.Properties, "value", 0f));
                    var edge = GetInputFloat(preparedEffect, outputs, node.Id, "edge", ReadFloat(node.Properties, "edge", 0.5f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(value >= edge ? 1f : 0f);
                    break;
                }

                case "math.smooth-step":
                {
                    var value = GetInputFloat(preparedEffect, outputs, node.Id, "value", ReadFloat(node.Properties, "value", 0f));
                    var edge0 = ReadFloat(node.Properties, "edge0", 0f);
                    var edge1 = ReadFloat(node.Properties, "edge1", 1f);
                    var t = Math.Clamp((value - edge0) / (edge1 - edge0 == 0f ? 1f : edge1 - edge0), 0f, 1f);
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(t * t * (3f - 2f * t));
                    break;
                }

                case "color.brightness":
                {
                    var color = GetInputColor(preparedEffect, outputs, node.Id, "color");
                    var factor = GetInputFloat(preparedEffect, outputs, node.Id, "factor", ReadFloat(node.Properties, "factor", 1f));
                    outputs[BuildOutputKey(node.Id, "color")] = RuntimeValue.FromColor(ScaleColor(color, factor));
                    break;
                }

                case "color.hsv":
                {
                    var hue = GetInputFloat(preparedEffect, outputs, node.Id, "hue", ReadFloat(node.Properties, "hue", 0f));
                    var saturation = GetInputFloat(preparedEffect, outputs, node.Id, "saturation", ReadFloat(node.Properties, "saturation", 1f));
                    var brightness = GetInputFloat(preparedEffect, outputs, node.Id, "brightness", ReadFloat(node.Properties, "brightness", 1f));
                    outputs[BuildOutputKey(node.Id, "color")] = RuntimeValue.FromColor(HsvToColor(hue, saturation, brightness));
                    break;
                }

                case "time.elapsed":
                    outputs[BuildOutputKey(node.Id, "seconds")] = RuntimeValue.FromFloat(totalSeconds);
                    break;

                case "time.oscillator":
                {
                    var speed = GetInputFloat(preparedEffect, outputs, node.Id, "speed", ReadFloat(node.Properties, "speed", 1f));
                    var waveform = ReadString(node.Properties, "waveform", "sine");
                    var phase = totalSeconds * speed;
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(EvaluateWaveform(waveform, phase));
                    break;
                }

                case "time.pulse":
                {
                    var trigger = GetInputBool(preparedEffect, outputs, node.Id, "trigger");
                    var duration = GetInputFloat(preparedEffect, outputs, node.Id, "duration", ReadFloat(node.Properties, "duration", 0.5f));
                    var edge = ReadString(node.Properties, "edge", "rising");
                    var pulseState = preparedEffect.GetOrCreatePulseState(node.Id);
                    pulseState.UpdatePulse(trigger, totalSeconds, duration, edge == "falling");
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(pulseState.CurrentValue);
                    break;
                }

                case "time.envelope":
                {
                    var trigger = GetInputBool(preparedEffect, outputs, node.Id, "trigger");
                    var release = GetInputFloat(preparedEffect, outputs, node.Id, "release", ReadFloat(node.Properties, "release", 0.5f));
                    var envelopeState = preparedEffect.GetOrCreateEnvelopeState(node.Id);
                    envelopeState.Update(trigger, totalSeconds, release);
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(envelopeState.CurrentValue);
                    break;
                }

                case "output.segment-color":
                    ApplySegmentColor(
                        settings,
                        node,
                        GetInputColor(preparedEffect, outputs, node.Id, "color"));
                    break;

                case "output.segment-gradient":
                    ApplySegmentGradient(
                        settings,
                        node,
                        GetInputColor(preparedEffect, outputs, node.Id, "colorA"),
                        GetInputColor(preparedEffect, outputs, node.Id, "colorB"),
                        GetInputFloat(preparedEffect, outputs, node.Id, "offset", ReadFloat(node.Properties, "offset", 0f)));
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

    private static float GetInputFloat(
        PreparedGraph preparedEffect,
        Dictionary<string, RuntimeValue> outputs,
        string nodeId,
        string portId,
        float defaultValue = 0f)
    {
        return TryGetInputValue(preparedEffect, outputs, nodeId, portId, out var value)
            && value.Type == NodeValueType.Float
            ? value.FloatValue
            : defaultValue;
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

    private static bool ReadMergedBool(InputSnapshot snapshot, JsonObject properties)
    {
        var keys = ReadStringList(properties, "key");
        if (keys.Count == 0)
        {
            return false;
        }

        var values = keys.Select(key => snapshot.GetBool(key)).ToArray();
        var mergeMode = ReadString(properties, "mergeMode", "any");

        return mergeMode switch
        {
            "all" => values.All(static value => value),
            _ => values.Any(static value => value)
        };
    }

    private static float ReadMergedFloat(InputSnapshot snapshot, JsonObject properties)
    {
        var keys = ReadStringList(properties, "key");
        if (keys.Count == 0)
        {
            return 0f;
        }

        var values = keys.Select(key => snapshot.GetFloat(key)).ToArray();
        var mergeMode = ReadString(properties, "mergeMode", "max");

        return mergeMode switch
        {
            "min" => values.Min(),
            "average" => values.Average(),
            _ => values.Max()
        };
    }

    private static Color ReadMergedColor(InputSnapshot snapshot, JsonObject properties)
    {
        var keys = ReadStringList(properties, "key");
        if (keys.Count == 0)
        {
            return Color.Black;
        }

        var colors = keys.Select(key => snapshot.GetColor(key, Color.Black)).ToArray();
        var mergeMode = ReadString(properties, "mergeMode", "average");

        if (mergeMode == "additive")
        {
            var totalRed = 0;
            var totalGreen = 0;
            var totalBlue = 0;

            foreach (var color in colors)
            {
                totalRed += color.R;
                totalGreen += color.G;
                totalBlue += color.B;
            }

            return Color.FromRgb(
                (byte)Math.Clamp(totalRed, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(totalGreen, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(totalBlue, byte.MinValue, byte.MaxValue));
        }

        var averageRed = (int)Math.Round(colors.Average(color => color.R));
        var averageGreen = (int)Math.Round(colors.Average(color => color.G));
        var averageBlue = (int)Math.Round(colors.Average(color => color.B));

        return Color.FromRgb(
            (byte)Math.Clamp(averageRed, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(averageGreen, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(averageBlue, byte.MinValue, byte.MaxValue));
    }

    private static Color ReadColor(JsonObject properties)
    {
        return Color.FromRgb(
            ReadByte(properties, "r", 255),
            ReadByte(properties, "g", 255),
            ReadByte(properties, "b", 255));
    }

    private static Color ScaleColor(Color color, float factor)
    {
        var clamped = MathF.Max(factor, 0f);
        return Color.FromRgb(
            (byte)Math.Clamp((int)MathF.Round(color.R * clamped), byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp((int)MathF.Round(color.G * clamped), byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp((int)MathF.Round(color.B * clamped), byte.MinValue, byte.MaxValue));
    }

    private static Color HsvToColor(float hue, float saturation, float value)
    {
        var s = Math.Clamp(saturation, 0f, 1f);
        var v = Math.Clamp(value, 0f, 1f);
        var h = ((hue % 360f) + 360f) % 360f;

        var c = v * s;
        var x = c * (1f - MathF.Abs((h / 60f) % 2f - 1f));
        var m = v - c;

        float r1, g1, b1;
        if (h < 60f) { r1 = c; g1 = x; b1 = 0f; }
        else if (h < 120f) { r1 = x; g1 = c; b1 = 0f; }
        else if (h < 180f) { r1 = 0f; g1 = c; b1 = x; }
        else if (h < 240f) { r1 = 0f; g1 = x; b1 = c; }
        else if (h < 300f) { r1 = x; g1 = 0f; b1 = c; }
        else { r1 = c; g1 = 0f; b1 = x; }

        return Color.FromRgb(
            (byte)Math.Clamp((int)MathF.Round((r1 + m) * 255f), 0, 255),
            (byte)Math.Clamp((int)MathF.Round((g1 + m) * 255f), 0, 255),
            (byte)Math.Clamp((int)MathF.Round((b1 + m) * 255f), 0, 255));
    }

    private static float EvaluateWaveform(string waveform, float phase)
    {
        var t = phase - MathF.Floor(phase);
        return waveform switch
        {
            "square" => t < 0.5f ? 1f : 0f,
            "triangle" => t < 0.5f ? t * 2f : 2f - (t * 2f),
            "sawtooth" => t,
            _ => (MathF.Sin(t * MathF.PI * 2f) + 1f) * 0.5f // sine
        };
    }

    private static void ApplySegmentGradient(Settings settings, NodeInstance node, Color colorA, Color colorB, float offset)
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

                FillSegmentGradient(segment, colorA, colorB, offset);
            }
        }
    }

    private static void FillSegmentGradient(Segment segment, Color colorA, Color colorB, float offset)
    {
        if (segment.Leds.Length == 0)
        {
            return;
        }

        for (var i = 0; i < segment.Leds.Length; i++)
        {
            var t = (float)i / Math.Max(segment.Leds.Length - 1, 1);
            var shifted = ((t + offset) % 1f + 1f) % 1f;
            segment.Leds[i] = MixColors(colorA, colorB, shifted);
        }
    }

    private static Color MixColors(Color a, Color b, float factor)
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        return Color.FromRgb(
            LerpByte(a.R, b.R, clamped),
            LerpByte(a.G, b.G, clamped),
            LerpByte(a.B, b.B, clamped));
    }

    private static byte LerpByte(byte start, byte end, float factor)
    {
        var value = start + ((end - start) * factor);
        return (byte)Math.Clamp((int)MathF.Round(value), byte.MinValue, byte.MaxValue);
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

    private static IReadOnlyList<string> ReadStringList(JsonObject properties, string key)
    {
        return ReadString(properties, key, string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
    private readonly Dictionary<string, PulseState> _pulseStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EnvelopeState> _envelopeStates = new(StringComparer.OrdinalIgnoreCase);

    public bool CanRender => CompiledGraph.Validation.IsValid && CompiledGraph.EvaluationOrder.Count > 0;

    public bool TryGetInputSource(string nodeId, string portId, out RuntimeConnectionSource source)
    {
        source = default;

        return InputConnectionsByNodeId.TryGetValue(nodeId, out var inputs)
            && inputs.TryGetValue(portId, out source);
    }

    public PulseState GetOrCreatePulseState(string nodeId)
    {
        if (!_pulseStates.TryGetValue(nodeId, out var state))
        {
            state = new PulseState();
            _pulseStates[nodeId] = state;
        }

        return state;
    }

    public EnvelopeState GetOrCreateEnvelopeState(string nodeId)
    {
        if (!_envelopeStates.TryGetValue(nodeId, out var state))
        {
            state = new EnvelopeState();
            _envelopeStates[nodeId] = state;
        }

        return state;
    }
}

public sealed class PulseState
{
    private bool _previousTrigger;
    private float _triggerTime = float.MinValue;

    public float CurrentValue { get; private set; }

    public void UpdatePulse(bool trigger, float currentTime, float duration, bool fallingEdge)
    {
        var shouldFire = fallingEdge
            ? !trigger && _previousTrigger
            : trigger && !_previousTrigger;

        if (shouldFire)
        {
            _triggerTime = currentTime;
        }

        _previousTrigger = trigger;

        var elapsed = currentTime - _triggerTime;
        if (elapsed < 0f || duration <= 0f)
        {
            CurrentValue = 0f;
            return;
        }

        CurrentValue = Math.Clamp(1f - (elapsed / duration), 0f, 1f);
    }
}

public sealed class EnvelopeState
{
    private bool _previousTrigger;
    private float _releaseTime = float.MinValue;

    public float CurrentValue { get; private set; }

    public void Update(bool trigger, float currentTime, float releaseDuration)
    {
        if (trigger)
        {
            CurrentValue = 1f;
            _previousTrigger = true;
            return;
        }

        if (_previousTrigger)
        {
            _releaseTime = currentTime;
            _previousTrigger = false;
        }

        var elapsed = currentTime - _releaseTime;
        if (elapsed < 0f || releaseDuration <= 0f)
        {
            CurrentValue = 0f;
            return;
        }

        CurrentValue = Math.Clamp(1f - (elapsed / releaseDuration), 0f, 1f);
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
