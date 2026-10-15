using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    // Owns the sole reader. The game UI drains messages on its own thread.
    public sealed class LobbyClient : IDisposable
    {
        private readonly NetworkConnection connection;
        private readonly CancellationTokenSource stopping = new CancellationTokenSource();
        private readonly ConcurrentQueue<WireMessage> incoming = new ConcurrentQueue<WireMessage>();
        private readonly Task reader;
        private int closed;
        private string? lastError;

        private LobbyClient(HandshakeResult handshake)
        {
            connection = handshake.Connection;
            SessionId = handshake.SessionId;
            HeartbeatSeconds = handshake.HeartbeatSeconds;
            reader = ReadLoopAsync();
        }

        public Guid SessionId { get; }
        public ushort HeartbeatSeconds { get; }
        public bool IsConnected => Volatile.Read(ref closed) == 0 && !connection.IsClosed;
        public string? LastError => Volatile.Read(ref lastError);
        public Task Completion => reader;

        public static async Task<LobbyClient> ConnectAsync(string host, int port, string playerName,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            HandshakeResult handshake = await HandshakeClient.ConnectAsync(host, port, playerName, timeout, cancellationToken)
                .ConfigureAwait(false);
            return new LobbyClient(handshake);
        }

        public Task SendAsync(MessageType type, uint requestId, byte[] payload)
        {
            if (!IsConnected) throw new InvalidOperationException("Client is disconnected.");
            return connection.SendAsync(new WireMessage(type, requestId, payload), stopping.Token);
        }

        public bool TryDequeue(out WireMessage? message) => incoming.TryDequeue(out message);

        public async Task DisconnectAsync()
        {
            if (IsConnected)
            {
                try { await SendAsync(MessageType.Disconnect, 0, Array.Empty<byte>()).ConfigureAwait(false); }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
            }
            Dispose();
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                while (!stopping.IsCancellationRequested)
                {
                    WireMessage? message = await connection.ReceiveAsync(stopping.Token).ConfigureAwait(false);
                    if (message == null) { Volatile.Write(ref lastError, "Server closed the connection."); break; }
                    if (message.Type == MessageType.Ping)
                    {
                        ulong nonce = LobbyPayloads.ReadPing(message.Payload);
                        await connection.SendAsync(new WireMessage(MessageType.Pong, message.RequestId,
                            LobbyPayloads.Ping(nonce)), stopping.Token).ConfigureAwait(false);
                        continue;
                    }
                    if (message.Type == MessageType.Disconnect)
                    { Volatile.Write(ref lastError, "Server disconnected the client."); break; }
                    incoming.Enqueue(message);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (stopping.IsCancellationRequested) { }
            catch (Exception error) when (error is IOException || error is ObjectDisposedException)
            { Volatile.Write(ref lastError, error.Message); }
            finally { Interlocked.Exchange(ref closed, 1); connection.Dispose(); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) == 0)
            { stopping.Cancel(); connection.Dispose(); }
        }
    }
}
