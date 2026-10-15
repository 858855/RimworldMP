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

// ID-001: persistent player key, verified over Steam-like links, unique per connected player.
PlayerKey hostKey = PlayerKey.Steam(76561198000000010UL);
PlayerKey friendKey = PlayerKey.Steam(76561198000000020UL);
await using (var keyed = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Key Room", 4, hostKey))
{
    LobbyView keyRoom = LobbyPayloads.ReadState((await ReceiveAsync(keyed.Client, MessageType.LobbyStateUpdate)).Payload);
    Assert(keyed.Client.PlayerKey == hostKey && keyRoom.Players.Single().Key == hostKey, "host joins with its own key");

    // Steam authenticated friendKey for this link; the friend claims the same.
    using var honestLink = new SteamLikeLink();
    Assert(keyed.AttachConnection(NetworkConnection.FromDuplexStream(honestLink.A), friendKey), "verified link attached");
    using LobbyClient friend = await LobbyClient.ConnectOverStreamAsync(honestLink.B, "Friend", TimeSpan.FromSeconds(4),
        CancellationToken.None, friendKey);
    await friend.SendAsync(MessageType.JoinLobby, 2, LobbyPayloads.GuidValue(keyRoom.Id));
    LobbyView withFriend = LobbyPayloads.ReadState((await ReceiveAsync(keyed.Client, MessageType.LobbyStateUpdate)).Payload);
    Assert(withFriend.Players.Single(p => p.Name == "Friend").Key == friendKey, "host sees the friend's SteamID");

    // A guest Steam verified as someone else claims the host's SteamID: rejected.
    using var spoofLink = new SteamLikeLink();
    Assert(keyed.AttachConnection(NetworkConnection.FromDuplexStream(spoofLink.A), PlayerKey.Steam(76561198000000099UL)), "spoof link attached");
    ProtocolException spoof = await ExpectAsync<ProtocolException>(() => LobbyClient.ConnectOverStreamAsync(spoofLink.B, "Mallory",
        TimeSpan.FromSeconds(4), CancellationToken.None, hostKey));
    Assert(spoof.Message.Contains("verified identity"), "claim that differs from the Steam identity is rejected");

    // The same player cannot be connected twice; once gone, the key is free again.
    ProtocolException twice = await ExpectAsync<ProtocolException>(() => LobbyClient.ConnectAsync("127.0.0.1", keyed.Port, "FriendAgain",
        TimeSpan.FromSeconds(4), CancellationToken.None, friendKey));
    Assert(twice.Message.Contains("already connected"), "second connection with the same key rejected");
    await friend.DisconnectAsync();
    // Must free within a second; before the per-connection heartbeat stop it took up to one 5 s heartbeat.
    var freed = System.Diagnostics.Stopwatch.StartNew();
    LobbyClient? again = null;
    for (int attempt = 0; attempt < 20 && again == null; attempt++)
    {
        try { again = await LobbyClient.ConnectAsync("127.0.0.1", keyed.Port, "FriendAgain", TimeSpan.FromSeconds(4), CancellationToken.None, friendKey); }
        catch (ProtocolException) { await Task.Delay(50); }
    }
    Assert(again != null && again.PlayerKey == friendKey && freed.Elapsed < TimeSpan.FromSeconds(1.5),
        "key usable again within a second of disconnect");
    again!.Dispose();
}
Console.WriteLine("PASS: player key reaches the lobby; Steam-verified identity cannot be spoofed; one connection per player");

// ADR 0015/0017 end to end: a Steam guest tries every host-only step and is refused by the host's server.
await using (var setupHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Setup Room", 4, PlayerKey.Steam(76561198000000030UL)))
{
    LobbyView setupRoom = LobbyPayloads.ReadState((await ReceiveAsync(setupHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using var setupLink = new SteamLikeLink();
    PlayerKey setupGuestKey = PlayerKey.Steam(76561198000000040UL);
    Assert(setupHost.AttachConnection(NetworkConnection.FromDuplexStream(setupLink.A), setupGuestKey), "setup guest attached");
    using LobbyClient setupGuest = await LobbyClient.ConnectOverStreamAsync(setupLink.B, "Guest", TimeSpan.FromSeconds(4),
        CancellationToken.None, setupGuestKey);
    await setupGuest.SendAsync(MessageType.JoinLobby, 2, LobbyPayloads.GuidValue(setupRoom.Id));
    await ReceiveAsync(setupGuest, MessageType.LobbyStateUpdate);
    LobbyClient hostClient = setupHost.Client;
    uint request = 100;

    Assert(await ErrorCodeAsync(setupGuest, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Scenario)) == 3, "guest cannot start");
    Assert(await ErrorCodeAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Scenario)) == 6, "start refused before everyone is ready");
    await StateAsync(hostClient, MessageType.PlayerReadyChanged, ++request, LobbyPayloads.Bool(true));
    await StateAsync(setupGuest, MessageType.PlayerReadyChanged, ++request, LobbyPayloads.Bool(true));
    LobbyView started = await StateAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Scenario));
    LobbyView guestSeesStart = await LatestStateAsync(setupGuest, started.Revision);
    Assert(started.Phase == SetupPhase.Scenario && guestSeesStart.Phase == SetupPhase.Scenario, "both see the setup start");

    using (LobbyClient latecomer = await LobbyClient.ConnectAsync("127.0.0.1", setupHost.Port, "Late", TimeSpan.FromSeconds(4)))
        Assert(await ErrorCodeAsync(latecomer, MessageType.JoinLobby, 1, LobbyPayloads.GuidValue(setupRoom.Id)) == 6, "joining during setup refused");

    foreach (SetupPhase next in new[] { SetupPhase.Storyteller, SetupPhase.WorldParams, SetupPhase.WorldTransfer })
        await StateAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(next));
    byte[] rulesWorld = Enumerable.Repeat((byte)5, 32).ToArray();
    await StateAsync(hostClient, MessageType.WorldReady, ++request, SetupPayloads.Fingerprint(rulesWorld));
    await StateAsync(setupGuest, MessageType.WorldLoaded, ++request, SetupPayloads.Fingerprint(rulesWorld));
    await StateAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.SiteSelection));
    var suggestion = new SiteTile(4242, 0);
    LobbyView guestFlag = await StateAsync(setupGuest, MessageType.SiteFlag, ++request, SetupPayloads.Flag(suggestion));
    LobbyView hostSeesFlag = await LatestStateAsync(hostClient, guestFlag.Revision);
    Assert(hostSeesFlag.Players.Single(p => p.Key == setupGuestKey).Flag == suggestion, "host sees the guest's flag");
    Assert(await ErrorCodeAsync(setupGuest, MessageType.SiteDecided, ++request, SetupPayloads.Site(suggestion)) == 3, "guest cannot choose the site");
    LobbyView decided = await StateAsync(hostClient, MessageType.SiteDecided, ++request, SetupPayloads.Site(suggestion));
    Assert(decided.Phase == SetupPhase.Ideology && decided.Site == suggestion, "host chose the flagged site");
    await StateAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Characters));
    await StateAsync(hostClient, MessageType.PlayerReadyChanged, ++request, LobbyPayloads.Bool(true));
    Assert(await ErrorCodeAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Generating)) == 6,
        "host cannot continue while the guest is still choosing");
    await StateAsync(setupGuest, MessageType.PlayerReadyChanged, ++request, LobbyPayloads.Bool(true));
    LobbyView generating = await StateAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Generating));
    Assert(generating.Phase == SetupPhase.Generating, "everyone done: setup continues");
    Assert(await ErrorCodeAsync(setupGuest, MessageType.SetupAbort, ++request, Array.Empty<byte>()) == 3, "guest cannot abort");
    LobbyView reset = await StateAsync(hostClient, MessageType.SetupAbort, ++request, Array.Empty<byte>());
    Assert(reset.Phase == SetupPhase.Lobby && reset.Site == null, "host abort returns to the lobby");
}
Console.WriteLine("PASS: setup rules hold over the network: guest refused for host steps, start and characters wait for everyone");

