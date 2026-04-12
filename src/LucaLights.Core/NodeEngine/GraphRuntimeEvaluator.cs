using System.Text.Json.Nodes;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;

namespace LucaLights.Core.NodeEngine;

public sealed class GraphRuntimeEvaluator
{
    private readonly record struct NormalizedColor(float R, float G, float B);

    private readonly record struct HsvColor(float Hue, float Saturation, float Value);

    private readonly record struct PendingSegmentColorOutput(
        IReadOnlySet<string> SegmentIds,
        Color Color,
        float Priority,
        string BlendMode,
        int EvaluationIndex);

    public readonly record struct PixelContext(int Index, int Length, float Normalized);

    private readonly NodeGraphCompiler _nodeGraphCompiler;
    private readonly INodeTypeCatalog _nodeTypeCatalog;

    private PixelContext? _currentPixel;
    private Segment? _currentSegment;

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

        if (preparedEffect.HasPixelInfoNodes)
        {
            RenderPerPixel(settings, preparedEffect, frameContext);
        }
        else
        {
            var outputs = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
            EvaluateGraph(settings, preparedEffect, frameContext, outputs);
        }
    }

    private void RenderPerPixel(
        Settings settings,
        PreparedGraph preparedEffect,
        LightingFrameContext frameContext)
    {
        var segments = CollectTargetSegments(settings, preparedEffect);
        var outputs = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in segments)
        {
            var length = segment.Leds.Length;
            if (length == 0)
            {
                continue;
            }

            for (var i = 0; i < length; i++)
            {
                var normalized = length > 1 ? (float)i / (length - 1) : 0f;
                _currentPixel = new PixelContext(i, length, normalized);
                _currentSegment = segment;

                outputs.Clear();
                EvaluateGraph(settings, preparedEffect, frameContext, outputs);
            }
        }

        _currentPixel = null;
        _currentSegment = null;
    }

    private static HashSet<Segment> CollectTargetSegments(Settings settings, PreparedGraph preparedEffect)
    {
        var segments = new HashSet<Segment>();

        foreach (var node in preparedEffect.CompiledGraph.EvaluationOrder)
        {
            if (!node.TypeId.StartsWith("output.", StringComparison.Ordinal))
            {
                continue;
            }

            var segmentIds = ParseStringSet(ReadString(node.Properties, "segmentIds", string.Empty));

            foreach (var device in settings.Devices)
            {
                foreach (var segment in device.Segments)
                {
                    if (!IsSegmentTargeted(segment, segmentIds))
                    {
                        continue;
                    }

                    segments.Add(segment);
                }
            }
        }

        return segments;
    }

    private void EvaluateGraph(
        Settings settings,
        PreparedGraph preparedEffect,
        LightingFrameContext frameContext,
        Dictionary<string, RuntimeValue> outputs)
    {
        var totalSeconds = (float)frameContext.TotalElapsed.TotalSeconds;
        var pendingSegmentColorOutputs = new List<PendingSegmentColorOutput>();

        for (var evaluationIndex = 0; evaluationIndex < preparedEffect.CompiledGraph.EvaluationOrder.Count; evaluationIndex++)
        {
            var node = preparedEffect.CompiledGraph.EvaluationOrder[evaluationIndex];

            switch (node.TypeId)
            {
                case "annotation.comment":
                    break;

                case "reroute.bool":
                case "reroute.float":
                case "reroute.color":
                {
                    if (TryGetInputValue(preparedEffect, outputs, node.Id, "value", out var reroutedValue))
                    {
                        outputs[BuildOutputKey(node.Id, "value")] = reroutedValue;
                    }

                    break;
                }

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

                case "pixel.info":
                {
                    var px = _currentPixel ?? new PixelContext(0, 1, 0f);
                    outputs[BuildOutputKey(node.Id, "index")] = RuntimeValue.FromFloat(px.Index);
                    outputs[BuildOutputKey(node.Id, "length")] = RuntimeValue.FromFloat(px.Length);
                    outputs[BuildOutputKey(node.Id, "normalized")] = RuntimeValue.FromFloat(px.Normalized);
                    break;
                }

                case "logic.select-color":
                {
                    var condition = GetInputBool(preparedEffect, outputs, node.Id, "condition", ReadBool(node.Properties, "condition"));
                    var selectedColor = condition
                        ? GetInputColor(preparedEffect, outputs, node.Id, "trueColor", ReadColor(node.Properties, "trueColor", Color.FromRgb(255, 255, 255)))
                        : GetInputColor(preparedEffect, outputs, node.Id, "falseColor", ReadColor(node.Properties, "falseColor", Color.Black));
                    outputs[BuildOutputKey(node.Id, "color")] = RuntimeValue.FromColor(selectedColor);
                    break;
                }

                case "logic.select-float":
                {
                    var condition = GetInputBool(preparedEffect, outputs, node.Id, "condition", ReadBool(node.Properties, "condition"));
                    var value = condition
                        ? GetInputFloat(preparedEffect, outputs, node.Id, "true", ReadFloat(node.Properties, "true", 1f))
                        : GetInputFloat(preparedEffect, outputs, node.Id, "false", ReadFloat(node.Properties, "false", 0f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(value);
                    break;
                }

                case "logic.not":
                {
                    var value = GetInputBool(preparedEffect, outputs, node.Id, "value", ReadBool(node.Properties, "value"));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromBool(!value);
                    break;
                }

                case "logic.and":
                {
                    var a = GetInputBool(preparedEffect, outputs, node.Id, "a", ReadBool(node.Properties, "a"));
                    var b = GetInputBool(preparedEffect, outputs, node.Id, "b", ReadBool(node.Properties, "b"));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromBool(a && b);
                    break;
                }

                case "logic.or":
                {
                    var a = GetInputBool(preparedEffect, outputs, node.Id, "a", ReadBool(node.Properties, "a"));
                    var b = GetInputBool(preparedEffect, outputs, node.Id, "b", ReadBool(node.Properties, "b"));
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
                    var colorA = GetInputColor(preparedEffect, outputs, node.Id, "a", ReadColor(node.Properties, "a", Color.Black));
                    var colorB = GetInputColor(preparedEffect, outputs, node.Id, "b", ReadColor(node.Properties, "b", Color.FromRgb(255, 255, 255)));
                    var mode = ReadString(node.Properties, "mode", "mix");
                    var factor = GetInputFloat(
                        preparedEffect,
                        outputs,
                        node.Id,
                        "factor",
                        ReadFloat(node.Properties, "factor", 0.5f));
                    outputs[BuildOutputKey(node.Id, "color")] = RuntimeValue.FromColor(
                        MixColors(colorA, colorB, factor, mode));
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

                case "math.wrap":
                {
                    var value = GetInputFloat(preparedEffect, outputs, node.Id, "value", ReadFloat(node.Properties, "value", 0f));
                    var min = GetInputFloat(preparedEffect, outputs, node.Id, "min", ReadFloat(node.Properties, "min", 0f));
                    var max = GetInputFloat(preparedEffect, outputs, node.Id, "max", ReadFloat(node.Properties, "max", 1f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(WrapValue(value, min, max));
                    break;
                }

                case "math.ping-pong":
                {
                    var value = GetInputFloat(preparedEffect, outputs, node.Id, "value", ReadFloat(node.Properties, "value", 0f));
                    var scale = GetInputFloat(preparedEffect, outputs, node.Id, "scale", ReadFloat(node.Properties, "scale", 1f));
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(PingPongValue(value, scale));
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
                    var color = GetInputColor(preparedEffect, outputs, node.Id, "color", ReadColor(node.Properties, "color", Color.FromRgb(255, 255, 255)));
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

                case "color.gradient":
                {
                    var factor = GetInputFloat(preparedEffect, outputs, node.Id, "factor", ReadFloat(node.Properties, "factor", 0.5f));
                    var stopsJson = ReadString(node.Properties, "stops", string.Empty);
                    var interpolation = ReadString(node.Properties, "interpolation", "linear");
                    var stops = preparedEffect.GetOrCreateGradientStops(node.Id, stopsJson);
                    outputs[BuildOutputKey(node.Id, "color")] = RuntimeValue.FromColor(
                        SampleGradient(stops, factor, interpolation));
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
                    var trigger = GetInputBool(preparedEffect, outputs, node.Id, "trigger", ReadBool(node.Properties, "trigger"));
                    var duration = GetInputFloat(preparedEffect, outputs, node.Id, "duration", ReadFloat(node.Properties, "duration", 0.5f));
                    var edge = ReadString(node.Properties, "edge", "rising");
                    var pulseState = preparedEffect.GetOrCreatePulseState(node.Id);
                    pulseState.UpdatePulse(trigger, totalSeconds, duration, edge == "falling");
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(pulseState.CurrentValue);
                    break;
                }

                case "time.envelope":
                {
                    var trigger = GetInputBool(preparedEffect, outputs, node.Id, "trigger", ReadBool(node.Properties, "trigger"));
                    var release = GetInputFloat(preparedEffect, outputs, node.Id, "release", ReadFloat(node.Properties, "release", 0.5f));
                    var envelopeState = preparedEffect.GetOrCreateEnvelopeState(node.Id);
                    envelopeState.Update(trigger, totalSeconds, release);
                    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(envelopeState.CurrentValue);
                    break;
                }

                case "output.segment-color":
                {
                    var isActive = GetInputBool(
                        preparedEffect,
                        outputs,
                        node.Id,
                        "active",
                        ReadBool(node.Properties, "active", true));

                    if (!isActive)
                    {
                        break;
                    }

                    pendingSegmentColorOutputs.Add(new PendingSegmentColorOutput(
                        ParseStringSet(ReadString(node.Properties, "segmentIds", string.Empty)),
                        GetInputColor(preparedEffect, outputs, node.Id, "color", ReadColor(node.Properties, "color", Color.FromRgb(255, 255, 255))),
                        ReadFloat(node.Properties, "priority", 0f),
                        ReadString(node.Properties, "blendMode", "override"),
                        evaluationIndex));
                    break;
                }
            }
        }

        ApplySegmentColorOutputs(settings, pendingSegmentColorOutputs);
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

    private void ApplySegmentColorOutputs(
        Settings settings,
        List<PendingSegmentColorOutput> pendingOutputs)
    {
        if (pendingOutputs.Count == 0)
        {
            return;
        }

        pendingOutputs.Sort(static (a, b) =>
        {
            var priorityComparison = a.Priority.CompareTo(b.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : a.EvaluationIndex.CompareTo(b.EvaluationIndex);
        });

        if (_currentPixel is { } px && _currentSegment is not null)
        {
            if (px.Index >= _currentSegment.Leds.Length)
            {
                return;
            }

            var currentColor = _currentSegment.Leds[px.Index];

            foreach (var pendingOutput in pendingOutputs)
            {
                if (!IsSegmentTargeted(_currentSegment, pendingOutput.SegmentIds))
                {
                    continue;
                }

                currentColor = BlendOutputColor(currentColor, pendingOutput.Color, pendingOutput.BlendMode);
            }

            _currentSegment.Leds[px.Index] = currentColor;

            return;
        }

        foreach (var device in settings.Devices)
        {
            foreach (var segment in device.Segments)
            {
                ApplySegmentColor(segment, pendingOutputs);
            }
        }
    }

    private static void ApplySegmentColor(
        Segment segment,
        IReadOnlyList<PendingSegmentColorOutput> pendingOutputs)
    {
        foreach (var pendingOutput in pendingOutputs)
        {
            if (!IsSegmentTargeted(segment, pendingOutput.SegmentIds))
            {
                continue;
            }

            for (var i = 0; i < segment.Leds.Length; i++)
            {
                segment.Leds[i] = BlendOutputColor(segment.Leds[i], pendingOutput.Color, pendingOutput.BlendMode);
            }
        }
    }

    private static Color BlendOutputColor(Color baseColor, Color outputColor, string blendMode)
    {
        var normalizedMode = string.IsNullOrWhiteSpace(blendMode)
            ? "override"
            : blendMode.Trim().ToLowerInvariant();

        if (normalizedMode is "override" or "replace" or "mix")
        {
            return outputColor;
        }

        var graphBlendMode = normalizedMode switch
        {
            "max" => "lighten",
            "min" => "darken",
            _ => normalizedMode
        };

        return DenormalizeColor(BlendColors(
            NormalizeColor(baseColor),
            NormalizeColor(outputColor),
            graphBlendMode));
    }

    private static bool IsSegmentTargeted(Segment segment, IReadOnlySet<string> segmentIds)
    {
        return segmentIds.Count == 0 || segmentIds.Contains(segment.Id);
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

    private static Color ReadColor(JsonObject properties, string key, Color defaultValue)
    {
        var node = properties[key];
        if (node is JsonObject obj)
        {
            return Color.FromRgb(
                ReadByte(obj, "r", defaultValue.R),
                ReadByte(obj, "g", defaultValue.G),
                ReadByte(obj, "b", defaultValue.B));
        }

        if (node is JsonValue stringNode
            && stringNode.TryGetValue<string>(out var hex)
            && TryParseHexColor(hex, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static bool TryParseHexColor(string value, out Color color)
    {
        color = Color.Black;
        var trimmed = value.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 3)
        {
            trimmed = string.Concat(trimmed.Select(ch => $"{ch}{ch}"));
        }

        if (trimmed.Length != 6
            || !byte.TryParse(trimmed[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !byte.TryParse(trimmed.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !byte.TryParse(trimmed.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return false;
        }

        color = Color.FromRgb(r, g, b);
        return true;
    }

    private static Color ScaleColor(Color color, float factor)
    {
        var clamped = MathF.Max(factor, 0f);
        return Color.FromRgb(
            (byte)Math.Clamp((int)MathF.Round(color.R * clamped), byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp((int)MathF.Round(color.G * clamped), byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp((int)MathF.Round(color.B * clamped), byte.MinValue, byte.MaxValue));
    }

    private static float WrapValue(float value, float min, float max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }

        var span = max - min;
        if (span <= 0f)
        {
            return min;
        }

        var wrapped = (value - min) % span;
        if (wrapped < 0f)
        {
            wrapped += span;
        }

        return min + wrapped;
    }

    private static float PingPongValue(float value, float scale)
    {
        var amplitude = MathF.Abs(scale);
        if (amplitude <= 0f)
        {
            return 0f;
        }

        var cycle = amplitude * 2f;
        var wrapped = value % cycle;
        if (wrapped < 0f)
        {
            wrapped += cycle;
        }

        return wrapped <= amplitude ? wrapped : cycle - wrapped;
    }

    private static Color SampleGradient(GradientStop[] stops, float factor, string interpolation)
    {
        if (stops.Length == 0)
        {
            return Color.Black;
        }

        if (stops.Length == 1)
        {
            return Color.FromRgb(stops[0].R, stops[0].G, stops[0].B);
        }

        var start = stops[0].Position;
        var end = stops[^1].Position;
        var position = Math.Clamp(factor, start, end);

        if (position <= start)
        {
            return Color.FromRgb(stops[0].R, stops[0].G, stops[0].B);
        }

        if (position >= end)
        {
            return Color.FromRgb(stops[^1].R, stops[^1].G, stops[^1].B);
        }

        for (var i = 0; i < stops.Length - 1; i++)
        {
            var a = stops[i];
            var b = stops[i + 1];

            if (position < a.Position || position > b.Position)
            {
                continue;
            }

            if (interpolation == "constant")
            {
                return Color.FromRgb(a.R, a.G, a.B);
            }

            var range = b.Position - a.Position;
            var t = range == 0f ? 0f : (position - a.Position) / range;
            return Color.FromRgb(
                ToByte(a.R + ((b.R - a.R) * t)),
                ToByte(a.G + ((b.G - a.G) * t)),
                ToByte(a.B + ((b.B - a.B) * t)));
        }

        return Color.FromRgb(stops[^1].R, stops[^1].G, stops[^1].B);
    }

    private static Color HsvToColor(float hue, float saturation, float value)
    {
        return DenormalizeColor(HsvToNormalizedColor(hue, saturation, value));
    }

    private static NormalizedColor HsvToNormalizedColor(float hue, float saturation, float value)
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

        return new NormalizedColor(r1 + m, g1 + m, b1 + m);
    }

    private static HsvColor ColorToHsv(NormalizedColor color)
    {
        var max = MathF.Max(color.R, MathF.Max(color.G, color.B));
        var min = MathF.Min(color.R, MathF.Min(color.G, color.B));
        var delta = max - min;

        var hue = 0f;
        if (delta > 0f)
        {
            if (max == color.R)
            {
                hue = 60f * (((color.G - color.B) / delta) % 6f);
            }
            else if (max == color.G)
            {
                hue = 60f * (((color.B - color.R) / delta) + 2f);
            }
            else
            {
                hue = 60f * (((color.R - color.G) / delta) + 4f);
            }
        }

        if (hue < 0f)
        {
            hue += 360f;
        }

        var saturation = max <= 0f ? 0f : delta / max;
        return new HsvColor(hue, saturation, max);
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

    private static Color MixColors(Color a, Color b, float factor, string mode = "mix")
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        var baseColor = NormalizeColor(a);
        var blendColor = NormalizeColor(b);
        var blended = BlendColors(baseColor, blendColor, mode);
        return DenormalizeColor(LerpColor(baseColor, blended, clamped));
    }

    private static NormalizedColor BlendColors(
        NormalizedColor baseColor,
        NormalizedColor blendColor,
        string mode)
    {
        var normalizedMode = string.IsNullOrWhiteSpace(mode)
            ? "mix"
            : mode.Trim().ToLowerInvariant();

        return normalizedMode switch
        {
            "darken" => new(
                MathF.Min(baseColor.R, blendColor.R),
                MathF.Min(baseColor.G, blendColor.G),
                MathF.Min(baseColor.B, blendColor.B)),
            "multiply" => BlendPerChannel(baseColor, blendColor, static (a, b) => a * b),
            "color-burn" => BlendPerChannel(baseColor, blendColor, ColorBurnChannel),
            "lighten" => new(
                MathF.Max(baseColor.R, blendColor.R),
                MathF.Max(baseColor.G, blendColor.G),
                MathF.Max(baseColor.B, blendColor.B)),
            "screen" => BlendPerChannel(baseColor, blendColor, static (a, b) => 1f - ((1f - a) * (1f - b))),
            "color-dodge" => BlendPerChannel(baseColor, blendColor, ColorDodgeChannel),
            "add" => BlendPerChannel(baseColor, blendColor, static (a, b) => MathF.Min(1f, a + b)),
            "overlay" => BlendPerChannel(baseColor, blendColor, OverlayChannel),
            "soft-light" => BlendPerChannel(baseColor, blendColor, SoftLightChannel),
            "linear-light" => BlendPerChannel(baseColor, blendColor, static (a, b) => Math.Clamp(a + (2f * b) - 1f, 0f, 1f)),
            "difference" => BlendPerChannel(baseColor, blendColor, static (a, b) => MathF.Abs(a - b)),
            "exclusion" => BlendPerChannel(baseColor, blendColor, static (a, b) => a + b - (2f * a * b)),
            "subtract" => BlendPerChannel(baseColor, blendColor, static (a, b) => MathF.Max(0f, a - b)),
            "divide" => BlendPerChannel(baseColor, blendColor, DivideChannel),
            "hue" or "saturation" or "color" or "value" => BlendHsv(baseColor, blendColor, normalizedMode),
            _ => blendColor
        };
    }

    private static NormalizedColor BlendPerChannel(
        NormalizedColor baseColor,
        NormalizedColor blendColor,
        Func<float, float, float> blendChannel)
    {
        return new NormalizedColor(
            blendChannel(baseColor.R, blendColor.R),
            blendChannel(baseColor.G, blendColor.G),
            blendChannel(baseColor.B, blendColor.B));
    }

    private static NormalizedColor BlendHsv(
        NormalizedColor baseColor,
        NormalizedColor blendColor,
        string mode)
    {
        var baseHsv = ColorToHsv(baseColor);
        var blendHsv = ColorToHsv(blendColor);

        return mode switch
        {
            "hue" => HsvToNormalizedColor(blendHsv.Hue, baseHsv.Saturation, baseHsv.Value),
            "saturation" => HsvToNormalizedColor(baseHsv.Hue, blendHsv.Saturation, baseHsv.Value),
            "color" => HsvToNormalizedColor(blendHsv.Hue, blendHsv.Saturation, baseHsv.Value),
            "value" => HsvToNormalizedColor(baseHsv.Hue, baseHsv.Saturation, blendHsv.Value),
            _ => blendColor
        };
    }

    private static float OverlayChannel(float baseChannel, float blendChannel)
    {
        return baseChannel < 0.5f
            ? 2f * baseChannel * blendChannel
            : 1f - (2f * (1f - baseChannel) * (1f - blendChannel));
    }

    private static float SoftLightChannel(float baseChannel, float blendChannel)
    {
        if (blendChannel <= 0.5f)
        {
            return baseChannel - ((1f - (2f * blendChannel)) * baseChannel * (1f - baseChannel));
        }

        var d = baseChannel <= 0.25f
            ? ((16f * baseChannel - 12f) * baseChannel + 4f) * baseChannel
            : MathF.Sqrt(baseChannel);

        return baseChannel + (((2f * blendChannel) - 1f) * (d - baseChannel));
    }

    private static float ColorDodgeChannel(float baseChannel, float blendChannel)
    {
        if (blendChannel >= 1f)
        {
            return 1f;
        }

        return MathF.Min(1f, baseChannel / (1f - blendChannel));
    }

    private static float ColorBurnChannel(float baseChannel, float blendChannel)
    {
        if (blendChannel <= 0f)
        {
            return 0f;
        }

        return 1f - MathF.Min(1f, (1f - baseChannel) / blendChannel);
    }

    private static float DivideChannel(float baseChannel, float blendChannel)
    {
        if (blendChannel <= 0f)
        {
            return 1f;
        }

        return MathF.Min(1f, baseChannel / blendChannel);
    }

    private static NormalizedColor NormalizeColor(Color color)
    {
        return new NormalizedColor(
            color.R / 255f,
            color.G / 255f,
            color.B / 255f);
    }

    private static Color DenormalizeColor(NormalizedColor color)
    {
        return Color.FromRgb(
            ToByte(color.R * 255f),
            ToByte(color.G * 255f),
            ToByte(color.B * 255f));
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), byte.MinValue, byte.MaxValue);
    }

    private static NormalizedColor LerpColor(NormalizedColor from, NormalizedColor to, float factor)
    {
        return new NormalizedColor(
            from.R + ((to.R - from.R) * factor),
            from.G + ((to.G - from.G) * factor),
            from.B + ((to.B - from.B) * factor));
    }

    private static List<string> ReadStringList(JsonObject properties, string key)
    {
        return ReadString(properties, key, string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static byte ReadByte(JsonObject properties, string key, byte defaultValue)
    {
        var raw = ReadFloat(properties, key, defaultValue);
        return (byte)Math.Clamp((int)MathF.Round(raw), byte.MinValue, byte.MaxValue);
    }

    private static float ReadFloat(JsonObject properties, string key, float defaultValue = 0f)
    {
        if (!properties.TryGetPropertyValue(key, out var node) || node is null)
        {
            return defaultValue;
        }

        if (node is JsonValue valueNode)
        {
            if (valueNode.TryGetValue<float>(out var floatValue))
            {
                return floatValue;
            }

            if (valueNode.TryGetValue<double>(out var doubleValue))
            {
                return (float)doubleValue;
            }

            if (valueNode.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (valueNode.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (valueNode.TryGetValue<string>(out var stringValue)
                && float.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static bool ReadBool(JsonObject properties, string key, bool defaultValue = false)
    {
        if (!properties.TryGetPropertyValue(key, out var node) || node is null)
        {
            return defaultValue;
        }

        if (node is JsonValue valueNode)
        {
            if (valueNode.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (valueNode.TryGetValue<string>(out var stringValue)
                && bool.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static string ReadString(JsonObject properties, string key, string defaultValue = "")
    {
        if (!properties.TryGetPropertyValue(key, out var node) || node is null)
        {
            return defaultValue;
        }

        if (node is JsonValue valueNode && valueNode.TryGetValue<string>(out var value))
        {
            return value;
        }

        return defaultValue;
    }

    private static HashSet<string> ParseStringSet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
public sealed record PreparedGraph(
    CompiledNodeGraph CompiledGraph,
    IReadOnlyDictionary<string, NodeTypeDefinition> NodeTypesByNodeId,
    IReadOnlyDictionary<string, Dictionary<string, RuntimeConnectionSource>> InputConnectionsByNodeId)
{
    private readonly Dictionary<string, PulseState> _pulseStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EnvelopeState> _envelopeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GradientStop[]> _gradientStopsCache = new(StringComparer.OrdinalIgnoreCase);

    public bool CanRender => CompiledGraph.Validation.IsValid && CompiledGraph.EvaluationOrder.Count > 0;

    public bool HasPixelInfoNodes { get; } = CompiledGraph.EvaluationOrder
        .Any(node => node.TypeId == "pixel.info");

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

    public GradientStop[] GetOrCreateGradientStops(string nodeId, string stopsJson)
    {
        if (_gradientStopsCache.TryGetValue(nodeId, out var cached))
        {
            return cached;
        }

        var stops = GradientStop.Parse(stopsJson);
        _gradientStopsCache[nodeId] = stops;
        return stops;
    }
}

public readonly record struct GradientStop(float Position, byte R, byte G, byte B)
{
    private static readonly GradientStop[] DefaultStops =
    [
        new(0f, 0, 0, 0),
        new(1f, 255, 255, 255)
    ];

    public static GradientStop[] Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DefaultStops;
        }

        try
        {
            var array = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsArray();
            if (array is null || array.Count == 0)
            {
                return DefaultStops;
            }

            var stops = new List<GradientStop>(array.Count);
            foreach (var item in array)
            {
                if (item is not System.Text.Json.Nodes.JsonObject obj)
                {
                    continue;
                }

                var p = GetFloat(obj, "p", 0f);
                var r = (byte)Math.Clamp(GetFloat(obj, "r", 0f), 0f, 255f);
                var g = (byte)Math.Clamp(GetFloat(obj, "g", 0f), 0f, 255f);
                var b = (byte)Math.Clamp(GetFloat(obj, "b", 0f), 0f, 255f);
                stops.Add(new GradientStop(p, r, g, b));
            }

            if (stops.Count == 0)
            {
                return DefaultStops;
            }

            stops.Sort((a, b) => a.Position.CompareTo(b.Position));
            return stops.ToArray();
        }
        catch
        {
            return DefaultStops;
        }
    }

    private static float GetFloat(System.Text.Json.Nodes.JsonObject obj, string key, float fallback)
    {
        var node = obj[key];
        if (node is System.Text.Json.Nodes.JsonValue val)
        {
            if (val.TryGetValue<float>(out var f)) return f;
            if (val.TryGetValue<int>(out var i)) return i;
            if (val.TryGetValue<double>(out var d)) return (float)d;
        }

        return fallback;
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
