using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Multiplayer.Foundation.Hosting;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Server;

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

await using (var dual = await ListenHost.StartAsync("::", 0, "Host", "Dual Room", 4))
{
    Assert(dual.AcceptsIPv4 && dual.AcceptsIPv6, "default bind is dual-stack");
    using LobbyClient viaIPv4 = await LobbyClient.ConnectAsync("127.0.0.1", dual.Port, "Four", TimeSpan.FromSeconds(4));
    using LobbyClient viaIPv6 = await LobbyClient.ConnectAsync("::1", dual.Port, "Six", TimeSpan.FromSeconds(4));
    using LobbyClient viaName = await LobbyClient.ConnectAsync("localhost", dual.Port, "Name", TimeSpan.FromSeconds(4));
    Assert(viaIPv4.IsConnected && viaIPv6.IsConnected && viaName.IsConnected, "IPv4, IPv6 and host name reach '::' host");
}
await using (var anyV4 = await ListenHost.StartAsync("0.0.0.0", 0, "Host", "V4 Room", 4))
{
    Assert(anyV4.AcceptsIPv4 && !anyV4.AcceptsIPv6 && anyV4.Client.IsConnected, "0.0.0.0 host joins its own lobby");
    using LobbyClient guestV4 = await LobbyClient.ConnectAsync("localhost", anyV4.Port, "Guest", TimeSpan.FromSeconds(4));
    Assert(guestV4.IsConnected, "host name falls back to IPv4 when ::1 is refused");
}
Assert(new HostConfig().BindAddress == "::", "server default listens on all interfaces");
Console.WriteLine("PASS: dual-stack listen host reachable over IPv4, IPv6 and DNS name");

Assert(!NetworkAddresses.IsPublic(IPAddress.Parse("192.168.1.10")) && !NetworkAddresses.IsPublic(IPAddress.Parse("100.72.1.1")) &&
    !NetworkAddresses.IsPublic(IPAddress.Parse("10.0.0.1")) && !NetworkAddresses.IsPublic(IPAddress.Parse("fd00::1")) &&
    !NetworkAddresses.IsPublic(IPAddress.Parse("fe80::1")) && NetworkAddresses.IsPublic(IPAddress.Parse("8.8.8.8")) &&
    NetworkAddresses.IsPublic(IPAddress.Parse("2a00:1450:4001::1")), "public address classification");
Assert(NetworkAddresses.Format(IPAddress.Parse("2a00::1"), 28491) == "[2a00::1]:28491" &&
    NetworkAddresses.Format(IPAddress.Parse("8.8.8.8"), 28491) == "8.8.8.8:28491", "address formatting");
NetworkAddresses.GetShareableAddresses();
Assert(UpnpPortMapping.ParseSsdpLocation("HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=120\r\nLocation: http://192.168.178.1:49000/igd.xml\r\n\r\n")
    ?.ToString() == "http://192.168.178.1:49000/igd.xml", "SSDP location parsed");
Assert(UpnpPortMapping.ParseSsdpLocation("NOTIFY * HTTP/1.1\r\nLOCATION: http://x/\r\n\r\n") == null, "SSDP notify ignored");
Console.WriteLine("PASS: address classification and SSDP parsing");

var igdCalls = new List<string>();
await using (var igd = FakeGateway.Start(igdCalls, "100.72.3.4", failAdd: false))
{
    var gatewayMapping = await UpnpPortMapping.CreateAsync(28491, TimeSpan.FromSeconds(3), igd.DescriptionUrl);
    Assert(gatewayMapping.ExternalAddress?.ToString() == "100.72.3.4" && !gatewayMapping.ExternalAddressIsPublic,
        "external address read and CGNAT detected");
    Assert(gatewayMapping.InternalAddress.Equals(IPAddress.Loopback), "internal client is the route towards the gateway");
    await gatewayMapping.RemoveAsync();
}
string add = igdCalls.Single(c => c.Contains("#AddPortMapping"));
Assert(add.Contains("<NewExternalPort>28491</NewExternalPort>") && add.Contains("<NewInternalClient>127.0.0.1</NewInternalClient>") &&
    add.Contains("<NewProtocol>TCP</NewProtocol>") && add.IndexOf("NewRemoteHost") < add.IndexOf("NewLeaseDuration"),
    "AddPortMapping arguments");
