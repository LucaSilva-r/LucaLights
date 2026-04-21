using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // If tosu stops sending v2 updates for this long we assume osu! quit — tosu doesn't
    // close the socket in that case, it just goes silent.
    private static readonly TimeSpan V2IdleTimeout = TimeSpan.FromMilliseconds(500);

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
            if (!await _osuProcessWatcher.WaitForAcquiredAsync(ct).ConfigureAwait(false))
            {
                break;
            }

            if (!await EnsureTosuReadyAsync(ct).ConfigureAwait(false))
            {
                try { await Task.Delay(ProcessPollInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            using var ws = new ClientWebSocket();
            // `idleDisconnected` tracks whether we've already fired the idle-disconnect
            // side effects for this socket, so we do it exactly once per osu! close and
            // silently re-arm when messages resume without spamming connect/disconnect logs.
            // Note: we must NOT cancel ReceiveAsync — ClientWebSocket aborts the socket on
            // cancellation, so we detect idle via Task.WhenAny with a timer instead.
            bool idleDisconnected = false;
            try
            {
                await ws.ConnectAsync(url, ct).ConfigureAwait(false);

                var                              lastMsgTicks = Environment.TickCount64;
                Task<WebSocketReceiveResult>?    receiveTask  = null;

                while (!ct.IsCancellationRequested)
                {
                    sb.Clear();
                    bool closed = false;
                    WebSocketReceiveResult msg = default!;

                    do
                    {
                        receiveTask ??= ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                        var completed = await Task.WhenAny(receiveTask, Task.Delay(V2IdleTimeout, ct)).ConfigureAwait(false);

                        if (completed != receiveTask)
                        {
                            if (ct.IsCancellationRequested || !_osuProcessWatcher.IsRunning)
                            {
                                closed = true;
                                break;
                            }

                            // Timer fired before a frame arrived.
                            if (!idleDisconnected &&
                                Environment.TickCount64 - lastMsgTicks >= (long)V2IdleTimeout.TotalMilliseconds)
                            {
                                ResetOsuState();
                                idleDisconnected = true;
                            }
                            continue;
                        }

                        msg          = await receiveTask.ConfigureAwait(false);
                        receiveTask  = null;
                        lastMsgTicks = Environment.TickCount64;

                        if (msg.MessageType == WebSocketMessageType.Close) { closed = true; break; }
                        if (msg.MessageType == WebSocketMessageType.Text)
                            sb.Append(Encoding.UTF8.GetString(buf, 0, msg.Count));
                    }
                    while (!msg.EndOfMessage);

                    if (closed) break;
                    if (sb.Length == 0) continue;

                    if (!_osuProcessWatcher.IsRunning)
                    {
                        break;
                    }

                    var data = TryDeserialize<TosuV2Data>(sb.ToString());
                    if (data is null) continue;

                    var wasDisconnected = idleDisconnected;
                    lock (_syncRoot)
                    {
                        wasDisconnected |= !_v2Connected;
                        _v2Connected = true;
                        _latestV2 = data;
                        idleDisconnected = false;
                    }

                    if (wasDisconnected)
                    {
                        _log?.Invoke("osu: v2 data connected.");
                    }

                    OnV2DataReceived(data);
                    PublishCurrentSnapshot();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch when (!ct.IsCancellationRequested) { /* swallow — reconnect quietly */ }

            ResetOsuState();

            try { await Task.Delay(2000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void ResetOsuState()
    {
        StopNoteEngine();
        lock (_syncRoot)
        {
            _v2Connected = false;
            _latestV2    = null;
            Array.Clear(_columnLevel);
            Array.Clear(_columnPulsePending);
            _noteActive      = false;
            _taikoDon        = false;
            _taikoKat        = false;
            _taikoDrumroll   = false;
            _taikoDenden     = false;
            _standardCircle  = false;
            _standardSlider  = false;
            _standardSpinner = false;
            _catchFruit      = false;
            _currentMode     = -1;
            _currentChecksum = string.Empty;
        }
        PublishCurrentSnapshot();
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
            if (!await _osuProcessWatcher.WaitForAcquiredAsync(ct).ConfigureAwait(false))
            {
                break;
            }

            if (!await EnsureTosuReadyAsync(ct).ConfigureAwait(false))
            {
                try { await Task.Delay(ProcessPollInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

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
                    WebSocketReceiveResult msg = default!;
                    Task<WebSocketReceiveResult>? receiveTask = null;

                    do
                    {
                        receiveTask ??= ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                        var completed = await Task.WhenAny(receiveTask, Task.Delay(ProcessPollInterval, ct)).ConfigureAwait(false);

                        if (completed != receiveTask)
                        {
                            if (ct.IsCancellationRequested || !_osuProcessWatcher.IsRunning)
                            {
                                closed = true;
                                break;
                            }

                            continue;
                        }

                        msg = await receiveTask.ConfigureAwait(false);
                        receiveTask = null;
                        if (msg.MessageType == WebSocketMessageType.Close) { closed = true; break; }
                        if (msg.MessageType == WebSocketMessageType.Text)
                            sb.Append(Encoding.UTF8.GetString(buf, 0, msg.Count));
                    }
                    while (!msg.EndOfMessage);

                    if (closed) break;
                    if (sb.Length == 0) continue;
                    if (!_osuProcessWatcher.IsRunning) break;

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
                if (data.Keys.K1.Count != _k1Count) { _k1HitPending = true; anyNewHit = true; }
                if (data.Keys.K2.Count != _k2Count) { _k2HitPending = true; anyNewHit = true; }
                if (data.Keys.M1.Count != _m1Count) { _m1HitPending = true; anyNewHit = true; }
                if (data.Keys.M2.Count != _m2Count) { _m2HitPending = true; anyNewHit = true; }

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
