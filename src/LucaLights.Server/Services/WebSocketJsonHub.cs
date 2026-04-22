using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace LucaLights.Server.Services;

public sealed class WebSocketJsonHub
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = [];

    public int ClientCount => _clients.Count;

    public Task AcceptAsync(
        WebSocket socket,
        IEnumerable<object>? initialMessages,
        CancellationToken cancellationToken)
        => AcceptAsync(socket, initialMessages, null, cancellationToken);

    public async Task AcceptAsync(
        WebSocket socket,
        IEnumerable<object>? initialMessages,
        IEnumerable<ReadOnlyMemory<byte>>? initialBinaryMessages,
        CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        var connection = new ClientConnection(socket);
        _clients[clientId] = connection;

        try
        {
            if (initialMessages is not null)
            {
                foreach (var message in initialMessages)
                {
                    await SendObjectAsync(connection, message, cancellationToken);
                }
            }

            if (initialBinaryMessages is not null)
            {
                foreach (var message in initialBinaryMessages)
                {
                    await SendBinaryAsync(connection, message, cancellationToken);
                }
            }

            await ReadUntilCloseAsync(socket, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            await CloseAndDisposeAsync(connection, CancellationToken.None);
        }
    }

    public async ValueTask BroadcastAsync(object message, CancellationToken cancellationToken = default)
    {
        if (_clients.IsEmpty)
        {
            return;
        }

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var staleClients = new List<Guid>();

        foreach (var (clientId, connection) in _clients)
        {
            if (connection.Socket.State != WebSocketState.Open)
            {
                staleClients.Add(clientId);
                continue;
            }

            try
            {
                await SendTextAsync(connection, json, cancellationToken);
            }
            catch (WebSocketException)
            {
                staleClients.Add(clientId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                staleClients.Add(clientId);
            }
        }

        foreach (var clientId in staleClients)
        {
            if (_clients.TryRemove(clientId, out var connection))
            {
                await CloseAndDisposeAsync(connection, CancellationToken.None);
            }
        }
    }

    public async ValueTask BroadcastBinaryAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (_clients.IsEmpty)
        {
            return;
        }

        var staleClients = new List<Guid>();

        foreach (var (clientId, connection) in _clients)
        {
            if (connection.Socket.State != WebSocketState.Open)
            {
                staleClients.Add(clientId);
                continue;
            }

            try
            {
                await SendBinaryAsync(connection, message, cancellationToken);
            }
            catch (WebSocketException)
            {
                staleClients.Add(clientId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                staleClients.Add(clientId);
            }
        }

        foreach (var clientId in staleClients)
        {
            if (_clients.TryRemove(clientId, out var connection))
            {
                await CloseAndDisposeAsync(connection, CancellationToken.None);
            }
        }
    }

    private static async Task ReadUntilCloseAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);

            while (!result.EndOfMessage && socket.State == WebSocketState.Open)
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }

    private static Task SendObjectAsync(
        ClientConnection connection,
        object message,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        return SendTextAsync(connection, json, cancellationToken);
    }

    private static async Task SendTextAsync(
        ClientConnection connection,
        string message,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await connection.SendLock.WaitAsync(cancellationToken);

        try
        {
            if (connection.Socket.State == WebSocketState.Open)
            {
                await connection.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            }
        }
        finally
        {
            connection.SendLock.Release();
        }
    }

    private static async Task SendBinaryAsync(
        ClientConnection connection,
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken)
    {
        await connection.SendLock.WaitAsync(cancellationToken);

        try
        {
            if (connection.Socket.State == WebSocketState.Open)
            {
                await connection.Socket.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken);
            }
        }
        finally
        {
            connection.SendLock.Release();
        }
    }

    private static async Task CloseAndDisposeAsync(
        ClientConnection connection,
        CancellationToken cancellationToken)
    {
        var lockTaken = false;

        try
        {
            await connection.SendLock.WaitAsync(cancellationToken);
            lockTaken = true;

            if (connection.Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await connection.Socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closed.",
                    cancellationToken);
            }
        }
        catch (WebSocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            if (lockTaken)
            {
                connection.SendLock.Release();
            }

            connection.Dispose();
        }
    }

    private sealed class ClientConnection : IDisposable
    {
        public ClientConnection(WebSocket socket)
        {
            Socket = socket;
        }

        public WebSocket Socket { get; }

        public SemaphoreSlim SendLock { get; } = new(1, 1);

        public void Dispose()
        {
            Socket.Dispose();
            SendLock.Dispose();
        }
    }
}
