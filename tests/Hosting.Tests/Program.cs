using System.Net;
using System.Net.Sockets;
using Multiplayer.Foundation.Hosting;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;

await using var host = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Direct Room", 4);
int port = host.Port;
Assert(port > 0, "listen host bound a port");
LobbyView created = LobbyPayloads.ReadState((await ReceiveAsync(host.Client, MessageType.LobbyStateUpdate)).Payload);
Assert(created.Players.Count == 1 && created.Players[0].Name == "Host", "host created a lobby");
Console.WriteLine("PASS: embedded host created lobby without server process");

using LobbyClient guest = await LobbyClient.ConnectAsync("127.0.0.1", port, "Guest", TimeSpan.FromSeconds(4));
await guest.SendAsync(MessageType.LobbyListRequest, 2, Array.Empty<byte>());
var rooms = LobbyPayloads.ReadList((await ReceiveAsync(guest, MessageType.LobbyListResponse)).Payload);
Assert(rooms.Count == 1 && rooms[0].Id == created.Id, "guest sees hosted room");
await guest.SendAsync(MessageType.JoinLobby, 3, LobbyPayloads.GuidValue(created.Id));
LobbyView hostJoined = LobbyPayloads.ReadState((await ReceiveAsync(host.Client, MessageType.LobbyStateUpdate)).Payload);
LobbyView guestJoined = LobbyPayloads.ReadState((await ReceiveAsync(guest, MessageType.LobbyStateUpdate)).Payload);
Assert(hostJoined.Revision == guestJoined.Revision && guestJoined.Players.Count == 2, "join synchronized");
Console.WriteLine("PASS: guest joined direct host and both saw same lobby");

await guest.SendAsync(MessageType.PlayerReadyChanged, 4, LobbyPayloads.Bool(true));
LobbyView hostReady = LobbyPayloads.ReadState((await ReceiveAsync(host.Client, MessageType.LobbyStateUpdate)).Payload);
LobbyView guestReady = LobbyPayloads.ReadState((await ReceiveAsync(guest, MessageType.LobbyStateUpdate)).Payload);
Assert(hostReady.Revision == guestReady.Revision && hostReady.Players.Single(p => p.Name == "Guest").Ready,
    "ready synchronized");
Console.WriteLine("PASS: direct-host Ready state synchronized");

// Exercise the same generic stream entry points used by the Steam transport.
var transport = new TcpListener(IPAddress.Loopback, 0);
transport.Start();
using var outbound = new TcpClient();
await outbound.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)transport.LocalEndpoint).Port);
using TcpClient inbound = await transport.AcceptTcpClientAsync();
Assert(host.AttachConnection(NetworkConnection.FromDuplexStream(inbound.GetStream())),
    "host accepts a non-listener duplex transport");
using LobbyClient streamGuest = await LobbyClient.ConnectOverStreamAsync(outbound.GetStream(),
    "StreamGuest", TimeSpan.FromSeconds(4));
await streamGuest.SendAsync(MessageType.LobbyListRequest, 5, Array.Empty<byte>());
var streamRooms = LobbyPayloads.ReadList((await ReceiveAsync(streamGuest, MessageType.LobbyListResponse)).Payload);
Assert(streamRooms.Count == 1 && streamRooms[0].Id == created.Id, "stream guest sees hosted room");
await streamGuest.SendAsync(MessageType.JoinLobby, 6, LobbyPayloads.GuidValue(created.Id));
LobbyView streamJoined = LobbyPayloads.ReadState((await ReceiveAsync(streamGuest, MessageType.LobbyStateUpdate)).Payload);
Assert(streamJoined.Players.Count == 3, "stream guest joined alongside TCP guest");
transport.Stop();
Console.WriteLine("PASS: external duplex transport reused handshake and lobby routing");

await host.StopAsync();
await guest.Completion.WaitAsync(TimeSpan.FromSeconds(4));
await streamGuest.Completion.WaitAsync(TimeSpan.FromSeconds(4));
Assert(!guest.IsConnected, "guest disconnected when host stopped");
var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();
listener.Stop();
Console.WriteLine("PASS: stopping host disconnected guest and released port");

static async Task<WireMessage> ReceiveAsync(LobbyClient client, MessageType wanted)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (!timeout.IsCancellationRequested)
    {
        while (client.TryDequeue(out WireMessage? message))
        {
            if (message?.Type == wanted) return message;
            if (message?.Type == MessageType.Error) throw new Exception("Server returned an error.");
        }
        await Task.Delay(20, timeout.Token);
    }
    throw new TimeoutException("Missing " + wanted);
}

static void Assert(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
}
