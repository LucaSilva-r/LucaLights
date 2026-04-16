namespace LucaLights.Core.GameInput;

public interface IGameInputModule
{
    string ModuleId { get; }

    string DisplayName { get; }

    InputDefinition GetDefinition();

    InputSnapshot GetLatestSnapshot();

    event Action<InputSnapshot>? SnapshotUpdated;

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    // Signals that `consumed` has been rendered. Modules with pulse/edge-triggered
    // signals should clear only the latches that were observed as true in `consumed`,
    // so pulses that arrived after the renderer sampled are preserved for the next frame.
    void AcknowledgePulses(InputSnapshot consumed) { }
}
