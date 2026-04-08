namespace LucaLights.Core.Engine;

public sealed class LightingManagerOptions
{
    public int TargetFps { get; set; } = 60;

    public bool ClearOutputWhenInactive { get; set; } = true;
}
