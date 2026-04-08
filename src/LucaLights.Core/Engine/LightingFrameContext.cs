using LucaLights.Core.GameInput;

namespace LucaLights.Core.Engine;

public readonly record struct LightingFrameContext(
    long FrameIndex,
    TimeSpan TotalElapsed,
    TimeSpan Delta,
    InputSnapshot InputSnapshot);
