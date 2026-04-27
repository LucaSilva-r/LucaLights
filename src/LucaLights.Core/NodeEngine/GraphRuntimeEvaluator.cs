using System.Diagnostics;
using System.Text.Json.Nodes;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;

namespace LucaLights.Core.NodeEngine;

public sealed class GraphRuntimeEvaluator
{
    private readonly record struct NormalizedColor(float R, float G, float B);

    private readonly record struct HsvColor(float Hue, float Saturation, float Value);

    public readonly record struct PixelContext(
        int Index,
        int Length,
        float Normalized,
        int DeviceIndex,
        int SegmentIndex,
        int DevicePixelIndex,
        int DeviceLength,
        float DeviceNormalized,
        int GlobalIndex,
        int GlobalLength,
        float GlobalNormalized);

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

    public PreparedGraph? Prepare(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return PrepareGraph(settings.Graph, settings);
    }

    public PreparedGraph? Prepare(NodeGraph? graph)
    {
        return PrepareGraph(graph, null);
    }

    private PreparedGraph? PrepareGraph(NodeGraph? graph, Settings? settings)
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

        var compiledNodes = BuildCompiledNodes(
            settings,
            compiled,
            nodeTypesByNodeId,
            inputConnectionsByNodeId,
            out var outputBuffer,
            out var outputNodes,
            out var targetSegments,
            out var pixelTargets);

        return new PreparedGraph(
            compiled,
            nodeTypesByNodeId,
            inputConnectionsByNodeId,
            compiledNodes,
            outputBuffer,
            outputNodes,
            targetSegments,
            pixelTargets,
            settings is not null);
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

        if (!preparedEffect.TargetsResolved)
        {
            preparedEffect.ResolveTargets(settings);
        }

