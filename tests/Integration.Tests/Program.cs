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

alice.Dispose(); charlie.Dispose();
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
