using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Multiplayer.Foundation.Lobby;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Server;

public sealed class FoundationServer : IAsyncDisposable
{
    private readonly HostConfig config;
    private readonly LobbyRegistry lobbies;
    private readonly ConcurrentDictionary<Guid, Peer> peers = new();
    private readonly ConcurrentDictionary<Guid, Task> peerTasks = new();
    private readonly ConcurrentDictionary<Guid, Relay> transfers = new();
    private readonly CancellationTokenSource stopping = new();
    private TcpListener? listener;
    private Task? acceptTask;
    private int pendingConnections;

    public FoundationServer(HostConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        lobbies = new LobbyRegistry(config.MaxPlayersPerLobby);
    }
    public int BoundPort => listener == null ? 0 : ((IPEndPoint)listener.LocalEndpoint).Port;
    public int ConnectedClients => peers.Count;
    public bool AcceptsIPv4 { get; private set; }
    public bool AcceptsIPv6 { get; private set; }

    /// <summary>Address a client on this machine uses to reach the listener.</summary>
    public IPAddress LoopbackAddress
    {
        get
        {
            if (listener == null) throw new InvalidOperationException("Server not started.");
            IPAddress bound = ((IPEndPoint)listener.LocalEndpoint).Address;
            if (bound.Equals(IPAddress.Any)) return IPAddress.Loopback;
            if (bound.Equals(IPAddress.IPv6Any)) return AcceptsIPv4 ? IPAddress.Loopback : IPAddress.IPv6Loopback;
            return bound;
        }
    }

    public Task StartAsync()
    {
        if (listener != null) throw new InvalidOperationException("Server already started.");
        IPAddress address = config.Validate(true);
        listener = StartListener(address, config.Port);
        acceptTask = AcceptLoopAsync(stopping.Token);
        return Task.CompletedTask;
    }

    // "::" listens dual-stack (IPv4 and IPv6). Without IPv6 support it falls back to all IPv4 interfaces.
    private TcpListener StartListener(IPAddress address, int port)
    {
        if (address.Equals(IPAddress.IPv6Any))
        {
            TcpListener? dual = null;
            try
            {
                dual = new TcpListener(IPAddress.IPv6Any, port);
                dual.Server.DualMode = true;
                dual.Start();
                AcceptsIPv4 = AcceptsIPv6 = true;
                return dual;
            }
            catch (Exception error) when (error is SocketException { SocketErrorCode:
                not (SocketError.AddressAlreadyInUse or SocketError.AccessDenied) } or NotSupportedException)
            {
                dual?.Stop();
                address = IPAddress.Any;
            }
        }
        var single = new TcpListener(address, port);
        single.Start();
        AcceptsIPv4 = address.AddressFamily == AddressFamily.InterNetwork;
        AcceptsIPv6 = address.AddressFamily == AddressFamily.InterNetworkV6;
        return single;
    }

