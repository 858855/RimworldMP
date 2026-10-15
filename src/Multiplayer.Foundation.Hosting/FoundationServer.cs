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
    private readonly TransferRelay transfers = new();
    private readonly ConcurrentDictionary<Guid, Peer> peers = new();
    private readonly ConcurrentDictionary<Guid, Task> peerTasks = new();
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
    public int ActiveTransfers => transfers.ActiveTransfers;
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
            AttachConnection(NetworkConnection.FromAcceptedClient(client), null);
        }
    }

    /// <param name="verifiedKey">Identity the transport has proven, e.g. the SteamID Steam authenticated for a P2P
    /// connection. The Hello must claim the same key. Null for TCP, where the claim cannot be checked (SEC-001).</param>
    public bool AttachConnection(NetworkConnection connection, PlayerKey? verifiedKey = null)
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
        Task task = HandleClientAsync(connection, verifiedKey, stopping.Token);
        peerTasks[taskId] = task;
        _ = task.ContinueWith(completed => peerTasks.TryRemove(taskId, out _),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return true;
    }

    private async Task HandleClientAsync(NetworkConnection connection, PlayerKey? verifiedKey, CancellationToken serverToken)
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
            string name;
            PlayerKey key;
            try { (name, key) = LobbyPayloads.ReadHello(hello.Payload); }
            catch (ProtocolException)
            { await RejectAsync(connection, 1, "Invalid Hello.", serverToken).ConfigureAwait(false); return; }
            if (verifiedKey != null && key != verifiedKey.Value)
            { await RejectAsync(connection, 3, "Player key does not match the verified identity.", serverToken).ConfigureAwait(false); return; }
            try { lobbies.AddMember(sessionId, name, key); }
            catch (LobbyRuleException error)
            { await RejectAsync(connection, error.Code, error.Message, serverToken).ConfigureAwait(false); return; }
            peer = new Peer(sessionId, name, connection);
            peers[sessionId] = peer;
            await connection.SendAsync(new WireMessage(MessageType.HandshakeAccepted, 1,
                LobbyPayloads.Accepted(sessionId, (ushort)config.HeartbeatIntervalSeconds)), serverToken).ConfigureAwait(false);
            // Per-connection stop: without it the heartbeat's delay holds up cleanup (PlayerLeft, freeing the
            // player key, aborting transfers) by up to one heartbeat interval after the client is gone.
            using var peerStop = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
            Task heartbeat = HeartbeatLoopAsync(peer, peerStop.Token);
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
            finally { connection.Dispose(); peerStop.Cancel(); await heartbeat.ConfigureAwait(false); }
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
                LobbyChange? change = lobbies.RemoveMember(sessionId);
                var gone = transfers.PeerGone(sessionId);
                if (!serverToken.IsCancellationRequested)
                {
                    try
                    {
                        if (change?.State != null) await PublishAsync(change, MessageType.PlayerLeft, serverToken).ConfigureAwait(false);
                        await DeliverAsync(gone, serverToken).ConfigureAwait(false);
                    }
                    catch (IOException) { }
                    catch (ObjectDisposedException) { }
                    catch (OperationCanceledException) { }
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
                await PublishAsync(lobbies.Leave(peer.Id), MessageType.PlayerLeft, token).ConfigureAwait(false);
                await DeliverAsync(transfers.PeerGone(peer.Id), token).ConfigureAwait(false);
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
                    await DeliverAsync(transfers.PeerGone(member), token).ConfigureAwait(false);
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
            case MessageType.SetupAdvance:
                await PublishAsync(lobbies.AdvanceSetup(peer.Id, SetupPayloads.ReadAdvance(message.Payload)), null, token, message.RequestId).ConfigureAwait(false);
                break;
            case MessageType.SetupAbort:
                RequireEmpty(message.Payload);
                await PublishAsync(lobbies.AbortSetup(peer.Id), null, token, message.RequestId).ConfigureAwait(false);
                break;
            case MessageType.SiteFlag:
                await PublishAsync(lobbies.SetSiteFlag(peer.Id, SetupPayloads.ReadFlag(message.Payload)), null, token, message.RequestId).ConfigureAwait(false);
                break;
            case MessageType.SiteDecided:
                await PublishAsync(lobbies.DecideSite(peer.Id, SetupPayloads.ReadSite(message.Payload)), null, token, message.RequestId).ConfigureAwait(false);
                break;
            case MessageType.TransferBegin:
                TransferBegin begin = TransferPayloads.ReadBegin(message.Payload);
                IReadOnlyList<Guid> members;
                try { members = lobbies.ChatRecipients(peer.Id).Recipients; }
                catch (LobbyRuleException) { members = Array.Empty<Guid>(); }
                await DeliverAsync(transfers.Begin(peer.Id, begin, members), token).ConfigureAwait(false);
                break;
            case MessageType.TransferChunk:
                await DeliverAsync(transfers.Chunk(peer.Id, message), token).ConfigureAwait(false);
                break;
            case MessageType.TransferAck:
                await DeliverAsync(transfers.Ack(peer.Id, message), token).ConfigureAwait(false);
                break;
            case MessageType.TransferAbort:
                await DeliverAsync(transfers.Abort(peer.Id, message), token).ConfigureAwait(false);
                break;
            default: throw new ProtocolException("Message not valid in current state.");
        }
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

    private async Task DeliverAsync(IReadOnlyList<(Guid To, WireMessage Message)> outgoing, CancellationToken token)
    {
        foreach (var (to, message) in outgoing)
        {
            if (!peers.TryGetValue(to, out Peer? target)) continue;
            try { await target.Connection.SendAsync(message, token).ConfigureAwait(false); }
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

    private sealed class Peer
    {
        public Peer(Guid id, string name, NetworkConnection connection)
        { Id = id; Name = name; Connection = connection; LastPongStamp = Stopwatch.GetTimestamp(); }
        public Guid Id { get; }
        public string Name { get; }
        public NetworkConnection Connection { get; }
        public long LastPongStamp;
        public long NextNonce;
        public long LastAcknowledgedNonce;
    }
}
