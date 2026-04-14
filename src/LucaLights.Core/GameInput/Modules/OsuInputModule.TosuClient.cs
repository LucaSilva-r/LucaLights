using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private async Task RunV2LoopAsync(CancellationToken ct)
    {
        var url = new Uri(_tosuUrl + "/websocket/v2");

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(url, ct);

                lock (_syncRoot) { _v2Connected = true; }
                _log?.Invoke("osu: tosu v2 connected.");
                PublishCurrentSnapshot();

                await ReceiveLoopAsync(ws, ct, json =>
                {
                    var data = JsonSerializer.Deserialize<TosuV2Data>(json, JsonOpts);
                    if (data is null) return;
                    lock (_syncRoot) { _latestV2 = data; }
                    OnV2DataReceived(data);
                    PublishCurrentSnapshot();
                });
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log?.Invoke($"osu: v2 WebSocket error: {ex.Message}");
            }

            lock (_syncRoot) { _v2Connected = false; }
            PublishCurrentSnapshot();

            if (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(2000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task RunPreciseLoopAsync(CancellationToken ct)
    {
        var url = new Uri(_tosuUrl + "/websocket/v2/precise");

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(url, ct);

                await ReceiveLoopAsync(ws, ct, json =>
                {
                    var data = JsonSerializer.Deserialize<TosuPreciseData>(json, JsonOpts);
                    if (data is null) return;
                    OnPreciseDataReceived(data);
                });
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log?.Invoke($"osu: precise WebSocket error: {ex.Message}");
            }

            if (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(2000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private static async Task ReceiveLoopAsync(
        ClientWebSocket ws, CancellationToken ct, Action<string> onMessage)
    {
        var buffer = new byte[65536];
        var sb     = new StringBuilder();

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            sb.Clear();
            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close) return;
                if (result.MessageType == WebSocketMessageType.Text)
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            if (sb.Length > 0)
            {
                try { onMessage(sb.ToString()); }
                catch (JsonException) { /* skip malformed frames */ }
            }
        }
    }

    private void OnPreciseDataReceived(TosuPreciseData data)
    {
        // Also sync the timing for the note engine
        lock (_syncRoot)
        {
            _k1Pressed         = data.Keys.K1.IsPressed;
            _k2Pressed         = data.Keys.K2.IsPressed;
            _m1Pressed         = data.Keys.M1.IsPressed;
            _m2Pressed         = data.Keys.M2.IsPressed;
            _lastPreciseTimeMs = data.CurrentTime;
        }
        PublishCurrentSnapshot();
    }
}