Assert(igdCalls.FindIndex(c => c.Contains("#GetExternalIPAddress")) < igdCalls.FindIndex(c => c.Contains("#AddPortMapping")),
    "external address read before mapping");
Assert(igdCalls.Any(c => c.Contains("#GetExternalIPAddress")) && igdCalls.Any(c => c.Contains("#DeletePortMapping")),
    "external address queried and mapping removed");
await using (var refusing = FakeGateway.Start(new List<string>(), "203.0.113.9", failAdd: true))
{
    try
    {
        await UpnpPortMapping.CreateAsync(28491, TimeSpan.FromSeconds(3), refusing.DescriptionUrl);
        throw new Exception("FAIL: refused mapping accepted");
    }
    catch (PortMappingException error)
    {
        Assert(error.Message.Contains("718"), "router error code reported");
        Assert(error.ExternalAddress?.ToString() == "203.0.113.9", "external address known although mapping failed");
    }
}
Console.WriteLine("PASS: UPnP port mapping against fake internet gateway");

// XFER-001: chunk transfers through the embedded listen host, the Steam-invite model of ADR 0013.
await using (var room = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Transfer Room", 4))
{
    LobbyView roomState = LobbyPayloads.ReadState((await ReceiveAsync(room.Client, MessageType.LobbyStateUpdate)).Payload);
    using LobbyClient tcpGuest = await LobbyClient.ConnectAsync("127.0.0.1", room.Port, "TcpGuest", TimeSpan.FromSeconds(4));
    using var steam = new SteamLikeLink();
    Assert(room.AttachConnection(NetworkConnection.FromDuplexStream(steam.A)), "host accepts Steam-like guest");
    using LobbyClient steamGuest = await LobbyClient.ConnectOverStreamAsync(steam.B, "SteamGuest", TimeSpan.FromSeconds(4));
    foreach (LobbyClient member in new[] { tcpGuest, steamGuest })
    {
        await member.SendAsync(MessageType.JoinLobby, 2, LobbyPayloads.GuidValue(roomState.Id));
        await ReceiveAsync(member, MessageType.LobbyStateUpdate);
    }

    byte[] big = new byte[50 * 1024 * 1024];
    new Random(42).NextBytes(big);
    string expected = Convert.ToHexString(SHA256.HashData(big));
    var clock = System.Diagnostics.Stopwatch.StartNew();
    byte[][] overTcp = await TransferAsync(room.Client, new[] { tcpGuest }, tcpGuest.SessionId, big, framePaced: false);
    Assert(Convert.ToHexString(SHA256.HashData(overTcp[0])) == expected, "TCP copy has the same SHA-256");
    Console.WriteLine($"PASS: 50 MB host to guest over TCP via the listen host, SHA-256 equal ({clock.Elapsed.TotalSeconds:F1} s)");

    clock.Restart();
    byte[][] overSteam = await TransferAsync(room.Client, new[] { steamGuest }, steamGuest.SessionId, big, framePaced: true);
    Assert(Convert.ToHexString(SHA256.HashData(overSteam[0])) == expected, "Steam-like copy has the same SHA-256");
    Assert(steam.Failure == null, "Steam-like link survived: " + steam.Failure);
    Assert(steam.MaxReceiveQueueBytes < SteamLikeLink.ReceiveQueueBytes, "receive queue stayed under 1 MiB");
    Assert(steam.MaxSendBufferBytes <= SteamLikeLink.SendBufferBytes, "send buffer never overflowed");
    Console.WriteLine($"PASS: 50 MB over Steam-like P2P, SHA-256 equal; peak send buffer {steam.MaxSendBufferBytes / 1024} of " +
        $"{SteamLikeLink.SendBufferBytes / 1024} KiB, peak receive queue {steam.MaxReceiveQueueBytes / 1024} of " +
        $"{SteamLikeLink.ReceiveQueueBytes / 1024} KiB ({clock.Elapsed.TotalSeconds:F1} s)");

    byte[] world = big.AsSpan(0, 8 * 1024 * 1024).ToArray();
    byte[][] fanOut = await TransferAsync(room.Client, new[] { tcpGuest, steamGuest }, Guid.Empty, world, framePaced: true);
    Assert(fanOut.All(copy => copy.AsSpan().SequenceEqual(world)), "both recipients received identical bytes");
    Assert(steam.Failure == null, "Steam-like link survived the fan-out");
    Console.WriteLine("PASS: one transfer reaches TCP and Steam-like recipients; the slowest sets the pace");

    // A recipient leaves mid-transfer: the sender is told, nothing hangs.
    var leaving = new TransferSender(TransferKind.World, tcpGuest.SessionId, world);
    await SendAllAsync(room.Client, leaving.Start(Environment.TickCount64));
    TransferReceiver? partial = null;
    using (var leaveDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
    {
        while (partial == null || partial.ReceivedChunks < 3)
        {
            leaveDeadline.Token.ThrowIfCancellationRequested();
            while (tcpGuest.TryDequeue(out WireMessage? message) && message != null)
            {
                if (message.Type == MessageType.TransferBegin) partial = new TransferReceiver(TransferPayloads.ReadBegin(message.Payload));
                else if (partial != null && partial.Owns(message)) await SendAllAsync(tcpGuest, partial.Handle(message));
            }
            while (room.Client.TryDequeue(out WireMessage? message) && message != null)
                if (leaving.Owns(message)) await SendAllAsync(room.Client, leaving.Handle(message, Environment.TickCount64));
            await Task.Delay(1, leaveDeadline.Token);
        }
    }
    await tcpGuest.SendAsync(MessageType.LeaveLobby, 3, Array.Empty<byte>());
    TransferAbort peerLeft = await ReceiveAbortAsync(room.Client, leaving.TransferId);
    Assert(peerLeft.Code == TransferAbortCode.PeerLeft, "sender learns that the recipient left");
    TransferAbort notMember = await StartAndExpectAbortAsync(room.Client, new TransferSender(TransferKind.World, tcpGuest.SessionId, world));
    Assert(notMember.Code == TransferAbortCode.Rejected && notMember.Reason.Contains("not in your lobby"), "recipient outside the lobby rejected");
    TransferAbort noLobby = await StartAndExpectAbortAsync(tcpGuest, new TransferSender(TransferKind.World, Guid.Empty, world));
    Assert(noLobby.Code == TransferAbortCode.Rejected, "sender outside any lobby rejected");
    Console.WriteLine("PASS: leaving mid-transfer aborts for the sender; senders and recipients outside the lobby are rejected");

    // A misbehaving sender cannot push past the window, and gets only one outgoing transfer at a time.
    await tcpGuest.SendAsync(MessageType.JoinLobby, 4, LobbyPayloads.GuidValue(roomState.Id));
    await ReceiveAsync(tcpGuest, MessageType.LobbyStateUpdate);
    Guid rogue = Guid.NewGuid();
    ulong rogueLength = 20UL * TransferPayloads.ChunkSize;
    await room.Client.SendAsync(MessageType.TransferBegin, 0, TransferPayloads.Begin(new TransferBegin(rogue, TransferKind.World,
        tcpGuest.SessionId, rogueLength, TransferPayloads.ChunkCountFor(rogueLength), new byte[32])));
    for (uint index = 0; index <= TransferPayloads.MaximumWindowChunks; index++)
        await room.Client.SendAsync(MessageType.TransferChunk, 0, TransferPayloads.Chunk(
            new TransferChunk(rogue, index, new byte[TransferPayloads.ChunkSize])));
    TransferAbort overrun = await ReceiveAbortAsync(room.Client, rogue);
    Assert(overrun.Code == TransferAbortCode.Rejected && overrun.Reason.Contains("window"), "window overrun rejected");
    var first = new TransferSender(TransferKind.World, tcpGuest.SessionId, world);
    await SendAllAsync(room.Client, first.Start(Environment.TickCount64).Take(1));
    TransferAbort second = await StartAndExpectAbortAsync(room.Client, new TransferSender(TransferKind.World, tcpGuest.SessionId, world));
    Assert(second.Reason.Contains("still running"), "second concurrent transfer from one sender rejected");
    await SendAllAsync(room.Client, first.Cancel("test done"));
    Console.WriteLine("PASS: server rejects a window overrun and a second concurrent transfer");
}

static async Task<byte[][]> TransferAsync(LobbyClient from, LobbyClient[] to, Guid recipient, byte[] data, bool framePaced)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    var sender = new TransferSender(TransferKind.Game, recipient, data);
    Task<byte[]>[] receiving = to.Select(client => ReceiveTransferAsync(client, framePaced, deadline.Token)).ToArray();
    await SendAllAsync(from, sender.Start(Environment.TickCount64));
    while (sender.State == TransferState.Active)
    {
        deadline.Token.ThrowIfCancellationRequested();
        bool idle = true;
        while (from.TryDequeue(out WireMessage? message) && message != null)
            if (sender.Owns(message)) { idle = false; await SendAllAsync(from, sender.Handle(message, Environment.TickCount64)); }
        await SendAllAsync(from, sender.Poll(Environment.TickCount64));
        if (idle) await Task.Delay(1, deadline.Token);
    }
    Assert(sender.State == TransferState.Completed, "sender completed: " + sender.Failure?.Reason);
    return await Task.WhenAll(receiving);
}

// Like the game: drain the client queue once per frame, acknowledge only what was consumed.
static async Task<byte[]> ReceiveTransferAsync(LobbyClient client, bool framePaced, CancellationToken token)
{
    TransferReceiver? receiver = null;
    while (receiver?.State != TransferState.Completed)
    {
        while (client.TryDequeue(out WireMessage? message) && message != null)
        {
            if (message.Type == MessageType.TransferBegin && receiver == null)
                receiver = new TransferReceiver(TransferPayloads.ReadBegin(message.Payload));
            else if (receiver != null && receiver.Owns(message))
                await SendAllAsync(client, receiver.Handle(message));
            if (receiver?.State == TransferState.Failed) throw new Exception("receiver failed: " + receiver.Failure!.Reason);
        }
        await Task.Delay(framePaced ? 16 : 1, token);
    }
    return receiver.Data!;
}

static async Task SendAllAsync(LobbyClient client, IEnumerable<WireMessage> messages)
{
    foreach (WireMessage message in messages) await client.SendAsync(message.Type, message.RequestId, message.Payload);
}

static async Task<TransferAbort> StartAndExpectAbortAsync(LobbyClient client, TransferSender sender)
{
    await SendAllAsync(client, sender.Start(Environment.TickCount64).Take(1));
    return await ReceiveAbortAsync(client, sender.TransferId);
}

static async Task<TransferAbort> ReceiveAbortAsync(LobbyClient client, Guid transferId)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    while (true)
    {
        while (client.TryDequeue(out WireMessage? message) && message != null)
            if (message.Type == MessageType.TransferAbort && TransferPayloads.ReadTransferId(message.Payload) == transferId)
                return TransferPayloads.ReadAbort(message.Payload);
        await Task.Delay(10, timeout.Token);
    }
}

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
