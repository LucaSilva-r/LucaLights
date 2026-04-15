using LucaLights.Core.Models;

namespace LucaLights.Core.GameInput;

public sealed class GameInputManager : IDisposable
{
    private readonly Dictionary<string, IGameInputModule> _modules =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InputSnapshot> _latestSnapshots =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastActiveTransitionTimes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private readonly Action<string>? _log;

    private IGameInputModule? _activeModule;
    private string? _preferredModuleId;
    private InputSnapshot _latestSnapshot = InputSnapshot.Empty;
    private bool _started;
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
            _latestSnapshots[module.ModuleId] = InputSnapshot.Empty;
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

        lock (_syncRoot)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _preferredModuleId = settings.ActiveInputModuleId;
        }

        foreach (var module in Modules)
        {
            module.SnapshotUpdated += snapshot => HandleSnapshotUpdated(module.ModuleId, snapshot);
            await module.StartAsync(cancellationToken);
            HandleSnapshotUpdated(module.ModuleId, module.GetLatestSnapshot());
            _log?.Invoke($"Game input module started: {module.ModuleId}");
        }

        lock (_syncRoot)
        {
            UpdateActiveModuleUnsafe(out _, out _);
        }
    }

    public Task SetActiveModuleAsync(string? moduleId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        string? newActiveId = null;
        InputSnapshot? newSnapshot = null;
        bool changed = false;

        lock (_syncRoot)
        {
            if (string.Equals(_preferredModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            _preferredModuleId = moduleId;
            changed = UpdateActiveModuleUnsafe(out newActiveId, out newSnapshot);
        }

        if (changed)
        {
            ActiveModuleChanged?.Invoke(newActiveId);
            if (newSnapshot != null)
            {
                SnapshotUpdated?.Invoke(newSnapshot);
            }
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        IGameInputModule[] modules;
        lock (_syncRoot)
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            modules = _modules.Values.ToArray();
            _activeModule = null;
            _latestSnapshot = InputSnapshot.Empty;
        }

        foreach (var module in modules)
        {
            await module.StopAsync(cancellationToken);
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

    private void HandleSnapshotUpdated(string moduleId, InputSnapshot snapshot)
    {
        InputSnapshot? toPublish = null;
        string? activeChangedTo = null;
        bool activeChanged = false;

        lock (_syncRoot)
        {
            bool wasActive = _latestSnapshots.TryGetValue(moduleId, out var prevSnap) && prevSnap.IsActive;
            bool isActive = snapshot.IsActive;

            if (isActive && !wasActive)
            {
                _lastActiveTransitionTimes[moduleId] = DateTimeOffset.UtcNow;
            }

            _latestSnapshots[moduleId] = snapshot;

            bool isCurrent = _activeModule?.ModuleId == moduleId;
            bool shouldReevaluate = _activeModule == null
                                    || (isActive && !isCurrent)
                                    || (isCurrent && !isActive);

            if (shouldReevaluate)
            {
                activeChanged = UpdateActiveModuleUnsafe(out activeChangedTo, out _);
            }

            if (_activeModule?.ModuleId == moduleId)
            {
                _latestSnapshot = snapshot;
                toPublish = snapshot;
            }
        }

        if (activeChanged)
        {
            _log?.Invoke($"Active input module changed to: {activeChangedTo ?? "(none)"}");
            ActiveModuleChanged?.Invoke(activeChangedTo);
        }

        if (toPublish != null)
        {
            SnapshotUpdated?.Invoke(toPublish);
        }
    }

    private bool UpdateActiveModuleUnsafe(out string? newActiveId, out InputSnapshot? snapshotToPublish)
    {
        newActiveId = null;
        snapshotToPublish = null;

        // 1. Pick the module that is IsActive and has the most recent transition to active.
        var nextModule = _modules.Values
            .Where(m => _latestSnapshots.TryGetValue(m.ModuleId, out var s) && s.IsActive)
            .OrderByDescending(m => _lastActiveTransitionTimes.TryGetValue(m.ModuleId, out var t) ? t : DateTimeOffset.MinValue)
            .FirstOrDefault();

        // 2. If none are active, pick preferred if it's connected
        if (nextModule == null && _preferredModuleId != null && _modules.TryGetValue(_preferredModuleId, out var preferred) && _latestSnapshots.TryGetValue(preferred.ModuleId, out var pSnap) && pSnap.IsConnected)
        {
            nextModule = preferred;
        }

        // 3. If none are active or preferred is not connected, pick any connected
        if (nextModule == null)
        {
            nextModule = _modules.Values.FirstOrDefault(m => _latestSnapshots.TryGetValue(m.ModuleId, out var s) && s.IsConnected);
        }

        // 4. Fallback to preferred module
        if (nextModule == null && _preferredModuleId != null)
        {
            _modules.TryGetValue(_preferredModuleId, out nextModule);
        }

        if (!ReferenceEquals(_activeModule, nextModule))
        {
            _activeModule = nextModule;
            newActiveId = _activeModule?.ModuleId;
            _latestSnapshots.TryGetValue(newActiveId ?? string.Empty, out snapshotToPublish);
            if (snapshotToPublish != null)
            {
                _latestSnapshot = snapshotToPublish;
            }
            return true;
        }

        return false;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
