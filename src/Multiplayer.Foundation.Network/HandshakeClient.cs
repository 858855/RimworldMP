using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    public sealed class HandshakeResult
    {
        internal HandshakeResult(NetworkConnection connection, Guid sessionId, ushort heartbeatSeconds, PlayerKey playerKey)
        {
            Connection = connection;
            SessionId = sessionId;
            HeartbeatSeconds = heartbeatSeconds;
            PlayerKey = playerKey;
        }
        public NetworkConnection Connection { get; }
        public PlayerKey PlayerKey { get; }
        public Guid SessionId { get; }
        public ushort HeartbeatSeconds { get; }
    }

    public static class HandshakeClient
    {
        /// <param name="playerKey">Persistent identity; without it a throwaway key is used, which is only fit for tests.</param>
        public static async Task<HandshakeResult> ConnectAsync(string host, int port, string playerName,
            TimeSpan timeout, CancellationToken cancellationToken = default, PlayerKey? playerKey = null)
        {
            ValidateName(playerName);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                NetworkConnection? connection = null;
                try
                {
                    connection = await NetworkConnection.ConnectAsync(host, port, timeout, deadline.Token)
                        .ConfigureAwait(false);
                    return await PerformAsync(connection, playerName, KeyOrThrowaway(playerKey), deadline.Token).ConfigureAwait(false);
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

        public static async Task<HandshakeResult> ConnectAsync(NetworkConnection connection, string playerName,
            TimeSpan timeout, CancellationToken cancellationToken = default, PlayerKey? playerKey = null)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            try { ValidateName(playerName); }
            catch { connection.Dispose(); throw; }
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                try { return await PerformAsync(connection, playerName, KeyOrThrowaway(playerKey), deadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                { connection.Dispose(); throw new TimeoutException("Handshake timed out."); }
                catch { connection.Dispose(); throw; }
            }
        }

        private static PlayerKey KeyOrThrowaway(PlayerKey? playerKey)
        {
            if (playerKey == null) return PlayerKey.Local(Guid.NewGuid());
            if (!playerKey.Value.IsValid) throw new ArgumentException("Invalid player key.", nameof(playerKey));
            return playerKey.Value;
        }

        private static async Task<HandshakeResult> PerformAsync(NetworkConnection connection, string playerName,
            PlayerKey playerKey, CancellationToken cancellationToken)
        {
            await connection.SendAsync(new WireMessage(MessageType.Hello, 1, LobbyPayloads.Hello(playerName, playerKey)), cancellationToken)
                .ConfigureAwait(false);
            WireMessage? reply = await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
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
            return new HandshakeResult(connection, sessionId, heartbeatSeconds, playerKey);
        }

        private static void ValidateName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 40)
                throw new ArgumentException("Player name must have 1-40 characters.", nameof(playerName));
            foreach (char character in playerName)
                if (char.IsControl(character)) throw new ArgumentException("Control characters are not allowed.", nameof(playerName));
        }
    }
}
