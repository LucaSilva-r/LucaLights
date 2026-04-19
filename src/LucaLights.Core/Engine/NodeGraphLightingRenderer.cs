using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;

namespace LucaLights.Core.Engine;

public sealed class NodeGraphLightingRenderer : ILightingRenderer
{
    private readonly GraphRuntimeEvaluator _runtimeEvaluator;
    private PreparedGraph? _preparedGraph;

    public NodeGraphLightingRenderer(GraphRuntimeEvaluator runtimeEvaluator)
    {
        _runtimeEvaluator = runtimeEvaluator;
    }

    public void Prepare(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _preparedGraph = _runtimeEvaluator.Prepare(settings);
    }

    public void Render(Settings settings, LightingFrameContext frameContext)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _runtimeEvaluator.Render(settings, _preparedGraph, frameContext);
    }

    public void Clear(Settings settings)
    {
    }
}
