using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Rate-limit precise publishes to ~60 fps; always publish immediately on a new hit.
    private readonly Stopwatch _lastPrecisePublish = Stopwatch.StartNew();

    // Debug: periodic heartbeat every 2 s so we can confirm the loop is alive.
    private readonly Stopwatch _debugLogTimer = Stopwatch.StartNew();

    // ---------------------------------------------------------------------------
    // v2 WebSocket — game state, beatmap info, score
    // ---------------------------------------------------------------------------

    private async Task RunV2LoopAsync(CancellationToken ct)
    {
        var url = new Uri(_tosuUrl + "/websocket/v2");
        var buf = new byte[65536];
        var sb  = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(url, ct).ConfigureAwait(false);

                lock (_syncRoot) { _v2Connected = true; }
                _log?.Invoke("osu: v2 connected.");
                PublishCurrentSnapshot();

                while (!ct.IsCancellationRequested)
                {
                    sb.Clear();
                    bool closed = false;
                    WebSocketReceiveResult msg;

                    do
                    {
                        msg = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct).ConfigureAwait(false);
                        if (msg.MessageType == WebSocketMessageType.Close) { closed = true; break; }
                        if (msg.MessageType == WebSocketMessageType.Text)
                            sb.Append(Encoding.UTF8.GetString(buf, 0, msg.Count));
                    }
                    while (!msg.EndOfMessage);

                    if (closed) break;
                    if (sb.Length == 0) continue;

                    var data = TryDeserialize<TosuV2Data>(sb.ToString());
                    if (data is null) continue;

                    lock (_syncRoot) { _latestV2 = data; }
                    OnV2DataReceived(data);
                    PublishCurrentSnapshot();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log?.Invoke($"osu: v2 error — {ex.Message}");
            }

            lock (_syncRoot) { _v2Connected = false; }
            PublishCurrentSnapshot();
            _log?.Invoke("osu: v2 disconnected.");

            try { await Task.Delay(2000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ---------------------------------------------------------------------------
    // Precise WebSocket — key presses / hit counts at 10 ms cadence
    // ---------------------------------------------------------------------------

    private async Task RunPreciseLoopAsync(CancellationToken ct)
    {
        var url = new Uri(_tosuUrl + "/websocket/v2/precise");
        var buf = new byte[16384];
        var sb  = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(url, ct).ConfigureAwait(false);

                // Reset counters so the first frame never looks like a hit.
                lock (_syncRoot) { _k1Count = _k2Count = _m1Count = _m2Count = -1; }
                _log?.Invoke("osu: precise connected.");

                while (!ct.IsCancellationRequested)
                {
                    sb.Clear();
                    bool closed = false;
                    WebSocketReceiveResult msg;

                    do
                    {
                        msg = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct).ConfigureAwait(false);
                        if (msg.MessageType == WebSocketMessageType.Close) { closed = true; break; }
                        if (msg.MessageType == WebSocketMessageType.Text)
                            sb.Append(Encoding.UTF8.GetString(buf, 0, msg.Count));
                    }
                    while (!msg.EndOfMessage);

                    if (closed) break;
                    if (sb.Length == 0) continue;

                    var data = TryDeserialize<TosuPreciseData>(sb.ToString());
                    if (data is null) continue;

                    OnPreciseDataReceived(data);
                    
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log?.Invoke($"osu: precise error — {ex.Message}");
            }

            _log?.Invoke("osu: precise disconnected.");

            try { await Task.Delay(2000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ---------------------------------------------------------------------------
    // Precise data processing
    // ---------------------------------------------------------------------------

    private void OnPreciseDataReceived(TosuPreciseData data)
    {
        bool anyNewHit = false;

        lock (_syncRoot)
        {
            var now    = DateTimeOffset.UtcNow;
            var window = TimeSpan.FromMilliseconds(120);

            // _k*Count == -1 → first frame after (re)connect; snapshot counts without triggering hits.
            if (_k1Count == -1)
            {
                _k1Count = data.Keys.K1.Count;
                _k2Count = data.Keys.K2.Count;
                _m1Count = data.Keys.M1.Count;
                _m2Count = data.Keys.M2.Count;
            }
            else
            {
                if (data.Keys.K1.Count != _k1Count) { _k1HitUntil = now.Add(window); anyNewHit = true; }
                if (data.Keys.K2.Count != _k2Count) { _k2HitUntil = now.Add(window); anyNewHit = true; }
                if (data.Keys.M1.Count != _m1Count) { _m1HitUntil = now.Add(window); anyNewHit = true; }
                if (data.Keys.M2.Count != _m2Count) { _m2HitUntil = now.Add(window); anyNewHit = true; }

                _k1Count = data.Keys.K1.Count;
                _k2Count = data.Keys.K2.Count;
                _m1Count = data.Keys.M1.Count;
                _m2Count = data.Keys.M2.Count;
            }

            _k1Pressed = data.Keys.K1.IsPressed;
            _k2Pressed = data.Keys.K2.IsPressed;
            _m1Pressed = data.Keys.M1.IsPressed;
            _m2Pressed = data.Keys.M2.IsPressed;

            _lastPreciseTimeMs = data.CurrentTime;
        }

        if (anyNewHit || _lastPrecisePublish.ElapsedMilliseconds >= 16)
        {
            PublishCurrentSnapshot();
            _lastPrecisePublish.Restart();
        }

        if (anyNewHit)
            _log?.Invoke($"[precise] HIT — k1={data.Keys.K1.Count} k2={data.Keys.K2.Count} m1={data.Keys.M1.Count} m2={data.Keys.M2.Count}");

        if (_debugLogTimer.ElapsedMilliseconds >= 2000)
        {
            _log?.Invoke($"[precise] alive — k1={data.Keys.K1.Count} k2={data.Keys.K2.Count} t={data.CurrentTime}ms");
            _debugLogTimer.Restart();
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static T? TryDeserialize<T>(string json)
    {
        try   { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return default; }
    }
}