// MOD-001 end to end over a Steam-like link: status in the snapshot, Ready refused, host's list requested and transferred.
await using (var modHost = await ListenHost.StartAsync("127.0.0.1", 0, "ModHost", "Mod Room", 4, PlayerKey.Steam(76561198000000050UL)))
{
    LobbyView modRoom = LobbyPayloads.ReadState((await ReceiveAsync(modHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using var modLink = new SteamLikeLink();
    PlayerKey modGuestKey = PlayerKey.Steam(76561198000000060UL);
    Assert(modHost.AttachConnection(NetworkConnection.FromDuplexStream(modLink.A), modGuestKey), "mod guest attached");
    using LobbyClient modGuest = await LobbyClient.ConnectOverStreamAsync(modLink.B, "ModGuest", TimeSpan.FromSeconds(4),
        CancellationToken.None, modGuestKey);
    var hostList = new ModManifest("1.6.4871 rev591", new[] { new ModEntry("ludeon.rimworld", "Core", "", Enumerable.Repeat((byte)1, 32).ToArray()),
        new ModEntry("some.mod", "Some Mod", "1.0", Enumerable.Repeat((byte)2, 32).ToArray()) });
    var guestList = new ModManifest("1.6.4871 rev590", hostList.Mods.Take(1).ToArray());
    uint req = 500;
    await modHost.Client.SendAsync(MessageType.ModManifestSummary, ++req, hostList.Summary().ToPayload());
    await modGuest.SendAsync(MessageType.ModManifestSummary, ++req, guestList.Summary().ToPayload());
    LobbyView joined = await StateAsync(modGuest, MessageType.JoinLobby, ++req, LobbyPayloads.GuidValue(modRoom.Id));
    Assert(joined.Players.Single(p => p.Key == modGuestKey).ModStatus == ModStatus.Mismatch, "guest marked as mismatch");
    Assert(await ErrorCodeAsync(modGuest, MessageType.PlayerReadyChanged, ++req, LobbyPayloads.Bool(true)) == 8, "ready refused on mismatch");
    await modGuest.SendAsync(MessageType.ModManifestRequest, ++req, LobbyPayloads.GuidValue(modRoom.HostId));
    WireMessage asked = await ReceiveAsync(modHost.Client, MessageType.ModManifestRequest);
    Guid requester = LobbyPayloads.ReadGuid(asked.Payload);
    Assert(requester == joined.Players.Single(p => p.Key == modGuestKey).Id, "request reaches the host with the requester");
    byte[][] arrived = await TransferAsync(modHost.Client, new[] { modGuest }, requester, hostList.Serialize(), true);
    var diff = ModManifestDiff.Compare(guestList, ModManifest.Deserialize(arrived[0]));
    Assert(diff.MissingLocally.Single().PackageId == "some.mod" && diff.GameVersionDiffers, "guest sees what is missing");
    await modGuest.SendAsync(MessageType.ModManifestSummary, ++req, new ModManifest("1.6.4871 rev590", hostList.Mods).Summary().ToPayload());
    LobbyView fixedUp = await LatestStateAsync(modGuest, joined.Revision + 1);
    Assert(fixedUp.Players.All(p => p.ModStatus == ModStatus.Match), "same mods on another game build match");
}
Console.WriteLine("PASS: mod check over a Steam-like link: status, ready gate, host list request and diff (MOD-001)");

// SETUP-001: the host's page mirror reaches guests only while setup runs, and only from the host.
await using (var mirrorHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Mirror Room", 4, PlayerKey.Steam(76561198000000070UL)))
{
    LobbyView mirrorRoom = LobbyPayloads.ReadState((await ReceiveAsync(mirrorHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using LobbyClient mirrorGuest = await LobbyClient.ConnectAsync("127.0.0.1", mirrorHost.Port, "Guest", TimeSpan.FromSeconds(4));
    uint m = 700;
    await StateAsync(mirrorGuest, MessageType.JoinLobby, ++m, LobbyPayloads.GuidValue(mirrorRoom.Id));
    var page = new SetupSettings("Erzähler", new[] { ("Erzähler", "Cassandra Classic"), ("Schwierigkeit", "Strive to survive") });
    Assert(await ErrorCodeAsync(mirrorHost.Client, MessageType.SetupSettings, ++m, page.ToPayload()) == 6, "no mirror before setup");
    await StateAsync(mirrorHost.Client, MessageType.PlayerReadyChanged, ++m, LobbyPayloads.Bool(true));
    await StateAsync(mirrorGuest, MessageType.PlayerReadyChanged, ++m, LobbyPayloads.Bool(true));
    await StateAsync(mirrorHost.Client, MessageType.SetupAdvance, ++m, SetupPayloads.Advance(SetupPhase.Scenario));
    await mirrorHost.Client.SendAsync(MessageType.SetupSettings, ++m, page.ToPayload());
    SetupSettings mirrored = SetupSettings.Read((await ReceiveAsync(mirrorGuest, MessageType.SetupSettings)).Payload);
    Assert(mirrored.SameAs(page), "guest sees the host's page");
    Assert(await ErrorCodeAsync(mirrorGuest, MessageType.SetupSettings, ++m, page.ToPayload()) == 3, "guests cannot send a mirror");
}
Console.WriteLine("PASS: host page mirror reaches guests during setup only, host only (SETUP-001)");

// Debug option end to end: a listen host started with allowSoloStart may start the setup alone.
await using (var soloHost = await ListenHost.StartAsync("127.0.0.1", 0, "Solo", "Solo Room", 2, PlayerKey.Steam(76561198000000080UL), true))
{
    await ReceiveAsync(soloHost.Client, MessageType.LobbyStateUpdate);
    Assert(soloHost.AllowSoloStart, "host reports the option");
    await StateAsync(soloHost.Client, MessageType.PlayerReadyChanged, 900, LobbyPayloads.Bool(true));
    LobbyView soloStarted = await StateAsync(soloHost.Client, MessageType.SetupAdvance, 901, SetupPayloads.Advance(SetupPhase.Scenario));
    Assert(soloStarted.Phase == SetupPhase.Scenario && soloStarted.Players.Count == 1, "host started alone");
}
await using (var normalHost = await ListenHost.StartAsync("127.0.0.1", 0, "Solo", "Normal Room", 2, PlayerKey.Steam(76561198000000081UL)))
{
    await ReceiveAsync(normalHost.Client, MessageType.LobbyStateUpdate);
    await StateAsync(normalHost.Client, MessageType.PlayerReadyChanged, 902, LobbyPayloads.Bool(true));
    Assert(await ErrorCodeAsync(normalHost.Client, MessageType.SetupAdvance, 903, SetupPayloads.Advance(SetupPhase.Scenario)) == 6,
        "without the option one player cannot start");
}
Console.WriteLine("PASS: debug solo start over a listen host, refused without the option");

// SETUP-002 end to end over a Steam-like link: the host announces the fingerprint, sends the world bytes with the
// chunk channel, the guest reports what it loaded; site selection opens only on a match.
await using (var worldHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "World Room", 4, PlayerKey.Steam(76561198000000090UL)))
{
    LobbyView worldRoom = LobbyPayloads.ReadState((await ReceiveAsync(worldHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using var worldLink = new SteamLikeLink();
    PlayerKey worldGuestKey = PlayerKey.Steam(76561198000000091UL);
    Assert(worldHost.AttachConnection(NetworkConnection.FromDuplexStream(worldLink.A), worldGuestKey), "world guest attached");
    using LobbyClient worldGuest = await LobbyClient.ConnectOverStreamAsync(worldLink.B, "Guest", TimeSpan.FromSeconds(4),
        CancellationToken.None, worldGuestKey);
    LobbyClient worldHostClient = worldHost.Client;
    uint w = 1000;
    await StateAsync(worldGuest, MessageType.JoinLobby, ++w, LobbyPayloads.GuidValue(worldRoom.Id));
    await StateAsync(worldHostClient, MessageType.PlayerReadyChanged, ++w, LobbyPayloads.Bool(true));
    await StateAsync(worldGuest, MessageType.PlayerReadyChanged, ++w, LobbyPayloads.Bool(true));
    foreach (SetupPhase next in new[] { SetupPhase.Scenario, SetupPhase.Storyteller, SetupPhase.WorldParams, SetupPhase.WorldTransfer })
        await StateAsync(worldHostClient, MessageType.SetupAdvance, ++w, SetupPayloads.Advance(next));
    byte[] worldBytes = new byte[3 * 1024 * 1024 + 17];
    new Random(11).NextBytes(worldBytes);
    byte[] worldPrint = new FingerprintBuilder().Add("world").Add(worldBytes.Length).Finish();
    Assert(await ErrorCodeAsync(worldGuest, MessageType.WorldReady, ++w, SetupPayloads.Fingerprint(worldPrint)) == 3, "guest cannot announce a world");
    LobbyView announced = await StateAsync(worldHostClient, MessageType.WorldReady, ++w, SetupPayloads.Fingerprint(worldPrint));
    Assert(announced.Players.Single(p => p.Key == worldGuestKey).WorldStatus == WorldStatus.Waiting, "guest waits for the world");
    Assert(await ErrorCodeAsync(worldHostClient, MessageType.SetupAdvance, ++w, SetupPayloads.Advance(SetupPhase.SiteSelection)) == 6,
        "site selection waits for the guest");
    byte[][] receivedWorld = await TransferAsync(worldHostClient, new[] { worldGuest }, Guid.Empty, worldBytes, true);
    Assert(receivedWorld[0].AsSpan().SequenceEqual(worldBytes), "world bytes arrived intact");
    byte[] wrongPrint = new FingerprintBuilder().Add("other").Finish();
    await StateAsync(worldGuest, MessageType.WorldLoaded, ++w, SetupPayloads.Fingerprint(wrongPrint));
    Assert(await ErrorCodeAsync(worldHostClient, MessageType.SetupAdvance, ++w, SetupPayloads.Advance(SetupPhase.SiteSelection)) == 8,
        "a different fingerprint blocks site selection");
    LobbyView matched = await StateAsync(worldGuest, MessageType.WorldLoaded, ++w, SetupPayloads.Fingerprint(worldPrint));
    Assert(matched.Players.All(p => p.WorldStatus == WorldStatus.Match), "both match");
    LobbyView siteSelection = await StateAsync(worldHostClient, MessageType.SetupAdvance, ++w, SetupPayloads.Advance(SetupPhase.SiteSelection));
    Assert(siteSelection.Phase == SetupPhase.SiteSelection, "site selection opens once every fingerprint matches");
}
Console.WriteLine("PASS: world transfer and fingerprint check gate site selection (SETUP-002)");

// Sends a request and returns the error code the server answers with.
static async Task<byte> ErrorCodeAsync(LobbyClient client, MessageType type, uint requestId, byte[] payload)
{
    await client.SendAsync(type, requestId, payload);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (true)
    {
        while (client.TryDequeue(out WireMessage? message) && message != null)
        {
            if (message.RequestId != requestId) continue;
            if (message.Type == MessageType.Error) return message.Payload[0];
            throw new Exception($"FAIL: expected an error for {type}, got {message.Type}");
        }
        await Task.Delay(10, timeout.Token);
    }
}

// Sends a request and returns the lobby state the server answers this request with.
static async Task<LobbyView> StateAsync(LobbyClient client, MessageType type, uint requestId, byte[] payload)
{
    await client.SendAsync(type, requestId, payload);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (true)
    {
        while (client.TryDequeue(out WireMessage? message) && message != null)
        {
            if (message.RequestId != requestId) continue;
            if (message.Type == MessageType.LobbyStateUpdate) return LobbyPayloads.ReadState(message.Payload);
            if (message.Type == MessageType.Error)
                throw new Exception($"FAIL: {type} refused with code {message.Payload[0]}");
        }
        await Task.Delay(10, timeout.Token);
    }
}

// Waits until this client has seen a lobby state at least as new as the given revision.
static async Task<LobbyView> LatestStateAsync(LobbyClient client, ulong revision)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (true)
    {
        while (client.TryDequeue(out WireMessage? message) && message != null)
            if (message.Type == MessageType.LobbyStateUpdate)
            {
                LobbyView state = LobbyPayloads.ReadState(message.Payload);
                if (state.Revision >= revision) return state;
            }
        await Task.Delay(10, timeout.Token);
    }
}

static async Task<T> ExpectAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T error) { return error; }
    throw new Exception("Expected " + typeof(T).Name);
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
