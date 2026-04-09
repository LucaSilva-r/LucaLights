using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using LucaLights.Core.Models;

namespace LucaLights.Core.GameInput.Modules;

public sealed class ITGManiaInputModule : IGameInputModule, IDisposable
{
    public const string ModuleIdValue = "itgmania";
    private const int FullSextetCount = 33;

    private static readonly Lazy<InputDefinition> Definition = new(BuildDefinition);

    private static readonly (ItgCabinetLight Flag, string Key, string Label)[] CabinetChannels =
    [
        (ItgCabinetLight.MarqueeUpLeft, "raw.itgmania.cabinet.marquee_up_left", "Marquee Up Left"),
        (ItgCabinetLight.MarqueeUpRight, "raw.itgmania.cabinet.marquee_up_right", "Marquee Up Right"),
        (ItgCabinetLight.MarqueeLowerLeft, "raw.itgmania.cabinet.marquee_lower_left", "Marquee Lower Left"),
        (ItgCabinetLight.MarqueeLowerRight, "raw.itgmania.cabinet.marquee_lower_right", "Marquee Lower Right"),
        (ItgCabinetLight.BassLeft, "raw.itgmania.cabinet.bass_left", "Bass Left"),
        (ItgCabinetLight.BassRight, "raw.itgmania.cabinet.bass_right", "Bass Right")
    ];

    private static readonly (ItgGameButton Flag, string Key, string Label)[] ButtonChannels =
    [
        (ItgGameButton.MenuLeft, "raw.itgmania.button.menu_left", "Menu Left"),
        (ItgGameButton.MenuRight, "raw.itgmania.button.menu_right", "Menu Right"),
        (ItgGameButton.MenuUp, "raw.itgmania.button.menu_up", "Menu Up"),
        (ItgGameButton.MenuDown, "raw.itgmania.button.menu_down", "Menu Down"),
        (ItgGameButton.Start, "raw.itgmania.button.start", "Start"),
        (ItgGameButton.Select, "raw.itgmania.button.select", "Select"),
        (ItgGameButton.Back, "raw.itgmania.button.back", "Back"),
        (ItgGameButton.Coin, "raw.itgmania.button.coin", "Coin"),
        (ItgGameButton.Operator, "raw.itgmania.button.operator", "Operator"),
        (ItgGameButton.EffectUp, "raw.itgmania.button.effect_up", "Effect Up"),
        (ItgGameButton.EffectDown, "raw.itgmania.button.effect_down", "Effect Down"),
        (ItgGameButton.P1Left, "raw.itgmania.button.p1_left", "P1 Left"),
        (ItgGameButton.P1Right, "raw.itgmania.button.p1_right", "P1 Right"),
        (ItgGameButton.P1Up, "raw.itgmania.button.p1_up", "P1 Up"),
        (ItgGameButton.P1Down, "raw.itgmania.button.p1_down", "P1 Down"),
        (ItgGameButton.P1UpLeftSolo, "raw.itgmania.button.p1_up_left_solo", "P1 Up Left Solo"),
        (ItgGameButton.P1UpRightSolo, "raw.itgmania.button.p1_up_right_solo", "P1 Up Right Solo"),
        (ItgGameButton.P1Custom07, "raw.itgmania.button.p1_custom_07", "P1 Custom 07"),
        (ItgGameButton.P1Custom08, "raw.itgmania.button.p1_custom_08", "P1 Custom 08"),
        (ItgGameButton.P1Custom09, "raw.itgmania.button.p1_custom_09", "P1 Custom 09"),
        (ItgGameButton.P2Left, "raw.itgmania.button.p2_left", "P2 Left"),
        (ItgGameButton.P2Right, "raw.itgmania.button.p2_right", "P2 Right"),
        (ItgGameButton.P2Up, "raw.itgmania.button.p2_up", "P2 Up"),
        (ItgGameButton.P2Down, "raw.itgmania.button.p2_down", "P2 Down"),
        (ItgGameButton.P2UpLeftSolo, "raw.itgmania.button.p2_up_left_solo", "P2 Up Left Solo"),
        (ItgGameButton.P2UpRightSolo, "raw.itgmania.button.p2_up_right_solo", "P2 Up Right Solo"),
        (ItgGameButton.P2Custom07, "raw.itgmania.button.p2_custom_07", "P2 Custom 07"),
        (ItgGameButton.P2Custom08, "raw.itgmania.button.p2_custom_08", "P2 Custom 08"),
        (ItgGameButton.P2Custom09, "raw.itgmania.button.p2_custom_09", "P2 Custom 09"),
        (ItgGameButton.P2Custom10, "raw.itgmania.button.p2_custom_10", "P2 Custom 10")
    ];