    public async Task StopAsync()
    {
        if (stopping.IsCancellationRequested) return;
        stopping.Cancel(); listener?.Stop();
        foreach (Peer peer in peers.Values) peer.Connection.Dispose();
        if (acceptTask != null) await acceptTask.ConfigureAwait(false);
        await Task.WhenAll(peerTasks.Values.ToArray()).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        stopping.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        if (listener == null) return;
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { break; }
            AttachConnection(NetworkConnection.FromAcceptedClient(client));
        }
    }

    public bool AttachConnection(NetworkConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (listener == null || stopping.IsCancellationRequested ||
            Interlocked.Increment(ref pendingConnections) > config.MaxClients)
        {
            if (listener != null && !stopping.IsCancellationRequested)
                Interlocked.Decrement(ref pendingConnections);
            connection.Dispose();
            return false;
        }
        Guid taskId = Guid.NewGuid();
        Task task = HandleClientAsync(connection, stopping.Token);
        peerTasks[taskId] = task;
        _ = task.ContinueWith(completed => peerTasks.TryRemove(taskId, out _),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return true;
    }

    private async Task HandleClientAsync(NetworkConnection connection, CancellationToken serverToken)
    {
        Guid sessionId = Guid.NewGuid();
        Peer? peer = null;
        using (connection)
        try
        {
            WireMessage? hello;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(serverToken))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(Math.Min(10, config.ClientTimeoutSeconds)));
                hello = await connection.ReceiveAsync(deadline.Token).ConfigureAwait(false);
            }
            if (hello == null || hello.Type != MessageType.Hello || hello.RequestId != 1)
            { await RejectAsync(connection, 1, "Expected Hello.", serverToken).ConfigureAwait(false); return; }
            string name, key;
            try { (name, key) = LobbyPayloads.ReadHello(hello.Payload); lobbies.AddMember(sessionId, name, key); }
            catch (Exception error) when (error is ProtocolException or LobbyRuleException)
            { await RejectAsync(connection, 1, "Invalid player name or key.", serverToken).ConfigureAwait(false); return; }
            // A new login with the same key replaces the older session (reconnect before the heartbeat noticed the drop).
            foreach (Peer stale in peers.Values.Where(p => p.PlayerKey == key).ToArray()) stale.Connection.Dispose();
            peer = new Peer(sessionId, name, key, connection);
            peers[sessionId] = peer;
            await connection.SendAsync(new WireMessage(MessageType.HandshakeAccepted, 1,
                LobbyPayloads.Accepted(sessionId, (ushort)config.HeartbeatIntervalSeconds)), serverToken).ConfigureAwait(false);
            Task heartbeat = HeartbeatLoopAsync(peer, serverToken);
            try
            {
                while (!serverToken.IsCancellationRequested)
                {
                    WireMessage? message = await connection.ReceiveAsync(serverToken).ConfigureAwait(false);
                    if (message == null || message.Type == MessageType.Disconnect) break;
                    try { await ProcessAsync(peer, message, serverToken).ConfigureAwait(false); }
                    catch (LobbyRuleException error)
                    { await SendErrorAsync(peer, message.RequestId, error.Code, error.Message, serverToken).ConfigureAwait(false); }
                    catch (ProtocolException error)
                    { await SendErrorAsync(peer, message.RequestId, 1, error.Message, serverToken).ConfigureAwait(false); break; }
                }
            }
            finally { connection.Dispose(); await heartbeat.ConfigureAwait(false); }
        }
        catch (OperationCanceledException) when (serverToken.IsCancellationRequested) { }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
        finally
        {
            if (peer != null)
            {
                peers.TryRemove(sessionId, out _);
                if (!serverToken.IsCancellationRequested)
                {
                    try { await AbortTransfersOfAsync(sessionId, "Teilnehmer hat die Verbindung getrennt.", serverToken).ConfigureAwait(false); }
                    catch (IOException) { }
                    catch (ObjectDisposedException) { }
                }
                LobbyChange? change = lobbies.RemoveMember(sessionId);
                if (change?.State != null && !serverToken.IsCancellationRequested)
                {
                    try { await PublishAsync(change, MessageType.PlayerLeft, serverToken).ConfigureAwait(false); }
                    catch (IOException) { }
                    catch (ObjectDisposedException) { }
                }
            }
            Interlocked.Decrement(ref pendingConnections);
        }
    }

    private async Task ProcessAsync(Peer peer, WireMessage message, CancellationToken token)
    {
        switch (message.Type)
        {
            case MessageType.Ping:
                LobbyPayloads.ReadPing(message.Payload);
                await peer.Connection.SendAsync(new WireMessage(MessageType.Pong, message.RequestId, message.Payload), token).ConfigureAwait(false);
                break;
            case MessageType.Pong:
                ulong acknowledged = LobbyPayloads.ReadPing(message.Payload);
                if (acknowledged == 0 || acknowledged <= (ulong)Volatile.Read(ref peer.LastAcknowledgedNonce)
                    || acknowledged > (ulong)Volatile.Read(ref peer.NextNonce))
                    throw new ProtocolException("Unexpected Pong.");
                Volatile.Write(ref peer.LastAcknowledgedNonce, (long)acknowledged);
                Volatile.Write(ref peer.LastPongStamp, Stopwatch.GetTimestamp());
                break;
            case MessageType.LobbyListRequest:
                RequireEmpty(message.Payload);
                await peer.Connection.SendAsync(new WireMessage(MessageType.LobbyListResponse, message.RequestId,
                    LobbyPayloads.List(lobbies.List())), token).ConfigureAwait(false);
                break;
            case MessageType.CreateLobby:
                var create = LobbyPayloads.ReadCreate(message.Payload);
                await PublishAsync(lobbies.Create(peer.Id, create.Name, create.Capacity), null, token, message.RequestId).ConfigureAwait(false);
                break;
            case MessageType.JoinLobby:
                await PublishAsync(lobbies.Join(peer.Id, LobbyPayloads.ReadGuid(message.Payload)), MessageType.PlayerJoined, token, message.RequestId).ConfigureAwait(false);
                break;
            case MessageType.LeaveLobby:
                RequireEmpty(message.Payload);
                await AbortTransfersOfAsync(peer.Id, "Teilnehmer hat die Lobby verlassen.", token).ConfigureAwait(false);
                await PublishAsync(lobbies.Leave(peer.Id), MessageType.PlayerLeft, token).ConfigureAwait(false);
                await peer.Connection.SendAsync(new WireMessage(MessageType.LeaveLobby, message.RequestId, Array.Empty<byte>()), token).ConfigureAwait(false);
                break;
            case MessageType.PlayerReadyChanged:
                bool ready = LobbyPayloads.ReadBool(message.Payload);
                await PublishAsync(lobbies.Ready(peer.Id, ready), MessageType.PlayerReadyChanged, token, message.RequestId, ready).ConfigureAwait(false);
                break;
            case MessageType.CloseLobby:
                RequireEmpty(message.Payload);
                LobbyChange closed = lobbies.Close(peer.Id);
                foreach (Guid member in closed.Recipients)
                    await AbortTransfersOfAsync(member, "Lobby wurde geschlossen.", token).ConfigureAwait(false);
                foreach (Guid recipient in closed.Recipients)
                    if (peers.TryGetValue(recipient, out Peer? target))
                        await target.Connection.SendAsync(new WireMessage(MessageType.LeaveLobby,
                            recipient == peer.Id ? message.RequestId : 0, Array.Empty<byte>()), token).ConfigureAwait(false);
                break;
            case MessageType.LobbyChatRequest:
                string text = LobbyPayloads.ReadChatRequest(message.Payload);
                var chat = lobbies.ChatRecipients(peer.Id);
                byte[] chatPayload = LobbyPayloads.ChatMessage(chat.LobbyId, peer.Id, peer.Name, text);
                foreach (Guid recipient in chat.Recipients)
                    if (peers.TryGetValue(recipient, out Peer? chatTarget))
                        try
                        {
                            await chatTarget.Connection.SendAsync(new WireMessage(MessageType.LobbyChatMessage,
                                recipient == peer.Id ? message.RequestId : 0, chatPayload), token).ConfigureAwait(false);
                        }
                        catch (IOException) { chatTarget.Connection.Dispose(); }
                        catch (ObjectDisposedException) { }
                break;
            case MessageType.StartSetupRequest:
                RequireEmpty(message.Payload);
                await PublishAsync(lobbies.StartSetup(peer.Id), null, token, message.RequestId).ConfigureAwait(false);
                break;
            case MessageType.AbortSetupRequest:
                RequireEmpty(message.Payload);
                await PublishAsync(lobbies.AbortSetup(peer.Id), null, token, message.RequestId).ConfigureAwait(false);
                break;
            case MessageType.ModManifestSummary:
                LobbyChange? modsChanged = lobbies.SetManifest(peer.Id, ModManifestSummary.Read(message.Payload));
                if (modsChanged != null) await PublishAsync(modsChanged, null, token).ConfigureAwait(false);
                break;
            case MessageType.ModManifestRequest:
                // A member asks the host for the full mod list; the host answers with a ModManifest transfer.
                Guid listHost = lobbies.HostOfCurrentLobby(peer.Id);
                if (LobbyPayloads.ReadGuid(message.Payload) != listHost) throw new LobbyRuleException(4, "Only the host's mod list can be requested.");
                if (listHost != peer.Id)
                    await TrySendAsync(listHost, new WireMessage(MessageType.ModManifestRequest, 0, LobbyPayloads.GuidValue(peer.Id)), token)
                        .ConfigureAwait(false);
                break;
            case MessageType.TransferBegin:
                await BeginTransferAsync(peer, TransferPayloads.ReadBegin(message.Payload), token).ConfigureAwait(false);
                break;
            case MessageType.TransferChunk:
                await RelayChunkAsync(peer, message.Payload, token).ConfigureAwait(false);
                break;
            case MessageType.TransferAck:
                await RelayAckAsync(peer, TransferPayloads.ReadAck(message.Payload), token).ConfigureAwait(false);
                break;
            case MessageType.TransferAbort:
                var abort = TransferPayloads.ReadAbort(message.Payload);
                if (transfers.TryGetValue(abort.TransferId, out Relay? aborted) && aborted.Involves(peer.Id))
                    await AbortRelayAsync(aborted, peer.Id, abort.Reason, token).ConfigureAwait(false);
                break;
            default: throw new ProtocolException("Message not valid in current state.");
        }
    }

    // XFER-001: the server relays chunks between lobby members without keeping the data.
    private async Task BeginTransferAsync(Peer peer, TransferHeader header, CancellationToken token)
    {
        if (transfers.Values.Any(r => r.Sender == peer.Id))
            throw new LobbyRuleException(6, "Another transfer from this player is still running.");
        IReadOnlyList<Guid> receivers = lobbies.TransferRecipients(peer.Id, header.Target);
        var relay = new Relay(header.WithSender(peer.Id), receivers);
        if (!transfers.TryAdd(header.TransferId, relay)) throw new LobbyRuleException(6, "Duplicate transfer ID.");
        byte[] begin = TransferPayloads.Begin(relay.Header);
        foreach (Guid receiver in receivers)
            if (!await TrySendAsync(receiver, new WireMessage(MessageType.TransferBegin, 0, begin), token).ConfigureAwait(false))
            { await AbortRelayAsync(relay, Guid.Empty, "Empfänger nicht erreichbar.", token).ConfigureAwait(false); return; }
        // Zero acks tell the sender who receives, so it can pace by the slowest receiver.
        foreach (Guid receiver in receivers)
            await peer.Connection.SendAsync(new WireMessage(MessageType.TransferAck, 0,
                TransferPayloads.Ack(header.TransferId, receiver, 0)), token).ConfigureAwait(false);
    }

    private async Task RelayChunkAsync(Peer peer, byte[] payload, CancellationToken token)
    {
        var chunk = TransferPayloads.ReadChunk(payload);
        if (!transfers.TryGetValue(chunk.TransferId, out Relay? relay) || relay.Sender != peer.Id)
            throw new LobbyRuleException(4, "Unknown transfer.");
        if (chunk.Index != relay.Next || chunk.Data.Length != relay.Header.ChunkLength(chunk.Index))
        {
            await AbortRelayAsync(relay, Guid.Empty, "Ungültiger Datenblock.", token).ConfigureAwait(false);
            throw new LobbyRuleException(6, "Chunk out of order or with wrong length.");
        }
        relay.Next++;
        foreach (Guid receiver in relay.Receivers)
            if (!await TrySendAsync(receiver, new WireMessage(MessageType.TransferChunk, 0, payload), token).ConfigureAwait(false))
            { await AbortRelayAsync(relay, Guid.Empty, "Empfänger nicht erreichbar.", token).ConfigureAwait(false); return; }
    }

    private async Task RelayAckAsync(Peer peer, (Guid TransferId, Guid Receiver, uint Received) ack, CancellationToken token)
    {
        if (!transfers.TryGetValue(ack.TransferId, out Relay? relay) || !relay.Receivers.Contains(peer.Id))
            throw new LobbyRuleException(4, "Unknown transfer.");
        if (ack.Received > relay.Next) throw new LobbyRuleException(6, "Acknowledged unsent chunk.");
        if (ack.Received == relay.Header.ChunkCount && relay.MarkDone(peer.Id)) transfers.TryRemove(relay.Header.TransferId, out _);
        await TrySendAsync(relay.Sender, new WireMessage(MessageType.TransferAck, 0,
            TransferPayloads.Ack(ack.TransferId, peer.Id, ack.Received)), token).ConfigureAwait(false);
    }

    private async Task AbortTransfersOfAsync(Guid participant, string reason, CancellationToken token)
    {
        foreach (Relay relay in transfers.Values.Where(r => r.Involves(participant)).ToArray())
            await AbortRelayAsync(relay, participant, reason, token).ConfigureAwait(false);
    }

    private async Task AbortRelayAsync(Relay relay, Guid origin, string reason, CancellationToken token)
    {
        if (!transfers.TryRemove(relay.Header.TransferId, out _)) return;
        byte[] payload = TransferPayloads.Abort(relay.Header.TransferId, reason);
        foreach (Guid participant in relay.Receivers.Append(relay.Sender).Where(id => id != origin))
            await TrySendAsync(participant, new WireMessage(MessageType.TransferAbort, 0, payload), token).ConfigureAwait(false);
    }

    private async Task<bool> TrySendAsync(Guid recipient, WireMessage message, CancellationToken token)
    {
        if (!peers.TryGetValue(recipient, out Peer? target)) return false;
        try { await target.Connection.SendAsync(message, token).ConfigureAwait(false); return true; }
        catch (IOException) { target.Connection.Dispose(); return false; }
        catch (ObjectDisposedException) { return false; }
    }

    private async Task PublishAsync(LobbyChange change, MessageType? eventType, CancellationToken token,
        uint actorRequestId = 0, bool ready = false)
    {
        if (change.State == null) return;
        byte[] state = LobbyPayloads.State(change.State);
        foreach (Guid recipient in change.Recipients)
        {
            if (!peers.TryGetValue(recipient, out Peer? target)) continue;
            try
            {
                if (eventType != null && recipient != change.Actor)
                {
                    var eventPayload = new PayloadWriter(); eventPayload.WriteGuid(change.Actor);
                    if (eventType == MessageType.PlayerReadyChanged) eventPayload.WriteBool(ready);
                    await target.Connection.SendAsync(new WireMessage(eventType.Value, 0, eventPayload.ToArray()), token).ConfigureAwait(false);
                }
                await target.Connection.SendAsync(new WireMessage(MessageType.LobbyStateUpdate,
                    recipient == change.Actor ? actorRequestId : 0, state), token).ConfigureAwait(false);
            }
            catch (IOException) { target.Connection.Dispose(); }
            catch (ObjectDisposedException) { }
        }
    }

    private async Task HeartbeatLoopAsync(Peer peer, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && !peer.Connection.IsClosed)
            {
                await Task.Delay(TimeSpan.FromSeconds(config.HeartbeatIntervalSeconds), token).ConfigureAwait(false);
                if ((Stopwatch.GetTimestamp() - Volatile.Read(ref peer.LastPongStamp))
                    / (double)Stopwatch.Frequency > config.ClientTimeoutSeconds)
                { peer.Connection.Dispose(); break; }
                long nonce = Interlocked.Increment(ref peer.NextNonce);
                await peer.Connection.SendAsync(new WireMessage(MessageType.Ping, 0, LobbyPayloads.Ping((ulong)nonce)), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private static void RequireEmpty(byte[] payload)
    { if (payload.Length != 0) throw new ProtocolException("Unexpected payload."); }
    private static Task RejectAsync(NetworkConnection connection, byte code, string reason, CancellationToken token)
        => connection.SendAsync(new WireMessage(MessageType.HandshakeRejected, 1, LobbyPayloads.Error(code, reason)), token);
    private static Task SendErrorAsync(Peer peer, uint requestId, byte code, string reason, CancellationToken token)
        => peer.Connection.SendAsync(new WireMessage(MessageType.Error, requestId, LobbyPayloads.Error(code, reason)), token);

    private sealed class Relay
    {
        private readonly HashSet<Guid> pending;
        public Relay(TransferHeader header, IReadOnlyList<Guid> receivers)
        { Header = header; Receivers = receivers.ToArray(); pending = new HashSet<Guid>(receivers); }
        public TransferHeader Header { get; }
        public Guid Sender => Header.Sender;
        public Guid[] Receivers { get; }
        public uint Next; // only the sender's message loop writes this
        public bool Involves(Guid id) => id == Sender || Receivers.Contains(id);
        /// <summary>Returns true when the last receiver finished.</summary>
        public bool MarkDone(Guid receiver) { lock (pending) { pending.Remove(receiver); return pending.Count == 0; } }
    }

    private sealed class Peer
    {
        public Peer(Guid id, string name, string playerKey, NetworkConnection connection)
        { Id = id; Name = name; PlayerKey = playerKey; Connection = connection; LastPongStamp = Stopwatch.GetTimestamp(); }
        public Guid Id { get; }
        public string Name { get; }
        public string PlayerKey { get; }
        public NetworkConnection Connection { get; }
        public long LastPongStamp;
        public long NextNonce;
        public long LastAcknowledgedNonce;
    }
}
