using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule
{
    private readonly object _processLock = new();
    private readonly SemaphoreSlim _tosuStartupLock = new(1, 1);
    private Process? _tosuProcess;
    private bool _reportedWaitingForOsuProcess;

    private static readonly string[] WindowsOsuProcessNames = ["osu!", "osu", "osu-lazer"];
    private static readonly string[] UnixOsuProcessNames = ["osu!", "osu", "osu-lazer"];

    private static string TosuDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tosu");

    private static string TosuExePath => Path.Combine(TosuDirectory,
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tosu.exe" : "tosu");

    private static string TosuVersionFile => Path.Combine(TosuDirectory, "lucalights-version.txt");

    private async Task<bool> WaitForOsuProcessAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (RefreshOsuProcessPresence())
            {
                return true;
            }

            try
            {
                await Task.Delay(ProcessPollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    private bool RefreshOsuProcessPresence()
    {
        bool running;
        try
        {
            running = IsOsuProcessRunning();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"osu: process watcher error: {ex.Message}");
            running = _osuProcessRunning;
        }

        var becameUnavailable = false;

        lock (_processLock)
        {
            if (running != _osuProcessRunning)
            {
                _osuProcessRunning = running;
                _reportedWaitingForOsuProcess = !running;

                if (running)
                {
                    _log?.Invoke("osu: osu! process detected.");
                }
                else
                {
                    _log?.Invoke("osu: osu! process no longer running; pausing tosu connection.");
                    becameUnavailable = true;
                }
            }
            else if (!running && !_reportedWaitingForOsuProcess)
            {
                _reportedWaitingForOsuProcess = true;
                _log?.Invoke("osu: waiting for osu! process before connecting to tosu.");
            }
        }

        if (becameUnavailable)
        {
            ResetOsuState();
            if (_autoManageProcess)
            {
                StopTosuProcess();
            }
        }

        return running;
    }

    private static bool IsOsuProcessRunning()
    {
        var names = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsOsuProcessNames
            : UnixOsuProcessNames;

        foreach (var name in names)
        {
            Process[]? procs = null;
            try
            {
                procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    return true;
                }
            }
            catch
            {
            }
            finally
            {
                if (procs is not null)
                {
                    foreach (var p in procs)
                    {
                        p.Dispose();
                    }
                }
            }
        }

        return false;
    }

    private async Task<bool> EnsureTosuReadyAsync(CancellationToken ct)
    {
        if (!_autoManageProcess)
        {
            return true;
        }

        await _tosuStartupLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!RefreshOsuProcessPresence())
            {
                return false;
            }

            await EnsureTosuRunningAsync(ct).ConfigureAwait(false);
            return RefreshOsuProcessPresence();
        }
        finally
        {
            _tosuStartupLock.Release();
        }
    }

    private async Task EnsureTosuRunningAsync(CancellationToken ct)
    {
        if (!RefreshOsuProcessPresence())
        {
            return;
        }

        // If tosu is already serving, nothing to do.
        if (await IsTosuListeningAsync(ct))
        {
            _log?.Invoke("osu: tosu already running.");
            return;
        }

        var exePath = await EnsureTosuExeAsync(ct);
        if (exePath is null)
        {
            _log?.Invoke("osu: could not obtain tosu executable. Start tosu manually.");
            return;
        }

        await EnsureTosuConfigAsync(ct);

        if (ct.IsCancellationRequested) return;

        try
        {
            lock (_processLock)
            {
                if (ct.IsCancellationRequested) return;

                _tosuProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName         = exePath,
                        WorkingDirectory = Path.GetDirectoryName(exePath),
                        UseShellExecute  = false,
                        CreateNoWindow   = true,
                    }
                };
                _tosuProcess.Start();
                _log?.Invoke($"osu: launched tosu from {exePath}.");
            }

            // Wait up to 15 seconds for tosu to start serving.
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                if (!RefreshOsuProcessPresence())
                {
                    _log?.Invoke("osu: osu! exited before tosu was ready.");
                    StopTosuProcess();
                    return;
                }

                if (await IsTosuListeningAsync(ct))
                {
                    _log?.Invoke("osu: tosu is ready.");
                    return;
                }
                await Task.Delay(500, ct);
            }

            if (ct.IsCancellationRequested)
            {
                _log?.Invoke("osu: tosu startup cancelled.");
                StopTosuProcess();
                return;
            }

            _log?.Invoke("osu: tosu did not become ready within 15s.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Invoke($"osu: failed to launch tosu: {ex.Message}");
        }
    }

    private async Task<bool> IsTosuListeningAsync(CancellationToken ct)
    {
        try
        {
            var httpBase = _tosuUrl
                .Replace("wss://", "https://", StringComparison.OrdinalIgnoreCase)
                .Replace("ws://",  "http://",  StringComparison.OrdinalIgnoreCase);
            using var client   = CreateHttpClient();
            using var response = await client.GetAsync(httpBase + "/json/v2", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task<string?> EnsureTosuExeAsync(CancellationToken ct)
    {
        if (File.Exists(TosuExePath))
        {
            await TryUpdateTosuAsync(ct);
            return TosuExePath;
        }

        _log?.Invoke("osu: tosu not found, downloading from GitHub...");
        return await DownloadLatestTosuAsync(ct);
    }

    private async Task TryUpdateTosuAsync(CancellationToken ct)
    {
        try
        {
            using var http    = CreateHttpClient();
            var release       = await FetchLatestReleaseAsync(http, ct);
            if (release is null) return;

            var localVersion  = File.Exists(TosuVersionFile)
                ? (await File.ReadAllTextAsync(TosuVersionFile, ct)).Trim()
                : null;

            if (localVersion is not null &&
                string.Compare(release.TagName, localVersion, StringComparison.OrdinalIgnoreCase) <= 0)
                return;

            _log?.Invoke($"osu: updating tosu {localVersion ?? "unknown"} → {release.TagName}.");
            await DownloadReleaseAsync(http, release, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Invoke($"osu: update check failed: {ex.Message}"); }
    }

    private async Task<string?> DownloadLatestTosuAsync(CancellationToken ct)
    {
        try
        {
            using var http = CreateHttpClient();
            var release    = await FetchLatestReleaseAsync(http, ct);
            if (release is null) { _log?.Invoke("osu: no GitHub release found."); return null; }
            return await DownloadReleaseAsync(http, release, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Invoke($"osu: download failed: {ex.Message}"); return null; }
    }

    private async Task<string?> DownloadReleaseAsync(
        HttpClient http, TosuGitHubRelease release, CancellationToken ct)
    {
        // Prefer platform-specific zip, fall back to any zip.
        string? platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                           RuntimeInformation.IsOSPlatform(OSPlatform.Linux)   ? "linux" : null;

        var asset = release.Assets
            .FirstOrDefault(a =>
                platform != null &&
                a.Name.Contains(platform, StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? release.Assets
                .FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (asset is null) { _log?.Invoke("osu: no suitable zip asset in release."); return null; }

        Directory.CreateDirectory(TosuDirectory);
        var zipPath = Path.Combine(TosuDirectory, asset.Name);

        _log?.Invoke($"osu: downloading {asset.Name} ({asset.Size / 1024 / 1024}MB)…");
        var bytes = await http.GetByteArrayAsync(asset.BrowserDownloadUrl, ct);
        await File.WriteAllBytesAsync(zipPath, bytes, ct);

        _log?.Invoke("osu: extracting…");
        ZipFile.ExtractToDirectory(zipPath, TosuDirectory, overwriteFiles: true);
        File.Delete(zipPath);

        // On Linux, we must ensure the binary is executable.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(TosuExePath))
        {
            try
            {
                Process.Start("chmod", $"+x \"{TosuExePath}\"")?.WaitForExit();
            }
            catch (Exception ex)
            {
                _log?.Invoke($"osu: failed to set executable permission: {ex.Message}");
            }
        }

        await File.WriteAllTextAsync(TosuVersionFile, release.TagName, ct);
        _log?.Invoke($"osu: tosu {release.TagName} installed at {TosuDirectory}.");

        return File.Exists(TosuExePath) ? TosuExePath : null;
    }

    private async Task EnsureTosuConfigAsync(CancellationToken ct)
    {
        var configPath = Path.Combine(TosuDirectory, "tosu.env");
        Directory.CreateDirectory(TosuDirectory);

        const string targetKey   = "OPEN_DASHBOARD_ON_STARTUP";
        const string targetValue = "false";
        const string targetLine  = $"{targetKey}={targetValue}";

        try
        {
            if (File.Exists(configPath))
            {
                var  lines = await File.ReadAllLinesAsync(configPath, ct);
                bool found = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith(targetKey, StringComparison.OrdinalIgnoreCase))
                    {
                        // Update if different
                        if (!lines[i].Contains(targetValue, StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = targetLine;
                            await File.WriteAllLinesAsync(configPath, lines, ct);
                        }
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var newLines = new string[lines.Length + 1];
                    lines.CopyTo(newLines, 0);
                    newLines[lines.Length] = targetLine;
                    await File.WriteAllLinesAsync(configPath, newLines, ct);
                }
            }
            else
            {
                // Create minimal config if it doesn't exist.
                await File.WriteAllTextAsync(configPath, targetLine + Environment.NewLine, ct);
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"osu: failed to update tosu.env: {ex.Message}");
        }
    }

    private static async Task<TosuGitHubRelease?> FetchLatestReleaseAsync(
        HttpClient http, CancellationToken ct)
    {
        var json = await http.GetStringAsync(
            "https://api.github.com/repos/tosuapp/tosu/releases/latest", ct);
        return JsonSerializer.Deserialize<TosuGitHubRelease>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private void StopTosuProcess()
    {
        lock (_processLock)
        {
            try
            {
                if (_tosuProcess is not null)
                {
                    if (!_tosuProcess.HasExited)
                    {
                        _log?.Invoke("osu: killing tosu process tree...");
                        _tosuProcess.Kill(entireProcessTree: true);
                        _tosuProcess.WaitForExit(3000);
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"osu: failed to kill tosu: {ex.Message}");
            }
            finally
            {
                _tosuProcess?.Dispose();
                _tosuProcess = null;
            }
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LucaLights/2.0");
        return client;
    }

    // Minimal GitHub releases API models
    private sealed class TosuGitHubRelease
    {
        [JsonPropertyName("tag_name")] public string              TagName { get; set; } = string.Empty;
        [JsonPropertyName("assets")]   public List<TosuGitHubAsset> Assets { get; set; } = [];
    }

    private sealed class TosuGitHubAsset
    {
        [JsonPropertyName("name")]                 public string Name                { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = string.Empty;
        [JsonPropertyName("size")]                 public long   Size               { get; set; }
    }
}
