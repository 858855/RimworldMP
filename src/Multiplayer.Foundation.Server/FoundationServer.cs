using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Multiplayer.Foundation.Lobby;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Server;

public sealed class FoundationServer : IAsyncDisposable
{
    private readonly ServerConfig config;
    private readonly LobbyRegistry lobbies;
    private readonly ConcurrentDictionary<Guid, Peer> peers = new();
    private readonly ConcurrentDictionary<Guid, Task> peerTasks = new();
    private readonly CancellationTokenSource stopping = new();
    private TcpListener? listener;
    private Task? acceptTask;
    private int pendingConnections;

    public FoundationServer(ServerConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        lobbies = new LobbyRegistry(config.MaxPlayersPerLobby);
    }
    public int BoundPort => listener == null ? 0 : ((IPEndPoint)listener.LocalEndpoint).Port;
    public int ConnectedClients => peers.Count;

    public Task StartAsync()
    {
        if (listener != null) throw new InvalidOperationException("Server already started.");
        IPAddress address = config.Validate(true);
        listener = new TcpListener(address, config.Port);
        listener.Start();
        acceptTask = AcceptLoopAsync(stopping.Token);
        return Task.CompletedTask;
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
            try { client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { break; }
            if (Interlocked.Increment(ref pendingConnections) > config.MaxClients)
            { Interlocked.Decrement(ref pendingConnections); client.Close(); continue; }
            Guid taskId = Guid.NewGuid();
            Task task = HandleClientAsync(client, cancellationToken);
            peerTasks[taskId] = task;
            _ = task.ContinueWith(completed => peerTasks.TryRemove(taskId, out var ignored), TaskScheduler.Default);
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken serverToken)
    {
        Guid sessionId = Guid.NewGuid();
        Peer? peer = null;
        using NetworkConnection connection = NetworkConnection.FromAcceptedClient(tcp);
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
            try { name = LobbyPayloads.ReadString(hello.Payload); lobbies.AddMember(sessionId, name); }
            catch (Exception error) when (error is ProtocolException or LobbyRuleException)
            { await RejectAsync(connection, 1, "Invalid player name.", serverToken).ConfigureAwait(false); return; }
            peer = new Peer(sessionId, name, connection);
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
                if (LobbyPayloads.ReadPing(message.Payload) != (ulong)Volatile.Read(ref peer.ExpectedNonce))
                    throw new ProtocolException("Unexpected Pong.");
                peer.LastPongUtc = DateTime.UtcNow;
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
                await peer.Connection.SendAsync(new WireMessage(MessageType.LeaveLobby, message.RequestId, Array.Empty<byte>()), token).ConfigureAwait(false);
                break;
            case MessageType.PlayerReadyChanged:
                bool ready = LobbyPayloads.ReadBool(message.Payload);
                await PublishAsync(lobbies.Ready(peer.Id, ready), MessageType.PlayerReadyChanged, token, message.RequestId, ready).ConfigureAwait(false);
                break;
            case MessageType.CloseLobby:
                RequireEmpty(message.Payload);
                LobbyChange closed = lobbies.Close(peer.Id);
                foreach (Guid recipient in closed.Recipients)
                    if (peers.TryGetValue(recipient, out Peer? target))
                        await target.Connection.SendAsync(new WireMessage(MessageType.LeaveLobby,
                            recipient == peer.Id ? message.RequestId : 0, Array.Empty<byte>()), token).ConfigureAwait(false);
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

    private async Task HeartbeatLoopAsync(Peer peer, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && !peer.Connection.IsClosed)
            {
                await Task.Delay(TimeSpan.FromSeconds(config.HeartbeatIntervalSeconds), token).ConfigureAwait(false);
                if (DateTime.UtcNow - peer.LastPongUtc > TimeSpan.FromSeconds(config.ClientTimeoutSeconds))
                { peer.Connection.Dispose(); break; }
                long nonce = Interlocked.Increment(ref peer.NextNonce);
                Volatile.Write(ref peer.ExpectedNonce, nonce);
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
        { Id = id; Name = name; Connection = connection; LastPongUtc = DateTime.UtcNow; }
        public Guid Id { get; }
        public string Name { get; }
        public NetworkConnection Connection { get; }
        public DateTime LastPongUtc { get; set; }
        public long NextNonce;
        public long ExpectedNonce;
    }
}
