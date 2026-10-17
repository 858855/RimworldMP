using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Multiplayer.Foundation.Hosting;
using Multiplayer.Foundation.Lobby;
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
    await StateAsync(hostClient, MessageType.PawnRoster, ++request, RosterFor(PlayerKey.Steam(76561198000000030UL), setupGuestKey).ToPayload());
    await StateAsync(hostClient, MessageType.PlayerReadyChanged, ++request, LobbyPayloads.Bool(true));
    Assert(await ErrorCodeAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Generating)) == 6,
        "host cannot continue while the guest is still choosing");
    await StateAsync(setupGuest, MessageType.PlayerReadyChanged, ++request, LobbyPayloads.Bool(true));
    LobbyView generating = await StateAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Generating));
    Assert(generating.Phase == SetupPhase.Generating, "everyone done: setup continues");
    byte[] rulesGame = Enumerable.Repeat((byte)3, 32).ToArray();
    Assert(await ErrorCodeAsync(setupGuest, MessageType.GameReady, ++request, SetupPayloads.Fingerprint(rulesGame)) == 3, "only the host sends the game");
    await StateAsync(hostClient, MessageType.GameReady, ++request, SetupPayloads.Fingerprint(rulesGame));
    Assert(await ErrorCodeAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Playing)) == 6,
        "landing waits for the guest's game");
    LobbyView guestLoaded = await StateAsync(setupGuest, MessageType.GameLoaded, ++request, SetupPayloads.Fingerprint(rulesGame));
    Assert(guestLoaded.Players.All(p => p.WorldStatus == WorldStatus.Match), "guest loaded the same game");
    LobbyView landed = await StateAsync(hostClient, MessageType.SetupAdvance, ++request, SetupPayloads.Advance(SetupPhase.Playing));
    Assert(landed.Phase == SetupPhase.Playing, "everyone loaded the game: landed (SETUP-005)");
    Assert(await ErrorCodeAsync(setupGuest, MessageType.SetupAbort, ++request, Array.Empty<byte>()) == 3, "guest cannot abort");
    LobbyView reset = await StateAsync(hostClient, MessageType.SetupAbort, ++request, Array.Empty<byte>());
    Assert(reset.Phase == SetupPhase.Lobby && reset.Site == null, "host abort returns to the lobby");
}
Console.WriteLine("PASS: setup rules hold over the network: guest refused for host steps, start, characters and landing wait for everyone");

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

