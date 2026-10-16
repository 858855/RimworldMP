using Multiplayer.Foundation.Protocol;

int passed = 0;
await Run("fragmented frame and second frame", async () =>
{
    using var stream = new FragmentingStream();
    var writer = new PayloadWriter();
    writer.WriteString("Spieler Ä");
    await FrameCodec.WriteAsync(stream, new WireMessage(MessageType.Hello, 42, writer.ToArray()));
    await FrameCodec.WriteAsync(stream, new WireMessage(MessageType.Ping, 0, Array.Empty<byte>()));
    stream.Position = 0;
    var first = await FrameCodec.ReadAsync(stream) ?? throw new Exception("missing first frame");
    Equal(MessageType.Hello, first.Type); Equal((uint)42, first.RequestId);
    var reader = new PayloadReader(first.Payload);
    Equal("Spieler Ä", reader.ReadString()); reader.EnsureComplete();
    Equal(MessageType.Ping, (await FrameCodec.ReadAsync(stream))!.Type);
    Equal(null, await FrameCodec.ReadAsync(stream));
});
await Run("invalid length and truncation", async () =>
{
    await Reject<ProtocolException>(new byte[] { 0, 1, 0, 1 });
    await Reject<ProtocolException>(new byte[] { 0, 0, 0, 5 });
    await Reject<EndOfStreamException>(new byte[] { 0, 0, 0, 6, 1 });
    await Reject<EndOfStreamException>(new byte[] { 0, 0 });
});
await Run("unknown version and type", async () =>
{
    await Reject<ProtocolException>(new byte[] { 0, 0, 0, 6, 1, 1, 0, 0, 0, 0 });
    await Reject<ProtocolException>(new byte[] { 0, 0, 0, 6, 1, 255, 0, 0, 0, 0 });
});
await Run("payload validation", () =>
{
    var writer = new PayloadWriter();
    writer.WriteGuid(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef"));
    writer.WriteUInt64(ulong.MaxValue);
    writer.WriteBool(true);
    var reader = new PayloadReader(writer.ToArray());
    Equal(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef"), reader.ReadGuid());
    Equal(ulong.MaxValue, reader.ReadUInt64()); Equal(true, reader.ReadBool()); reader.EnsureComplete();
    Throws<ProtocolException>(() => new PayloadReader(new byte[] { 2 }).ReadBool());
    Throws<ProtocolException>(() => new PayloadReader(new byte[] { 0, 1, 255 }).ReadString());
    Throws<ProtocolException>(() => new PayloadReader(new byte[] { 0, 2, 65 }).ReadString());
    Throws<ProtocolException>(() => new PayloadReader(new byte[] { 1 }).EnsureComplete());
    Throws<ProtocolException>(() => new PayloadWriter().WriteString(new string('x', 257)));
    return Task.CompletedTask;
});
await Run("outbound limit", async () =>
{
    using var stream = new MemoryStream();
    await ThrowsAsync<ProtocolException>(() => FrameCodec.WriteAsync(stream,
        new WireMessage(MessageType.Hello, 1, new byte[FrameCodec.MaximumBodyLength])));
    Equal(0L, stream.Length);
});
await Run("lobby chat payload and limits", () =>
{
    Guid room = Guid.NewGuid(), sender = Guid.NewGuid();
    byte[] request = LobbyPayloads.ChatRequest("Hallo Ä");
    Equal("Hallo Ä", LobbyPayloads.ReadChatRequest(request));
    LobbyChatView message = LobbyPayloads.ReadChatMessage(
        LobbyPayloads.ChatMessage(room, sender, "Alice", "Hallo Ä"));
    Equal(room, message.LobbyId); Equal(sender, message.SenderId);
    Equal("Alice", message.SenderName); Equal("Hallo Ä", message.Text);
    Throws<ProtocolException>(() => LobbyPayloads.ChatRequest("\n"));
    Throws<ProtocolException>(() => LobbyPayloads.ChatRequest(new string('x', 513)));
    Throws<ProtocolException>(() => LobbyPayloads.ReadChatRequest(new byte[] { 0, 1, 10 }));
    return Task.CompletedTask;
});
await Run("transfer payloads and limits", () =>
{
    Equal((byte)3, FrameCodec.Version);
    byte[] hash = new byte[32]; hash[0] = 7;
    Guid id = Guid.NewGuid(), peer = Guid.NewGuid();
    ulong length = 3UL * TransferPayloads.ChunkSize + 5;
    Equal(4u, TransferPayloads.ChunkCountFor(length));
    Equal(1u, TransferPayloads.ChunkCountFor(1));
    Equal(2u, TransferPayloads.ChunkCountFor(2UL * TransferPayloads.ChunkSize));
    Equal(TransferPayloads.ChunkSize, TransferPayloads.ChunkLength(length, 0));
    Equal(5, TransferPayloads.ChunkLength(length, 3));
    Throws<ProtocolException>(() => TransferPayloads.ChunkLength(length, 4));

    TransferBegin begin = TransferPayloads.ReadBegin(TransferPayloads.Begin(
        new TransferBegin(id, TransferKind.World, peer, length, 4, hash)));
    Equal(id, begin.TransferId); Equal(TransferKind.World, begin.Kind); Equal(peer, begin.Peer);
    Equal(length, begin.TotalLength); Equal(4u, begin.ChunkCount); Equal((byte)7, begin.Sha256[0]);
    Equal(peer, begin.WithPeer(peer).Peer);

    Throws<ProtocolException>(() => TransferPayloads.Begin(new TransferBegin(id, TransferKind.World, peer, 0, 0, hash)));
    Throws<ProtocolException>(() => TransferPayloads.Begin(new TransferBegin(id, TransferKind.World, peer, length, 3, hash)));
    Throws<ProtocolException>(() => TransferPayloads.Begin(new TransferBegin(id, TransferKind.World, peer, length, 4, new byte[31])));
    Throws<ProtocolException>(() => TransferPayloads.Begin(new TransferBegin(Guid.Empty, TransferKind.World, peer, length, 4, hash)));
    Throws<ProtocolException>(() => TransferPayloads.Begin(new TransferBegin(id, (TransferKind)9, peer, length, 4, hash)));
    ulong manifestLimit = TransferPayloads.MaximumLength(TransferKind.ModManifest);
    Throws<ProtocolException>(() => TransferPayloads.Begin(new TransferBegin(id, TransferKind.ModManifest, peer,
        manifestLimit + 1, TransferPayloads.ChunkCountFor(manifestLimit + 1), hash)));
    TransferPayloads.Begin(new TransferBegin(id, TransferKind.ModManifest, peer, manifestLimit,
        TransferPayloads.ChunkCountFor(manifestLimit), hash));

    byte[] data = new byte[TransferPayloads.ChunkSize]; data[^1] = 9;
    TransferChunk chunk = TransferPayloads.ReadChunk(TransferPayloads.Chunk(new TransferChunk(id, 2, data)));
    Equal(id, chunk.TransferId); Equal(2u, chunk.Index); Equal(TransferPayloads.ChunkSize, chunk.Data.Length); Equal((byte)9, chunk.Data[^1]);
    Throws<ProtocolException>(() => TransferPayloads.Chunk(new TransferChunk(id, 0, new byte[TransferPayloads.ChunkSize + 1])));
    Throws<ProtocolException>(() => TransferPayloads.Chunk(new TransferChunk(id, 0, Array.Empty<byte>())));
    var oversized = new PayloadWriter(); oversized.WriteGuid(id); oversized.WriteUInt32(0); oversized.WriteUInt32(TransferPayloads.ChunkSize + 1);
    Throws<ProtocolException>(() => TransferPayloads.ReadChunk(oversized.ToArray()));
    // A full chunk frame fits the 64 KiB frame body and Steam's per-message limit.
    Equal(true, FrameCodec.MinimumBodyLength + TransferPayloads.Chunk(new TransferChunk(id, 0, data)).Length <= FrameCodec.MaximumBodyLength);

    var ack = TransferPayloads.ReadAck(TransferPayloads.Ack(id, 42));
    Equal(id, ack.TransferId); Equal(42u, ack.Received);
    Throws<ProtocolException>(() => TransferPayloads.ReadAck(TransferPayloads.Ack(id, 1).Concat(new byte[] { 0 }).ToArray()));
    TransferAbort abort = TransferPayloads.ReadAbort(TransferPayloads.Abort(new TransferAbort(id, TransferAbortCode.PeerLeft, "gone")));
    Equal(TransferAbortCode.PeerLeft, abort.Code); Equal("gone", abort.Reason);
    var badCode = new PayloadWriter(); badCode.WriteGuid(id); badCode.WriteByte(99); badCode.WriteString("x");
    Throws<ProtocolException>(() => TransferPayloads.ReadAbort(badCode.ToArray()));
    Equal(id, TransferPayloads.ReadTransferId(TransferPayloads.Ack(id, 0)));
    // Plan §6.4 asked for 32 chunks in flight; that would exceed SteamPeerStream's 1 MiB queue (ADR 0014).
    Equal(true, (TransferPayloads.MaximumWindowChunks * (TransferPayloads.ChunkSize + 64)) < 512 * 1024);
    return Task.CompletedTask;
});
await Run("player key and hello", () =>
{
    PlayerKey steam = PlayerKey.Steam(76561198000000001UL);
    PlayerKey local = PlayerKey.Local(Guid.Parse("0123456789abcdef0123456789abcdef"));
    Equal("steam:76561198000000001", steam.ToString());
    Equal("local:0123456789abcdef0123456789abcdef", local.ToString());
    Equal(true, PlayerKey.TryParse(steam.ToString(), out PlayerKey parsedSteam) && parsedSteam == steam);
    Equal(true, PlayerKey.TryParse(local.ToString(), out PlayerKey parsedLocal) && parsedLocal == local);
    foreach (string bad in new[] { "", "steam:0", "steam:-1", "steam: 5", "local:", "local:00000000000000000000000000000000", "x:1", "STEAM:5" })
        Equal(false, PlayerKey.TryParse(bad, out _));
    Equal(false, steam == local);
    Equal(false, default(PlayerKey).IsValid);
    Throws<ArgumentOutOfRangeException>(() => PlayerKey.Steam(0));
    Throws<ArgumentOutOfRangeException>(() => PlayerKey.Local(Guid.Empty));
    var (name, key, noProof) = LobbyPayloads.ReadHello(LobbyPayloads.Hello("Alice", steam));
    Equal(true, noProof == null);
    Equal("Alice", name); Equal(steam, key);
    Equal(local, LobbyPayloads.ReadHello(LobbyPayloads.Hello("Bob", local)).Key);
    Throws<ProtocolException>(() => LobbyPayloads.Hello("Alice", default));
    var oldHello = new PayloadWriter(); oldHello.WriteString("Alice", 160);
    Throws<ProtocolException>(() => LobbyPayloads.ReadHello(oldHello.ToArray()));
    var badKind = new PayloadWriter(); badKind.WriteString("Alice", 160); badKind.WriteByte(9); badKind.WriteUInt64(5);
    Throws<ProtocolException>(() => LobbyPayloads.ReadHello(badKind.ToArray()));
    var zeroSteam = new PayloadWriter(); zeroSteam.WriteString("Alice", 160); zeroSteam.WriteByte(1); zeroSteam.WriteUInt64(0);
    Throws<ProtocolException>(() => LobbyPayloads.ReadHello(zeroSteam.ToArray()));
    return Task.CompletedTask;
});
await Run("setup payloads and lobby state", () =>
{
    var tile = new SiteTile(123456, 1);
    Equal(tile, SetupPayloads.ReadSite(SetupPayloads.Site(tile)));
    Equal(tile, SetupPayloads.ReadFlag(SetupPayloads.Flag(tile))!.Value);
    Equal(false, SetupPayloads.ReadFlag(SetupPayloads.Flag(null)).HasValue);
    Equal(SetupPhase.Characters, SetupPayloads.ReadAdvance(SetupPayloads.Advance(SetupPhase.Characters)));
    Throws<ProtocolException>(() => SetupPayloads.Advance((SetupPhase)200));
    Equal(SetupPhase.Playing, SetupPayloads.ReadAdvance(SetupPayloads.Advance(SetupPhase.Playing)));     // SETUP-005
    Throws<ProtocolException>(() => SetupPayloads.ReadAdvance(new byte[] { 10 }));
    Throws<ProtocolException>(() => SetupPayloads.ReadAdvance(new byte[] { 200 }));
    Throws<ArgumentOutOfRangeException>(() => new SiteTile(-1, 0));
    Throws<ArgumentOutOfRangeException>(() => new SiteTile(0, -1));
    var negative = new PayloadWriter(); negative.WriteUInt32(uint.MaxValue); negative.WriteUInt32(0);
    Throws<ProtocolException>(() => SetupPayloads.ReadSite(negative.ToArray()));
    Throws<ProtocolException>(() => SetupPayloads.ReadSite(SetupPayloads.Site(tile).Concat(new byte[] { 0 }).ToArray()));
    Equal(false, new SiteTile(5, 0) == new SiteTile(5, 1));

    var key = PlayerKey.Steam(76561198000000001UL);
    var view = new LobbyView(Guid.NewGuid(), "Room", Guid.NewGuid(), 7, 4,
        new[] { new PlayerView(Guid.NewGuid(), "A", true, key, tile), new PlayerView(Guid.NewGuid(), "B", false, key) },
        SetupPhase.Ideology, new SiteTile(9, 0));
    LobbyView back = LobbyPayloads.ReadState(LobbyPayloads.State(view));
    Equal(SetupPhase.Ideology, back.Phase); Equal(new SiteTile(9, 0), back.Site!.Value);
    Equal(tile, back.Players[0].Flag!.Value); Equal(false, back.Players[1].Flag.HasValue);
    var summary = LobbyPayloads.ReadList(LobbyPayloads.List(new[] { new LobbySummary(Guid.NewGuid(), "R", "H", 2, 4, SetupPhase.Characters) }));
    Equal(SetupPhase.Characters, summary[0].Phase);
    return Task.CompletedTask;
});
await Run("mod manifest, diff report and content hash", () =>
{
    byte[] h(byte v) => Enumerable.Repeat(v, 32).ToArray();
    ModEntry core = new("Ludeon.RimWorld", "Core", "", h(1)), harmony = new("brrainz.harmony", "Harmony", "2.3", h(2)),
        ui = new("some.ui", "UI Mod", "1.0", h(3)), big = new("big.mod", "Big", "1.0", h(4));
    var host = new ModManifest("1.6.4871 rev590", new[] { core, harmony, ui, big });
    var copy = ModManifest.Deserialize(host.Serialize());
    Equal(true, copy.Hash().SequenceEqual(host.Hash())); Equal("ludeon.rimworld", copy.Mods[0].PackageId);
    Equal(true, host.Summary().SameAs(ModManifestSummary.Read(host.Summary().ToPayload())));
    Equal(true, ModManifestDiff.Compare(copy, host).IsMatch);
    var reordered = new ModManifest("1.6.4871 rev590", new[] { core, ui, harmony, big });
    var order = ModManifestDiff.Compare(reordered, host);
    Equal(false, order.IsMatch); Equal(1, order.FirstOrderDifference);
    Equal(false, reordered.Hash().SequenceEqual(host.Hash()));
    var otherBuild = new ModManifest("1.6.4871 rev600", host.Mods);
    Equal(true, otherBuild.Hash().SequenceEqual(host.Hash())); // checksum covers mods only, not the game version
    var buildOnly = ModManifestDiff.Compare(otherBuild, host);
    Equal(true, buildOnly.IsMatch); Equal(true, buildOnly.GameVersionDiffers);
    var renamed = new ModManifest(host.GameVersion, host.Mods.Select(m => new ModEntry(m.PackageId, "Anderer Name", m.Version, m.ContentHash)).ToArray());
    Equal(true, renamed.Hash().SequenceEqual(host.Hash())); // display names do not count either
    var changed = new ModManifest("1.6.4871 rev600", new[] { core, new ModEntry("brrainz.harmony", "Harmony", "2.4", h(9)), ui, new ModEntry("extra.mod", "Extra", "", h(5)) });
    var diff = ModManifestDiff.Compare(changed, host);
    Equal(true, diff.GameVersionDiffers); Equal("big.mod", diff.MissingLocally.Single().PackageId);
    Equal("extra.mod", diff.ExtraLocally.Single().PackageId); Equal("2.4", diff.Changed.Single().Local.Version);
    Equal(-1, diff.FirstOrderDifference);
    Throws<ProtocolException>(() => ModManifest.Deserialize(new byte[] { 0, 0, 255, 255 }));
    var longName = new ModManifest("x", new[] { new ModEntry("a.b", new string('Ä', 200), "", h(1)) });
    Equal(80, ModManifest.Deserialize(longName.Serialize()).Mods[0].Name.Length); // cut at 160 UTF-8 bytes
    string dir = Path.Combine(Path.GetTempPath(), "mf-" + Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(Path.Combine(dir, "About")); Directory.CreateDirectory(Path.Combine(dir, "1.6", "Defs"));
        Directory.CreateDirectory(Path.Combine(dir, "Textures"));
        File.WriteAllText(Path.Combine(dir, "About", "About.xml"), "<ModMetaData/>");
        File.WriteAllText(Path.Combine(dir, "1.6", "Defs", "a.xml"), "<Defs/>");
        File.WriteAllText(Path.Combine(dir, "Textures", "t.png"), "png");
        string[] folders = { Path.Combine(dir, "1.6"), dir };
        byte[] first = ModContentHasher.Hash(dir, folders);
        File.WriteAllText(Path.Combine(dir, "Textures", "t.png"), "other texture");
        Equal(true, first.SequenceEqual(ModContentHasher.Hash(dir, folders))); // textures ignored
        File.WriteAllText(Path.Combine(dir, "1.6", "Defs", "a.xml"), "<Defs><X/></Defs>");
        Equal(false, first.SequenceEqual(ModContentHasher.Hash(dir, folders))); // defs counted
        Equal(false, first.SequenceEqual(ModContentHasher.Hash(dir, new[] { dir }))); // load folders counted
    }
    finally { Directory.Delete(dir, true); }
    return Task.CompletedTask;
});
await Run("mod status and host mods in lobby payloads", () =>
{
    var summary = new ModManifestSummary(Enumerable.Repeat((byte)3, 32).ToArray(), "1.6.4871 rev591", 7);
    var view = new LobbyView(Guid.NewGuid(), "R", Guid.NewGuid(), 4, 4, new[]
    {
        new PlayerView(Guid.NewGuid(), "A", true, PlayerKey.Local(Guid.NewGuid()), null, ModStatus.Match),
        new PlayerView(Guid.NewGuid(), "B", false, PlayerKey.Steam(76561198000000001UL), new SiteTile(1, 0), ModStatus.Mismatch)
    });
    LobbyView back = LobbyPayloads.ReadState(LobbyPayloads.State(view));
    Equal(ModStatus.Match, back.Players[0].ModStatus); Equal(ModStatus.Mismatch, back.Players[1].ModStatus);
    var listed = LobbyPayloads.ReadList(LobbyPayloads.List(new[] { new LobbySummary(Guid.NewGuid(), "R", "A", 2, 4, SetupPhase.Lobby, summary),
        new LobbySummary(Guid.NewGuid(), "S", "B", 1, 4) }));
    Equal(true, listed[0].HostMods!.SameAs(summary)); Equal("1.6.4871 rev591", listed[0].HostMods!.GameVersion);
    Equal(true, listed[1].HostMods == null);
    return Task.CompletedTask;
});
await Run("setup settings mirror payload", () =>
{
    var settings = new SetupSettings("Welt erstellen", new[] { ("Seed", "icy"), ("Abdeckung", "50 %"), ("Fraktionen", "Ä, Ö") });
    SetupSettings back = SetupSettings.Read(settings.ToPayload());
    Equal(true, back.SameAs(settings)); Equal("icy", back.Entries[0].Value);
    Throws<ProtocolException>(() => new SetupSettings("x", Enumerable.Range(0, 25).Select(i => ("a", "b")).ToArray()));
    Throws<ProtocolException>(() => new SetupSettings("x", new[] { ("a", new string('x', 257)) }).ToPayload());
    Throws<ProtocolException>(() => SetupSettings.Read(new byte[] { 0, 0, 25 }));
    return Task.CompletedTask;
});
await Run("world fingerprint and world status", () =>
{
    byte[] a = new FingerprintBuilder().Add("Temperate forest").Add(3).Add(19.74f, 1).Finish();
    byte[] b = new FingerprintBuilder().Add("Temperate forest").Add(3).Add(19.70f, 1).Finish();
    byte[] c = new FingerprintBuilder().Add("Temperate forest").Add(3).Add(19.66f, 1).Finish();
    Equal(32, a.Length); Equal(true, a.SequenceEqual(b)); Equal(true, a.SequenceEqual(c)); // rounding to one decimal
    Equal(false, a.SequenceEqual(new FingerprintBuilder().Add("Temperate forest").Add(4).Add(19.7f, 1).Finish()));
    Equal(false, new FingerprintBuilder().Add("ab").Add("c").Finish().SequenceEqual(new FingerprintBuilder().Add("a").Add("bc").Finish()));
    Equal(true, SetupPayloads.ReadFingerprint(SetupPayloads.Fingerprint(a)).SequenceEqual(a));
    Throws<ProtocolException>(() => SetupPayloads.ReadFingerprint(new byte[31]));
    var view = new LobbyView(Guid.NewGuid(), "R", Guid.NewGuid(), 2, 4, new[]
    { new PlayerView(Guid.NewGuid(), "A", false, PlayerKey.Local(Guid.NewGuid()), null, ModStatus.Match, WorldStatus.Mismatch) },
        SetupPhase.WorldTransfer);
    Equal(WorldStatus.Mismatch, LobbyPayloads.ReadState(LobbyPayloads.State(view)).Players[0].WorldStatus);
    return Task.CompletedTask;
});
await Run("pawn roster and requests", () =>
{
    PlayerKey owner = PlayerKey.Steam(76561198000000002UL);
    var card = new PawnCard(7, true, owner, "Malvina 'Nails' Kim", "Nails", "Frau, 55, Baseliner", "Kinderstar", "Geologin",
        new[] { "Misandrist", "Wimp" }, new[] { ("Bergbau", (byte)13, (byte)2), ("Schießen", (byte)255, (byte)0) },
        new[] { "Einfache Arbeit" }, Array.Empty<string>());
    var roster = new PawnRoster(new[] { card, new PawnCard(8, false, null, "Hunin Hune", "Hunin", "", "", "", Array.Empty<string>(),
        Array.Empty<(string, byte, byte)>(), Array.Empty<string>(), Array.Empty<string>()) });
    PawnRoster back = PawnRoster.Read(roster.ToPayload());
    Equal(2, back.Cards.Count); Equal("Nails", back.Cards[0].ShortName); Equal(owner, back.Cards[0].Owner!.Value);
    Equal((byte)13, back.Cards[0].Skills[0].Level); Equal(false, back.Cards[1].Selected); Equal(true, back.OwnsSelectedPawn(owner));
    Equal(true, back.OwnerHash(owner).SequenceEqual(roster.OwnerHash(owner)));
    Equal(false, back.OwnerHash(owner).SequenceEqual(back.OwnerHash(PlayerKey.Local(Guid.NewGuid()))));
    var request = PawnRequest.Read(new PawnRequest(PawnRequestKind.Rename, 7, 0, "Steini").WithRequester(owner).ToPayload());
    Equal(PawnRequestKind.Rename, request.Kind); Equal("Steini", request.Text); Equal(owner, request.Requester!.Value);
    Throws<ProtocolException>(() => PawnRequest.Read(new byte[] { 9, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0 }));
    Throws<ProtocolException>(() => new PawnRoster(Enumerable.Range(0, 25).Select(i => card).ToArray()));
    return Task.CompletedTask;
});
await Run("game mode (ADR 0031)", () =>
{
    foreach (LobbyMode mode in new[] { LobbyMode.CoopHost, LobbyMode.CoopDemocracy, LobbyMode.Factions(FactionRelation.Allied),
        LobbyMode.Factions(FactionRelation.Neutral), LobbyMode.Factions(FactionRelation.Hostile) })
    {
        var created = LobbyPayloads.ReadCreate(LobbyPayloads.Create("Room", 4, mode));
        Equal(mode, created.Mode); Equal("Room", created.Name);
    }
    Equal(LobbyMode.CoopHost, LobbyPayloads.ReadCreate(LobbyPayloads.Create("Room", 4)).Mode);
    Throws<ArgumentOutOfRangeException>(() => LobbyMode.Factions(FactionRelation.None));
    foreach (byte[] bad in new[] { new byte[] { 1, 0, 0 }, new byte[] { 1, 1, 1 }, new byte[] { 2, 1, 1 }, new byte[] { 2, 0, 0 }, new byte[] { 3, 1, 0 } })
    {
        var writer = new PayloadWriter(); writer.WriteString("Room", 160); writer.WriteByte(4);
        foreach (byte b in bad) writer.WriteByte(b);
        Throws<ProtocolException>(() => LobbyPayloads.ReadCreate(writer.ToArray()));
    }
    var view = new LobbyView(Guid.NewGuid(), "R", Guid.NewGuid(), 1, 4, Array.Empty<PlayerView>(), SetupPhase.Lobby, null,
        LobbyMode.Factions(FactionRelation.Hostile));
    Equal(LobbyMode.Factions(FactionRelation.Hostile), LobbyPayloads.ReadState(LobbyPayloads.State(view)).Mode);
    var listed = LobbyPayloads.ReadList(LobbyPayloads.List(new[] { new LobbySummary(Guid.NewGuid(), "R", "H", 1, 4, SetupPhase.Lobby, null, LobbyMode.CoopDemocracy) }));
    Equal(LobbyMode.CoopDemocracy, listed[0].Mode);
    return Task.CompletedTask;
});
await Run("key proof, password and list flag (ADR 0035)", () =>
{
    using var rsa = System.Security.Cryptography.RSA.Create(2048);
    var pub = rsa.ExportParameters(false);
    PlayerKey key = PlayerKey.Local(Guid.NewGuid());
    long now = 1_800_000_000;
    byte[] nonce = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
    byte[] signature = rsa.SignData(HelloProof.SignedBytes("Alice", key, now, nonce), System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    var proof = new HelloProof(pub.Modulus!, pub.Exponent!, now, nonce, signature);
    Equal(true, proof.Verify("Alice", key));
    Equal(false, proof.Verify("Bob", key));                                              // name is signed
    Equal(false, proof.Verify("Alice", PlayerKey.Local(Guid.NewGuid())));                // player key is signed
    byte[] tampered = (byte[])signature.Clone(); tampered[10] ^= 1;
    Equal(false, new HelloProof(pub.Modulus!, pub.Exponent!, now, nonce, tampered).Verify("Alice", key));
    Equal(false, new HelloProof(pub.Modulus!, pub.Exponent!, now + 1, nonce, signature).Verify("Alice", key));   // timestamp is signed
    Equal(false, new HelloProof(pub.Modulus!, pub.Exponent!, now, new byte[16], signature).Verify("Alice", key));  // nonce is signed
    var (name, sameKey, carried) = LobbyPayloads.ReadHello(LobbyPayloads.Hello("Alice", key, proof));
    Equal("Alice", name); Equal(key, sameKey); Equal(true, carried!.Verify("Alice", key));
    Equal(HelloProof.FingerprintText(proof.Fingerprint()), HelloProof.FingerprintText(carried.Fingerprint()));
    using var small = System.Security.Cryptography.RSA.Create(1024);
    var weak = small.ExportParameters(false);
    Throws<ProtocolException>(() => new HelloProof(weak.Modulus!, weak.Exponent!, now, nonce, new byte[128]));      // 1024 bit refused
    Throws<ProtocolException>(() => new HelloProof(pub.Modulus!, pub.Exponent!, now, nonce, new byte[100]));        // wrong signature size

    Guid lobby = Guid.NewGuid();
    Equal((lobby, (string?)null), LobbyPayloads.ReadJoin(LobbyPayloads.GuidValue(lobby)));                  // old JoinLobby still valid
    Equal((lobby, (string?)"geheim"), LobbyPayloads.ReadJoin(LobbyPayloads.Join(lobby, "geheim")));
    Throws<ProtocolException>(() => LobbyPayloads.Join(lobby, "bad\npassword"));
    Throws<ProtocolException>(() => LobbyPayloads.Join(lobby, new string('x', 65)));
    Equal("geheim", LobbyPayloads.ReadCreate(LobbyPayloads.Create("Room", 4, null, "geheim")).Password);
    Equal(true, LobbyPayloads.ReadCreate(LobbyPayloads.Create("Room", 4)).Password == null);
    var listed = LobbyPayloads.ReadList(LobbyPayloads.List(new[] { new LobbySummary(lobby, "R", "H", 1, 4, passwordRequired: true) }));
    Equal(true, listed[0].PasswordRequired);
    return Task.CompletedTask;
});
await Run("faction storytellers and shared difficulty (ADR 0037)", () =>
{
    var options = new FactionOptions("Rough", new[] { "Cassandra", "Phoebe", "Randy" });
    var back = FactionOptions.FromPayload(options.ToPayload());
    Equal("Rough", back.Difficulty); Equal(true, back.SameAs(options)); Equal(true, back.Offers("Phoebe")); Equal(false, back.Offers("phoebe"));
    Equal("Randy", FactionPayloads.ReadStorytellerChoice(FactionPayloads.StorytellerChoice("Randy")));
    Equal("Mod_Teller-2", FactionPayloads.ReadStorytellerChoice(FactionPayloads.StorytellerChoice("Mod_Teller-2")));
    Throws<ProtocolException>(() => new FactionOptions("Rough", Array.Empty<string>()));                         // nothing to choose
    Throws<ProtocolException>(() => new FactionOptions("Rough", Enumerable.Range(0, 33).Select(i => "T" + i).ToArray()));
    Throws<ProtocolException>(() => new FactionOptions("Rough", new[] { "Randy", "Randy" }));                   // duplicates
    Throws<ProtocolException>(() => new FactionOptions("", new[] { "Randy" }));
    Throws<ProtocolException>(() => new FactionOptions("Rough", new[] { "Rand y" }));                           // not a def name
    Throws<ProtocolException>(() => FactionPayloads.StorytellerChoice("Ränder"));
    Throws<ProtocolException>(() => FactionPayloads.StorytellerChoice(new string('a', 129)));
    Throws<ProtocolException>(() => FactionPayloads.ReadStorytellerChoice(LobbyPayloads.String("bad.name")));
    var wire = new PayloadWriter(); wire.WriteString("Rough"); wire.WriteByte(0);
    Throws<ProtocolException>(() => FactionOptions.FromPayload(wire.ToArray()));                                 // empty list on the wire

    PlayerKey hostKey = PlayerKey.Local(Guid.NewGuid()), guestKey = PlayerKey.Local(Guid.NewGuid());
    var players = new[]
    {
        new PlayerView(Guid.NewGuid(), "Host", true, hostKey, factionSlot: 0, storyteller: "Randy"),
        new PlayerView(Guid.NewGuid(), "Guest", false, guestKey, factionSlot: 1)
    };
    var factions = new LobbyView(Guid.NewGuid(), "F", players[0].Id, 7, 4, players, SetupPhase.Storyteller, null,
        LobbyMode.Factions(FactionRelation.Allied), options);
    LobbyView read = LobbyPayloads.ReadState(LobbyPayloads.State(factions));
    Equal(true, read.FactionOptions!.SameAs(options));
    Equal((byte?)0, read.Players[0].FactionSlot); Equal("Randy", read.Players[0].Storyteller);
    Equal((byte?)1, read.Players[1].FactionSlot); Equal(null, read.Players[1].Storyteller);
    Equal(guestKey, read.Players[1].Key); Equal(false, read.Players[1].Ready);
    var noOptions = new LobbyView(factions.Id, "F", players[0].Id, 7, 4, players, SetupPhase.Scenario, null, factions.Mode);
    Equal(null, LobbyPayloads.ReadState(LobbyPayloads.State(noOptions)).FactionOptions);
    // Compatibility: Coop states and Factions states before the setup end with the mode, exactly as before ADR 0037.
    var plain = players.Select(p => new PlayerView(p.Id, p.Name, p.Ready, p.Key)).ToArray();
    foreach (LobbyView unchanged in new[]
    {
        new LobbyView(factions.Id, "C", players[0].Id, 7, 4, plain, SetupPhase.Storyteller, null, LobbyMode.CoopDemocracy),
        new LobbyView(factions.Id, "F", players[0].Id, 7, 4, plain, SetupPhase.Lobby, null, factions.Mode)
    })
    {
        byte[] bytes = LobbyPayloads.State(unchanged);
        var modeOnly = new PayloadWriter(); unchanged.Mode.Write(modeOnly);
        Equal(true, bytes.Skip(bytes.Length - 3).SequenceEqual(modeOnly.ToArray()));                     // nothing after the mode
        Equal(false, LobbyPayloads.HasFactionBlock(unchanged.Mode, unchanged.Phase));
        Throws<ProtocolException>(() => LobbyPayloads.ReadState(bytes.Concat(new byte[] { 0, 0, 0 }).ToArray()));  // extra bytes refused
    }
    // Faction data where no block belongs is refused instead of silently dropped.
    Throws<ProtocolException>(() => LobbyPayloads.State(new LobbyView(factions.Id, "C", players[0].Id, 7, 4, players,
        SetupPhase.Storyteller, null, LobbyMode.CoopHost)));
    Throws<ProtocolException>(() => LobbyPayloads.State(new LobbyView(factions.Id, "C", players[0].Id, 7, 4, plain,
        SetupPhase.Storyteller, null, LobbyMode.CoopHost, options)));
    Throws<ProtocolException>(() => LobbyPayloads.State(new LobbyView(factions.Id, "F", players[0].Id, 7, 4, players,
        SetupPhase.Lobby, null, factions.Mode)));
    // Once the Factions setup has started the block is required: a reader that stops after the mode (as before ADR 0037)
    // finds extra bytes, and a state cut after the mode is refused by the current reader.
    byte[] withBlock = LobbyPayloads.State(factions);
    byte[] sameAsCoop = LobbyPayloads.State(new LobbyView(factions.Id, "F", players[0].Id, 7, 4, plain, SetupPhase.Storyteller,
        null, LobbyMode.CoopHost));
    Equal(true, withBlock.Length > sameAsCoop.Length && withBlock.Take(sameAsCoop.Length - 3).SequenceEqual(sameAsCoop.Take(sameAsCoop.Length - 3)));
    Throws<ProtocolException>(() => LobbyPayloads.ReadState(withBlock.Take(sameAsCoop.Length).ToArray()));
    var twice = new[] { new PlayerView(Guid.NewGuid(), "A", false, hostKey, factionSlot: 2), new PlayerView(Guid.NewGuid(), "B", false, guestKey, factionSlot: 2) };
    Throws<ProtocolException>(() => LobbyPayloads.ReadState(LobbyPayloads.State(new LobbyView(factions.Id, "F", twice[0].Id, 7, 4, twice,
        SetupPhase.Storyteller, null, factions.Mode))));                                                       // one slot per player
    Throws<ProtocolException>(() => LobbyPayloads.State(new LobbyView(factions.Id, "F", players[0].Id, 7, 4,
        new[] { new PlayerView(Guid.NewGuid(), "A", false, hostKey, factionSlot: 64) }, SetupPhase.Storyteller, null, factions.Mode)));
    return Task.CompletedTask;
});
await Run("faction message codes 96/97 in the frame, 98 not yet (ADR 0037)", async () =>
{
    Equal((byte)96, (byte)MessageType.FactionOptions); Equal((byte)97, (byte)MessageType.StorytellerChoice);
    using var stream = new FragmentingStream();
    byte[] choice = FactionPayloads.StorytellerChoice("Randy");
    await FrameCodec.WriteAsync(stream, new WireMessage(MessageType.StorytellerChoice, 7, choice));
    await FrameCodec.WriteAsync(stream, new WireMessage(MessageType.FactionOptions, 8, new FactionOptions("Rough", new[] { "Randy" }).ToPayload()));
    stream.Position = 0;
    WireMessage first = (await FrameCodec.ReadAsync(stream))!, second = (await FrameCodec.ReadAsync(stream))!;
    Equal(MessageType.StorytellerChoice, first.Type); Equal("Randy", FactionPayloads.ReadStorytellerChoice(first.Payload));
    Equal(MessageType.FactionOptions, second.Type); Equal("Rough", FactionOptions.FromPayload(second.Payload).Difficulty);
    await Reject<ProtocolException>(new byte[] { 0, 0, 0, 6, 3, 98, 0, 0, 0, 0 });   // rest of 96-111 is reserved, not valid
});
Console.WriteLine($"PASS: {passed} protocol test groups");

async Task Run(string name, Func<Task> body)
{
    await body(); passed++; Console.WriteLine($"PASS: {name}");
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"expected {expected}, got {actual}");
}
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception($"expected {typeof(T).Name}");
}
static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new Exception($"expected {typeof(T).Name}");
}
static async Task Reject<T>(byte[] bytes) where T : Exception
{
    using var stream = new FragmentingStream();
    await stream.WriteAsync(bytes);
    stream.Position = 0;
    await ThrowsAsync<T>(async () => { await FrameCodec.ReadAsync(stream); });
}
sealed class FragmentingStream : MemoryStream
{
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => base.ReadAsync(buffer, offset, Math.Min(count, 1), cancellationToken);
}
