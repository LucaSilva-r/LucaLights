using LucaLights.Core.Models;

namespace LucaLights.Core.Engine;

public interface ILightingRenderer
{
    void Prepare(Settings settings);

    void Render(Settings settings, LightingFrameContext frameContext);

    void Clear(Settings settings);
}