    private static readonly (ItgLightsMode Mode, string Key, string Label)[] LightsModeChannels =
    [
        (ItgLightsMode.Attract, "raw.itgmania.lights_mode.attract", "Attract"),
        (ItgLightsMode.Joining, "raw.itgmania.lights_mode.joining", "Joining"),
        (ItgLightsMode.MenuStartOnly, "raw.itgmania.lights_mode.menu_start_only", "Menu Start Only"),
        (ItgLightsMode.MenuStartAndDirections, "raw.itgmania.lights_mode.menu_start_and_directions", "Menu Start And Directions"),
        (ItgLightsMode.Demonstration, "raw.itgmania.lights_mode.demonstration", "Demonstration"),
        (ItgLightsMode.Gameplay, "raw.itgmania.lights_mode.gameplay", "Gameplay"),
        (ItgLightsMode.Stage, "raw.itgmania.lights_mode.stage", "Stage"),
        (ItgLightsMode.AllCleared, "raw.itgmania.lights_mode.all_cleared", "All Cleared"),
        (ItgLightsMode.TestAutoCycle, "raw.itgmania.lights_mode.test_auto_cycle", "Test Auto Cycle"),
        (ItgLightsMode.TestManualCycle, "raw.itgmania.lights_mode.test_manual_cycle", "Test Manual Cycle")
    ];

    private readonly string _pipeName;
    private readonly Action<string>? _log;
    private readonly object _syncRoot = new();
    private readonly byte[] _buffer = new byte[FullSextetCount];

    private CancellationTokenSource? _run;
    private Task? _backgroundTask;
    private Stream? _activeStream;
    private InputSnapshot _latestSnapshot = InputSnapshot.Empty;
    private long _sequence;
    private bool _disposed;

    public ITGManiaInputModule(string pipeName, Action<string>? log = null)
    {
        _pipeName = ExpandPipeName(pipeName);
        _log = log;
    }

    public string ModuleId => ModuleIdValue;

    public string DisplayName => "ITGMania";

    public event Action<InputSnapshot>? SnapshotUpdated;

    public static ITGManiaInputModule CreateFromSettings(Settings settings, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var moduleSettings = settings.GetOrCreateInputModuleSettings(ModuleIdValue);
        var configuredPipeName = moduleSettings["pipeName"]?.GetValue<string>();
        var pipeName = string.IsNullOrWhiteSpace(configuredPipeName)
            ? GetDefaultPipeName()
            : configuredPipeName;

        return new ITGManiaInputModule(pipeName, log);
    }

    public InputDefinition GetDefinition()
    {
        return Definition.Value;
    }

    public InputSnapshot GetLatestSnapshot()
    {
        lock (_syncRoot)
        {
            return _latestSnapshot;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            if (_backgroundTask is not null)
            {
                return Task.CompletedTask;
            }

            _run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(() => RunLoop(_run.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        Task? backgroundTask;
        CancellationTokenSource? run;

        lock (_syncRoot)
        {
            backgroundTask = _backgroundTask;
            run = _run;
            _backgroundTask = null;
            _run = null;
        }

        if (backgroundTask is null)
        {
            return;
        }

        run?.Cancel();
        CloseActiveStream();

        try
        {
            await backgroundTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _log?.Invoke("ITGMania input module shutdown wait was cancelled.");
        }
        catch (TimeoutException)
        {
            _log?.Invoke("ITGMania input module did not stop within timeout; continuing shutdown.");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"ITGMania input module stopped with error: {ex.Message}");
        }
        finally
        {
            run?.Dispose();
            PublishDisconnectedSnapshot();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _disposed = true;
    }

    private void RunLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunWindows(cancellationToken);
            }
            else
            {
                RunLinux(cancellationToken);
            }
        }
    }

