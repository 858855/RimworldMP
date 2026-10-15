using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    public sealed class HandshakeResult
    {
        internal HandshakeResult(NetworkConnection connection, Guid sessionId, ushort heartbeatSeconds)
        {
            Connection = connection;
            SessionId = sessionId;
            HeartbeatSeconds = heartbeatSeconds;
        }
        public NetworkConnection Connection { get; }
        public Guid SessionId { get; }
        public ushort HeartbeatSeconds { get; }
    }

    public static class HandshakeClient
    {
        public static async Task<HandshakeResult> ConnectAsync(string host, int port, string playerName,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 40)
                throw new ArgumentException("Player name must have 1-40 characters.", nameof(playerName));
            foreach (char character in playerName)
                if (char.IsControl(character)) throw new ArgumentException("Control characters are not allowed.", nameof(playerName));
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                NetworkConnection? connection = null;
                try
                {
                    connection = await NetworkConnection.ConnectAsync(host, port, timeout, deadline.Token)
                        .ConfigureAwait(false);
                    var payload = new PayloadWriter();
                    payload.WriteString(playerName, 160);
                    await connection.SendAsync(new WireMessage(MessageType.Hello, 1, payload.ToArray()), deadline.Token)
                        .ConfigureAwait(false);
                    WireMessage? reply = await connection.ReceiveAsync(deadline.Token).ConfigureAwait(false);
                    if (reply == null) throw new ProtocolException("Server closed during handshake.");
                    if (reply.RequestId != 1) throw new ProtocolException("Handshake request ID mismatch.");
                    var reader = new PayloadReader(reply.Payload);
                    if (reply.Type == MessageType.HandshakeRejected)
                    {
                        byte code = reader.ReadByte();
                        string reason = reader.ReadString();
                        reader.EnsureComplete();
                        throw new ProtocolException("Handshake rejected (" + code + "): " + reason);
                    }
                    if (reply.Type != MessageType.HandshakeAccepted)
                        throw new ProtocolException("Unexpected handshake reply.");
                    Guid sessionId = reader.ReadGuid();
                    ushort heartbeatSeconds = reader.ReadUInt16();
                    reader.EnsureComplete();
                    if (sessionId == Guid.Empty || heartbeatSeconds == 0)
                        throw new ProtocolException("Invalid handshake parameters.");
                    return new HandshakeResult(connection, sessionId, heartbeatSeconds);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    connection?.Dispose();
                    throw new TimeoutException("Handshake timed out.");
                }
                catch
                {
                    connection?.Dispose();
                    throw;
                }
            }
        }
    }
}
