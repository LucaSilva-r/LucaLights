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
    private InputSimulationState? _simulation;
    private InputSnapshot _latestSnapshot = InputSnapshot.Empty;
    private long _simulationSequence;
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
                if (_simulation is { Enabled: true } simulation)
                {
                    return simulation.ModuleId;
                }

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

    // Call once per render frame after sampling LatestSnapshot. Lets pulse/edge modules
    // clear their latched state so the next frame reflects fresh inputs.
    public void AcknowledgePulses(InputSnapshot consumed)
    {
        IGameInputModule? active;
        InputSnapshot? simulationSnapshotToPublish = null;
        lock (_syncRoot)
        {
            if (_simulation is { Enabled: true } simulation)
            {
                var cleared = false;
                foreach (var pulseKey in simulation.PulseKeys.ToArray())
                {
                    if (consumed.GetBool(pulseKey))
                    {
                        simulation.PulseKeys.Remove(pulseKey);
                        cleared = true;
                    }
                }

                if (cleared)
                {
                    _latestSnapshot = BuildSimulationSnapshotUnsafe(simulation);
                    simulationSnapshotToPublish = _latestSnapshot;
                }

                active = null;
            }
            else
            {
            active = _activeModule;
            }
        }

        if (simulationSnapshotToPublish is not null)
        {
            SnapshotUpdated?.Invoke(simulationSnapshotToPublish);
            return;
        }

        active?.AcknowledgePulses(consumed);
    }

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

    public InputSimulationState GetSimulationState()
    {
        lock (_syncRoot)
        {
            return _simulation?.Clone() ?? new InputSimulationState();
        }
    }

    public bool SetSimulationEnabled(string moduleId, bool enabled, out InputSnapshot? snapshotToPublish, out string? activeModuleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_syncRoot)
        {
            if (!_modules.ContainsKey(moduleId))
            {
                snapshotToPublish = null;
                activeModuleId = null;
                return false;
            }

            _simulation ??= new InputSimulationState();
            _simulation.ModuleId = moduleId;
            _simulation.Enabled = enabled;
            _simulation.PulseKeys.Clear();

            if (enabled)
            {
                _latestSnapshot = BuildSimulationSnapshotUnsafe(_simulation);
                snapshotToPublish = _latestSnapshot;
                activeModuleId = _simulation.ModuleId;
            }
            else
            {
                UpdateActiveModuleUnsafe(out activeModuleId, out snapshotToPublish);
                activeModuleId ??= _activeModule?.ModuleId;
                if (snapshotToPublish is null)
                {
                    snapshotToPublish = activeModuleId is not null
                        && _latestSnapshots.TryGetValue(activeModuleId, out var activeSnapshot)
                            ? activeSnapshot
                            : InputSnapshot.Empty;
                    _latestSnapshot = snapshotToPublish;
                }
            }

            return true;
        }
    }

    public bool SetSimulationBool(string key, bool value, out InputSnapshot? snapshotToPublish)
    {
        lock (_syncRoot)
        {
            if (_simulation is not { Enabled: true } simulation)
            {
                snapshotToPublish = null;
                return false;
            }

            simulation.BoolValues[key] = value;
            simulation.PulseKeys.Remove(key);
            _latestSnapshot = BuildSimulationSnapshotUnsafe(simulation);
            snapshotToPublish = _latestSnapshot;
            return true;
        }
    }

    public bool TriggerSimulationPulse(string key, out InputSnapshot? snapshotToPublish)
    {
        lock (_syncRoot)
        {
            if (_simulation is not { Enabled: true } simulation)
            {
                snapshotToPublish = null;
                return false;
            }

            simulation.PulseKeys.Add(key);
            _latestSnapshot = BuildSimulationSnapshotUnsafe(simulation);
            snapshotToPublish = _latestSnapshot;
            return true;
        }
    }

    public bool SetSimulationFloat(string key, float value, out InputSnapshot? snapshotToPublish)
    {
        lock (_syncRoot)
        {
            if (_simulation is not { Enabled: true } simulation)
            {
                snapshotToPublish = null;
                return false;
            }

            simulation.FloatValues[key] = value;
            _latestSnapshot = BuildSimulationSnapshotUnsafe(simulation);
            snapshotToPublish = _latestSnapshot;
            return true;
        }
    }

    public bool SetSimulationColor(string key, Color value, out InputSnapshot? snapshotToPublish)
    {
        lock (_syncRoot)
        {
            if (_simulation is not { Enabled: true } simulation)
            {
                snapshotToPublish = null;
                return false;
            }

            simulation.ColorValues[key] = value;
            _latestSnapshot = BuildSimulationSnapshotUnsafe(simulation);
            snapshotToPublish = _latestSnapshot;
            return true;
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

            if (_simulation is { Enabled: true })
            {
                return;
            }

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

        if (_simulation is { Enabled: true } simulation)
        {
            newActiveId = simulation.ModuleId;
            snapshotToPublish = BuildSimulationSnapshotUnsafe(simulation);
            _latestSnapshot = snapshotToPublish;
            return _activeModule?.ModuleId != simulation.ModuleId;
        }

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

    private InputSnapshot BuildSimulationSnapshotUnsafe(InputSimulationState simulation)
    {
        var boolValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var floatValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var colorValues = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        if (_modules.TryGetValue(simulation.ModuleId, out var module))
        {
            foreach (var channel in module.GetDefinition().Channels)
            {
                switch (channel.ValueType)
                {
                    case InputValueType.Bool:
                        boolValues[channel.Key] = simulation.BoolValues.TryGetValue(channel.Key, out var boolValue)
                            ? boolValue
                            : false;
                        break;
                    case InputValueType.Float:
                        floatValues[channel.Key] = simulation.FloatValues.TryGetValue(channel.Key, out var floatValue)
                            ? floatValue
                            : channel.DefaultFloatValue ?? 0f;
                        break;
                    case InputValueType.Color:
                        colorValues[channel.Key] = simulation.ColorValues.TryGetValue(channel.Key, out var colorValue)
                            ? colorValue
                            : Color.Black;
                        break;
                }
            }
        }

        foreach (var pair in simulation.BoolValues)
        {
            boolValues[pair.Key] = pair.Value;
        }

        foreach (var pulseKey in simulation.PulseKeys)
        {
            boolValues[pulseKey] = true;
        }

        foreach (var pair in simulation.FloatValues)
        {
            floatValues[pair.Key] = pair.Value;
        }

        foreach (var pair in simulation.ColorValues)
        {
            colorValues[pair.Key] = pair.Value;
        }

        return new InputSnapshot
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Sequence = Interlocked.Increment(ref _simulationSequence),
            IsConnected = true,
            IsActive = true,
            BoolValues = boolValues,
            FloatValues = floatValues,
            ColorValues = colorValues,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = "Simulation",
                ["module"] = simulation.ModuleId
            }
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

public sealed class InputSimulationState
{
    public bool Enabled { get; set; }

    public string ModuleId { get; set; } = string.Empty;

    public Dictionary<string, bool> BoolValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, float> FloatValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Color> ColorValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> PulseKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public InputSimulationState Clone()
    {
        return new InputSimulationState
        {
            Enabled = Enabled,
            ModuleId = ModuleId,
            BoolValues = new Dictionary<string, bool>(BoolValues, StringComparer.OrdinalIgnoreCase),
            FloatValues = new Dictionary<string, float>(FloatValues, StringComparer.OrdinalIgnoreCase),
            ColorValues = new Dictionary<string, Color>(ColorValues, StringComparer.OrdinalIgnoreCase),
            PulseKeys = new HashSet<string>(PulseKeys, StringComparer.OrdinalIgnoreCase)
        };
    }
}
