using System.Net.Sockets;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Server;

var invalid = new ServerConfig { Port = -1 };
AssertThrows<InvalidDataException>(() => invalid.Validate(false));
string configFile = Path.GetTempFileName();
try
{
    await File.WriteAllTextAsync(configFile, "{\"BindAddress\":\"127.0.0.1\",\"Port\":28491}");
    Assert(ServerConfig.Load(configFile).Port == 28491, "configuration load");
}
finally { File.Delete(configFile); }
Console.WriteLine("PASS: configuration load and invalid port");

var config = new ServerConfig { Port = 0, HeartbeatIntervalSeconds = 1, ClientTimeoutSeconds = 3 };
await using var server = new FoundationServer(config);
await server.StartAsync();
int port = server.BoundPort;
Assert(port > 0, "server bound port");
using var alice = (await HandshakeClient.ConnectAsync("127.0.0.1", port, "Alice", TimeSpan.FromSeconds(3))).Connection;
using var bob = (await HandshakeClient.ConnectAsync("127.0.0.1", port, "Bob", TimeSpan.FromSeconds(3))).Connection;
Assert(server.ConnectedClients == 2, "two sessions");
Console.WriteLine("PASS: server start and two real TCP handshakes");

await alice.SendAsync(new WireMessage(MessageType.CreateLobby, 2, LobbyPayloads.Create("Colony", 2)));
LobbyView created = LobbyPayloads.ReadState((await ReceiveType(alice, MessageType.LobbyStateUpdate)).Payload);
Assert(created.Players.Count == 1 && created.Players[0].Name == "Alice", "create lobby");
await bob.SendAsync(new WireMessage(MessageType.LobbyListRequest, 2, Array.Empty<byte>()));
var list = LobbyPayloads.ReadList((await ReceiveType(bob, MessageType.LobbyListResponse)).Payload);
Assert(list.Count == 1 && list[0].Id == created.Id, "lobby browser");
await bob.SendAsync(new WireMessage(MessageType.JoinLobby, 3, LobbyPayloads.GuidValue(created.Id)));
LobbyView joinedBob = LobbyPayloads.ReadState((await ReceiveType(bob, MessageType.LobbyStateUpdate)).Payload);
LobbyView joinedAlice = LobbyPayloads.ReadState((await ReceiveType(alice, MessageType.LobbyStateUpdate)).Payload);
Assert(joinedBob.Players.Count == 2 && joinedAlice.Revision == joinedBob.Revision, "join broadcast");
Console.WriteLine("PASS: create, list, join, synchronized lobby state");

await bob.SendAsync(new WireMessage(MessageType.PlayerReadyChanged, 4, LobbyPayloads.Bool(true)));
LobbyView readyBob = LobbyPayloads.ReadState((await ReceiveType(bob, MessageType.LobbyStateUpdate)).Payload);
LobbyView readyAlice = LobbyPayloads.ReadState((await ReceiveType(alice, MessageType.LobbyStateUpdate)).Payload);
Assert(readyBob.Players.Single(p => p.Name == "Bob").Ready && readyAlice.Revision == readyBob.Revision, "ready broadcast");
await bob.SendAsync(new WireMessage(MessageType.CloseLobby, 5, Array.Empty<byte>()));
WireMessage denied = await ReceiveType(bob, MessageType.Error);
Assert(denied.RequestId == 5, "host authorization");
Console.WriteLine("PASS: ready broadcast and unauthorized close rejected");

await bob.SendAsync(new WireMessage(MessageType.LeaveLobby, 6, Array.Empty<byte>()));
await ReceiveType(bob, MessageType.LeaveLobby);
LobbyView left = LobbyPayloads.ReadState((await ReceiveType(alice, MessageType.LobbyStateUpdate)).Payload);
Assert(left.Players.Count == 1 && left.Players[0].Name == "Alice", "leave broadcast");
await bob.SendAsync(new WireMessage(MessageType.JoinLobby, 7, LobbyPayloads.GuidValue(created.Id)));
await ReceiveType(bob, MessageType.LobbyStateUpdate);
await ReceiveType(alice, MessageType.LobbyStateUpdate);
bob.Dispose();
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
LobbyView afterDisconnect = LobbyPayloads.ReadState((await ReceiveType(alice, MessageType.LobbyStateUpdate, deadline.Token)).Payload);
Assert(afterDisconnect.Players.Count == 1, "disconnect cleanup");
Console.WriteLine("PASS: leave, rejoin and abrupt disconnect cleanup");

