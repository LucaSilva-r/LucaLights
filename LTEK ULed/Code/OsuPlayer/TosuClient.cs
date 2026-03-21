using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LTEK_ULed.Code.OsuPlayer;

public class TosuClient : IDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private readonly string _url;

    public bool IsConnected { get; private set; }

    public event Action<TosuData>? DataReceived;
    public event Action<bool>? ConnectionChanged;

    public TosuClient(string url = "ws://127.0.0.1:24050/websocket/v2")
    {
        _url = url;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ConnectLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _ws?.Dispose(); } catch { }
        _ws = null;
        IsConnected = false;
        ConnectionChanged?.Invoke(false);
    }

    private async Task ConnectLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(_url), ct);
                IsConnected = true;
                ConnectionChanged?.Invoke(true);
                Debug.WriteLine("[tosu] Connected");

                await ReceiveLoop(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[tosu] Connection error: {ex.Message}");
            }

            IsConnected = false;
            ConnectionChanged?.Invoke(false);
            _ws?.Dispose();
            _ws = null;

            if (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(2000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buffer = new byte[32768];

        while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    var data = JsonSerializer.Deserialize<TosuData>(json);
                    if (data != null)
                    {
                        DataReceived?.Invoke(data);
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[tosu] JSON parse error: {ex.Message}");
                }
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

// Minimal tosu v2 data model — only the fields we need
public class TosuData
{
    [JsonPropertyName("game")]
    public TosuGame Game { get; set; } = new();

    [JsonPropertyName("beatmap")]
    public TosuBeatmap Beatmap { get; set; } = new();

    [JsonPropertyName("folders")]
    public TosuFolders Folders { get; set; } = new();

    [JsonPropertyName("directPath")]
    public TosuDirectPath DirectPath { get; set; } = new();
}

public class TosuGame
{
    [JsonPropertyName("paused")]
    public bool Paused { get; set; }
}

public class TosuBeatmap
{
    [JsonPropertyName("time")]
    public TosuBeatmapTime Time { get; set; } = new();

    [JsonPropertyName("checksum")]
    public string Checksum { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public TosuMode Mode { get; set; } = new();

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public TosuBeatmapStats Stats { get; set; } = new();
}

public class TosuBeatmapTime
{
    [JsonPropertyName("live")]
    public int Live { get; set; }

    [JsonPropertyName("mp3Length")]
    public int Mp3Length { get; set; }
}

public class TosuMode
{
    [JsonPropertyName("number")]
    public int Number { get; set; }
}

public class TosuBeatmapStats
{
    [JsonPropertyName("cs")]
    public TosuStatValue Cs { get; set; } = new();
}

public class TosuStatValue
{
    [JsonPropertyName("original")]
    public float Original { get; set; }
}

public class TosuFolders
{
    [JsonPropertyName("songs")]
    public string Songs { get; set; } = string.Empty;
}

public class TosuDirectPath
{
    [JsonPropertyName("beatmapFile")]
    public string BeatmapFile { get; set; } = string.Empty;
}
