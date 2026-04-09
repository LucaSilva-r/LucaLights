using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;

namespace LucaLights.Server.Services;

public sealed class RuntimeEventHostedService : IHostedService
{
    private readonly GameInputManager _gameInputManager;
    private readonly LightingManager _lightingManager;
    private readonly RuntimeEventBroadcaster _eventBroadcaster;

    public RuntimeEventHostedService(
        GameInputManager gameInputManager,
        LightingManager lightingManager,
        RuntimeEventBroadcaster eventBroadcaster)
    {
        _gameInputManager = gameInputManager;
        _lightingManager = lightingManager;
        _eventBroadcaster = eventBroadcaster;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _gameInputManager.SnapshotUpdated += HandleSnapshotUpdated;
        _gameInputManager.ActiveModuleChanged += HandleActiveModuleChanged;
        _lightingManager.SettingsApplied += HandleSettingsApplied;
        _lightingManager.OutputCleared += HandleOutputCleared;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _gameInputManager.SnapshotUpdated -= HandleSnapshotUpdated;
        _gameInputManager.ActiveModuleChanged -= HandleActiveModuleChanged;
        _lightingManager.SettingsApplied -= HandleSettingsApplied;
        _lightingManager.OutputCleared -= HandleOutputCleared;

        return Task.CompletedTask;
    }

    private void HandleSnapshotUpdated(InputSnapshot snapshot)
    {
        _ = _eventBroadcaster.PublishInputSnapshotAsync(snapshot).AsTask();
    }

    private void HandleActiveModuleChanged(string? moduleId)
    {
        _ = _eventBroadcaster.PublishModuleChangedAsync(moduleId).AsTask();
    }

    private void HandleSettingsApplied()
    {
        _ = _eventBroadcaster.PublishSystemEventAsync("settings.applied").AsTask();
    }

    private void HandleOutputCleared()
    {
        _ = _eventBroadcaster.PublishSystemEventAsync("output.cleared").AsTask();
    }
}
