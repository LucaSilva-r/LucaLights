using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LucaLights.Core.GameInput;

// Watches for the presence of a game process by name. While the process is
// absent, polls at PollInterval. Once found, subscribes to Process.Exited so
// callers can detect termination without paying per-message enumeration cost.
// Side effects (teardown, snapshot publishing, etc.) are wired via the
// Acquired / Exited events so the watcher itself stays module-agnostic.
public sealed class GameProcessWatcher : IDisposable
{
    private readonly IReadOnlyList<string> _windowsNames;
    private readonly IReadOnlyList<string> _unixNames;
    private readonly TimeSpan _pollInterval;
    private readonly Action<string>? _log;
    private readonly string _logPrefix;
    private readonly object _lock = new();

    private Process? _watched;
    private volatile bool _running;
    private CancellationTokenSource? _runCts;
    private Task? _loopTask;
    private bool _reportedWaiting;
    private bool _disposed;

    // Raised on a ThreadPool thread when the process appears / exits.
    public event Action? Acquired;
    public event Action? Exited;

    public bool IsRunning => _running;

    public GameProcessWatcher(
        IReadOnlyList<string> windowsNames,
        IReadOnlyList<string> unixNames,
        TimeSpan pollInterval,
        Action<string>? log = null,
        string logPrefix = "process:")
    {
        _windowsNames = windowsNames ?? throw new ArgumentNullException(nameof(windowsNames));
        _unixNames = unixNames ?? throw new ArgumentNullException(nameof(unixNames));
        _pollInterval = pollInterval;
        _log = log;
        _logPrefix = logPrefix;
    }

    public void Start(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            if (_loopTask is not null) return;
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = Task.Run(() => RunLoopAsync(_runCts.Token), CancellationToken.None);
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_lock)
        {
            cts = _runCts;
            task = _loopTask;
            _runCts = null;
            _loopTask = null;
        }

        cts?.Cancel();
        if (task is not null)
        {
            try { await task.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { }
        }
        cts?.Dispose();

        ReleaseWatched();
    }

    public async Task<bool> WaitForAcquiredAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_running) return true;
            try { await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { StopAsync().GetAwaiter().GetResult(); } catch { }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!_running)
            {
                TryAcquire();
            }

            try { await Task.Delay(_pollInterval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void TryAcquire()
    {
        Process? found;
        try
        {
            found = FindProcess();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"{_logPrefix} process watcher error: {ex.Message}");
            return;
        }

        if (found is null)
        {
            lock (_lock)
            {
                if (!_reportedWaiting)
                {
                    _reportedWaiting = true;
                    _log?.Invoke($"{_logPrefix} waiting for process.");
                }
            }
            return;
        }

        bool acquired = false;
        lock (_lock)
        {
            if (_running)
            {
                found.Dispose();
                return;
            }

            try
            {
                found.EnableRaisingEvents = true;
                found.Exited += OnWatchedExited;

                if (found.HasExited)
                {
                    found.Exited -= OnWatchedExited;
                    found.Dispose();
                    return;
                }

                _watched?.Dispose();
                _watched = found;
                _running = true;
                _reportedWaiting = false;
                acquired = true;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"{_logPrefix} failed to watch process: {ex.Message}");
                try { found.Dispose(); } catch { }
                return;
            }
        }

        if (acquired)
        {
            _log?.Invoke($"{_logPrefix} process detected.");
            Acquired?.Invoke();
        }
    }

    private void OnWatchedExited(object? sender, EventArgs e)
    {
        bool notify;
        lock (_lock)
        {
            if (!_running) return;
            _running = false;
            notify = true;
            ReleaseWatchedUnsafe();
        }

        if (notify)
        {
            _log?.Invoke($"{_logPrefix} process no longer running.");
            Exited?.Invoke();
        }
    }

    private void ReleaseWatched()
    {
        lock (_lock)
        {
            ReleaseWatchedUnsafe();
            _running = false;
        }
    }

    private void ReleaseWatchedUnsafe()
    {
        if (_watched is null) return;
        try { _watched.Exited -= OnWatchedExited; } catch { }
        try { _watched.Dispose(); } catch { }
        _watched = null;
    }

    private Process? FindProcess()
    {
        var names = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _windowsNames : _unixNames;
        foreach (var name in names)
        {
            Process[]? procs = null;
            try
            {
                procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    var chosen = procs[0];
                    for (var i = 1; i < procs.Length; i++) procs[i].Dispose();
                    return chosen;
                }
            }
            catch
            {
                if (procs is not null) foreach (var p in procs) p.Dispose();
            }
        }
        return null;
    }
}
