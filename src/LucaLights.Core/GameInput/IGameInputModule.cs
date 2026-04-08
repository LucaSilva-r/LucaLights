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
}
