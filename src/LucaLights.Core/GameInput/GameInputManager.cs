using LucaLights.Core.Models;

namespace LucaLights.Core.GameInput;

public sealed class GameInputManager : IDisposable
{
    private readonly Dictionary<string, IGameInputModule> _modules =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private readonly Action<string>? _log;

    private IGameInputModule? _activeModule;
    private InputSnapshot _latestSnapshot = InputSnapshot.Empty;
    private bool _disposed;

    public GameInputManager(Action<string>? log = null)
    {
        _log = log;
    }

    public event Action<string?>? ActiveModuleChanged;

    public event Action<InputSnapshot>? SnapshotUpdated;

    public object SyncRoot => _syncRoot;

    public string? ActiveModuleId
    {
        get
        {
            lock (_syncRoot)
            {
                return _activeModule?.ModuleId;
            }
        }
    }

    public InputSnapshot LatestSnapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return _latestSnapshot;
            }
        }
    }

    public bool IsRenderingActive => LatestSnapshot.IsActive;

    public IReadOnlyCollection<IGameInputModule> Modules
    {
        get
        {
            lock (_syncRoot)
            {
                return _modules.Values.ToArray();
            }
        }
    }

    public void RegisterModule(IGameInputModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            _modules[module.ModuleId] = module;
        }
    }

    public bool TryGetModule(string moduleId, out IGameInputModule? module)
    {
        lock (_syncRoot)
        {
            return _modules.TryGetValue(moduleId, out module);
        }
    }

    public IReadOnlyList<InputDefinition> GetDefinitions()
    {
        lock (_syncRoot)
        {
            return _modules.Values
                .Select(module => module.GetDefinition())
                .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public async Task StartAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ThrowIfDisposed();

        await SetActiveModuleAsync(settings.ActiveInputModuleId, cancellationToken);
    }

    public async Task SetActiveModuleAsync(string? moduleId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        IGameInputModule? currentModule;
        IGameInputModule? nextModule = null;

        lock (_syncRoot)
        {
            currentModule = _activeModule;

            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                _modules.TryGetValue(moduleId, out nextModule);
            }

            if (ReferenceEquals(currentModule, nextModule))
            {
                return;
            }
        }

        if (currentModule is not null)
        {
            currentModule.SnapshotUpdated -= HandleSnapshotUpdated;
            await currentModule.StopAsync(cancellationToken);
        }

        lock (_syncRoot)
        {
            _activeModule = nextModule;
            _latestSnapshot = InputSnapshot.Empty;
        }

        if (nextModule is not null)
        {
            nextModule.SnapshotUpdated += HandleSnapshotUpdated;
            await nextModule.StartAsync(cancellationToken);
            HandleSnapshotUpdated(nextModule.GetLatestSnapshot());
            _log?.Invoke($"Game input module started: {nextModule.ModuleId}");
        }
        else
        {
            _log?.Invoke($"Game input module not found: {moduleId}");
        }

        ActiveModuleChanged?.Invoke(nextModule?.ModuleId);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        IGameInputModule? currentModule;
        lock (_syncRoot)
        {
            currentModule = _activeModule;
            _activeModule = null;
            _latestSnapshot = InputSnapshot.Empty;
        }

        if (currentModule is not null)
        {
            currentModule.SnapshotUpdated -= HandleSnapshotUpdated;
            await currentModule.StopAsync(cancellationToken);
        }

        ActiveModuleChanged?.Invoke(null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        _disposed = true;
    }

    private void HandleSnapshotUpdated(InputSnapshot snapshot)
    {
        lock (_syncRoot)
        {
            _latestSnapshot = snapshot;
        }

        SnapshotUpdated?.Invoke(snapshot);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
