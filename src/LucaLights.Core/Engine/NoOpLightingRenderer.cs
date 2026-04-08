using LucaLights.Core.Models;

namespace LucaLights.Core.Engine;

public sealed class NoOpLightingRenderer : ILightingRenderer
{
    public void Prepare(Settings settings)
    {
    }

    public void Render(Settings settings, LightingFrameContext frameContext)
    {
    }

    public void Clear(Settings settings)
    {
    }
}