using (var bobAgain = (await HandshakeClient.ConnectAsync("127.0.0.1", port,
    "Bob", TimeSpan.FromSeconds(3))).Connection)
{
    await bobAgain.SendAsync(new WireMessage(MessageType.JoinLobby, 2, LobbyPayloads.GuidValue(created.Id)));
    LobbyView rejoined = LobbyPayloads.ReadState((await ReceiveType(bobAgain, MessageType.LobbyStateUpdate)).Payload);
    Assert(rejoined.Players.Count == 2 && rejoined.Players.Count(p => p.Name == "Bob") == 1,
        "reconnect creates one session membership");
    await ReceiveType(alice, MessageType.LobbyStateUpdate);
}
LobbyView afterReconnectDisconnect = LobbyPayloads.ReadState(
    (await ReceiveType(alice, MessageType.LobbyStateUpdate)).Payload);
Assert(afterReconnectDisconnect.Players.Count == 1, "reconnect disconnect cleanup");
Console.WriteLine("PASS: reconnect has no duplicate lobby membership");

using (var malformed = new TcpClient())
{
    await malformed.ConnectAsync("127.0.0.1", port);
    await malformed.GetStream().WriteAsync(new byte[4]);
}
using var charlie = (await HandshakeClient.ConnectAsync("127.0.0.1", port, "Charlie", TimeSpan.FromSeconds(3))).Connection;
await charlie.SendAsync(new WireMessage(MessageType.LobbyListRequest, 2, Array.Empty<byte>()));
Assert(LobbyPayloads.ReadList((await ReceiveType(charlie, MessageType.LobbyListResponse)).Payload).Count == 1,
    "server survives malformed client");
Console.WriteLine("PASS: malformed frame isolated from later clients");

using (var wrongVersion = new TcpClient())
{
    await wrongVersion.ConnectAsync("127.0.0.1", port);
    await wrongVersion.GetStream().WriteAsync(new byte[] { 0, 0, 0, 6, 1, 1, 0, 0, 0, 1 });
    using var rejectedTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    byte[] response = new byte[1];
    int count = await wrongVersion.GetStream().ReadAsync(response, rejectedTimeout.Token);
    Assert(count == 0, "wrong protocol version closed");
}
Console.WriteLine("PASS: wrong protocol version rejected without affecting server");

// A responsive client answers server heartbeats. A silent client is removed.
await Task.Delay(1100);
WireMessage ping = await ReceiveType(alice, MessageType.Ping);
await alice.SendAsync(new WireMessage(MessageType.Pong, ping.RequestId, ping.Payload));
await Task.Delay(1100);
ping = await ReceiveType(alice, MessageType.Ping);
await alice.SendAsync(new WireMessage(MessageType.Pong, ping.RequestId, ping.Payload));
Assert(server.ConnectedClients >= 1, "heartbeat responsive client");
Console.WriteLine("PASS: heartbeat ping and pong");

await alice.SendAsync(new WireMessage(MessageType.CloseLobby, 8, Array.Empty<byte>()));
await ReceiveType(alice, MessageType.LeaveLobby);
await charlie.SendAsync(new WireMessage(MessageType.LobbyListRequest, 3, Array.Empty<byte>()));
Assert(LobbyPayloads.ReadList((await ReceiveType(charlie, MessageType.LobbyListResponse)).Payload).Count == 0,
    "host closes lobby");
Console.WriteLine("PASS: host closes lobby");

using var dave = (await HandshakeClient.ConnectAsync("127.0.0.1", port,
    "Dave", TimeSpan.FromSeconds(3))).Connection;
await alice.SendAsync(new WireMessage(MessageType.CreateLobby, 9, LobbyPayloads.Create("Transfer", 2)));
LobbyView transferStart = LobbyPayloads.ReadState((await ReceiveType(alice, MessageType.LobbyStateUpdate)).Payload);
await dave.SendAsync(new WireMessage(MessageType.JoinLobby, 2, LobbyPayloads.GuidValue(transferStart.Id)));
await ReceiveType(dave, MessageType.LobbyStateUpdate);
await ReceiveType(alice, MessageType.LobbyStateUpdate);
alice.Dispose();
LobbyView transferred = LobbyPayloads.ReadState((await ReceiveType(dave, MessageType.LobbyStateUpdate)).Payload);
Assert(transferred.Players.Count == 1 && transferred.HostId == transferred.Players[0].Id,
    "host transfer after disconnect");
await dave.SendAsync(new WireMessage(MessageType.CloseLobby, 3, Array.Empty<byte>()));
await ReceiveType(dave, MessageType.LeaveLobby);
Console.WriteLine("PASS: host rights transfer after abrupt disconnect");

charlie.Dispose(); dave.Dispose();
await server.StopAsync();
Assert(server.ConnectedClients == 0, "graceful shutdown");
Console.WriteLine("PASS: graceful server shutdown");