        if (preparedEffect.HasPixelInfoNodes)
        {
            RenderPerPixel(preparedEffect, frameContext);
        }
        else
        {
            EvaluateGraph(preparedEffect, frameContext);
        }
    }

    private void RenderPerPixel(
        PreparedGraph preparedEffect,
        LightingFrameContext frameContext)
    {
        try
        {
            foreach (var target in preparedEffect.PixelTargets)
            {
                var segment = target.Segment;
                var length = segment.Leds.Length;
                if (length == 0)
                {
                    continue;
                }

                for (var i = 0; i < length; i++)
                {
                    var normalized = length > 1 ? (float)i / (length - 1) : 0f;
                    var devicePixelIndex = target.DeviceStartIndex + i;
                    var deviceNormalized = target.DeviceLength > 1
                        ? (float)devicePixelIndex / (target.DeviceLength - 1)
                        : 0f;
                    var globalIndex = target.GlobalStartIndex + i;
                    var globalNormalized = target.GlobalLength > 1
                        ? (float)globalIndex / (target.GlobalLength - 1)
                        : 0f;

                    _currentPixel = new PixelContext(
                        i,
                        length,
                        normalized,
                        target.DeviceIndex,
                        target.SegmentIndex,
                        devicePixelIndex,
                        target.DeviceLength,
                        deviceNormalized,
                        globalIndex,
                        target.GlobalLength,
                        globalNormalized);
                    _currentSegment = segment;

                    EvaluateGraph(preparedEffect, frameContext);
                }
            }
        }
        finally
        {
            _currentPixel = null;
            _currentSegment = null;
        }
    }

    private void EvaluateGraph(
        PreparedGraph preparedEffect,
        LightingFrameContext frameContext)
    {
        var totalSeconds = (float)frameContext.TotalElapsed.TotalSeconds;
        var nodes = preparedEffect.CompiledNodes;

        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];

            switch (node.Op)
            {
                case NodeOp.AnnotationComment:
                case NodeOp.OutputSegmentColor:
                    break;

                case NodeOp.RerouteBool:
                case NodeOp.RerouteFloat:
                case NodeOp.RerouteColor:
                    WriteOutput(preparedEffect, node, 0, ReadInputValue(preparedEffect, node, 0));
                    break;

                case NodeOp.ConstantColor:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromColor(node.PropColorA));
                    break;

                case NodeOp.ConstantFloat:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(node.PropFloatA));
                    break;

                case NodeOp.ConstantBool:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromBool(node.PropBoolA));
                    break;

                case NodeOp.InputBool:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromBool(
                        ReadMergedBool(frameContext.InputSnapshot, node.InputKeys, node.MergeOp)));
                    break;

                case NodeOp.InputFloat:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        ReadMergedFloat(frameContext.InputSnapshot, node.InputKeys, node.MergeOp)));
                    break;

                case NodeOp.InputColor:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromColor(
                        ReadMergedColor(frameContext.InputSnapshot, node.InputKeys, node.MergeOp)));
                    break;

                case NodeOp.PixelInfo:
                {
                    var px = _currentPixel ?? new PixelContext(0, 1, 0f, 0, 0, 0, 1, 0f, 0, 1, 0f);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(px.Index));
                    WriteOutput(preparedEffect, node, 1, RuntimeValue.FromFloat(px.Length));
                    WriteOutput(preparedEffect, node, 2, RuntimeValue.FromFloat(px.Normalized));
                    WriteOutput(preparedEffect, node, 3, RuntimeValue.FromFloat(px.DeviceIndex));
                    WriteOutput(preparedEffect, node, 4, RuntimeValue.FromFloat(px.SegmentIndex));
                    WriteOutput(preparedEffect, node, 5, RuntimeValue.FromFloat(px.DevicePixelIndex));
                    WriteOutput(preparedEffect, node, 6, RuntimeValue.FromFloat(px.DeviceLength));
                    WriteOutput(preparedEffect, node, 7, RuntimeValue.FromFloat(px.DeviceNormalized));
                    WriteOutput(preparedEffect, node, 8, RuntimeValue.FromFloat(px.GlobalIndex));
                    WriteOutput(preparedEffect, node, 9, RuntimeValue.FromFloat(px.GlobalLength));
                    WriteOutput(preparedEffect, node, 10, RuntimeValue.FromFloat(px.GlobalNormalized));
                    break;
                }

                case NodeOp.LogicSelectColor:
                {
                    var condition = GetInputBool(preparedEffect, node, 0);
                    var selectedColor = condition
                        ? GetInputColor(preparedEffect, node, 1)
                        : GetInputColor(preparedEffect, node, 2);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromColor(selectedColor));
                    break;
                }

                case NodeOp.LogicSelectFloat:
                {
                    var condition = GetInputBool(preparedEffect, node, 0);
                    var value = condition
                        ? GetInputFloat(preparedEffect, node, 1)
                        : GetInputFloat(preparedEffect, node, 2);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(value));
                    break;
                }

                case NodeOp.LogicNot:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromBool(!GetInputBool(preparedEffect, node, 0)));
                    break;

                case NodeOp.LogicAnd:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromBool(
                        GetInputBool(preparedEffect, node, 0) && GetInputBool(preparedEffect, node, 1)));
                    break;

                case NodeOp.LogicOr:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromBool(
                        GetInputBool(preparedEffect, node, 0) || GetInputBool(preparedEffect, node, 1)));
                    break;

                case NodeOp.LogicCompare:
                {
                    var a = GetInputFloat(preparedEffect, node, 0);
                    var b = GetInputFloat(preparedEffect, node, 1);
                    var result = node.CompareOp switch
                    {
                        CompareOp.Less => a < b,
                        CompareOp.Equal => MathF.Abs(a - b) < 0.0001f,
                        _ => a > b
                    };
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromBool(result));
                    break;
                }

                case NodeOp.LogicMixColor:
                {
                    var colorA = GetInputColor(preparedEffect, node, 0);
                    var colorB = GetInputColor(preparedEffect, node, 1);
                    var factor = GetInputFloat(preparedEffect, node, 2);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromColor(
                        MixColors(colorA, colorB, factor, node.BlendOp)));
                    break;
                }

                case NodeOp.MathAdd:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        GetInputFloat(preparedEffect, node, 0) + GetInputFloat(preparedEffect, node, 1)));
                    break;

                case NodeOp.MathMultiply:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        GetInputFloat(preparedEffect, node, 0) * GetInputFloat(preparedEffect, node, 1)));
                    break;

                case NodeOp.MathClamp:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(Math.Clamp(
                        GetInputFloat(preparedEffect, node, 0),
                        GetInputFloat(preparedEffect, node, 1),
                        GetInputFloat(preparedEffect, node, 2))));
                    break;

                case NodeOp.MathRemap:
                {
                    var value = GetInputFloat(preparedEffect, node, 0);
                    var inRange = node.PropFloatB - node.PropFloatA;
                    var t = inRange == 0f ? 0f : (value - node.PropFloatA) / inRange;
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        node.PropFloatC + (t * (node.PropFloatD - node.PropFloatC))));
                    break;
                }

                case NodeOp.MathWrap:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(WrapValue(
                        GetInputFloat(preparedEffect, node, 0),
                        GetInputFloat(preparedEffect, node, 1),
                        GetInputFloat(preparedEffect, node, 2))));
                    break;

                case NodeOp.MathPingPong:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(PingPongValue(
                        GetInputFloat(preparedEffect, node, 0),
                        GetInputFloat(preparedEffect, node, 1))));
                    break;

                case NodeOp.MathModulo:
                {
                    var divisor = GetInputFloat(preparedEffect, node, 1);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        divisor == 0f ? 0f : GetInputFloat(preparedEffect, node, 0) % divisor));
                    break;
                }

                case NodeOp.MathAbs:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        MathF.Abs(GetInputFloat(preparedEffect, node, 0))));
                    break;

                case NodeOp.MathStep:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        GetInputFloat(preparedEffect, node, 0) >= GetInputFloat(preparedEffect, node, 1) ? 1f : 0f));
                    break;

                case NodeOp.MathSmoothStep:
                {
                    var value = GetInputFloat(preparedEffect, node, 0);
                    var range = node.PropFloatB - node.PropFloatA;
                    var t = Math.Clamp((value - node.PropFloatA) / (range == 0f ? 1f : range), 0f, 1f);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(t * t * (3f - 2f * t)));
                    break;
                }

                case NodeOp.MathSin:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        MathF.Sin(GetInputFloat(preparedEffect, node, 0))));
                    break;

                case NodeOp.MathCos:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        MathF.Cos(GetInputFloat(preparedEffect, node, 0))));
                    break;

                case NodeOp.MathTan:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        MathF.Tan(GetInputFloat(preparedEffect, node, 0))));
                    break;

                case NodeOp.MathRandom:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        NextRandomFloat(
                            node,
                            GetInputFloat(preparedEffect, node, 0),
                            GetInputFloat(preparedEffect, node, 1),
                            GetInputFloat(preparedEffect, node, 2))));
                    break;

                case NodeOp.MathPerlinNoise2D:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        PerlinNoise2D(
                            GetInputFloat(preparedEffect, node, 0),
                            GetInputFloat(preparedEffect, node, 1))));
                    break;

                case NodeOp.MathValueNoise2D:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        ValueNoise2D(
                            GetInputFloat(preparedEffect, node, 0),
                            GetInputFloat(preparedEffect, node, 1))));
                    break;

                case NodeOp.ColorBrightness:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromColor(ScaleColor(
                        GetInputColor(preparedEffect, node, 0),
                        GetInputFloat(preparedEffect, node, 1))));
                    break;

                case NodeOp.ColorHsv:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromColor(HsvToColor(
                        GetInputFloat(preparedEffect, node, 0),
                        GetInputFloat(preparedEffect, node, 1),
                        GetInputFloat(preparedEffect, node, 2))));
                    break;

                case NodeOp.ColorToHsv:
                {
                    var hsv = ColorToHsv(NormalizeColor(GetInputColor(preparedEffect, node, 0)));
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(hsv.Hue));
                    WriteOutput(preparedEffect, node, 1, RuntimeValue.FromFloat(hsv.Saturation));
                    WriteOutput(preparedEffect, node, 2, RuntimeValue.FromFloat(hsv.Value));
                    break;
                }

                case NodeOp.ColorGradient:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromColor(SampleGradient(
                        node.GradientStops,
                        GetInputFloat(preparedEffect, node, 0),
                        node.InterpolationOp)));
                    break;

                case NodeOp.TimeElapsed:
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(totalSeconds));
                    break;

                case NodeOp.TimeOscillator:
                {
                    var speed = GetInputFloat(preparedEffect, node, 0);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(
                        EvaluateWaveform(node.WaveformOp, totalSeconds * speed)));
                    break;
                }

                case NodeOp.TimePulse:
                {
                    var trigger = GetInputBool(preparedEffect, node, 0);
                    var duration = GetInputFloat(preparedEffect, node, 1);
                    node.PulseState!.UpdatePulse(trigger, totalSeconds, duration, node.EdgeOp == EdgeOp.Falling);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(node.PulseState.CurrentValue));
                    break;
                }

                case NodeOp.TimeEnvelope:
                {
                    var trigger = GetInputBool(preparedEffect, node, 0);
                    var release = GetInputFloat(preparedEffect, node, 1);
                    node.EnvelopeState!.Update(trigger, totalSeconds, release);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromFloat(node.EnvelopeState.CurrentValue));
                    break;
                }

                case NodeOp.TimeBlink:
                {
                    var onTime = GetInputFloat(preparedEffect, node, 0);
                    var offTime = GetInputFloat(preparedEffect, node, 1);
                    node.BlinkState!.Update(totalSeconds, onTime, offTime);
                    WriteOutput(preparedEffect, node, 0, RuntimeValue.FromBool(node.BlinkState.CurrentValue));
                    break;
                }
            }
        }

        ApplySegmentColorOutputs(preparedEffect);
    }

    private static CompiledNode[] BuildCompiledNodes(
        Settings? settings,
        CompiledNodeGraph compiled,
        IReadOnlyDictionary<string, NodeTypeDefinition> nodeTypesByNodeId,
        IReadOnlyDictionary<string, Dictionary<string, RuntimeConnectionSource>> inputConnectionsByNodeId,
        out RuntimeValue[] outputBuffer,
        out CompiledOutputNode[] outputNodes,
        out Segment[] targetSegments,
        out PixelTarget[] pixelTargets)
    {
        var evaluationOrder = compiled.EvaluationOrder;
        var compiledNodes = new CompiledNode[evaluationOrder.Count];
        var outputSlotsByNodeId = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var slotCount = 0;

        for (var evaluationIndex = 0; evaluationIndex < evaluationOrder.Count; evaluationIndex++)
        {
            var node = evaluationOrder[evaluationIndex];
            if (!nodeTypesByNodeId.TryGetValue(node.Id, out var nodeType))
            {
                continue;
            }

            var outputSlots = new int[nodeType.Outputs.Count];
            if (nodeType.Outputs.Count > 0)
            {
                var outputSlotsByPortId = new Dictionary<string, int>(nodeType.Outputs.Count, StringComparer.OrdinalIgnoreCase);
                outputSlotsByNodeId[node.Id] = outputSlotsByPortId;

                for (var outputIndex = 0; outputIndex < nodeType.Outputs.Count; outputIndex++)
                {
                    var slot = slotCount++;
                    outputSlots[outputIndex] = slot;
                    outputSlotsByPortId[nodeType.Outputs[outputIndex].Id] = slot;
                }
            }

            compiledNodes[evaluationIndex] = CreateCompiledNode(settings, node, nodeType, outputSlots, evaluationIndex);
        }

        outputBuffer = new RuntimeValue[slotCount];
        var outputNodeList = new List<CompiledOutputNode>();
        var targetSegmentSet = new HashSet<Segment>();

        for (var evaluationIndex = 0; evaluationIndex < evaluationOrder.Count; evaluationIndex++)
        {
            var sourceNode = evaluationOrder[evaluationIndex];
            var compiledNode = compiledNodes[evaluationIndex];

            if (!nodeTypesByNodeId.TryGetValue(sourceNode.Id, out var nodeType))
            {
                continue;
            }

            compiledNode.InputSlots = new int[nodeType.Inputs.Count];
            compiledNode.InputDefaults = new RuntimeValue[nodeType.Inputs.Count];
            Array.Fill(compiledNode.InputSlots, -1);

            for (var inputIndex = 0; inputIndex < nodeType.Inputs.Count; inputIndex++)
            {
                var inputPort = nodeType.Inputs[inputIndex];
                compiledNode.InputDefaults[inputIndex] = ReadInputDefault(sourceNode.Properties, nodeType, inputPort);

                if (!inputConnectionsByNodeId.TryGetValue(sourceNode.Id, out var inputSources)
                    || !inputSources.TryGetValue(inputPort.Id, out var source)
                    || !outputSlotsByNodeId.TryGetValue(source.SourceNodeId, out var sourceOutputSlots)
                    || !sourceOutputSlots.TryGetValue(source.SourcePortId, out var sourceSlot))
                {
                    continue;
                }

                compiledNode.InputSlots[inputIndex] = sourceSlot;
            }

            if (compiledNode.Op == NodeOp.OutputSegmentColor)
            {
                outputNodeList.Add(new CompiledOutputNode(compiledNode));

                foreach (var segment in compiledNode.TargetSegments)
                {
                    targetSegmentSet.Add(segment);
                }
            }
        }

        outputNodeList.Sort(static (a, b) =>
        {
            var priorityComparison = a.Node.Priority.CompareTo(b.Node.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : a.Node.EvaluationIndex.CompareTo(b.Node.EvaluationIndex);
        });

        outputNodes = outputNodeList.ToArray();
        if (settings is null)
        {
            targetSegments = new Segment[targetSegmentSet.Count];
            targetSegmentSet.CopyTo(targetSegments);
            pixelTargets = [];
        }
        else
        {
            pixelTargets = PixelTargetLayout.Build(settings, targetSegmentSet);
            targetSegments = new Segment[pixelTargets.Length];
            for (var i = 0; i < pixelTargets.Length; i++)
            {
                targetSegments[i] = pixelTargets[i].Segment;
            }
        }

        var expectedSlotCount = 0;
        for (var i = 0; i < compiledNodes.Length; i++)
        {
            expectedSlotCount += compiledNodes[i].OutputSlots.Length;
        }

        Debug.Assert(slotCount == expectedSlotCount);
        return compiledNodes;
    }

    private static CompiledNode CreateCompiledNode(
        Settings? settings,
        NodeInstance node,
        NodeTypeDefinition nodeType,
        int[] outputSlots,
        int evaluationIndex)
    {
        var compiledNode = new CompiledNode
        {
            Op = ParseNodeOp(node.TypeId),
            OutputSlots = outputSlots,
            EvaluationIndex = evaluationIndex
        };

        var properties = node.Properties;
        switch (compiledNode.Op)
        {
            case NodeOp.ConstantColor:
                compiledNode.PropColorA = ReadColor(properties);
                break;

            case NodeOp.ConstantFloat:
                compiledNode.PropFloatA = ReadFloat(properties, "value");
                break;

            case NodeOp.ConstantBool:
                compiledNode.PropBoolA = ReadBool(properties, "value");
                break;

            case NodeOp.InputBool:
                compiledNode.InputKeys = ReadStringArray(properties, "key");
                compiledNode.MergeOp = ParseBoolMergeOp(ReadString(properties, "mergeMode", "any"));
                break;

            case NodeOp.InputFloat:
                compiledNode.InputKeys = ReadStringArray(properties, "key");
                compiledNode.MergeOp = ParseFloatMergeOp(ReadString(properties, "mergeMode", "max"));
                break;

            case NodeOp.InputColor:
                compiledNode.InputKeys = ReadStringArray(properties, "key");
                compiledNode.MergeOp = ParseColorMergeOp(ReadString(properties, "mergeMode", "average"));
                break;

            case NodeOp.LogicCompare:
                compiledNode.CompareOp = ParseCompareOp(ReadString(properties, "mode", "greater"));
                break;

            case NodeOp.LogicMixColor:
                compiledNode.BlendOp = ParseBlendOp(ReadString(properties, "mode", "mix"));
                break;

            case NodeOp.MathRemap:
                compiledNode.PropFloatA = ReadFloat(properties, "inMin", 0f);
                compiledNode.PropFloatB = ReadFloat(properties, "inMax", 1f);
                compiledNode.PropFloatC = ReadFloat(properties, "outMin", 0f);
                compiledNode.PropFloatD = ReadFloat(properties, "outMax", 1f);
                break;

            case NodeOp.MathSmoothStep:
                compiledNode.PropFloatA = ReadFloat(properties, "edge0", 0f);
                compiledNode.PropFloatB = ReadFloat(properties, "edge1", 1f);
                break;

            case NodeOp.MathRandom:
                compiledNode.RandomNodeSeed = HashNodeId(node.Id);
                compiledNode.RandomSeed = SeedFromFloat(ReadFloat(properties, "seed", 0f));
                compiledNode.RandomState = BuildRandomState(compiledNode.RandomNodeSeed, compiledNode.RandomSeed);
                if (compiledNode.RandomState == 0)
                {
                    compiledNode.RandomState = 0x9E3779B9u;
                }

                break;

            case NodeOp.ColorGradient:
                compiledNode.GradientStops = GradientStop.Parse(ReadString(properties, "stops", string.Empty));
                compiledNode.InterpolationOp = ParseInterpolationOp(ReadString(properties, "interpolation", "linear"));
                break;

            case NodeOp.TimeOscillator:
                compiledNode.WaveformOp = ParseWaveformOp(ReadString(properties, "waveform", "sine"));
                break;

            case NodeOp.TimePulse:
                compiledNode.EdgeOp = ParseEdgeOp(ReadString(properties, "edge", "rising"));
                compiledNode.PulseState = new PulseState();
                break;

           case NodeOp.TimeEnvelope:
                compiledNode.EnvelopeState = new EnvelopeState();
                break;

            case NodeOp.TimeBlink:
                compiledNode.PropFloatA = ReadFloat(properties, "onTime", 0.5f);
                compiledNode.PropFloatB = ReadFloat(properties, "offTime", 0.5f);
                compiledNode.BlinkState = new BlinkState();
                break;

            case NodeOp.OutputSegmentColor:
            {
                var segmentIds = ReadStringArray(properties, "segmentIds");
                compiledNode.Priority = ReadFloat(properties, "priority", 0f);
                compiledNode.BlendOp = ParseOutputBlendOp(ReadString(properties, "blendMode", "override"));
                compiledNode.IsActiveStatic = ReadBool(properties, "active", true);
                compiledNode.SegmentIds = segmentIds;
                compiledNode.TargetSegments = ResolveTargetSegments(settings, segmentIds);

#if DEBUG
                Debug.Assert(!HasInputPort(nodeType, "blendMode"));
                Debug.Assert(!HasInputPort(nodeType, "priority"));
                Debug.Assert(!HasInputPort(nodeType, "segmentIds"));
#endif
                break;
            }
        }

        return compiledNode;
    }

    private static RuntimeValue ReadInputDefault(
        JsonObject properties,
        NodeTypeDefinition nodeType,
        NodePortDefinition port)
    {
        return port.ValueType switch
        {
            NodeValueType.Bool => RuntimeValue.FromBool(
                ReadBool(properties, port.Id, ReadDefaultBool(nodeType, port.Id, false))),
            NodeValueType.Float => RuntimeValue.FromFloat(
                ReadFloat(properties, port.Id, ReadDefaultFloat(nodeType, port.Id, 0f))),
            NodeValueType.Color => RuntimeValue.FromColor(
                ReadColor(properties, port.Id, ReadDefaultColor(nodeType, port.Id, Color.Black))),
            _ => default
        };
    }

    private static RuntimeValue ReadInputValue(
        PreparedGraph preparedEffect,
        CompiledNode node,
        int inputIndex)
    {
        var slot = node.InputSlots[inputIndex];
        return slot >= 0
            ? preparedEffect.OutputBuffer[slot]
            : node.InputDefaults[inputIndex];
    }

    private static bool GetInputBool(
        PreparedGraph preparedEffect,
        CompiledNode node,
        int inputIndex)
    {
        var value = ReadInputValue(preparedEffect, node, inputIndex);
        return value.Type == NodeValueType.Bool
            ? value.BoolValue
            : node.InputDefaults[inputIndex].BoolValue;
    }

    private static Color GetInputColor(
        PreparedGraph preparedEffect,
        CompiledNode node,
        int inputIndex)
    {
        var value = ReadInputValue(preparedEffect, node, inputIndex);
        return value.Type == NodeValueType.Color
            ? value.ColorValue
            : node.InputDefaults[inputIndex].ColorValue;
    }

    private static float GetInputFloat(
        PreparedGraph preparedEffect,
        CompiledNode node,
        int inputIndex)
    {
        var value = ReadInputValue(preparedEffect, node, inputIndex);
        return value.Type == NodeValueType.Float
            ? value.FloatValue
            : node.InputDefaults[inputIndex].FloatValue;
    }

    private static void WriteOutput(
        PreparedGraph preparedEffect,
        CompiledNode node,
        int outputIndex,
        RuntimeValue value)
    {
        preparedEffect.OutputBuffer[node.OutputSlots[outputIndex]] = value;
    }

    private void ApplySegmentColorOutputs(PreparedGraph preparedEffect)
    {
        var outputNodes = preparedEffect.OutputNodes;
        if (outputNodes.Length == 0)
        {
            return;
        }

        if (_currentPixel is { } px && _currentSegment is not null)
        {
            if (px.Index >= _currentSegment.Leds.Length)
            {
                return;
            }

            var currentColor = _currentSegment.Leds[px.Index];

            for (var i = 0; i < outputNodes.Length; i++)
            {
                var outputNode = outputNodes[i].Node;
                if (!TargetsSegment(outputNode.TargetSegments, _currentSegment)
                    || !IsOutputActive(preparedEffect, outputNode))
                {
                    continue;
                }

                currentColor = BlendOutputColor(
                    currentColor,
                    GetInputColor(preparedEffect, outputNode, 0),
                    outputNode.BlendOp);
            }

            _currentSegment.Leds[px.Index] = currentColor;
            return;
        }

        for (var outputIndex = 0; outputIndex < outputNodes.Length; outputIndex++)
        {
            var outputNode = outputNodes[outputIndex].Node;
            if (!IsOutputActive(preparedEffect, outputNode))
            {
                continue;
            }

            var color = GetInputColor(preparedEffect, outputNode, 0);
            for (var segmentIndex = 0; segmentIndex < outputNode.TargetSegments.Length; segmentIndex++)
            {
                ApplySegmentColor(outputNode.TargetSegments[segmentIndex], color, outputNode.BlendOp);
            }
        }
    }

    private static bool IsOutputActive(
        PreparedGraph preparedEffect,
        CompiledNode outputNode)
    {
        var activeSlot = outputNode.InputSlots[1];
        if (activeSlot < 0)
        {
            return outputNode.IsActiveStatic;
        }

        var value = preparedEffect.OutputBuffer[activeSlot];
        return value.Type == NodeValueType.Bool
            ? value.BoolValue
            : outputNode.IsActiveStatic;
    }

    private static void ApplySegmentColor(
        Segment segment,
        Color color,
        BlendOp blendMode)
    {
        for (var i = 0; i < segment.Leds.Length; i++)
        {
            segment.Leds[i] = BlendOutputColor(segment.Leds[i], color, blendMode);
        }
    }

    private static bool TargetsSegment(Segment[] targets, Segment segment)
    {
        for (var i = 0; i < targets.Length; i++)
        {
            if (ReferenceEquals(targets[i], segment))
            {
                return true;
            }
        }

        return false;
    }

    private static Segment[] ResolveTargetSegments(Settings? settings, string[] segmentIds)
    {
        if (settings is null)
        {
            return [];
        }

        var segments = new List<Segment>();
        foreach (var device in settings.Devices)
        {
            foreach (var segment in device.Segments)
            {
                if (IsSegmentTargeted(segment, segmentIds))
                {
                    segments.Add(segment);
                }
            }
        }

        return segments.ToArray();
    }

    private static bool IsSegmentTargeted(Segment segment, string[] segmentIds)
    {
        if (segmentIds.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < segmentIds.Length; i++)
        {
            if (string.Equals(segmentIds[i], segment.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadMergedBool(InputSnapshot snapshot, string[] keys, MergeOp mergeMode)
    {
        if (keys.Length == 0)
        {
            return false;
        }

        if (mergeMode == MergeOp.All)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                if (!snapshot.GetBool(keys[i]))
                {
                    return false;
                }
            }

            return true;
        }

        for (var i = 0; i < keys.Length; i++)
        {
            if (snapshot.GetBool(keys[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static float ReadMergedFloat(InputSnapshot snapshot, string[] keys, MergeOp mergeMode)
    {
        if (keys.Length == 0)
        {
            return 0f;
        }

        var result = snapshot.GetFloat(keys[0]);
        if (mergeMode == MergeOp.Average)
        {
            double total = result;
            for (var i = 1; i < keys.Length; i++)
            {
                total += snapshot.GetFloat(keys[i]);
            }

            return (float)(total / keys.Length);
        }

        for (var i = 1; i < keys.Length; i++)
        {
            var value = snapshot.GetFloat(keys[i]);
            result = mergeMode == MergeOp.Min
                ? MathF.Min(result, value)
                : MathF.Max(result, value);
        }

        return result;
    }

    private static Color ReadMergedColor(InputSnapshot snapshot, string[] keys, MergeOp mergeMode)
    {
        if (keys.Length == 0)
        {
            return Color.Black;
        }

        if (mergeMode == MergeOp.Additive)
        {
            var totalRed = 0;
            var totalGreen = 0;
            var totalBlue = 0;

            for (var i = 0; i < keys.Length; i++)
            {
                var color = snapshot.GetColor(keys[i], Color.Black);
                totalRed += color.R;
                totalGreen += color.G;
                totalBlue += color.B;
            }

            return Color.FromRgb(
                (byte)Math.Clamp(totalRed, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(totalGreen, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(totalBlue, byte.MinValue, byte.MaxValue));
        }

        double totalRedAverage = 0;
        double totalGreenAverage = 0;
        double totalBlueAverage = 0;

        for (var i = 0; i < keys.Length; i++)
        {
            var color = snapshot.GetColor(keys[i], Color.Black);
            totalRedAverage += color.R;
            totalGreenAverage += color.G;
            totalBlueAverage += color.B;
        }

        var averageRed = (int)Math.Round(totalRedAverage / keys.Length);
        var averageGreen = (int)Math.Round(totalGreenAverage / keys.Length);
        var averageBlue = (int)Math.Round(totalBlueAverage / keys.Length);

        return Color.FromRgb(
            (byte)Math.Clamp(averageRed, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(averageGreen, byte.MinValue, byte.MaxValue),
            (byte)Math.Clamp(averageBlue, byte.MinValue, byte.MaxValue));
    }

    private static Color BlendOutputColor(Color baseColor, Color outputColor, BlendOp blendMode)
    {
        if (blendMode == BlendOp.Override)
        {
            return outputColor;
        }

        return DenormalizeColor(BlendColors(
            NormalizeColor(baseColor),
            NormalizeColor(outputColor),
            blendMode));
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

    private static float NextRandomFloat(CompiledNode node, float min, float max, float seed)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }

        var seedValue = SeedFromFloat(seed);
        if (seedValue != node.RandomSeed)
        {
            node.RandomSeed = seedValue;
            node.RandomState = BuildRandomState(node.RandomNodeSeed, seedValue);
        }

        var state = node.RandomState;
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        node.RandomState = state == 0 ? 0x9E3779B9u : state;

        var normalized = (state >> 8) * (1f / 16777216f);
        return min + ((max - min) * normalized);
    }

    private static uint BuildRandomState(uint nodeSeed, uint seed)
    {
        var state = nodeSeed ^ seed;
        state ^= state >> 16;
        state *= 0x85EBCA6Bu;
        state ^= state >> 13;
        state *= 0xC2B2AE35u;
        state ^= state >> 16;
        return state == 0 ? 0x9E3779B9u : state;
    }

    private static float PerlinNoise2D(float x, float y)
    {
        var x0 = FastFloor(x);
        var y0 = FastFloor(y);
        var xf = x - x0;
        var yf = y - y0;
        var u = Fade(xf);
        var v = Fade(yf);

        var aa = PerlinGradient(HashGrid(x0, y0), xf, yf);
        var ba = PerlinGradient(HashGrid(x0 + 1, y0), xf - 1f, yf);
        var ab = PerlinGradient(HashGrid(x0, y0 + 1), xf, yf - 1f);
        var bb = PerlinGradient(HashGrid(x0 + 1, y0 + 1), xf - 1f, yf - 1f);

        var x1 = Lerp(aa, ba, u);
        var x2 = Lerp(ab, bb, u);
        return Math.Clamp((Lerp(x1, x2, v) + 1f) * 0.5f, 0f, 1f);
    }

    private static float ValueNoise2D(float x, float y)
    {
        var x0 = FastFloor(x);
        var y0 = FastFloor(y);
        var xf = x - x0;
        var yf = y - y0;
        var u = Fade(xf);
        var v = Fade(yf);

        var aa = HashToUnitFloat(HashGrid(x0, y0));
        var ba = HashToUnitFloat(HashGrid(x0 + 1, y0));
        var ab = HashToUnitFloat(HashGrid(x0, y0 + 1));
        var bb = HashToUnitFloat(HashGrid(x0 + 1, y0 + 1));

        return Lerp(Lerp(aa, ba, u), Lerp(ab, bb, u), v);
    }

    private static int FastFloor(float value)
    {
        var integer = (int)value;
        return value < integer ? integer - 1 : integer;
    }

    private static float Fade(float value)
    {
        return value * value * value * (value * ((value * 6f) - 15f) + 10f);
    }

    private static float Lerp(float from, float to, float factor)
    {
        return from + ((to - from) * factor);
    }

    private static float PerlinGradient(uint hash, float x, float y)
    {
        const float diagonalScale = 0.70710678118f;
        return (hash & 7u) switch
        {
            0u => (x + y) * diagonalScale,
            1u => (-x + y) * diagonalScale,
            2u => (x - y) * diagonalScale,
            3u => (-x - y) * diagonalScale,
            4u => x,
            5u => -x,
            6u => y,
            _ => -y
        };
    }

    private static uint HashGrid(int x, int y)
    {
        unchecked
        {
            var hash = (uint)x * 0x8DA6B343u;
            hash ^= (uint)y * 0xD8163841u;
            hash ^= hash >> 13;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }

    private static float HashToUnitFloat(uint hash)
    {
        return (hash >> 8) * (1f / 16777216f);
    }

    private static Color SampleGradient(GradientStop[] stops, float factor, InterpolationOp interpolation)
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

            if (interpolation == InterpolationOp.Constant)
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

    private static float EvaluateWaveform(WaveformOp waveform, float phase)
    {
        var t = phase - MathF.Floor(phase);
        return waveform switch
        {
            WaveformOp.Square => t < 0.5f ? 1f : 0f,
            WaveformOp.Triangle => t < 0.5f ? t * 2f : 2f - (t * 2f),
            WaveformOp.Sawtooth => t,
            _ => (MathF.Sin(t * MathF.PI * 2f) + 1f) * 0.5f
        };
    }

    private static Color MixColors(Color a, Color b, float factor, BlendOp mode)
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
        BlendOp mode)
    {
        return mode switch
        {
            BlendOp.Darken => new(
                MathF.Min(baseColor.R, blendColor.R),
                MathF.Min(baseColor.G, blendColor.G),
                MathF.Min(baseColor.B, blendColor.B)),
            BlendOp.Multiply => new(
                baseColor.R * blendColor.R,
                baseColor.G * blendColor.G,
                baseColor.B * blendColor.B),
            BlendOp.ColorBurn => new(
                ColorBurnChannel(baseColor.R, blendColor.R),
                ColorBurnChannel(baseColor.G, blendColor.G),
                ColorBurnChannel(baseColor.B, blendColor.B)),
            BlendOp.Lighten => new(
                MathF.Max(baseColor.R, blendColor.R),
                MathF.Max(baseColor.G, blendColor.G),
                MathF.Max(baseColor.B, blendColor.B)),
            BlendOp.Screen => new(
                1f - ((1f - baseColor.R) * (1f - blendColor.R)),
                1f - ((1f - baseColor.G) * (1f - blendColor.G)),
                1f - ((1f - baseColor.B) * (1f - blendColor.B))),
            BlendOp.ColorDodge => new(
                ColorDodgeChannel(baseColor.R, blendColor.R),
                ColorDodgeChannel(baseColor.G, blendColor.G),
                ColorDodgeChannel(baseColor.B, blendColor.B)),
            BlendOp.Add => new(
                MathF.Min(1f, baseColor.R + blendColor.R),
                MathF.Min(1f, baseColor.G + blendColor.G),
                MathF.Min(1f, baseColor.B + blendColor.B)),
            BlendOp.Overlay => new(
                OverlayChannel(baseColor.R, blendColor.R),
                OverlayChannel(baseColor.G, blendColor.G),
                OverlayChannel(baseColor.B, blendColor.B)),
            BlendOp.SoftLight => new(
                SoftLightChannel(baseColor.R, blendColor.R),
                SoftLightChannel(baseColor.G, blendColor.G),
                SoftLightChannel(baseColor.B, blendColor.B)),
            BlendOp.LinearLight => new(
                Math.Clamp(baseColor.R + (2f * blendColor.R) - 1f, 0f, 1f),
                Math.Clamp(baseColor.G + (2f * blendColor.G) - 1f, 0f, 1f),
                Math.Clamp(baseColor.B + (2f * blendColor.B) - 1f, 0f, 1f)),
            BlendOp.Difference => new(
                MathF.Abs(baseColor.R - blendColor.R),
                MathF.Abs(baseColor.G - blendColor.G),
                MathF.Abs(baseColor.B - blendColor.B)),
            BlendOp.Exclusion => new(
                baseColor.R + blendColor.R - (2f * baseColor.R * blendColor.R),
                baseColor.G + blendColor.G - (2f * baseColor.G * blendColor.G),
                baseColor.B + blendColor.B - (2f * baseColor.B * blendColor.B)),
            BlendOp.Subtract => new(
                MathF.Max(0f, baseColor.R - blendColor.R),
                MathF.Max(0f, baseColor.G - blendColor.G),
                MathF.Max(0f, baseColor.B - blendColor.B)),
            BlendOp.Divide => new(
                DivideChannel(baseColor.R, blendColor.R),
                DivideChannel(baseColor.G, blendColor.G),
                DivideChannel(baseColor.B, blendColor.B)),
            BlendOp.Hue or BlendOp.Saturation or BlendOp.Color or BlendOp.Value => BlendHsv(baseColor, blendColor, mode),
            _ => blendColor
        };
    }

    private static NormalizedColor BlendHsv(
        NormalizedColor baseColor,
        NormalizedColor blendColor,
        BlendOp mode)
    {
        var baseHsv = ColorToHsv(baseColor);
        var blendHsv = ColorToHsv(blendColor);

        return mode switch
        {
            BlendOp.Hue => HsvToNormalizedColor(blendHsv.Hue, baseHsv.Saturation, baseHsv.Value),
            BlendOp.Saturation => HsvToNormalizedColor(baseHsv.Hue, blendHsv.Saturation, baseHsv.Value),
            BlendOp.Color => HsvToNormalizedColor(blendHsv.Hue, blendHsv.Saturation, baseHsv.Value),
            BlendOp.Value => HsvToNormalizedColor(baseHsv.Hue, baseHsv.Saturation, blendHsv.Value),
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

        return TryReadFloat(node, out var value) ? value : defaultValue;
    }

    private static bool ReadBool(JsonObject properties, string key, bool defaultValue = false)
    {
        if (!properties.TryGetPropertyValue(key, out var node) || node is null)
        {
            return defaultValue;
        }

        return TryReadBool(node, out var value) ? value : defaultValue;
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

    private static string[] ReadStringArray(JsonObject properties, string key)
    {
        return ParseStringArray(ReadString(properties, key, string.Empty));
    }

    private static string[] ParseStringArray(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var values = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 2)
        {
            return values;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinctValues = new List<string>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            if (seen.Add(values[i]))
            {
                distinctValues.Add(values[i]);
            }
        }

        return distinctValues.ToArray();
    }

    private static uint HashNodeId(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            for (var i = 0; i < value.Length; i++)
            {
                hash = (hash ^ value[i]) * 16777619u;
            }

            return hash;
        }
    }

    private static uint SeedFromFloat(float value)
    {
        unchecked
        {
            var seed = (uint)BitConverter.SingleToInt32Bits(value);
            seed ^= seed >> 16;
            seed *= 0x7FEB352Du;
            seed ^= seed >> 15;
            seed *= 0x846CA68Bu;
            seed ^= seed >> 16;
            return seed;
        }
    }

    private static bool TryReadFloat(JsonNode node, out float value)
    {
        value = default;
        if (node is not JsonValue valueNode)
        {
            return false;
        }

        if (valueNode.TryGetValue<float>(out var floatValue))
        {
            value = floatValue;
            return true;
        }

        if (valueNode.TryGetValue<double>(out var doubleValue))
        {
            value = (float)doubleValue;
            return true;
        }

        if (valueNode.TryGetValue<int>(out var intValue))
        {
            value = intValue;
            return true;
        }

        if (valueNode.TryGetValue<long>(out var longValue))
        {
            value = longValue;
            return true;
        }

        if (valueNode.TryGetValue<string>(out var stringValue)
            && float.TryParse(stringValue, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryReadBool(JsonNode node, out bool value)
    {
        value = default;
        if (node is not JsonValue valueNode)
        {
            return false;
        }

        if (valueNode.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue;
            return true;
        }

        if (valueNode.TryGetValue<string>(out var stringValue)
            && bool.TryParse(stringValue, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static float ReadDefaultFloat(NodeTypeDefinition nodeType, string key, float fallback)
    {
        var defaultValue = FindPropertyDefault(nodeType, key);
        return defaultValue is not null && TryReadFloat(defaultValue, out var value)
            ? value
            : fallback;
    }

    private static bool ReadDefaultBool(NodeTypeDefinition nodeType, string key, bool fallback)
    {
        var defaultValue = FindPropertyDefault(nodeType, key);
        return defaultValue is not null && TryReadBool(defaultValue, out var value)
            ? value
            : fallback;
    }

    private static Color ReadDefaultColor(NodeTypeDefinition nodeType, string key, Color fallback)
    {
        var defaultValue = FindPropertyDefault(nodeType, key);
        if (defaultValue is JsonObject obj)
        {
            return Color.FromRgb(
                ReadByte(obj, "r", fallback.R),
                ReadByte(obj, "g", fallback.G),
                ReadByte(obj, "b", fallback.B));
        }

        if (defaultValue is JsonValue stringNode
            && stringNode.TryGetValue<string>(out var hex)
            && TryParseHexColor(hex, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static JsonNode? FindPropertyDefault(NodeTypeDefinition nodeType, string key)
    {
        for (var i = 0; i < nodeType.Properties.Count; i++)
        {
            var property = nodeType.Properties[i];
            if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return property.DefaultValue;
            }
        }

        return null;
    }

    private static bool HasInputPort(NodeTypeDefinition nodeType, string portId)
    {
        for (var i = 0; i < nodeType.Inputs.Count; i++)
        {
            if (string.Equals(nodeType.Inputs[i].Id, portId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static NodeOp ParseNodeOp(string typeId)
    {
        return typeId.Trim().ToLowerInvariant() switch
        {
            "annotation.comment" => NodeOp.AnnotationComment,
            "reroute.bool" => NodeOp.RerouteBool,
            "reroute.float" => NodeOp.RerouteFloat,
            "reroute.color" => NodeOp.RerouteColor,
            "constant.color" => NodeOp.ConstantColor,
            "constant.float" => NodeOp.ConstantFloat,
            "constant.bool" => NodeOp.ConstantBool,
            "input.bool" => NodeOp.InputBool,
            "input.float" => NodeOp.InputFloat,
            "input.color" => NodeOp.InputColor,
            "pixel.info" => NodeOp.PixelInfo,
            "logic.select-color" => NodeOp.LogicSelectColor,
            "logic.select-float" => NodeOp.LogicSelectFloat,
            "logic.not" => NodeOp.LogicNot,
            "logic.and" => NodeOp.LogicAnd,
            "logic.or" => NodeOp.LogicOr,
            "logic.compare" => NodeOp.LogicCompare,
            "logic.mix-color" => NodeOp.LogicMixColor,
            "math.add" => NodeOp.MathAdd,
            "math.multiply" => NodeOp.MathMultiply,
            "math.clamp" => NodeOp.MathClamp,
            "math.remap" => NodeOp.MathRemap,
            "math.wrap" => NodeOp.MathWrap,
            "math.ping-pong" => NodeOp.MathPingPong,
            "math.modulo" => NodeOp.MathModulo,
            "math.abs" => NodeOp.MathAbs,
            "math.step" => NodeOp.MathStep,
            "math.smooth-step" => NodeOp.MathSmoothStep,
            "math.sin" => NodeOp.MathSin,
            "math.cos" => NodeOp.MathCos,
            "math.tan" => NodeOp.MathTan,
            "math.random" => NodeOp.MathRandom,
            "math.perlin-2d" => NodeOp.MathPerlinNoise2D,
            "math.value-noise-2d" => NodeOp.MathValueNoise2D,
            "color.brightness" => NodeOp.ColorBrightness,
            "color.hsv" => NodeOp.ColorHsv,
            "color.to-hsv" => NodeOp.ColorToHsv,
            "color.gradient" => NodeOp.ColorGradient,
            "time.elapsed" => NodeOp.TimeElapsed,
            "time.oscillator" => NodeOp.TimeOscillator,
            "time.pulse" => NodeOp.TimePulse,
            "time.envelope" => NodeOp.TimeEnvelope,
            "time.blink" => NodeOp.TimeBlink,
            "output.segment-color" => NodeOp.OutputSegmentColor,
            _ => NodeOp.Unknown
        };
    }

    private static MergeOp ParseBoolMergeOp(string mode)
    {
        return NormalizeMode(mode) == "all" ? MergeOp.All : MergeOp.Any;
    }

    private static MergeOp ParseFloatMergeOp(string mode)
    {
        return NormalizeMode(mode) switch
        {
            "min" => MergeOp.Min,
            "average" => MergeOp.Average,
            _ => MergeOp.Max
        };
    }

    private static MergeOp ParseColorMergeOp(string mode)
    {
        return NormalizeMode(mode) == "additive" ? MergeOp.Additive : MergeOp.Average;
    }

    private static CompareOp ParseCompareOp(string mode)
    {
        return NormalizeMode(mode) switch
        {
            "less" => CompareOp.Less,
            "equal" => CompareOp.Equal,
            _ => CompareOp.Greater
        };
    }

    private static BlendOp ParseOutputBlendOp(string mode)
    {
        return NormalizeMode(mode) switch
        {
            "override" or "replace" or "mix" => BlendOp.Override,
            "max" => BlendOp.Lighten,
            "min" => BlendOp.Darken,
            var normalized => ParseBlendOp(normalized)
        };
    }

    private static BlendOp ParseBlendOp(string mode)
    {
        return NormalizeMode(mode) switch
        {
            "darken" => BlendOp.Darken,
            "multiply" => BlendOp.Multiply,
            "color-burn" => BlendOp.ColorBurn,
            "lighten" => BlendOp.Lighten,
            "screen" => BlendOp.Screen,
            "color-dodge" => BlendOp.ColorDodge,
            "add" => BlendOp.Add,
            "overlay" => BlendOp.Overlay,
            "soft-light" => BlendOp.SoftLight,
            "linear-light" => BlendOp.LinearLight,
            "difference" => BlendOp.Difference,
            "exclusion" => BlendOp.Exclusion,
            "subtract" => BlendOp.Subtract,
            "divide" => BlendOp.Divide,
            "hue" => BlendOp.Hue,
            "saturation" => BlendOp.Saturation,
            "color" => BlendOp.Color,
            "value" => BlendOp.Value,
            _ => BlendOp.Mix
        };
    }

    private static WaveformOp ParseWaveformOp(string waveform)
    {
        return NormalizeMode(waveform) switch
        {
            "square" => WaveformOp.Square,
            "triangle" => WaveformOp.Triangle,
            "sawtooth" => WaveformOp.Sawtooth,
            _ => WaveformOp.Sine
        };
    }

    private static EdgeOp ParseEdgeOp(string edge)
    {
        return NormalizeMode(edge) == "falling" ? EdgeOp.Falling : EdgeOp.Rising;
    }

    private static InterpolationOp ParseInterpolationOp(string interpolation)
    {
        return NormalizeMode(interpolation) == "constant" ? InterpolationOp.Constant : InterpolationOp.Linear;
    }

    private static string NormalizeMode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}

internal readonly record struct PixelTarget(
    Segment Segment,
    int DeviceIndex,
    int SegmentIndex,
    int DeviceStartIndex,
    int DeviceLength,
    int GlobalStartIndex,
    int GlobalLength);

internal static class PixelTargetLayout
{
    public static PixelTarget[] Build(Settings settings, HashSet<Segment> targetSegmentSet)
    {
        if (targetSegmentSet.Count == 0)
        {
            return [];
        }

        var globalLength = 0;
        foreach (var segment in targetSegmentSet)
        {
            globalLength += segment.Leds.Length;
        }

        var targets = new List<PixelTarget>(targetSegmentSet.Count);
        var globalStartIndex = 0;

        for (var deviceIndex = 0; deviceIndex < settings.Devices.Count; deviceIndex++)
        {
            var device = settings.Devices[deviceIndex];
            var deviceLength = 0;
            for (var segmentIndex = 0; segmentIndex < device.Segments.Count; segmentIndex++)
            {
                deviceLength += device.Segments[segmentIndex].Leds.Length;
            }

            var deviceStartIndex = 0;
            for (var segmentIndex = 0; segmentIndex < device.Segments.Count; segmentIndex++)
            {
                var segment = device.Segments[segmentIndex];
                if (targetSegmentSet.Contains(segment))
                {
                    targets.Add(new PixelTarget(
                        segment,
                        deviceIndex,
                        segmentIndex,
                        deviceStartIndex,
                        deviceLength,
                        globalStartIndex,
                        globalLength));
                    globalStartIndex += segment.Leds.Length;
                }

                deviceStartIndex += segment.Leds.Length;
            }
        }

        return targets.ToArray();
    }
}

public sealed class PreparedGraph
{
    internal PreparedGraph(
        CompiledNodeGraph compiledGraph,
        IReadOnlyDictionary<string, NodeTypeDefinition> nodeTypesByNodeId,
        IReadOnlyDictionary<string, Dictionary<string, RuntimeConnectionSource>> inputConnectionsByNodeId,
        CompiledNode[] compiledNodes,
        RuntimeValue[] outputBuffer,
        CompiledOutputNode[] outputNodes,
        Segment[] targetSegments,
        PixelTarget[] pixelTargets,
        bool targetsResolved)
    {
        CompiledGraph = compiledGraph;
        NodeTypesByNodeId = nodeTypesByNodeId;
        InputConnectionsByNodeId = inputConnectionsByNodeId;
        CompiledNodes = compiledNodes;
        OutputBuffer = outputBuffer;
        OutputNodes = outputNodes;
        TargetSegments = targetSegments;
        PixelTargets = pixelTargets;
        TargetsResolved = targetsResolved;
        HasPixelInfoNodes = ComputeHasPixelInfoNodes(compiledNodes);
    }

    public CompiledNodeGraph CompiledGraph { get; }

    public IReadOnlyDictionary<string, NodeTypeDefinition> NodeTypesByNodeId { get; }

    public IReadOnlyDictionary<string, Dictionary<string, RuntimeConnectionSource>> InputConnectionsByNodeId { get; }

    internal CompiledNode[] CompiledNodes { get; }

    internal RuntimeValue[] OutputBuffer { get; }

    internal CompiledOutputNode[] OutputNodes { get; }

    internal Segment[] TargetSegments { get; private set; }

    internal PixelTarget[] PixelTargets { get; private set; }

    internal bool TargetsResolved { get; private set; }

    public bool CanRender => CompiledGraph.Validation.IsValid && CompiledNodes.Length > 0;

    public bool HasPixelInfoNodes { get; }

    internal void ResolveTargets(Settings settings)
    {
        var targetSegmentSet = new HashSet<Segment>();
        for (var i = 0; i < OutputNodes.Length; i++)
        {
            var node = OutputNodes[i].Node;
            node.TargetSegments = ResolveTargetSegments(settings, node.SegmentIds);
            for (var segmentIndex = 0; segmentIndex < node.TargetSegments.Length; segmentIndex++)
            {
                targetSegmentSet.Add(node.TargetSegments[segmentIndex]);
            }
        }

        PixelTargets = PixelTargetLayout.Build(settings, targetSegmentSet);
        TargetSegments = new Segment[PixelTargets.Length];
        for (var i = 0; i < PixelTargets.Length; i++)
        {
            TargetSegments[i] = PixelTargets[i].Segment;
        }

        TargetsResolved = true;
    }

    private static bool ComputeHasPixelInfoNodes(CompiledNode[] compiledNodes)
    {
        for (var i = 0; i < compiledNodes.Length; i++)
        {
            if (compiledNodes[i].Op == NodeOp.PixelInfo)
            {
                return true;
            }
        }

        return false;
    }

    private static Segment[] ResolveTargetSegments(Settings settings, string[] segmentIds)
    {
        var segments = new List<Segment>();
        foreach (var device in settings.Devices)
        {
            foreach (var segment in device.Segments)
            {
                if (IsSegmentTargeted(segment, segmentIds))
                {
                    segments.Add(segment);
                }
            }
        }

        return segments.ToArray();
    }

    private static bool IsSegmentTargeted(Segment segment, string[] segmentIds)
    {
        if (segmentIds.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < segmentIds.Length; i++)
        {
            if (string.Equals(segmentIds[i], segment.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetInputSource(string nodeId, string portId, out RuntimeConnectionSource source)
    {
        source = default;

        return InputConnectionsByNodeId.TryGetValue(nodeId, out var inputs)
            && inputs.TryGetValue(portId, out source);
    }
}

internal sealed class CompiledNode
{
    public NodeOp Op;
    public int EvaluationIndex;
    public int[] InputSlots = [];
    public int[] OutputSlots = [];
    public RuntimeValue[] InputDefaults = [];

    public float PropFloatA;
    public float PropFloatB;
    public float PropFloatC;
    public float PropFloatD;
    public bool PropBoolA;
    public Color PropColorA;

    public BlendOp BlendOp;
    public MergeOp MergeOp;
    public WaveformOp WaveformOp;
    public EdgeOp EdgeOp;
    public InterpolationOp InterpolationOp;
    public CompareOp CompareOp;

    public string[] InputKeys = [];
    public string[] SegmentIds = [];
    public Segment[] TargetSegments = [];
    public GradientStop[] GradientStops = [];
    public uint RandomNodeSeed;
    public uint RandomSeed;
    public uint RandomState;
    public PulseState? PulseState;
    public EnvelopeState? EnvelopeState;
    public BlinkState? BlinkState;

    public float Priority;
    public bool IsActiveStatic;
}

internal readonly record struct CompiledOutputNode(CompiledNode Node);

internal enum NodeOp
{
    Unknown,
    AnnotationComment,
    RerouteBool,
    RerouteFloat,
    RerouteColor,
    ConstantColor,
    ConstantFloat,
    ConstantBool,
    InputBool,
    InputFloat,
    InputColor,
    PixelInfo,
    LogicSelectColor,
    LogicSelectFloat,
    LogicNot,
    LogicAnd,
    LogicOr,
    LogicCompare,
    LogicMixColor,
    MathAdd,
    MathMultiply,
    MathClamp,
    MathRemap,
    MathWrap,
    MathPingPong,
    MathModulo,
    MathAbs,
    MathStep,
    MathSmoothStep,
    MathSin,
    MathCos,
    MathTan,
    MathRandom,
    MathPerlinNoise2D,
    MathValueNoise2D,
    ColorBrightness,
    ColorHsv,
    ColorToHsv,
    ColorGradient,
           TimeElapsed,
            TimeOscillator,
            TimePulse,
            TimeEnvelope,
            TimeBlink,
            OutputSegmentColor
}

internal enum BlendOp
{
    Mix,
    Override,
    Darken,
    Multiply,
    ColorBurn,
    Lighten,
    Screen,
    ColorDodge,
    Add,
    Overlay,
    SoftLight,
    LinearLight,
    Difference,
    Exclusion,
    Subtract,
    Divide,
    Hue,
    Saturation,
    Color,
    Value
}

internal enum MergeOp
{
    Any,
    All,
    Max,
    Min,
    Average,
    Additive
}

internal enum WaveformOp
{
    Sine,
    Square,
    Triangle,
    Sawtooth
}

internal enum EdgeOp
{
    Rising,
    Falling
}

internal enum InterpolationOp
{
    Linear,
    Constant
}

internal enum CompareOp
{
    Greater,
    Less,
    Equal
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
            var array = JsonNode.Parse(json)?.AsArray();
            if (array is null || array.Count == 0)
            {
                return DefaultStops;
            }

            var stops = new List<GradientStop>(array.Count);
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
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

    private static float GetFloat(JsonObject obj, string key, float fallback)
    {
        var node = obj[key];
        if (node is JsonValue val)
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

public sealed class BlinkState
{
    private float _switchTime;
    private bool _started;

    public bool CurrentValue { get; private set; }

    public void Update(float currentTime, float onTime, float offTime)
    {
        var cycleDuration = onTime + offTime;
        if (cycleDuration <= 0f)
        {
            return;
        }

        if (!_started)
        {
            _started = true;
            _switchTime = currentTime;
            CurrentValue = true;
            return;
        }

        var elapsed = currentTime - _switchTime;
        if (elapsed < 0f)
        {
            return;
        }

        var cyclePhase = elapsed % cycleDuration;
        var newValue = cyclePhase < onTime;
        if (CurrentValue != newValue)
        {
            CurrentValue = newValue;
            _started = true;
        }
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
