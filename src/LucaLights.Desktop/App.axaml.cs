using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Diagnostics;
using System.Net.Http;
using Velopack;
using Velopack.Sources;

namespace LucaLights.Desktop;

public partial class App : Application
{
    private const string DefaultServerUrl = "http://127.0.0.1:5050";
    private const string UpdatesRepositoryUrl = "https://github.com/LucaSilva-r/LucaLights";

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly HttpClient _httpClient = new();
    private Process? _serverProcess;
    private TrayIcon? _trayIcon;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private CancellationTokenSource? _updateCheckCancellation;
    private UpdateManager? _updateManager;
    private VelopackAsset? _pendingUpdate;
    private string _webInterfaceUrl = DefaultServerUrl;
    private int _shouldApplyPendingUpdateOnExit;
    private int _isShuttingDown;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += OnDesktopExit;

            ConfigureTrayIcon();
            _ = StartServerAsync();
            _ = StartUpdateFlowAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureTrayIcon()
    {
        var openItem = new NativeMenuItem("Open Web Interface");
        openItem.Click += (_, _) => OpenWebInterface();

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += async (_, _) => await RequestShutdownAsync();

        _trayIcon = new TrayIcon
        {
            ToolTipText = "LucaLights",
            Icon = LoadTrayIcon(),
            IsVisible = true,
            Menu = new NativeMenu
            {
                openItem,
                new NativeMenuItemSeparator(),
                quitItem
            }
        };

        _trayIcon.Clicked += (_, _) => OpenWebInterface();
        SetValue(TrayIcon.IconsProperty, new TrayIcons
        {
            _trayIcon
        });
    }

