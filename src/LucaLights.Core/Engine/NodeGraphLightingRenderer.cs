using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;

namespace LucaLights.Core.Engine;

public sealed class NodeGraphLightingRenderer : ILightingRenderer
{
    private readonly GraphRuntimeEvaluator _runtimeEvaluator;
    private PreparedGraphEffect? _preparedEffect;

    public NodeGraphLightingRenderer(GraphRuntimeEvaluator runtimeEvaluator)
    {
        _runtimeEvaluator = runtimeEvaluator;
    }

    public void Prepare(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _preparedEffect = _runtimeEvaluator.Prepare(FindActiveEffect(settings));
    }

    public void Render(Settings settings, LightingFrameContext frameContext)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _runtimeEvaluator.Render(settings, _preparedEffect, frameContext);
    }

    public void Clear(Settings settings)
    {
    }

    private static Effect? FindActiveEffect(Settings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ActiveEffectId))
        {
            var activeEffect = settings.Effects.FirstOrDefault(effect =>
                string.Equals(effect.Id, settings.ActiveEffectId, StringComparison.OrdinalIgnoreCase));
            if (activeEffect is not null)
            {
                return activeEffect;
            }
        }

        return settings.Effects.FirstOrDefault();
    }
}
