using System.Net;
using System.Net.Sockets;
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