    private WindowIcon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
        return new WindowIcon(iconPath);
    }

    private async Task StartServerAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (_serverProcess is not null)
            {
                return;
            }

            _webInterfaceUrl = DefaultServerUrl;
            if (await IsServerReadyAsync(_webInterfaceUrl))
            {
                Debug.WriteLine($"Using existing LucaLights server at {_webInterfaceUrl}.");
                return;
            }

            var serverExecutable = ResolveServerExecutable();
            var startInfo = CreateServerStartInfo(serverExecutable);
            var serverProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to launch LucaLights.Server.");

            serverProcess.EnableRaisingEvents = true;
            serverProcess.Exited += OnServerProcessExited;

            _serverProcess = serverProcess;
            await WaitForServerReadyAsync(serverProcess);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to start LucaLights server: {exception}");
            await Dispatcher.UIThread.InvokeAsync(() => _desktop?.Shutdown(-1));
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task StartUpdateFlowAsync()
    {
        _updateCheckCancellation = new CancellationTokenSource();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), _updateCheckCancellation.Token);

            var updateManager = new UpdateManager(new GithubSource(UpdatesRepositoryUrl, null, false, null));
            if (!updateManager.IsInstalled)
            {
                return;
            }

            _updateManager = updateManager;
            _pendingUpdate = updateManager.UpdatePendingRestart;
            if (_pendingUpdate is not null)
            {
                Debug.WriteLine($"LucaLights update {_pendingUpdate.Version} is pending restart.");
                return;
            }

            var availableUpdate = await updateManager.CheckForUpdatesAsync();
            if (availableUpdate is null)
            {
                return;
            }

            Debug.WriteLine($"Downloading LucaLights update {availableUpdate.TargetFullRelease.Version}.");
            await updateManager.DownloadUpdatesAsync(
                availableUpdate,
                progress => Debug.WriteLine($"LucaLights update download progress: {progress}%"),
                _updateCheckCancellation.Token);

            _pendingUpdate = updateManager.UpdatePendingRestart ?? availableUpdate.TargetFullRelease;
            Debug.WriteLine($"LucaLights update {_pendingUpdate.Version} downloaded and ready to apply on exit.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to check for LucaLights updates: {exception}");
        }
    }

    private string ResolveServerExecutable()
    {
        foreach (var serverDirectory in GetCandidateServerDirectories())
        {
            var dllPath = Path.Combine(serverDirectory, "LucaLights.Server.dll");
            if (File.Exists(dllPath))
            {
                return dllPath;
            }

            var executableName = OperatingSystem.IsWindows() ? "LucaLights.Server.exe" : "LucaLights.Server";
            var executablePath = Path.Combine(serverDirectory, executableName);

            if (File.Exists(executablePath))
            {
                return executablePath;
            }
        }

        throw new FileNotFoundException("Bundled LucaLights.Server executable was not found.", AppContext.BaseDirectory);
    }

    private static string[] GetCandidateServerDirectories()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var repoRoot = FindRepoRoot(baseDirectory);

        return
        [
            Path.Combine(baseDirectory, "server"),
            Path.Combine(repoRoot, "src", "LucaLights.Server", "obj", "BundledServerPublish"),
            Path.Combine(repoRoot, "src", "LucaLights.Server", "bin", "Debug", "net10.0"),
            Path.Combine(repoRoot, "src", "LucaLights.Server", "bin", "Release", "net10.0")
        ];
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Luca Lights.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine(startDirectory, "..", "..", "..", "..", ".."));
    }

    private ProcessStartInfo CreateServerStartInfo(string serverExecutable)
    {
        var workingDirectory = Path.GetDirectoryName(serverExecutable)
            ?? throw new InvalidOperationException("Server executable path is invalid.");
        var commandLineArgs = Environment.GetCommandLineArgs().Skip(1).ToList();

        if (!commandLineArgs.Any(arg =>
                arg.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--urls", StringComparison.OrdinalIgnoreCase)))
        {
            commandLineArgs.Add($"--urls={DefaultServerUrl}");
        }

        if (serverExecutable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var dotnetStartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            dotnetStartInfo.ArgumentList.Add(serverExecutable);
            foreach (var argument in commandLineArgs)
            {
                dotnetStartInfo.ArgumentList.Add(argument);
            }

            return dotnetStartInfo;
        }

        var directStartInfo = new ProcessStartInfo(serverExecutable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in commandLineArgs)
        {
            directStartInfo.ArgumentList.Add(argument);
        }

        return directStartInfo;
    }

    private async Task WaitForServerReadyAsync(Process serverProcess)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            if (await IsServerReadyAsync(_webInterfaceUrl))
            {
                return;
            }

            if (serverProcess.HasExited)
            {
                throw new InvalidOperationException($"LucaLights.Server exited with code {serverProcess.ExitCode}.");
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Timed out waiting for LucaLights.Server to accept requests.");
    }

    private async Task<bool> IsServerReadyAsync(string baseUrl)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/api");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private void OpenWebInterface()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _webInterfaceUrl,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to open LucaLights web interface: {exception}");
        }
    }

    private async Task RequestShutdownAsync()
    {
        if (Interlocked.Exchange(ref _isShuttingDown, 1) != 0)
        {
            return;
        }

        if (_desktop is null)
        {
            return;
        }

        _updateCheckCancellation?.Cancel();
        if (_pendingUpdate is not null || _updateManager?.UpdatePendingRestart is not null)
        {
            Interlocked.Exchange(ref _shouldApplyPendingUpdateOnExit, 1);
        }

        await Dispatcher.UIThread.InvokeAsync(() => _desktop.Shutdown());
    }

    private async Task StopServerProcessAsync()
    {
        var serverProcess = _serverProcess;
        _serverProcess = null;

        if (serverProcess is null)
        {
            return;
        }

        try
        {
            if (!serverProcess.HasExited)
            {
                try
                {
                    await _httpClient.PostAsync($"{_webInterfaceUrl.TrimEnd('/')}/api/system/shutdown", content: null);
                }
                catch (HttpRequestException)
                {
                }

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await serverProcess.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!serverProcess.HasExited)
                    {
                        serverProcess.Kill(entireProcessTree: true);
                        await serverProcess.WaitForExitAsync();
                    }
                }
            }
        }
        finally
        {
            serverProcess.Exited -= OnServerProcessExited;
            serverProcess.Dispose();
        }
    }

    private void OnServerProcessExited(object? sender, EventArgs e)
    {
        if (_desktop is null || Interlocked.Exchange(ref _isShuttingDown, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _desktop.Shutdown());
    }

    private async void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            _updateCheckCancellation?.Cancel();
            _trayIcon?.Dispose();
            _trayIcon = null;

            await StopServerProcessAsync();

            if (Interlocked.CompareExchange(ref _shouldApplyPendingUpdateOnExit, 0, 0) == 1)
            {
                var pendingUpdate = _pendingUpdate ?? _updateManager?.UpdatePendingRestart;
                if (pendingUpdate is not null && _updateManager is not null)
                {
                    Debug.WriteLine($"Applying LucaLights update {pendingUpdate.Version}.");
                    _updateManager.ApplyUpdatesAndRestart(pendingUpdate);
                    return;
                }
            }
        }
        finally
        {
            _updateCheckCancellation?.Dispose();
            _httpClient.Dispose();
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
        }
    }
}