    private void RunWindows(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                1_000_000,
                100_000);

            SetActiveStream(pipe);
            IAsyncResult? waitHandle = null;

            try
            {
                waitHandle = pipe.BeginWaitForConnection(null, this);
                while (!pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                pipe.EndWaitForConnection(waitHandle);
                _log?.Invoke("ITGMania pipe connected.");
                ReadStream(pipe, cancellationToken);
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _log?.Invoke($"ITGMania pipe error: {ex.Message}");
                Thread.Sleep(250);
            }
            finally
            {
                SetActiveStream(null);
                PublishDisconnectedSnapshot();
            }
        }
    }

    private void RunLinux(CancellationToken cancellationToken)
    {
        EnsureFifoExists();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var stream = new FileStream(_pipeName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                SetActiveStream(stream);
                _log?.Invoke($"ITGMania FIFO open: {_pipeName}");
                ReadStream(stream, cancellationToken);
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _log?.Invoke($"ITGMania FIFO error: {ex.Message}");
                Thread.Sleep(250);
            }
            finally
            {
                SetActiveStream(null);
                PublishDisconnectedSnapshot();
            }
        }
    }

    private void ReadStream(Stream stream, CancellationToken cancellationToken)
    {
        var counter = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            int currentData;
            try
            {
                currentData = stream.ReadByte();
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                break;
            }

            if (currentData == -1)
            {
                break;
            }

            if (currentData == (byte)'\n')
            {
                counter = _buffer.Length;
            }
            else if (counter < _buffer.Length)
            {
                _buffer[counter] = (byte)currentData;
                counter++;
            }

            if (counter == _buffer.Length)
            {
                PublishSnapshot(ParsePacket(_buffer));
                counter = 0;
            }
        }
    }

    private InputSnapshot ParsePacket(byte[] data)
    {
        var cabinetLights = (ItgCabinetLight)(data[0] & 0b0111111);
        var lightsMode = (ItgLightsMode)data[0];
        ItgGameButton gameButtons = 0;

        gameButtons |= (data[1] & (1 << 0)) != 0 ? ItgGameButton.MenuLeft : 0;
        gameButtons |= (data[1] & (1 << 1)) != 0 ? ItgGameButton.MenuRight : 0;
        gameButtons |= (data[1] & (1 << 2)) != 0 ? ItgGameButton.MenuUp : 0;
        gameButtons |= (data[1] & (1 << 3)) != 0 ? ItgGameButton.MenuDown : 0;
        gameButtons |= (data[1] & (1 << 4)) != 0 ? ItgGameButton.Start : 0;
        gameButtons |= (data[1] & (1 << 5)) != 0 ? ItgGameButton.Select : 0;
        gameButtons |= (data[2] & (1 << 0)) != 0 ? ItgGameButton.Back : 0;
        gameButtons |= (data[2] & (1 << 1)) != 0 ? ItgGameButton.Coin : 0;
        gameButtons |= (data[2] & (1 << 2)) != 0 ? ItgGameButton.Operator : 0;
        gameButtons |= (data[2] & (1 << 3)) != 0 ? ItgGameButton.EffectUp : 0;
        gameButtons |= (data[2] & (1 << 4)) != 0 ? ItgGameButton.EffectDown : 0;
        gameButtons |= (data[3] & (1 << 0)) != 0 ? ItgGameButton.P1Left : 0;
        gameButtons |= (data[3] & (1 << 1)) != 0 ? ItgGameButton.P1Right : 0;
        gameButtons |= (data[3] & (1 << 2)) != 0 ? ItgGameButton.P1Up : 0;
        gameButtons |= (data[3] & (1 << 3)) != 0 ? ItgGameButton.P1Down : 0;
        gameButtons |= (data[3] & (1 << 4)) != 0 ? ItgGameButton.P1UpLeftSolo : 0;
        gameButtons |= (data[3] & (1 << 5)) != 0 ? ItgGameButton.P1UpRightSolo : 0;
        gameButtons |= (data[4] & (1 << 0)) != 0 ? ItgGameButton.P1Custom07 : 0;
        gameButtons |= (data[4] & (1 << 1)) != 0 ? ItgGameButton.P1Custom08 : 0;
        gameButtons |= (data[4] & (1 << 2)) != 0 ? ItgGameButton.P1Custom09 : 0;
        gameButtons |= (data[4] & (1 << 3)) != 0 ? ItgGameButton.P2Left : 0;
        gameButtons |= (data[4] & (1 << 4)) != 0 ? ItgGameButton.P2Right : 0;
        gameButtons |= (data[4] & (1 << 5)) != 0 ? ItgGameButton.P2Up : 0;
        gameButtons |= (data[5] & (1 << 0)) != 0 ? ItgGameButton.P2Down : 0;
        gameButtons |= (data[5] & (1 << 1)) != 0 ? ItgGameButton.P2UpLeftSolo : 0;
        gameButtons |= (data[5] & (1 << 2)) != 0 ? ItgGameButton.P2UpRightSolo : 0;
        gameButtons |= (data[5] & (1 << 3)) != 0 ? ItgGameButton.P2Custom07 : 0;
        gameButtons |= (data[5] & (1 << 4)) != 0 ? ItgGameButton.P2Custom08 : 0;
        gameButtons |= (data[5] & (1 << 5)) != 0 ? ItgGameButton.P2Custom09 : 0;
        gameButtons |= (data[6] & (1 << 0)) != 0 ? ItgGameButton.P2Custom10 : 0;

        var boolValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw.itgmania.connected"] = true
        };

        foreach (var channel in CabinetChannels)
        {
            boolValues[channel.Key] = cabinetLights.HasFlag(channel.Flag);
        }

        foreach (var channel in ButtonChannels)
        {
            boolValues[channel.Key] = gameButtons.HasFlag(channel.Flag);
        }

        foreach (var channel in LightsModeChannels)
        {
            boolValues[channel.Key] = lightsMode == channel.Mode;
        }

        var floatValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw.itgmania.lights_mode_index"] = (float)lightsMode
        };

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw.itgmania.lights_mode_name"] = lightsMode.ToString(),
            ["raw.itgmania.pipe_name"] = _pipeName
        };

        return new InputSnapshot
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Sequence = Interlocked.Increment(ref _sequence),
            IsConnected = true,
            IsActive = true,
            BoolValues = boolValues,
            FloatValues = floatValues,
            Metadata = metadata
        };
    }

    private void PublishSnapshot(InputSnapshot snapshot)
    {
        lock (_syncRoot)
        {
            _latestSnapshot = snapshot;
        }

        SnapshotUpdated?.Invoke(snapshot);
    }

    private void PublishDisconnectedSnapshot()
    {
        PublishSnapshot(new InputSnapshot
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Sequence = Interlocked.Increment(ref _sequence),
            IsConnected = false,
            IsActive = false,
            BoolValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["raw.itgmania.connected"] = false
            },
            FloatValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["raw.itgmania.lights_mode_index"] = -1f
            },
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["raw.itgmania.pipe_name"] = _pipeName
            }
        });
    }

    private void EnsureFifoExists()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || File.Exists(_pipeName))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_pipeName);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var result = mkfifo(_pipeName, 0b110_110_100);
        if (result != 0 && Marshal.GetLastWin32Error() != 17)
        {
            throw new IOException($"Failed to create FIFO at {_pipeName}");
        }
    }

    private void SetActiveStream(Stream? stream)
    {
        lock (_syncRoot)
        {
            _activeStream = stream;
        }
    }

    private void CloseActiveStream()
    {
        lock (_syncRoot)
        {
            try
            {
                _activeStream?.Dispose();
            }
            catch
            {
            }

            _activeStream = null;
        }
    }

    private static InputDefinition BuildDefinition()
    {
        var definition = new InputDefinition
        {
            ModuleId = ModuleIdValue,
            DisplayName = "ITGMania"
        };

        definition.Channels.Add(new InputChannelDefinition
        {
            Key = "raw.itgmania.connected",
            Label = "Connected",
            ValueType = InputValueType.Bool,
            Category = "Raw / System",
            Description = "True while the ITGMania input stream is connected."
        });

        definition.Channels.Add(new InputChannelDefinition
        {
            Key = "raw.itgmania.lights_mode_index",
            Label = "Lights Mode Index",
            ValueType = InputValueType.Float,
            Category = "Raw / Lights Mode",
            Description = "Current ITGMania lights mode as a numeric enum value."
        });

        foreach (var channel in LightsModeChannels)
        {
            definition.Channels.Add(new InputChannelDefinition
            {
                Key = channel.Key,
                Label = channel.Label,
                ValueType = InputValueType.Bool,
                Category = "Raw / Lights Mode",
                Description = $"True when ITGMania is in lights mode {channel.Label}."
            });
        }

        foreach (var channel in CabinetChannels)
        {
            definition.Channels.Add(new InputChannelDefinition
            {
                Key = channel.Key,
                Label = channel.Label,
                ValueType = InputValueType.Bool,
                Category = "Raw / Cabinet",
                Description = $"Raw ITGMania cabinet light state for {channel.Label}."
            });
        }

        foreach (var channel in ButtonChannels)
        {
            definition.Channels.Add(new InputChannelDefinition
            {
                Key = channel.Key,
                Label = channel.Label,
                ValueType = InputValueType.Bool,
                Category = "Raw / Buttons",
                Description = $"Raw ITGMania button state for {channel.Label}."
            });
        }

        return definition;
    }

    private static string ExpandPipeName(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return GetDefaultPipeName();
        }

        return pipeName.StartsWith("~", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), pipeName[1..])
            : pipeName;
    }

    private static string GetDefaultPipeName()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "StepMania-Lights-SextetStream"
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".itgmania/Save/StepMania-Lights-SextetStream.out");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo(string pathname, uint mode);

    [Flags]
    private enum ItgCabinetLight
    {
        None = 0,
        MarqueeUpLeft = 1,
        MarqueeUpRight = 2,
        MarqueeLowerLeft = 4,
        MarqueeLowerRight = 8,
        BassLeft = 16,
        BassRight = 32
    }

    [Flags]
    private enum ItgGameButton
    {
        None = 0,
        MenuLeft = 1,
        MenuRight = 2,
        MenuUp = 4,
        MenuDown = 8,
        Start = 16,
        Select = 32,
        Back = 64,
        Coin = 256,
        Operator = 512,
        EffectUp = 1024,
        EffectDown = 2048,
        P1Left = 4096,
        P1Right = 8192,
        P1Up = 16384,
        P1Down = 32768,
        P1UpLeftSolo = 65536,
        P1UpRightSolo = 131072,
        P1Custom07 = 262144,
        P1Custom08 = 524288,
        P1Custom09 = 1048576,
        P2Left = 2097152,
        P2Right = 4194304,
        P2Up = 8388608,
        P2Down = 16777216,
        P2UpLeftSolo = 33554432,
        P2UpRightSolo = 67108864,
        P2Custom07 = 134217728,
        P2Custom08 = 268435456,
        P2Custom09 = 536870912,
        P2Custom10 = 1073741824
    }

    private enum ItgLightsMode
    {
        Attract = 0,
        Joining = 1,
        MenuStartOnly = 2,
        MenuStartAndDirections = 3,
        Demonstration = 4,
        Gameplay = 5,
        Stage = 6,
        AllCleared = 7,
        TestAutoCycle = 8,
        TestManualCycle = 9
    }
}