await using var timeoutServer = new FoundationServer(config);
await timeoutServer.StartAsync();
using var silent = (await HandshakeClient.ConnectAsync("127.0.0.1", timeoutServer.BoundPort,
    "Silent", TimeSpan.FromSeconds(3))).Connection;
await Task.Delay(4200);
Assert(timeoutServer.ConnectedClients == 0, "silent heartbeat timeout");
Console.WriteLine("PASS: silent client removed after heartbeat timeout");

// The first reply may arrive after the next scheduled ping and is still valid.
var delayedConfig = new ServerConfig { Port = 0, HeartbeatIntervalSeconds = 1, ClientTimeoutSeconds = 4 };
await using var delayedServer = new FoundationServer(delayedConfig);
await delayedServer.StartAsync();
using var delayed = (await HandshakeClient.ConnectAsync("127.0.0.1", delayedServer.BoundPort,
    "Delayed", TimeSpan.FromSeconds(3))).Connection;
using (var pingDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
{
    WireMessage first = await delayed.ReceiveAsync(pingDeadline.Token) ?? throw new Exception("First ping missing.");
    WireMessage second = await delayed.ReceiveAsync(pingDeadline.Token) ?? throw new Exception("Second ping missing.");
    Assert(first.Type == MessageType.Ping && second.Type == MessageType.Ping, "two scheduled pings");
    Assert(LobbyPayloads.ReadPing(second.Payload) > LobbyPayloads.ReadPing(first.Payload), "ping nonce advances");
    await delayed.SendAsync(new WireMessage(MessageType.Pong, first.RequestId, first.Payload));
    await delayed.SendAsync(new WireMessage(MessageType.LobbyListRequest, 10, Array.Empty<byte>()));
    await ReceiveType(delayed, MessageType.LobbyListResponse);
    Assert(delayedServer.ConnectedClients == 1, "delayed valid pong kept session alive");
    Console.WriteLine("PASS: delayed but outstanding pong accepted");

    await delayed.SendAsync(new WireMessage(MessageType.Pong, first.RequestId, first.Payload));
    await ReceiveType(delayed, MessageType.Error);
    Console.WriteLine("PASS: duplicate pong rejected");
}
using (var future = (await HandshakeClient.ConnectAsync("127.0.0.1", delayedServer.BoundPort,
    "Future", TimeSpan.FromSeconds(3))).Connection)
{
    await future.SendAsync(new WireMessage(MessageType.Pong, 0, LobbyPayloads.Ping(99)));
    await ReceiveType(future, MessageType.Error);
    Console.WriteLine("PASS: unsent pong rejected");
}
await delayedServer.StopAsync();

var chatConfig = new ServerConfig { Port = 0, HeartbeatIntervalSeconds = 10, ClientTimeoutSeconds = 30 };
await using var chatServer = new FoundationServer(chatConfig);
await chatServer.StartAsync();
using var chatAlice = (await HandshakeClient.ConnectAsync("127.0.0.1", chatServer.BoundPort,
    "Alice", TimeSpan.FromSeconds(3))).Connection;
using var chatBob = (await HandshakeClient.ConnectAsync("127.0.0.1", chatServer.BoundPort,
    "Bob", TimeSpan.FromSeconds(3))).Connection;
using var chatCharlie = (await HandshakeClient.ConnectAsync("127.0.0.1", chatServer.BoundPort,
    "Charlie", TimeSpan.FromSeconds(3))).Connection;
await chatAlice.SendAsync(new WireMessage(MessageType.CreateLobby, 2, LobbyPayloads.Create("A", 2)));
LobbyView firstRoom = LobbyPayloads.ReadState((await ReceiveType(chatAlice, MessageType.LobbyStateUpdate)).Payload);
await chatBob.SendAsync(new WireMessage(MessageType.JoinLobby, 2, LobbyPayloads.GuidValue(firstRoom.Id)));
await ReceiveType(chatBob, MessageType.LobbyStateUpdate);
await ReceiveType(chatAlice, MessageType.LobbyStateUpdate);
await chatCharlie.SendAsync(new WireMessage(MessageType.LobbyChatRequest, 2, LobbyPayloads.ChatRequest("Ohne Lobby")));
Assert((await ReceiveType(chatCharlie, MessageType.Error)).RequestId == 2, "nonmember chat rejected");
await chatCharlie.SendAsync(new WireMessage(MessageType.CreateLobby, 2, LobbyPayloads.Create("B", 2)));
await ReceiveType(chatCharlie, MessageType.LobbyStateUpdate);
await chatBob.SendAsync(new WireMessage(MessageType.LobbyChatRequest, 3, LobbyPayloads.ChatRequest("Hallo Alice")));
WireMessage sent = await ReceiveType(chatBob, MessageType.LobbyChatMessage);
WireMessage received = await ReceiveType(chatAlice, MessageType.LobbyChatMessage);
LobbyChatView fromBob = LobbyPayloads.ReadChatMessage(received.Payload);
Assert(sent.RequestId == 3 && received.RequestId == 0 &&
    fromBob.LobbyId == firstRoom.Id && fromBob.SenderName == "Bob" &&
    fromBob.Text == "Hallo Alice" && sent.Payload.SequenceEqual(received.Payload),
    "chat delivered to both members with server identity");
using (var quiet = new CancellationTokenSource(TimeSpan.FromMilliseconds(250)))
{
    try { await chatCharlie.ReceiveAsync(quiet.Token); throw new Exception("Cross-lobby chat leaked."); }
    catch (OperationCanceledException) when (quiet.IsCancellationRequested) { }
}
await chatCharlie.SendAsync(new WireMessage(MessageType.LobbyChatRequest, 3, LobbyPayloads.ChatRequest("Andere Lobby")));
Assert(LobbyPayloads.ReadChatMessage((await ReceiveType(chatCharlie, MessageType.LobbyChatMessage)).Payload).Text ==
    "Andere Lobby", "second lobby chat");
using (var quiet = new CancellationTokenSource(TimeSpan.FromMilliseconds(250)))
{
    try { await chatAlice.ReceiveAsync(quiet.Token); throw new Exception("Cross-lobby chat leaked."); }
    catch (OperationCanceledException) when (quiet.IsCancellationRequested) { }
}
Console.WriteLine("PASS: server-authoritative lobby chat and lobby isolation");
await chatBob.SendAsync(new WireMessage(MessageType.LobbyChatRequest, 4, LobbyPayloads.String("\n")));
Assert((await ReceiveType(chatBob, MessageType.Error)).RequestId == 4, "invalid chat rejected");
Console.WriteLine("PASS: invalid chat rejected");
await chatServer.StopAsync();

await using var keyServer = new FoundationServer(chatConfig);
await keyServer.StartAsync();
string sharedKey = PlayerKeys.Local(Guid.NewGuid());
using var firstLogin = (await HandshakeClient.ConnectAsync("127.0.0.1", keyServer.BoundPort,
    "Erik", TimeSpan.FromSeconds(3), default, sharedKey)).Connection;
using var secondLogin = (await HandshakeClient.ConnectAsync("127.0.0.1", keyServer.BoundPort,
    "Erik", TimeSpan.FromSeconds(3), default, sharedKey)).Connection;
using (var closedDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
    Assert(await firstLogin.ReceiveAsync(closedDeadline.Token) == null, "older session with same key closed");
await secondLogin.SendAsync(new WireMessage(MessageType.LobbyListRequest, 2, Array.Empty<byte>()));
await ReceiveType(secondLogin, MessageType.LobbyListResponse);
using (var invalidKey = new TcpClient())
{
    await invalidKey.ConnectAsync("127.0.0.1", keyServer.BoundPort);
    using var raw = NetworkConnection.FromAcceptedClient(invalidKey);
    var noKey = new PayloadWriter(); noKey.WriteString("Mallory", 160);
    await raw.SendAsync(new WireMessage(MessageType.Hello, 1, noKey.ToArray()));
    WireMessage rejected = await raw.ReceiveAsync() ?? throw new Exception("no handshake reply");
    Assert(rejected.Type == MessageType.HandshakeRejected, "hello without key rejected");
}
Console.WriteLine("PASS: player key replaces older session and is required");
await keyServer.StopAsync();

static async Task<WireMessage> ReceiveType(NetworkConnection connection, MessageType wanted, CancellationToken token = default)
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
    timeout.CancelAfter(TimeSpan.FromSeconds(4));
    while (true)
    {
        WireMessage packet = await connection.ReceiveAsync(timeout.Token) ?? throw new Exception("Connection ended before " + wanted);
        if (packet.Type == wanted) return packet;
        if (packet.Type == MessageType.Ping)
        {
            await connection.SendAsync(new WireMessage(MessageType.Pong, packet.RequestId, packet.Payload), timeout.Token);
            continue;
        }
        if (packet.Type == MessageType.Error) throw new Exception("Unexpected server error: " + packet.RequestId);
    }
}
static void Assert(bool condition, string message)
{ if (!condition) throw new Exception("FAIL: " + message); }
static void AssertThrows<T>(Action action) where T : Exception
{ try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