// SYNC-001/DESYNC-001 (ADR 0030) end to end: after landing, the host's embedded coordinator drives lockstep over a
// Steam-like link. Both games run ticks only within the horizon and execute the guest's command at the same point.
PlayerKey lockHostKey = PlayerKey.Steam(76561198000000070UL), lockGuestKey = PlayerKey.Steam(76561198000000080UL);
await using (var lockHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Lockstep Room", 4, lockHostKey))
{
    LobbyView lockRoom = LobbyPayloads.ReadState((await ReceiveAsync(lockHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using var lockLink = new SteamLikeLink();
    Assert(lockHost.AttachConnection(NetworkConnection.FromDuplexStream(lockLink.A), lockGuestKey), "lockstep guest attached");
    using LobbyClient lockGuest = await LobbyClient.ConnectOverStreamAsync(lockLink.B, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, lockGuestKey);
    await lockGuest.SendAsync(MessageType.JoinLobby, 2, LobbyPayloads.GuidValue(lockRoom.Id));
    await ReceiveAsync(lockGuest, MessageType.LobbyStateUpdate);
    await ReachPlayingAsync(lockHost.Client, lockGuest, lockHostKey, lockGuestKey);

    var games = new[] { (Client: lockHost.Client, Game: new LockstepClient()), (Client: lockGuest, Game: new LockstepClient()) };
    var completed = new int[2];
    var executed = new[] { new List<(int Boundary, ulong Sequence, PlayerKey? Issuer)>(), new List<(int Boundary, ulong Sequence, PlayerKey? Issuer)>() };
    var errors = new List<byte>[] { new(), new() };
    int desyncs = 0;
    async Task Pump(int ticksPerRound)
    {
        for (int i = 0; i < 2; i++)
        {
            var (client, game) = games[i];
            while (client.TryDequeue(out WireMessage? message) && message != null)
            {
                if (LockstepClient.Handles(message)) { game.Receive(message); if (message.Type == MessageType.DesyncDetected) desyncs++; }
                else if (message.Type == MessageType.Error) errors[i].Add(message.Payload[0]);
            }
            foreach (ScheduledCommand command in game.TakeCommandsAt(completed[i])) executed[i].Add((command.Boundary, command.Sequence, command.IssuerKey));
            for (int t = 0; t < ticksPerRound && game.MayRunTick(completed[i]); t++)
            {
                completed[i]++;
                foreach (ScheduledCommand command in game.TakeCommandsAt(completed[i])) executed[i].Add((command.Boundary, command.Sequence, command.IssuerKey));
            }
            if (game.ProgressMessage(completed[i], Environment.TickCount64) is WireMessage progress)
                await client.SendAsync(progress.Type, 0, progress.Payload);
        }
    }
    async Task RunFor(int milliseconds, int ticksPerRound)
    {
        var until = System.Diagnostics.Stopwatch.StartNew();
        while (until.ElapsedMilliseconds < milliseconds) { await Pump(ticksPerRound); await Task.Delay(16); }
    }

    await RunFor(300, 50);
    Assert(completed.All(c => c == 0) && games.All(g => g.Game.Paused), "session starts paused at tick 0");
    await lockGuest.SendAsync(MessageType.Command, 0, LockstepPayloads.Command(new byte[] { 42 }));        // order given in pause
    await RunFor(300, 50);
    Assert(executed.All(log => log.Count == 1 && log[0].Boundary == 0 && log[0].Issuer == lockGuestKey),
        "order given in pause runs at once in both games, issuer stamped by the coordinator");
    await lockGuest.SendAsync(MessageType.SessionControl, 0, LockstepPayloads.Control(ScheduledKind.Speed, 3));
    await lockGuest.SendAsync(MessageType.SessionControl, 0, LockstepPayloads.Control(ScheduledKind.Pause, 0));   // anyone may resume
    await RunFor(1500, 4);                                                                                       // guest computes slowly
    Assert(errors[1].Contains(3), "guest cannot change the speed");
    Assert(completed.All(c => c > 30), "time runs after resuming: " + string.Join(",", completed));
    Assert(games.All(g => !g.Game.Paused), "both games resumed at the same point");
    await lockHost.Client.SendAsync(MessageType.Command, 0, LockstepPayloads.Command(new byte[] { 7 }));
    await lockGuest.SendAsync(MessageType.Command, 0, LockstepPayloads.Command(new byte[] { 8 }));
    await RunFor(800, 4);
    int common = completed.Min();
    Assert(executed[0].Where(e => e.Boundary < common).SequenceEqual(executed[1].Where(e => e.Boundary < common)) && executed[0].Count >= 3,
        "both games executed the same commands at the same ticks: " + executed[0].Count);

    // A diverging state is caught and stops time for both at one point.
    int hashTick = (Math.Min(completed[0], completed[1]) / 60) * 60;
    Assert(hashTick > 0, "a hash tick was reached");
    await lockHost.Client.SendAsync(MessageType.StateHash, 0, LockstepPayloads.Hash(hashTick, 111));
    await lockGuest.SendAsync(MessageType.StateHash, 0, LockstepPayloads.Hash(hashTick, 222));
    await RunFor(1000, 50);
    Assert(desyncs == 2 && games.All(g => g.Game.Desyncs.Single().Sessions.Single() == lockGuest.SessionId), "both games told the guest diverged");
    Assert(games.All(g => g.Game.Paused) && completed[0] == completed[1], "time stopped for both at the same tick: " + string.Join(",", completed));
}
Console.WriteLine("PASS: lockstep over the network: pause orders act at once, slow guest sets the pace, same commands at the same ticks, desync stops both");

// ADR 0031 over the network: the host picks Factions once; a Steam guest sees it in the list before joining.
await using (var factionHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Faction Room", 4, PlayerKey.Steam(76561198000000090UL),
    mode: LobbyMode.Factions(FactionRelation.Hostile)))
{
    LobbyView factionRoom = LobbyPayloads.ReadState((await ReceiveAsync(factionHost.Client, MessageType.LobbyStateUpdate)).Payload);
    Assert(factionRoom.Mode == LobbyMode.Factions(FactionRelation.Hostile), "host's lobby carries the chosen mode");
    using var modeLink = new SteamLikeLink();
    PlayerKey modeGuestKey = PlayerKey.Steam(76561198000000091UL);
    Assert(factionHost.AttachConnection(NetworkConnection.FromDuplexStream(modeLink.A), modeGuestKey), "mode guest attached");
    using LobbyClient modeGuest = await LobbyClient.ConnectOverStreamAsync(modeLink.B, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, modeGuestKey);
    await modeGuest.SendAsync(MessageType.LobbyListRequest, 700, Array.Empty<byte>());
    var modeList = LobbyPayloads.ReadList((await ReceiveAsync(modeGuest, MessageType.LobbyListResponse)).Payload);
    Assert(modeList.Single().Mode == LobbyMode.Factions(FactionRelation.Hostile), "guest sees Factions/Hostile before joining");
    LobbyView modeJoined = await StateAsync(modeGuest, MessageType.JoinLobby, 701, LobbyPayloads.GuidValue(factionRoom.Id));
    Assert(modeJoined.Mode == factionRoom.Mode, "joined state keeps the mode");
    await StateAsync(factionHost.Client, MessageType.PlayerReadyChanged, 702, LobbyPayloads.Bool(true));
    await StateAsync(modeGuest, MessageType.PlayerReadyChanged, 703, LobbyPayloads.Bool(true));
    Assert(await ErrorCodeAsync(factionHost.Client, MessageType.SetupAdvance, 704, SetupPayloads.Advance(SetupPhase.Scenario)) == 6,
        "Factions setup refused until it exists");
}
Console.WriteLine("PASS: game mode over the network: chosen once by the host, visible before joining, Factions start refused for now");

// SAVE-001/JOIN-001 (ADR 0032) end to end: a third player joins the running game over a Steam-like link. A player with
// other mods is refused; with matching mods the host is told to save a snapshot for them, the joiner continues with the
// next command, catches up and goes live.
var joinMods = new ModManifestSummary(Enumerable.Repeat((byte)7, 32).ToArray(), "1.6.4871 rev600", 3);
var otherMods = new ModManifestSummary(Enumerable.Repeat((byte)8, 32).ToArray(), "1.6.4871 rev600", 3);
PlayerKey jHostKey = PlayerKey.Steam(76561198000000100UL), jGuestKey = PlayerKey.Steam(76561198000000101UL), jLateKey = PlayerKey.Steam(76561198000000102UL);
await using (var joinHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Join Room", 4, jHostKey))
{
    LobbyView joinRoom = LobbyPayloads.ReadState((await ReceiveAsync(joinHost.Client, MessageType.LobbyStateUpdate)).Payload);
    await joinHost.Client.SendAsync(MessageType.ModManifestSummary, 0, joinMods.ToPayload());
    using var guestLink = new SteamLikeLink();
    Assert(joinHost.AttachConnection(NetworkConnection.FromDuplexStream(guestLink.A), jGuestKey), "join-test guest attached");
    using LobbyClient jGuest = await LobbyClient.ConnectOverStreamAsync(guestLink.B, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, jGuestKey);
    await jGuest.SendAsync(MessageType.ModManifestSummary, 0, joinMods.ToPayload());
    await StateAsync(jGuest, MessageType.JoinLobby, 800, LobbyPayloads.GuidValue(joinRoom.Id));
    await ReachPlayingAsync(joinHost.Client, jGuest, jHostKey, jGuestKey);

    using (LobbyClient wrongMods = await LobbyClient.ConnectAsync("127.0.0.1", joinHost.Port, "Wrong", TimeSpan.FromSeconds(4), CancellationToken.None, PlayerKey.Steam(76561198000000103UL)))
    {
        await wrongMods.SendAsync(MessageType.ModManifestSummary, 0, otherMods.ToPayload());
        Assert(await ErrorCodeAsync(wrongMods, MessageType.JoinLobby, 801, LobbyPayloads.GuidValue(joinRoom.Id)) == 8, "joining a running game with other mods refused");
    }

    using var lateLink = new SteamLikeLink();
    Assert(joinHost.AttachConnection(NetworkConnection.FromDuplexStream(lateLink.A), jLateKey), "late joiner attached");
    using LobbyClient jLate = await LobbyClient.ConnectOverStreamAsync(lateLink.B, "Late", TimeSpan.FromSeconds(4), CancellationToken.None, jLateKey);
    await jLate.SendAsync(MessageType.ModManifestSummary, 0, joinMods.ToPayload());
    LobbyView lateJoined = await StateAsync(jLate, MessageType.JoinLobby, 802, LobbyPayloads.GuidValue(joinRoom.Id));
    Assert(lateJoined.Phase == SetupPhase.Playing && lateJoined.Players.Count == 3, "joined the running game");

    // Host: receives the snapshot command naming the joiner. Joiner: receives JoinSnapshot first, then the horizon.
    ScheduledCommand? snapshotOrder = null; JoinSnapshot? lateStart = null;
    var hostGame = new LockstepClient();
    using (var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        while (snapshotOrder == null || lateStart == null)
        {
            while (joinHost.Client.TryDequeue(out WireMessage? m) && m != null)
                if (m.Type == MessageType.ScheduledCommand && LockstepPayloads.ReadScheduled(m.Payload) is { Kind: ScheduledKind.Save } save && save.Data.Length == 16)
                    snapshotOrder = save;
            while (lateStart == null && jLate.TryDequeue(out WireMessage? m) && m != null)
                if (m.Type == MessageType.JoinSnapshot) lateStart = LockstepPayloads.ReadJoin(m.Payload);
            await Task.Delay(10, wait.Token);
        }
    Assert(new Guid(snapshotOrder.Data) == jLate.SessionId && snapshotOrder.IssuerSession == Guid.Empty, "host is told to save a snapshot for the joiner");
    Assert(lateStart.Sequence == snapshotOrder.Sequence && lateStart.Boundary == snapshotOrder.Boundary && lateStart.Host == lateJoined.HostId,
        "joiner starts exactly at the snapshot point");
    var lateGame = LockstepClient.ForJoin(lateStart);
    await jLate.SendAsync(MessageType.SnapshotLoaded, 0, Array.Empty<byte>());
    await jGuest.SendAsync(MessageType.Command, 0, LockstepPayloads.Command(new byte[] { 33 }));
    bool live = false; int lateTick = lateStart.Boundary; ScheduledCommand? seen = null;
    using (var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        while (!live || seen == null)
        {
            while (jLate.TryDequeue(out WireMessage? m) && m != null)
            {
                if (m.Type == MessageType.PlayerSynced && LobbyPayloads.ReadGuid(m.Payload) == jLate.SessionId) live = true;
                else if (LockstepClient.Handles(m)) lateGame.Receive(m);
            }
            seen ??= lateGame.TakeCommandsAt(lateTick).FirstOrDefault();
            if (lateGame.ProgressMessage(lateTick, Environment.TickCount64) is WireMessage progress) await jLate.SendAsync(progress.Type, 0, progress.Payload);
            await Task.Delay(10, wait.Token);
        }
    Assert(seen.Data[0] == 33 && seen.Sequence == lateStart.Sequence + 1, "joiner executes the next command at the same point as the others");
}
Console.WriteLine("PASS: join a running game over the network: other mods refused, snapshot ordered from the host, joiner continues at the next command and goes live");

// DESYNC-003 (ADR 0033) end to end: the guest's state hash differs; the host's server orders a snapshot for it, the
// guest reloads, confirms, goes live again and time resumes for both on its own.
PlayerKey rHostKey = PlayerKey.Steam(76561198000000110UL), rGuestKey = PlayerKey.Steam(76561198000000111UL);
await using (var repairHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Repair Room", 4, rHostKey))
{
    LobbyView repairRoom = LobbyPayloads.ReadState((await ReceiveAsync(repairHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using var repairLink = new SteamLikeLink();
    Assert(repairHost.AttachConnection(NetworkConnection.FromDuplexStream(repairLink.A), rGuestKey), "repair guest attached");
    using LobbyClient rGuest = await LobbyClient.ConnectOverStreamAsync(repairLink.B, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, rGuestKey);
    await StateAsync(rGuest, MessageType.JoinLobby, 900, LobbyPayloads.GuidValue(repairRoom.Id));
    await ReachPlayingAsync(repairHost.Client, rGuest, rHostKey, rGuestKey);

    var rHost = new LockstepClient(); var rGame = new LockstepClient();
    int hostTick = 0, guestTick = 0; JoinSnapshot? reloadAt = null; bool resolvedHost = false, resolvedGuest = false; ScheduledCommand? order = null;
    async Task Drive(int ms, int ticks)
    {
        var until = System.Diagnostics.Stopwatch.StartNew();
        while (until.ElapsedMilliseconds < ms)
        {
            while (repairHost.Client.TryDequeue(out WireMessage? m) && m != null)
            {
                if (m.Type == MessageType.DesyncResolved) resolvedHost = true;
                else if (LockstepClient.Handles(m)) rHost.Receive(m);
            }
            while (reloadAt == null && rGuest.TryDequeue(out WireMessage? m) && m != null)
            {
                if (m.Type == MessageType.DesyncResolved) resolvedGuest = true;
                else if (m.Type == MessageType.JoinSnapshot) reloadAt = LockstepPayloads.ReadJoin(m.Payload);
                else if (LockstepClient.Handles(m)) rGame.Receive(m);
            }
            foreach (var c in rHost.TakeCommandsAt(hostTick)) if (c.Kind == ScheduledKind.Save) order = c;
            for (int t = 0; t < ticks && rHost.MayRunTick(hostTick); t++) { hostTick++; foreach (var c in rHost.TakeCommandsAt(hostTick)) if (c.Kind == ScheduledKind.Save) order = c; }
            if (reloadAt == null)
            {
                rGame.TakeCommandsAt(guestTick);
                for (int t = 0; t < ticks && rGame.MayRunTick(guestTick); t++) { guestTick++; rGame.TakeCommandsAt(guestTick); }
                if (rGame.ProgressMessage(guestTick, Environment.TickCount64) is WireMessage gp) await rGuest.SendAsync(gp.Type, 0, gp.Payload);
            }
            if (rHost.ProgressMessage(hostTick, Environment.TickCount64) is WireMessage hp) await repairHost.Client.SendAsync(hp.Type, 0, hp.Payload);
            await Task.Delay(16);
        }
    }
    await repairHost.Client.SendAsync(MessageType.SessionControl, 0, LockstepPayloads.Control(ScheduledKind.Pause, 0));
    await Drive(1200, 10);
    int hashTick = (Math.Min(hostTick, guestTick) / 60) * 60;
    Assert(hashTick > 0, "a hash tick was reached before the repair");
    await repairHost.Client.SendAsync(MessageType.StateHash, 0, LockstepPayloads.Hash(hashTick, 111));
    await rGuest.SendAsync(MessageType.StateHash, 0, LockstepPayloads.Hash(hashTick, 222));
    await Drive(600, 10);
    Assert(reloadAt != null && order != null && LockstepPayloads.SaveTargets(order.Data).Single() == rGuest.SessionId && reloadAt.Sequence == order.Sequence,
        "guest told to reload at the point where the host saved for it");
    Assert(rHost.Paused, "time stopped for the repair");
    // The guest "loads" the host's state (here: the host's tick) and confirms.
    rGame = LockstepClient.ForJoin(reloadAt!); guestTick = reloadAt!.Boundary; reloadAt = null;
    await rGuest.SendAsync(MessageType.SnapshotLoaded, 0, Array.Empty<byte>());
    int pausedAt = hostTick;
    await Drive(1500, 10);
    Assert(resolvedHost && resolvedGuest, "both told the desync is resolved");
    Assert(!rHost.Paused && !rGame.Paused && hostTick > pausedAt && guestTick > pausedAt, $"time resumed for both: host {hostTick}, guest {guestTick}, repaired at {pausedAt}");
}
Console.WriteLine("PASS: desync repair over the network: snapshot ordered for the differing guest, guest reloads and confirms, time resumes for both");

// VOTE-001 (ADR 0034) end to end in a Coop democracy lobby: the host's game opens a vote, the Steam guest cannot resume
// during it, both vote, and both games get the same result at the same point.
PlayerKey vHostKey = PlayerKey.Steam(76561198000000120UL), vGuestKey = PlayerKey.Steam(76561198000000121UL);
await using (var voteHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Vote Room", 4, vHostKey, mode: LobbyMode.CoopDemocracy))
{
    LobbyView voteRoom = LobbyPayloads.ReadState((await ReceiveAsync(voteHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using var voteLink = new SteamLikeLink();
    Assert(voteHost.AttachConnection(NetworkConnection.FromDuplexStream(voteLink.A), vGuestKey), "vote guest attached");
    using LobbyClient vGuest = await LobbyClient.ConnectOverStreamAsync(voteLink.B, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, vGuestKey);
    await StateAsync(vGuest, MessageType.JoinLobby, 1000, LobbyPayloads.GuidValue(voteRoom.Id));
    await ReachPlayingAsync(voteHost.Client, vGuest, vHostKey, vGuestKey);

    Assert(await ErrorCodeAsync(vGuest, MessageType.VoteOpen, 1001, LockstepPayloads.VoteOpen(2, new byte[] { 7 })) == 3, "only the host opens a vote");
    await voteHost.Client.SendAsync(MessageType.VoteOpen, 1002, LockstepPayloads.VoteOpen(2, new byte[] { 7 }));
    VoteView? hostView = null, guestView = null;
    var hostLockstep = new LockstepClient(); var guestLockstep = new LockstepClient();
    var hostResults = new List<(int, ulong, byte)>(); var guestResults = new List<(int, ulong, byte)>();
    async Task PumpVotes(int ms)
    {
        var until = System.Diagnostics.Stopwatch.StartNew();
        while (until.ElapsedMilliseconds < ms)
        {
            foreach (var (client, game, results, isHost) in new[] { (voteHost.Client, hostLockstep, hostResults, true), (vGuest, guestLockstep, guestResults, false) })
            {
                while (client.TryDequeue(out WireMessage? m) && m != null)
                {
                    if (m.Type == MessageType.VoteState) { if (isHost) hostView = LockstepPayloads.ReadVoteState(m.Payload); else guestView = LockstepPayloads.ReadVoteState(m.Payload); }
                    else if (LockstepClient.Handles(m)) game.Receive(m);
                }
                foreach (var c in game.TakeCommandsAt(0))
                    if (c.Kind == ScheduledKind.VoteResult) results.Add((c.Boundary, c.Sequence, LockstepPayloads.ReadVoteResultData(c.Data).Option));
            }
            await Task.Delay(16);
        }
    }
    await PumpVotes(400);
    Assert(hostView is { Open: true } && guestView is { Open: true } && hostView.Id == guestView.Id, "both see the open vote");
    Assert(await ErrorCodeAsync(vGuest, MessageType.SessionControl, 1003, LockstepPayloads.Control(ScheduledKind.Pause, 0)) == 6, "no resuming during the vote");
    await vGuest.SendAsync(MessageType.VoteCast, 1004, LockstepPayloads.VoteCast(guestView!.Id, 1));
    await voteHost.Client.SendAsync(MessageType.VoteCast, 1005, LockstepPayloads.VoteCast(hostView!.Id, 1));
    await PumpVotes(600);
    Assert(hostResults.Count == 1 && hostResults.SequenceEqual(guestResults) && hostResults[0].Item3 == 1,
        "both games got the same result at the same point: " + string.Join(";", hostResults) + " / " + string.Join(";", guestResults));
    Assert(hostView is { Open: false, Result: 1 } && guestView is { Open: false, Result: 1 }, "both see the closed vote with its result");
}
Console.WriteLine("PASS: democracy vote over the network: host opens, no resuming meanwhile, both vote, same result at the same point in both games");
// SEC-001 (ADR 0035) end to end: key pinning makes a spoofed player key fail; replays, stale proofs and missing proofs too.
var aliceId = PlayerIdentity.Create(); var malloryId = PlayerIdentity.Create(); var secHostId = PlayerIdentity.Create();
PlayerKey aliceKey = PlayerKey.Local(Guid.NewGuid()), secHostKey = PlayerKey.Local(Guid.NewGuid());
await using (var secured = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Secure Room", 4, secHostKey,
    password: "geheim", identity: secHostId))
{
    LobbyView securedRoom = LobbyPayloads.ReadState((await ReceiveAsync(secured.Client, MessageType.LobbyStateUpdate)).Payload);
    Assert(secured.Pins.IsPinned(secHostKey), "the host's own key is pinned");
    async Task<LobbyClient> Connect(string name, PlayerKey key, PlayerIdentity? who) =>
        await LobbyClient.ConnectAsync("127.0.0.1", secured.Port, name, TimeSpan.FromSeconds(4), CancellationToken.None, key, who);
    async Task<string> Refused(string name, PlayerKey key, PlayerIdentity? who) =>
        (await ExpectAsync<ProtocolException>(() => Connect(name, key, who))).Message;

    LobbyClient alice = await Connect("Alice", aliceKey, aliceId);
    Assert(secured.Pins.IsPinned(aliceKey), "first contact pins Alice's key");
    // Password: the list flags it; missing and wrong are refused; the right one joins.
    await alice.SendAsync(MessageType.LobbyListRequest, 1100, Array.Empty<byte>());
    Assert(LobbyPayloads.ReadList((await ReceiveAsync(alice, MessageType.LobbyListResponse)).Payload).Single().PasswordRequired, "list shows the lock");
    Assert(await ErrorCodeAsync(alice, MessageType.JoinLobby, 1101, LobbyPayloads.GuidValue(securedRoom.Id)) == 3, "no password refused");
    Assert(await ErrorCodeAsync(alice, MessageType.JoinLobby, 1102, LobbyPayloads.Join(securedRoom.Id, "falsch")) == 3, "wrong password refused");
    Assert((await StateAsync(alice, MessageType.JoinLobby, 1103, LobbyPayloads.Join(securedRoom.Id, "geheim"))).Players.Count == 2, "right password joins");
    await alice.DisconnectAsync();
    alice.Dispose();
    await Task.Delay(300);

    // The acceptance criterion of SEC-001: someone else claiming Alice's key is refused.
    Assert((await Refused("Alice", aliceKey, malloryId)).Contains("bound to another identity"), "spoofed player key with another key pair refused");
    Assert((await Refused("Alice", aliceKey, null)).Contains("requires proof"), "spoofed player key without proof refused");
    using (LobbyClient back = await Connect("Alice", aliceKey, aliceId)) Assert(back.IsConnected, "the real Alice is accepted again");
    await Task.Delay(300);

    // Replay: the same signed Hello a second time is refused. A stale timestamp is refused.
    byte[] recorded = LobbyPayloads.Hello("Alice", aliceKey, aliceId.Prove("Alice", aliceKey, PlayerIdentity.UnixNow));
    async Task<WireMessage?> RawHello(byte[] payload)
    {
        using var raw = await NetworkConnection.ConnectAsync("127.0.0.1", secured.Port, TimeSpan.FromSeconds(4));
        await raw.SendAsync(new WireMessage(MessageType.Hello, 1, payload));
        return await raw.ReceiveAsync();
    }
    Assert((await RawHello(recorded))!.Type == MessageType.HandshakeAccepted, "first use of the proof accepted");
    await Task.Delay(300);
    WireMessage? replayed = await RawHello(recorded);
    Assert(replayed!.Type == MessageType.HandshakeRejected && LobbyPayloads.ReadString(replayed.Payload.Skip(1).ToArray(), 256).Contains("already used"),
        "replayed proof refused");
    WireMessage? stale = await RawHello(LobbyPayloads.Hello("Alice", aliceKey, aliceId.Prove("Alice", aliceKey, PlayerIdentity.UnixNow - 3600)));
    Assert(stale!.Type == MessageType.HandshakeRejected, "proof from an hour ago refused");

    // Pins survive into another game through Export/Import.
    var carried = new PlayerKeyPinStore(); carried.Import(secured.Pins.Export());
    await using var nextGame = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Next Game", 4, secHostKey, identity: secHostId, pins: carried);
    Assert((await ExpectAsync<ProtocolException>(() => LobbyClient.ConnectAsync("127.0.0.1", nextGame.Port, "Alice", TimeSpan.FromSeconds(4),
        CancellationToken.None, aliceKey, malloryId))).Message.Contains("bound to another identity"), "imported pins keep protecting Alice");
}
await using (var strict = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Strict Room", 4, PlayerKey.Local(Guid.NewGuid()),
    identity: PlayerIdentity.Create(), requireKeyProof: true))
{
    Assert((await ExpectAsync<ProtocolException>(() => LobbyClient.ConnectAsync("127.0.0.1", strict.Port, "Plain", TimeSpan.FromSeconds(4))))
        .Message.Contains("Key proof required"), "required mode refuses TCP players without proof");
    using var steamOk = new SteamLikeLink();
    PlayerKey steamOkKey = PlayerKey.Steam(76561198000000130UL);
    Assert(strict.AttachConnection(NetworkConnection.FromDuplexStream(steamOk.A), steamOkKey), "Steam-like link attached");
    using LobbyClient steamPlayer = await LobbyClient.ConnectOverStreamAsync(steamOk.B, "SteamPlayer", TimeSpan.FromSeconds(4), CancellationToken.None, steamOkKey);
    Assert(steamPlayer.IsConnected, "Steam-verified players need no key proof");
}
Console.WriteLine("PASS: IP lobby security: password, key pinned on first contact, spoofed key refused (with or without proof), replay and stale proofs refused, pins carried over, required mode, Steam exempt");
// ADR 0036 end to end: a player joins the running game but never confirms loading the snapshot. After the (short, for
// the test) time limit the host's server tells everyone, removes the player from the session and the lobby.
PlayerKey tHostKey = PlayerKey.Steam(76561198000000140UL), tGuestKey = PlayerKey.Steam(76561198000000141UL), tLateKey = PlayerKey.Steam(76561198000000142UL);
await using (var timeoutHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Timeout Room", 4, tHostKey, snapshotLoadTimeoutSeconds: 5))
{
    LobbyView timeoutRoom = LobbyPayloads.ReadState((await ReceiveAsync(timeoutHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using LobbyClient tGuest = await LobbyClient.ConnectAsync("127.0.0.1", timeoutHost.Port, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, tGuestKey);
    await StateAsync(tGuest, MessageType.JoinLobby, 1200, LobbyPayloads.GuidValue(timeoutRoom.Id));
    await ReachPlayingAsync(timeoutHost.Client, tGuest, tHostKey, tGuestKey);
    using var lateLink2 = new SteamLikeLink();
    Assert(timeoutHost.AttachConnection(NetworkConnection.FromDuplexStream(lateLink2.A), tLateKey), "late joiner attached");
    using LobbyClient tLate = await LobbyClient.ConnectOverStreamAsync(lateLink2.B, "Late", TimeSpan.FromSeconds(4), CancellationToken.None, tLateKey);
    LobbyView withLate = await StateAsync(tLate, MessageType.JoinLobby, 1201, LobbyPayloads.GuidValue(timeoutRoom.Id));
    Assert(withLate.Players.Count == 3, "late player joined the running game");
    bool toldLate = false, leftLobby = false, hostSawLeave = false;
    using (var wait = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
        while (!(toldLate && leftLobby && hostSawLeave))
        {
            while (tLate.TryDequeue(out WireMessage? m) && m != null)
            {
                if (m.Type == MessageType.ResyncGaveUp && LockstepPayloads.ReadDesync(m.Payload).Sessions.Contains(tLate.SessionId)) toldLate = true;
                if (m.Type == MessageType.LeaveLobby) leftLobby = true;
            }
            while (timeoutHost.Client.TryDequeue(out WireMessage? m) && m != null)
                if (m.Type == MessageType.LobbyStateUpdate && LobbyPayloads.ReadState(m.Payload) is { } s && s.Players.Count == 2 && s.Players.All(p => p.Key != tLateKey))
                    hostSawLeave = true;
            await Task.Delay(50, wait.Token);
        }
    Assert(tLate.IsConnected, "the player stays connected and can join again");
    LobbyView rejoined = await StateAsync(tLate, MessageType.JoinLobby, 1202, LobbyPayloads.GuidValue(timeoutRoom.Id));
    Assert(rejoined.Players.Count == 3, "joining again after the timeout works");
}
Console.WriteLine("PASS: snapshot load timeout over the network: player told, taken out of session and lobby, can join again");



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

// SETUP-004 end to end: the guest's request reaches the host stamped with the guest's key; the host's roster reaches
// the guest; done needs an own pawn.
await using (var pawnHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Pawn Room", 4, PlayerKey.Steam(76561198000000100UL)))
{
    PlayerKey pawnHostKey = PlayerKey.Steam(76561198000000100UL), pawnGuestKey = PlayerKey.Steam(76561198000000101UL);
    LobbyView pawnRoom = LobbyPayloads.ReadState((await ReceiveAsync(pawnHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using var pawnLink = new SteamLikeLink();
    Assert(pawnHost.AttachConnection(NetworkConnection.FromDuplexStream(pawnLink.A), pawnGuestKey), "pawn guest attached");
    using LobbyClient pawnGuest = await LobbyClient.ConnectOverStreamAsync(pawnLink.B, "Guest", TimeSpan.FromSeconds(4),
        CancellationToken.None, pawnGuestKey);
    LobbyClient ph = pawnHost.Client;
    uint q = 1100;
    await StateAsync(pawnGuest, MessageType.JoinLobby, ++q, LobbyPayloads.GuidValue(pawnRoom.Id));
    await StateAsync(ph, MessageType.PlayerReadyChanged, ++q, LobbyPayloads.Bool(true));
    await StateAsync(pawnGuest, MessageType.PlayerReadyChanged, ++q, LobbyPayloads.Bool(true));
    foreach (SetupPhase next in new[] { SetupPhase.Scenario, SetupPhase.Storyteller, SetupPhase.WorldParams, SetupPhase.WorldTransfer })
        await StateAsync(ph, MessageType.SetupAdvance, ++q, SetupPayloads.Advance(next));
    byte[] pawnWorld = Enumerable.Repeat((byte)6, 32).ToArray();
    await StateAsync(ph, MessageType.WorldReady, ++q, SetupPayloads.Fingerprint(pawnWorld));
    await StateAsync(pawnGuest, MessageType.WorldLoaded, ++q, SetupPayloads.Fingerprint(pawnWorld));
    await StateAsync(ph, MessageType.SetupAdvance, ++q, SetupPayloads.Advance(SetupPhase.SiteSelection));
    await StateAsync(ph, MessageType.SiteDecided, ++q, SetupPayloads.Site(new SiteTile(7, 0)));
    await StateAsync(ph, MessageType.SetupAdvance, ++q, SetupPayloads.Advance(SetupPhase.Characters));
    // A client cannot claim to be someone else: the server replaces the requester.
    var forged = new PawnRequest(PawnRequestKind.Claim, 101, 0, "", pawnHostKey);
    await pawnGuest.SendAsync(MessageType.PawnRequest, ++q, forged.ToPayload());
    PawnRequest atHost = PawnRequest.Read((await ReceiveAsync(ph, MessageType.PawnRequest)).Payload);
    Assert(atHost.Kind == PawnRequestKind.Claim && atHost.PawnId == 101 && atHost.Requester == pawnGuestKey,
        "request reaches the host with the server-stamped guest key");
    Assert(await ErrorCodeAsync(pawnGuest, MessageType.PlayerReadyChanged, ++q, LobbyPayloads.Bool(true)) == 6, "no pawn, not done");
    await StateAsync(ph, MessageType.PawnRoster, ++q, RosterFor(pawnHostKey, pawnGuestKey).ToPayload());
    PawnRoster guestSees = PawnRoster.Read((await ReceiveAsync(pawnGuest, MessageType.PawnRoster)).Payload);
    Assert(guestSees.Cards.Count == 2 && guestSees.OwnsSelectedPawn(pawnGuestKey), "guest sees the roster with its pawn");
    LobbyView done = await StateAsync(pawnGuest, MessageType.PlayerReadyChanged, ++q, LobbyPayloads.Bool(true));
    Assert(done.Players.Single(p => p.Key == pawnGuestKey).Ready, "guest done with its own pawn");
    Assert(await ErrorCodeAsync(ph, MessageType.PawnRequest, ++q, forged.ToPayload()) == 6, "host does not send requests");
}
Console.WriteLine("PASS: pawn requests stamped and routed to the host, roster to guests, done needs a pawn (SETUP-004)");

// FACTION-004 (ADR 0037) end to end over TCP. Only a test switch opens the Factions setup. Host options, storyteller
// choices and done travel with codes 96/97 and Ready; every refusal comes from the server. A player who reconnects to the
// running game finds their faction slot and storyteller again; a new player finds none.
PlayerKey fHostKey = PlayerKey.Local(Guid.NewGuid()), fGuestKey = PlayerKey.Local(Guid.NewGuid()),
    fThirdKey = PlayerKey.Local(Guid.NewGuid()), fNewKey = PlayerKey.Local(Guid.NewGuid());
await using (var factionServer = new FoundationServer(new HostConfig { BindAddress = "127.0.0.1", Port = 0, EnableFactionsSetup = true }))
{
    await factionServer.StartAsync();
    int fPort = factionServer.BoundPort;
    using LobbyClient fHost = await LobbyClient.ConnectAsync("127.0.0.1", fPort, "Host", TimeSpan.FromSeconds(4), CancellationToken.None, fHostKey);
    using LobbyClient fGuest = await LobbyClient.ConnectAsync("127.0.0.1", fPort, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, fGuestKey);
    using LobbyClient fThird = await LobbyClient.ConnectAsync("127.0.0.1", fPort, "Third", TimeSpan.FromSeconds(4), CancellationToken.None, fThirdKey);
    uint f = 2000;
    byte? SlotOf(LobbyView view, PlayerKey key) => view.Players.Single(p => p.Key == key).FactionSlot;
    string? TellerOf(LobbyView view, PlayerKey key) => view.Players.Single(p => p.Key == key).Storyteller;
    LobbyView fRoom = await StateAsync(fHost, MessageType.CreateLobby, ++f, LobbyPayloads.Create("Fraktionen", 4, LobbyMode.Factions(FactionRelation.Allied)));
    await StateAsync(fGuest, MessageType.JoinLobby, ++f, LobbyPayloads.GuidValue(fRoom.Id));
    LobbyView fLobby = await StateAsync(fThird, MessageType.JoinLobby, ++f, LobbyPayloads.GuidValue(fRoom.Id));
    Assert(fLobby.Players.Count == 3 && fLobby.Players.All(p => p.FactionSlot == null) && fLobby.FactionOptions == null, "no factions in the lobby");
    Assert(await ErrorCodeAsync(fGuest, MessageType.StorytellerChoice, ++f, FactionPayloads.StorytellerChoice("Randy")) == 6, "no choice in the lobby");
    foreach (LobbyClient member in new[] { fHost, fGuest, fThird })
        await StateAsync(member, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true));
    LobbyView fScenario = await StateAsync(fHost, MessageType.SetupAdvance, ++f, SetupPayloads.Advance(SetupPhase.Scenario));
    Assert(SlotOf(fScenario, fHostKey) == 0 && SlotOf(fScenario, fGuestKey) == 1 && SlotOf(fScenario, fThirdKey) == 2, "slots: host first, then joining order");
    Assert(SlotOf(await UntilStateAsync(fGuest, s => s.Revision >= fScenario.Revision), fGuestKey) == 1, "guests receive their slot");
    await StateAsync(fHost, MessageType.SetupAdvance, ++f, SetupPayloads.Advance(SetupPhase.Storyteller));
    var fOptions = new FactionOptions("Rough", new[] { "Cassandra", "Phoebe", "Randy" });
    Assert(await ErrorCodeAsync(fGuest, MessageType.FactionOptions, ++f, fOptions.ToPayload()) == 3, "a guest cannot set the shared difficulty");
    Assert(await ErrorCodeAsync(fGuest, MessageType.StorytellerChoice, ++f, FactionPayloads.StorytellerChoice("Randy")) == 6, "no choice before the host's list");
    Assert(await ErrorCodeAsync(fGuest, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true)) == 6, "done needs a choice");
    Assert(await ErrorCodeAsync(fHost, MessageType.SetupAdvance, ++f, SetupPayloads.Advance(SetupPhase.WorldParams)) == 6, "setup waits for the host's options");
    LobbyView fOffered = await StateAsync(fHost, MessageType.FactionOptions, ++f, fOptions.ToPayload());
    Assert(fOffered.FactionOptions!.SameAs(fOptions), "host set difficulty and storytellers");
    Assert((await UntilStateAsync(fGuest, s => s.FactionOptions != null)).FactionOptions!.Difficulty == "Rough", "guests see the shared difficulty");
    Assert(await ErrorCodeAsync(fGuest, MessageType.StorytellerChoice, ++f, FactionPayloads.StorytellerChoice("Tutor")) == 6, "only offered storytellers");
    await StateAsync(fHost, MessageType.StorytellerChoice, ++f, FactionPayloads.StorytellerChoice("Cassandra"));
    LobbyView guestChose = await StateAsync(fGuest, MessageType.StorytellerChoice, ++f, FactionPayloads.StorytellerChoice("Randy"));
    Assert(TellerOf(guestChose, fGuestKey) == "Randy" && TellerOf(guestChose, fHostKey) == "Cassandra", "each player chose for their own faction");
    await StateAsync(fHost, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true));
    LobbyView guestDone = await StateAsync(fGuest, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true));
    Assert(LobbyRegistry.StorytellerBlockReason(guestDone) == "Erzähler fehlt: Third", "the reason names who has no storyteller");
    Assert(await ErrorCodeAsync(fHost, MessageType.SetupAdvance, ++f, SetupPayloads.Advance(SetupPhase.WorldParams)) == 6, "setup waits for every choice");
    LobbyView harder = await StateAsync(fHost, MessageType.FactionOptions, ++f, new FactionOptions("Brutal", fOptions.Storytellers).ToPayload());
    Assert(harder.Players.All(p => !p.Ready) && TellerOf(harder, fGuestKey) == "Randy", "a new difficulty clears done for everyone, choices stay");
    var malformed = new PayloadWriter(); malformed.WriteString("not a def name", 128);
    Assert(await ErrorCodeAsync(fThird, MessageType.StorytellerChoice, ++f, malformed.ToArray()) == 1, "a malformed choice is a protocol error");
    LobbyView withoutThird = await UntilStateAsync(fHost, s => s.Players.Count == 2);
    Assert(SlotOf(withoutThird, fHostKey) == 0 && SlotOf(withoutThird, fGuestKey) == 1, "the dropped player's slot is gone, the others keep theirs");
    await StateAsync(fHost, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true));
    await StateAsync(fGuest, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true));
    LobbyView fWorldParams = await StateAsync(fHost, MessageType.SetupAdvance, ++f, SetupPayloads.Advance(SetupPhase.WorldParams));
    Assert(fWorldParams.Phase == SetupPhase.WorldParams && fWorldParams.FactionOptions!.Difficulty == "Brutal", "all chose and are done: setup continues");
    Assert(await ErrorCodeAsync(fHost, MessageType.FactionOptions, ++f, fOptions.ToPayload()) == 6, "options only in the storyteller step");
    Assert(await ErrorCodeAsync(fGuest, MessageType.StorytellerChoice, ++f, FactionPayloads.StorytellerChoice("Phoebe")) == 6, "choice only in the storyteller step");
    LobbyView fLanded = await FinishSetupAsync(fHost, fGuest, fHostKey, fGuestKey, f += 100, factions: true);
    Assert(SlotOf(fLanded, fGuestKey) == 1 && TellerOf(fLanded, fGuestKey) == "Randy", "the running game keeps slots and choices");
    await fGuest.DisconnectAsync();
    await UntilStateAsync(fHost, s => s.Players.Count == 1);
    using (LobbyClient newcomer = await LobbyClient.ConnectAsync("127.0.0.1", fPort, "New", TimeSpan.FromSeconds(4), CancellationToken.None, fNewKey))
        Assert(await ErrorCodeAsync(newcomer, MessageType.JoinLobby, ++f, LobbyPayloads.GuidValue(fRoom.Id)) == 6, "a running Factions game has no faction for a new player");
    using LobbyClient fBack = await LobbyClient.ConnectAsync("127.0.0.1", fPort, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, fGuestKey);
    LobbyView back = await StateAsync(fBack, MessageType.JoinLobby, ++f, LobbyPayloads.GuidValue(fRoom.Id));
    Assert(back.Phase == SetupPhase.Playing && SlotOf(back, fGuestKey) == 1 && TellerOf(back, fGuestKey) == "Randy" && back.FactionOptions!.Difficulty == "Brutal",
        "reconnect: same faction slot, storyteller and shared difficulty");

    // Coop on the same server: no slots, and the faction messages are refused.
    using LobbyClient cHost = await LobbyClient.ConnectAsync("127.0.0.1", fPort, "CoopHost", TimeSpan.FromSeconds(4), CancellationToken.None, PlayerKey.Local(Guid.NewGuid()));
    using LobbyClient cGuest = await LobbyClient.ConnectAsync("127.0.0.1", fPort, "CoopGuest", TimeSpan.FromSeconds(4), CancellationToken.None, PlayerKey.Local(Guid.NewGuid()));
    LobbyView cRoom = await StateAsync(cHost, MessageType.CreateLobby, ++f, LobbyPayloads.Create("Coop", 4, LobbyMode.CoopDemocracy));
    await StateAsync(cGuest, MessageType.JoinLobby, ++f, LobbyPayloads.GuidValue(cRoom.Id));
    await StateAsync(cHost, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true));
    await StateAsync(cGuest, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true));
    await StateAsync(cHost, MessageType.SetupAdvance, ++f, SetupPayloads.Advance(SetupPhase.Scenario));
    LobbyView cStep = await StateAsync(cHost, MessageType.SetupAdvance, ++f, SetupPayloads.Advance(SetupPhase.Storyteller));
    Assert(cStep.Players.All(p => p.FactionSlot == null && p.Storyteller == null) && cStep.FactionOptions == null, "Coop has no faction data");
    Assert(await ErrorCodeAsync(cHost, MessageType.FactionOptions, ++f, fOptions.ToPayload()) == 6, "Coop: the host's storyteller page decides, no faction options");
    Assert(await ErrorCodeAsync(cGuest, MessageType.StorytellerChoice, ++f, FactionPayloads.StorytellerChoice("Randy")) == 6, "Coop: no own storyteller");
    Assert(await ErrorCodeAsync(cGuest, MessageType.PlayerReadyChanged, ++f, LobbyPayloads.Bool(true)) == 6, "Coop: no done in the storyteller step");
}
// The real hosting path cannot open the Factions setup: ListenHost has no switch, so the start stays refused.
await using (var prodHost = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Fraktionen", 4, mode: LobbyMode.Factions(FactionRelation.Neutral)))
{
    LobbyView prodRoom = LobbyPayloads.ReadState((await ReceiveAsync(prodHost.Client, MessageType.LobbyStateUpdate)).Payload);
    using LobbyClient prodGuest = await LobbyClient.ConnectAsync("127.0.0.1", prodHost.Port, "Guest", TimeSpan.FromSeconds(4));
    await StateAsync(prodGuest, MessageType.JoinLobby, 2500, LobbyPayloads.GuidValue(prodRoom.Id));
    await StateAsync(prodHost.Client, MessageType.PlayerReadyChanged, 2501, LobbyPayloads.Bool(true));
    await StateAsync(prodGuest, MessageType.PlayerReadyChanged, 2502, LobbyPayloads.Bool(true));
    Assert(await ErrorCodeAsync(prodHost.Client, MessageType.SetupAdvance, 2503, SetupPayloads.Advance(SetupPhase.Scenario)) == 6,
        "Factions start refused on the real hosting path");
    Assert(new HostConfig().EnableFactionsSetup == false, "the switch is off by default");
}
Console.WriteLine("PASS: Factions storytellers end to end: host-only options, choices and done over 96/97, refusals, reconnect keeps the faction, Coop unaffected, start refused without the test switch (ADR 0037)");

// FACTION-006 (ADR 0038) end to end over TCP: own site and ideology per faction with codes 98/99, the host's rejection (100),
// ideology data only to the host, a forged relay refused, reconnect keeps everything; continuing a saved game (101).
PlayerKey oHostKey = PlayerKey.Local(Guid.NewGuid()), oGuestKey = PlayerKey.Local(Guid.NewGuid()), oThirdKey = PlayerKey.Local(Guid.NewGuid());
await using (var siteServer = new FoundationServer(new HostConfig { BindAddress = "127.0.0.1", Port = 0, EnableFactionsSetup = true }))
{
    await siteServer.StartAsync();
    int oPort = siteServer.BoundPort;
    using LobbyClient oHost = await LobbyClient.ConnectAsync("127.0.0.1", oPort, "Host", TimeSpan.FromSeconds(4), CancellationToken.None, oHostKey);
    using LobbyClient oGuest = await LobbyClient.ConnectAsync("127.0.0.1", oPort, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, oGuestKey);
    using LobbyClient oThird = await LobbyClient.ConnectAsync("127.0.0.1", oPort, "Third", TimeSpan.FromSeconds(4), CancellationToken.None, oThirdKey);
    uint o = 3000;
    LobbyClient[] oAll = { oHost, oGuest, oThird };
    PlayerView Of(LobbyView view, PlayerKey key) => view.Players.Single(p => p.Key == key);
    bool Stable(LobbyView view) => view.Players.All(p => p.FactionSlot == (p.Key == oHostKey ? 0 : p.Key == oGuestKey ? 1 : 2));
    LobbyView oRoom = await StateAsync(oHost, MessageType.CreateLobby, ++o, LobbyPayloads.Create("Orte", 4, LobbyMode.Factions(FactionRelation.Hostile)));
    await StateAsync(oGuest, MessageType.JoinLobby, ++o, LobbyPayloads.GuidValue(oRoom.Id));
    await StateAsync(oThird, MessageType.JoinLobby, ++o, LobbyPayloads.GuidValue(oRoom.Id));
    foreach (LobbyClient member in oAll) await StateAsync(member, MessageType.PlayerReadyChanged, ++o, LobbyPayloads.Bool(true));
    await StateAsync(oHost, MessageType.SetupAdvance, ++o, SetupPayloads.Advance(SetupPhase.Scenario));
    await StateAsync(oHost, MessageType.SetupAdvance, ++o, SetupPayloads.Advance(SetupPhase.Storyteller));
    await StateAsync(oHost, MessageType.FactionOptions, ++o, new FactionOptions("Rough", new[] { "Randy" }).ToPayload());
    foreach (LobbyClient member in oAll)
    {
        await StateAsync(member, MessageType.StorytellerChoice, ++o, FactionPayloads.StorytellerChoice("Randy"));
        await StateAsync(member, MessageType.PlayerReadyChanged, ++o, LobbyPayloads.Bool(true));
    }
    await StateAsync(oHost, MessageType.SetupAdvance, ++o, SetupPayloads.Advance(SetupPhase.WorldParams));
    await StateAsync(oHost, MessageType.SetupAdvance, ++o, SetupPayloads.Advance(SetupPhase.WorldTransfer));
    byte[] oWorld = Enumerable.Repeat((byte)6, 32).ToArray();
    await StateAsync(oHost, MessageType.WorldReady, ++o, SetupPayloads.Fingerprint(oWorld));
    await StateAsync(oGuest, MessageType.WorldLoaded, ++o, SetupPayloads.Fingerprint(oWorld));
    await StateAsync(oThird, MessageType.WorldLoaded, ++o, SetupPayloads.Fingerprint(oWorld));
    await StateAsync(oHost, MessageType.SetupAdvance, ++o, SetupPayloads.Advance(SetupPhase.SiteSelection));
    var siteA = new SiteTile(40, 0); var siteB = new SiteTile(55, 0); var siteC = new SiteTile(70, 0); var siteD = new SiteTile(85, 0);
    Assert(await ErrorCodeAsync(oGuest, MessageType.SiteFlag, ++o, SetupPayloads.Flag(siteA)) == 6, "Factions: no Coop flags");
    Assert(await ErrorCodeAsync(oHost, MessageType.SiteDecided, ++o, SetupPayloads.Site(siteA)) == 6, "Factions: the host does not choose for everyone");
    Assert(await ErrorCodeAsync(oGuest, MessageType.FactionIdeologyChoice, ++o, FactionPayloads.IdeologyChoice("Ideo", new byte[] { 1 })) == 6, "no ideology during site selection");
    Assert(await ErrorCodeAsync(oGuest, MessageType.PlayerReadyChanged, ++o, LobbyPayloads.Bool(true)) == 6, "done needs an own site");
    LobbyView guestSite = await StateAsync(oGuest, MessageType.FactionSiteChoice, ++o, FactionPayloads.SiteChoice(siteA));
    Assert(Of(guestSite, oGuestKey).FactionSite == siteA && Of(guestSite, oHostKey).FactionSite == null && Of(guestSite, oThirdKey).FactionSite == null && Stable(guestSite),
        "a site lands on the sender's own faction only");
    Assert(await ErrorCodeAsync(oThird, MessageType.FactionSiteChoice, ++o, FactionPayloads.SiteChoice(siteA)) == 6, "a site another faction holds is refused");
    await StateAsync(oThird, MessageType.FactionSiteChoice, ++o, FactionPayloads.SiteChoice(siteB));
    await StateAsync(oHost, MessageType.FactionSiteChoice, ++o, FactionPayloads.SiteChoice(siteC));
    Assert(await ErrorCodeAsync(oGuest, MessageType.FactionChoiceClear, ++o, FactionPayloads.ClearChoice(2, FactionChoiceKind.Site, "nein")) == 3,
        "only the host rejects a choice");
    LobbyView siteRejected = await StateAsync(oHost, MessageType.FactionChoiceClear, ++o, FactionPayloads.ClearChoice(2, FactionChoiceKind.Site, "Zu nah an Guest"));
    Assert(Of(siteRejected, oThirdKey).FactionSite == null && Of(siteRejected, oThirdKey).FactionNote == "Zu nah an Guest" && Stable(siteRejected),
        "the host's game rejected the third player's site with a reason");
    Assert((await UntilStateAsync(oThird, s => Of(s, oThirdKey).FactionNote != null)).Players.Single(p => p.Key == oThirdKey).FactionNote == "Zu nah an Guest",
        "the player sees why");
    Assert(Of(await StateAsync(oThird, MessageType.FactionSiteChoice, ++o, FactionPayloads.SiteChoice(siteD)), oThirdKey).FactionNote == null, "a new choice clears the note");
    foreach (LobbyClient member in oAll) await StateAsync(member, MessageType.PlayerReadyChanged, ++o, LobbyPayloads.Bool(true));
    await StateAsync(oHost, MessageType.SetupAdvance, ++o, SetupPayloads.Advance(SetupPhase.Ideology));

    // Ideology: data only to the host, with the chooser's slot and key; nothing for an unchanged choice.
    async Task<WireMessage?> NextIdeologyAsync(LobbyClient client, int milliseconds)
    {
        using var wait = new CancellationTokenSource(milliseconds);
        try
        {
            while (true)
            {
                while (client.TryDequeue(out WireMessage? m) && m != null)
                    if (m.Type == MessageType.FactionIdeologyChoice) return m;
                await Task.Delay(10, wait.Token);
            }
        }
        catch (OperationCanceledException) { return null; }
    }
    byte[] ancestors = Enumerable.Range(0, 2000).Select(i => (byte)(i * 7)).ToArray();
    LobbyView guestIdeo = await StateAsync(oGuest, MessageType.FactionIdeologyChoice, ++o, FactionPayloads.IdeologyChoice("Ahnen", ancestors));
    Assert(Of(guestIdeo, oGuestKey).Ideology!.SameAs(IdeologySummary.Of("Ahnen", ancestors)), "everyone sees label and hash");
    WireMessage? relayed = await NextIdeologyAsync(oHost, 3000);
    var relay = FactionPayloads.ReadIdeologyForHost(relayed!.Payload);
    Assert(relay.Slot == 1 && relay.Player == oGuestKey && relay.Label == "Ahnen" && relay.Data.SequenceEqual(ancestors), "the host gets the data with slot and key");
    Assert(await NextIdeologyAsync(oGuest, 300) == null && await NextIdeologyAsync(oThird, 300) == null, "guests never receive ideology data");
    await StateAsync(oGuest, MessageType.FactionIdeologyChoice, ++o, FactionPayloads.IdeologyChoice("Ahnen", ancestors));
    Assert(await NextIdeologyAsync(oHost, 400) == null, "an unchanged choice is not forwarded again");
    // A guest posing as the relay is not a valid choice: protocol error, connection closed, the faction is gone before the game.
    Assert(await ErrorCodeAsync(oThird, MessageType.FactionIdeologyChoice, ++o,
        FactionPayloads.IdeologyForHost(1, oGuestKey, "Falsch", new byte[] { 9 })) == 1, "a forged relay is a protocol error");
    LobbyView withoutThird = await UntilStateAsync(oHost, s => s.Players.Count == 2);
    Assert(withoutThird.Players.All(p => p.Key != oThirdKey) && Stable(withoutThird), "the dropped player's faction is gone, slots 0/1 stay");
    await StateAsync(oHost, MessageType.FactionIdeologyChoice, ++o, FactionPayloads.IdeologyChoice("Host-Ideo", new byte[] { 5 }));
    LobbyView ideoRejected = await StateAsync(oHost, MessageType.FactionChoiceClear, ++o, FactionPayloads.ClearChoice(1, FactionChoiceKind.Ideology, "Daten fehlen"));
    Assert(Of(ideoRejected, oGuestKey).Ideology == null && Of(ideoRejected, oGuestKey).FactionNote == "Daten fehlen", "the host's game rejects an ideology");
    await StateAsync(oGuest, MessageType.FactionIdeologyChoice, ++o, FactionPayloads.IdeologyChoice("Ahnen", ancestors));
    Assert(FactionPayloads.ReadIdeologyForHost((await NextIdeologyAsync(oHost, 3000))!.Payload).Slot == 1, "sent again after the rejection, it reaches the host again");
    await StateAsync(oHost, MessageType.PlayerReadyChanged, ++o, LobbyPayloads.Bool(true));
    await StateAsync(oGuest, MessageType.PlayerReadyChanged, ++o, LobbyPayloads.Bool(true));
    LobbyView oLanded = await LandAsync(oHost, oGuest, oHostKey, oGuestKey, o += 100);
    Assert(Of(oLanded, oGuestKey).FactionSite == siteA && Of(oLanded, oGuestKey).Ideology!.Label == "Ahnen" && Stable(oLanded), "the running game keeps sites and ideologies");
    await oGuest.DisconnectAsync();
    await UntilStateAsync(oHost, s => s.Players.Count == 1);
    using LobbyClient oBack = await LobbyClient.ConnectAsync("127.0.0.1", oPort, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, oGuestKey);
    LobbyView oReturned = await StateAsync(oBack, MessageType.JoinLobby, ++o, LobbyPayloads.GuidValue(oRoom.Id));
    Assert(Of(oReturned, oGuestKey).FactionSlot == 1 && Of(oReturned, oGuestKey).FactionSite == siteA &&
        Of(oReturned, oGuestKey).Ideology!.SameAs(IdeologySummary.Of("Ahnen", ancestors)) && Of(oReturned, oGuestKey).Storyteller == "Randy",
        "reconnect: same slot, site, ideology and storyteller");
}
Console.WriteLine("PASS: faction sites and ideologies end to end: own faction only, shared site refused, host-only rejection with reason, ideology data only to the host, forged relay refused, reconnect keeps everything (ADR 0038)");

// SAVE-002, Factions part (ADR 0038): the host restores the save's table; only its players join; the start goes straight
// to the game transfer; an absent player joins the running game later. Through ListenHost the start stays refused.
PlayerKey kHostKey = PlayerKey.Local(Guid.NewGuid()), kGuestKey = PlayerKey.Local(Guid.NewGuid()), kAbsentKey = PlayerKey.Local(Guid.NewGuid());
var savedTable = new FactionRestore(new[] { new FactionRestoreEntry(kHostKey, 0, "Randy"), new FactionRestoreEntry(kAbsentKey, 1, "Cassandra"),
    new FactionRestoreEntry(kGuestKey, 2, "Phoebe") });
await using (var contServer = new FoundationServer(new HostConfig { BindAddress = "127.0.0.1", Port = 0, EnableFactionsSetup = true }))
{
    await contServer.StartAsync();
    int kPort = contServer.BoundPort;
    using LobbyClient kHost = await LobbyClient.ConnectAsync("127.0.0.1", kPort, "Host", TimeSpan.FromSeconds(4), CancellationToken.None, kHostKey);
    using LobbyClient kGuest = await LobbyClient.ConnectAsync("127.0.0.1", kPort, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, kGuestKey);
    using LobbyClient kStranger = await LobbyClient.ConnectAsync("127.0.0.1", kPort, "Stranger", TimeSpan.FromSeconds(4), CancellationToken.None, PlayerKey.Local(Guid.NewGuid()));
    using LobbyClient kAbsent = await LobbyClient.ConnectAsync("127.0.0.1", kPort, "Absent", TimeSpan.FromSeconds(4), CancellationToken.None, kAbsentKey);
    uint k = 4000;
    LobbyView kRoom = await StateAsync(kHost, MessageType.CreateLobby, ++k, LobbyPayloads.Create("Fortsetzen", 4, LobbyMode.Factions(FactionRelation.Allied)));
    LobbyView kRestored = await StateAsync(kHost, MessageType.FactionRestore, ++k, savedTable.ToPayload());
    Assert(kRestored.Restored && kRestored.Players.Single().FactionSlot == 0 && kRestored.Players.Single().Storyteller == "Randy", "the save's table is in the lobby");
    Assert(await ErrorCodeAsync(kStranger, MessageType.JoinLobby, ++k, LobbyPayloads.GuidValue(kRoom.Id)) == 6, "a player outside the save cannot join");
    LobbyView kJoined = await StateAsync(kGuest, MessageType.JoinLobby, ++k, LobbyPayloads.GuidValue(kRoom.Id));
    Assert(kJoined.Restored && kJoined.Players.Single(p => p.Key == kGuestKey).FactionSlot == 2 &&
        kJoined.Players.Single(p => p.Key == kGuestKey).Storyteller == "Phoebe", "the guest sees its saved faction");
    Assert(await ErrorCodeAsync(kGuest, MessageType.FactionRestore, ++k, savedTable.ToPayload()) == 3, "only the host restores");
    await StateAsync(kHost, MessageType.PlayerReadyChanged, ++k, LobbyPayloads.Bool(true));
    await StateAsync(kGuest, MessageType.PlayerReadyChanged, ++k, LobbyPayloads.Bool(true));
    Assert(await ErrorCodeAsync(kHost, MessageType.SetupAdvance, ++k, SetupPayloads.Advance(SetupPhase.Scenario)) == 6, "a continued game skips the new-game setup");
    LobbyView kGenerating = await StateAsync(kHost, MessageType.SetupAdvance, ++k, SetupPayloads.Advance(SetupPhase.Generating));
    Assert(kGenerating.Phase == SetupPhase.Generating && kGenerating.Players.Single(p => p.Key == kGuestKey).FactionSlot == 2, "start goes to the game transfer");
    await TransferGameAsync(kHost, kGuest, k += 100);
    LobbyView kLate = await StateAsync(kAbsent, MessageType.JoinLobby, ++k, LobbyPayloads.GuidValue(kRoom.Id));
    Assert(kLate.Players.Single(p => p.Key == kAbsentKey).FactionSlot == 1 && kLate.Players.Single(p => p.Key == kAbsentKey).Storyteller == "Cassandra",
        "an absent player of the save joins the running game with its faction");
    Assert(await ErrorCodeAsync(kStranger, MessageType.JoinLobby, ++k, LobbyPayloads.GuidValue(kRoom.Id)) == 6, "still no faction for the stranger");

    // Coop on the same server: all faction messages are refused.
    using LobbyClient cpHost = await LobbyClient.ConnectAsync("127.0.0.1", kPort, "CoopHost", TimeSpan.FromSeconds(4), CancellationToken.None, PlayerKey.Local(Guid.NewGuid()));
    await StateAsync(cpHost, MessageType.CreateLobby, ++k, LobbyPayloads.Create("Coop", 4));
    Assert(await ErrorCodeAsync(cpHost, MessageType.FactionRestore, ++k, savedTable.ToPayload()) == 6, "Coop: no faction table");
    Assert(await ErrorCodeAsync(cpHost, MessageType.FactionSiteChoice, ++k, FactionPayloads.SiteChoice(new SiteTile(1, 0))) == 6, "Coop: no own site");
    Assert(await ErrorCodeAsync(cpHost, MessageType.FactionIdeologyChoice, ++k, FactionPayloads.IdeologyChoice("I", new byte[] { 1 })) == 6, "Coop: no own ideology");
    Assert(await ErrorCodeAsync(cpHost, MessageType.FactionChoiceClear, ++k, FactionPayloads.ClearChoice(0, FactionChoiceKind.Site, "x")) == 6, "Coop: nothing to reject");
}
PlayerKey pHostKey = PlayerKey.Local(Guid.NewGuid()), pGuestKey = PlayerKey.Local(Guid.NewGuid());
await using (var prodContinue = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Fortsetzen", 4, pHostKey, mode: LobbyMode.Factions(FactionRelation.Neutral)))
{
    LobbyView pRoom = LobbyPayloads.ReadState((await ReceiveAsync(prodContinue.Client, MessageType.LobbyStateUpdate)).Payload);
    await StateAsync(prodContinue.Client, MessageType.FactionRestore, 4500, new FactionRestore(new[] { new FactionRestoreEntry(pHostKey, 0, "Randy"),
        new FactionRestoreEntry(pGuestKey, 1, "Randy") }).ToPayload());
    using LobbyClient pGuest = await LobbyClient.ConnectAsync("127.0.0.1", prodContinue.Port, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, pGuestKey);
    await StateAsync(pGuest, MessageType.JoinLobby, 4501, LobbyPayloads.GuidValue(pRoom.Id));
    await StateAsync(prodContinue.Client, MessageType.PlayerReadyChanged, 4502, LobbyPayloads.Bool(true));
    await StateAsync(pGuest, MessageType.PlayerReadyChanged, 4503, LobbyPayloads.Bool(true));
    Assert(await ErrorCodeAsync(prodContinue.Client, MessageType.SetupAdvance, 4504, SetupPayloads.Advance(SetupPhase.Generating)) == 6,
        "continuing a Factions game is refused on the real hosting path");
}
Console.WriteLine("PASS: continue a saved Factions game end to end: host-only restore, only the save's players join, start to the game transfer, late join with the saved faction, Coop refuses all faction messages, locked via ListenHost (ADR 0038)");

// SAVE-002, Coop part (ADR 0027): the host restores the save's player list over code 105; only its players join; the start
// goes straight to the game transfer; through ListenHost (the real hosting path) continuing is allowed.
PlayerKey sHostKey = PlayerKey.Local(Guid.NewGuid()), sGuestKey = PlayerKey.Local(Guid.NewGuid()), sAbsentKey = PlayerKey.Local(Guid.NewGuid());
var savedPlayers = new SessionRestore(new[] { sHostKey, sGuestKey, sAbsentKey });
await using (var coopContinue = new FoundationServer(new HostConfig { BindAddress = "127.0.0.1", Port = 0 }))
{
    await coopContinue.StartAsync();
    int sPort = coopContinue.BoundPort;
    using LobbyClient sHost = await LobbyClient.ConnectAsync("127.0.0.1", sPort, "Host", TimeSpan.FromSeconds(4), CancellationToken.None, sHostKey);
    using LobbyClient sGuest = await LobbyClient.ConnectAsync("127.0.0.1", sPort, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, sGuestKey);
    using LobbyClient sStranger = await LobbyClient.ConnectAsync("127.0.0.1", sPort, "Stranger", TimeSpan.FromSeconds(4), CancellationToken.None, PlayerKey.Local(Guid.NewGuid()));
    using LobbyClient sAbsent = await LobbyClient.ConnectAsync("127.0.0.1", sPort, "Absent", TimeSpan.FromSeconds(4), CancellationToken.None, sAbsentKey);
    uint s = 4600;
    LobbyView sRoom = await StateAsync(sHost, MessageType.CreateLobby, ++s, LobbyPayloads.Create("Koop fortsetzen", 4));
    Assert(await ErrorCodeAsync(sGuest, MessageType.SessionRestore, ++s, savedPlayers.ToPayload()) != 0, "a player outside a lobby cannot restore");
    LobbyView sRestored = await StateAsync(sHost, MessageType.SessionRestore, ++s, savedPlayers.ToPayload());
    Assert(sRestored.Restored && sRestored.Mode.Mode == GameMode.Coop && sRestored.Players.Single().FactionSlot == null, "the lobby continues the Coop save");
    Assert(await ErrorCodeAsync(sStranger, MessageType.JoinLobby, ++s, LobbyPayloads.GuidValue(sRoom.Id)) == 6, "a player outside the save cannot join");
    LobbyView sJoined = await StateAsync(sGuest, MessageType.JoinLobby, ++s, LobbyPayloads.GuidValue(sRoom.Id));
    Assert(sJoined.Restored && sJoined.Players.Count == 2, "a player of the save joins");
    Assert(await ErrorCodeAsync(sGuest, MessageType.SessionRestore, ++s, savedPlayers.ToPayload()) == 3, "only the host restores");
    await StateAsync(sHost, MessageType.PlayerReadyChanged, ++s, LobbyPayloads.Bool(true));
    await StateAsync(sGuest, MessageType.PlayerReadyChanged, ++s, LobbyPayloads.Bool(true));
    Assert(await ErrorCodeAsync(sHost, MessageType.SetupAdvance, ++s, SetupPayloads.Advance(SetupPhase.Scenario)) == 6, "a continued game skips the new-game setup");
    LobbyView sGenerating = await StateAsync(sHost, MessageType.SetupAdvance, ++s, SetupPayloads.Advance(SetupPhase.Generating));
    Assert(sGenerating.Phase == SetupPhase.Generating && !sGenerating.Restored, "start goes to the game transfer");
    await TransferGameAsync(sHost, sGuest, s += 100);
    LobbyView sLate = await StateAsync(sAbsent, MessageType.JoinLobby, ++s, LobbyPayloads.GuidValue(sRoom.Id));
    Assert(sLate.Phase == SetupPhase.Playing && sLate.Players.Any(p => p.Key == sAbsentKey), "an absent player of the save joins the running game later");
    using LobbyClient fHost = await LobbyClient.ConnectAsync("127.0.0.1", sPort, "FactionHost", TimeSpan.FromSeconds(4), CancellationToken.None, PlayerKey.Local(Guid.NewGuid()));
    await StateAsync(fHost, MessageType.CreateLobby, ++s, LobbyPayloads.Create("Fraktionen", 4, LobbyMode.Factions(FactionRelation.Allied)));
    Assert(await ErrorCodeAsync(fHost, MessageType.SessionRestore, ++s, savedPlayers.ToPayload()) == 6, "Factions: no Coop player list");
}
PlayerKey lHostKey = PlayerKey.Local(Guid.NewGuid()), lGuestKey = PlayerKey.Local(Guid.NewGuid());
await using (var listenContinue = await ListenHost.StartAsync("127.0.0.1", 0, "Host", "Koop fortsetzen", 4, lHostKey))
{
    LobbyView lRoom = LobbyPayloads.ReadState((await ReceiveAsync(listenContinue.Client, MessageType.LobbyStateUpdate)).Payload);
    await StateAsync(listenContinue.Client, MessageType.SessionRestore, 4700, new SessionRestore(new[] { lHostKey, lGuestKey }).ToPayload());
    using LobbyClient lGuest = await LobbyClient.ConnectAsync("127.0.0.1", listenContinue.Port, "Guest", TimeSpan.FromSeconds(4), CancellationToken.None, lGuestKey);
    await StateAsync(lGuest, MessageType.JoinLobby, 4701, LobbyPayloads.GuidValue(lRoom.Id));
    await StateAsync(listenContinue.Client, MessageType.PlayerReadyChanged, 4702, LobbyPayloads.Bool(true));
    await StateAsync(lGuest, MessageType.PlayerReadyChanged, 4703, LobbyPayloads.Bool(true));
    LobbyView lGenerating = await StateAsync(listenContinue.Client, MessageType.SetupAdvance, 4704, SetupPayloads.Advance(SetupPhase.Generating));
    Assert(lGenerating.Phase == SetupPhase.Generating, "continuing a Coop game works on the real hosting path");
}
Console.WriteLine("PASS: continue a saved Coop game end to end: host-only restore over 105, only the save's players join, start to the game transfer, late join, Factions refuse the list, allowed via ListenHost (ADR 0027)");

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

// One selected pawn per given owner (SETUP-004 test helper).
// Walks a two-player lobby through setup to Playing, as the setup test above does.
// (SAVE-001/JOIN-001 end-to-end test is above, next to the lockstep test.)
static async Task ReachPlayingAsync(LobbyClient host, LobbyClient guest, PlayerKey hostKey, PlayerKey guestKey)
{
    uint id = 500;
    await StateAsync(host, MessageType.PlayerReadyChanged, ++id, LobbyPayloads.Bool(true));
    await StateAsync(guest, MessageType.PlayerReadyChanged, ++id, LobbyPayloads.Bool(true));
    foreach (SetupPhase next in new[] { SetupPhase.Scenario, SetupPhase.Storyteller, SetupPhase.WorldParams })
        await StateAsync(host, MessageType.SetupAdvance, ++id, SetupPayloads.Advance(next));
    await FinishSetupAsync(host, guest, hostKey, guestKey, id);
}

// From WorldParams on: world, site, characters and the game start, until both landed in Playing. Coop: the host decides the
// site. Factions (ADR 0038): each player chooses an own site and ideology and reports done.
static async Task<LobbyView> FinishSetupAsync(LobbyClient host, LobbyClient guest, PlayerKey hostKey, PlayerKey guestKey, uint id,
    bool factions = false)
{
    await StateAsync(host, MessageType.SetupAdvance, ++id, SetupPayloads.Advance(SetupPhase.WorldTransfer));
    byte[] world = Enumerable.Repeat((byte)5, 32).ToArray();
    await StateAsync(host, MessageType.WorldReady, ++id, SetupPayloads.Fingerprint(world));
    await StateAsync(guest, MessageType.WorldLoaded, ++id, SetupPayloads.Fingerprint(world));
    await StateAsync(host, MessageType.SetupAdvance, ++id, SetupPayloads.Advance(SetupPhase.SiteSelection));
    if (factions)
    {
        int tile = 10;
        foreach (LobbyClient player in new[] { host, guest })
        {
            await StateAsync(player, MessageType.FactionSiteChoice, ++id, FactionPayloads.SiteChoice(new SiteTile(tile++, 0)));
            await StateAsync(player, MessageType.PlayerReadyChanged, ++id, LobbyPayloads.Bool(true));
        }
        await StateAsync(host, MessageType.SetupAdvance, ++id, SetupPayloads.Advance(SetupPhase.Ideology));
        foreach (LobbyClient player in new[] { host, guest })
        {
            await StateAsync(player, MessageType.FactionIdeologyChoice, ++id, FactionPayloads.IdeologyChoice("Ideo", new byte[] { 7 }));
            await StateAsync(player, MessageType.PlayerReadyChanged, ++id, LobbyPayloads.Bool(true));
        }
    }
    else await StateAsync(host, MessageType.SiteDecided, ++id, SetupPayloads.Site(new SiteTile(1, 0)));
    return await LandAsync(host, guest, hostKey, guestKey, id);
}

// From the end of Ideology on: characters, then the host sends the game and both land in Playing.
static async Task<LobbyView> LandAsync(LobbyClient host, LobbyClient guest, PlayerKey hostKey, PlayerKey guestKey, uint id)
{
    await StateAsync(host, MessageType.SetupAdvance, ++id, SetupPayloads.Advance(SetupPhase.Characters));
    await StateAsync(host, MessageType.PawnRoster, ++id, RosterFor(hostKey, guestKey).ToPayload());
    await StateAsync(host, MessageType.PlayerReadyChanged, ++id, LobbyPayloads.Bool(true));
    await StateAsync(guest, MessageType.PlayerReadyChanged, ++id, LobbyPayloads.Bool(true));
    await StateAsync(host, MessageType.SetupAdvance, ++id, SetupPayloads.Advance(SetupPhase.Generating));
    return await TransferGameAsync(host, guest, id);
}

// Generating: the host announces the game, the guest reports the same fingerprint, everyone lands in Playing.
static async Task<LobbyView> TransferGameAsync(LobbyClient host, LobbyClient guest, uint id)
{
    byte[] game = Enumerable.Repeat((byte)3, 32).ToArray();
    await StateAsync(host, MessageType.GameReady, ++id, SetupPayloads.Fingerprint(game));
    await StateAsync(guest, MessageType.GameLoaded, ++id, SetupPayloads.Fingerprint(game));
    LobbyView landed = await StateAsync(host, MessageType.SetupAdvance, ++id, SetupPayloads.Advance(SetupPhase.Playing));
    if (landed.Phase != SetupPhase.Playing) throw new Exception("FAIL: did not reach Playing");
    return landed;
}

// Waits for a pushed lobby state that fulfils the condition; older pushes are skipped.
static async Task<LobbyView> UntilStateAsync(LobbyClient client, Func<LobbyView, bool> condition)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (true)
    {
        while (client.TryDequeue(out WireMessage? message) && message != null)
            if (message.Type == MessageType.LobbyStateUpdate && LobbyPayloads.ReadState(message.Payload) is LobbyView state && condition(state))
                return state;
        await Task.Delay(10, timeout.Token);
    }
}

static PawnRoster RosterFor(params PlayerKey[] owners) => new PawnRoster(owners.Select((owner, i) => new PawnCard(100 + i, true, owner,
    "Pawn " + i, "P" + i, "Age 30", "", "", Array.Empty<string>(), Array.Empty<(string, byte, byte)>(),
    Array.Empty<string>(), Array.Empty<string>())).ToArray());
static void Assert(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
}
