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
    var (name, key) = LobbyPayloads.ReadHello(LobbyPayloads.Hello("Alice", steam));
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
    // The vanilla-like guest page: details travel with the card, portraits as their own message.
    var details = new PawnCardDetails("Malvina", "Nails", "Kim", 2, 55, 79, "Sanguophage", "x:Sanguophage", 0x80FF80FFu,
        new[] { ("Rechter Arm", "Kratznarbe (juckt)") }, new[] { ("Liebhaber", "Darrien 'Mal' Maliphalo", (short)35, (short)-15) },
        new[] { ("Hämogenpack", 12) });
    PawnCard withDetails = PawnRoster.Read(new PawnRoster(new[] { new PawnCard(9, true, owner, "Malvina 'Nails' Kim", "Nails", "", "", "",
        Array.Empty<string>(), Array.Empty<(string, byte, byte)>(), Array.Empty<string>(), Array.Empty<string>(), details) }).ToPayload()).Cards[0];
    PawnCardDetails d = withDetails.Details!;
    Equal("Kim", d.Last); Equal((ushort)79, d.AgeChronological); Equal("x:Sanguophage", d.XenotypeIcon); Equal(0x80FF80FFu, d.FavoriteColor);
    Equal("Kratznarbe (juckt)", d.Health[0].Label); Equal((short)-15, d.Relations[0].TheirOpinion); Equal(12, d.Possessions[0].Count);
    Equal(null, back.Cards[1].Details);
    var portrait = PawnPortrait.Read(new PawnPortrait(9, new byte[] { 137, 80, 78, 71 }, PawnPortrait.Headgear).ToPayload());
    Equal(9, portrait.PawnId); Equal(4, portrait.Png.Length); Equal(PawnPortrait.Headgear, portrait.Variant);
    Throws<ProtocolException>(() => new PawnPortrait(9, new byte[PawnPortrait.MaximumImageBytes + 1]));
    Throws<ProtocolException>(() => new PawnPortrait(9, Array.Empty<byte>()));
    Throws<ProtocolException>(() => new PawnPortrait(9, new byte[] { 1 }, PawnPortrait.Variants));
    // The rest of the host page for guests: names, xenotype (also custom), development stage, tooltips, abilities.
    var full = new PawnCardDetails("A", "B", "C", 1, 20, 20, "Hussar", "x:Hussar", 0, Array.Empty<(string, string)>(),
        Array.Empty<(string, string, short, short)>(), new[] { ("Medizin", 5) }, 4, "Kindheit-Tipp", "Erwachsen-Tipp",
        new[] { "Merkmal-Tipp" }, new[] { "Longjump" }, new[] { "MedicineIndustrial" });
    PawnCardDetails fullBack = PawnRoster.Read(new PawnRoster(new[] { new PawnCard(10, true, null, "A B C", "B", "", "", "",
        Array.Empty<string>(), Array.Empty<(string, byte, byte)>(), Array.Empty<string>(), Array.Empty<string>(), full) }).ToPayload()).Cards[0].Details!;
    Equal((byte)4, fullBack.DevelopmentStage); Equal("Erwachsen-Tipp", fullBack.AdulthoodTip); Equal("Merkmal-Tipp", fullBack.TraitTips[0]);
    Equal("Longjump", fullBack.Abilities[0]); Equal("MedicineIndustrial", fullBack.PossessionDefs[0]);
    var names = PawnRequest.Read(new PawnRequest(PawnRequestKind.SetName, 7, 0, PawnRequest.Names("Irma", "Ir", "Dominguez")).ToPayload());
    Equal(true, PawnRequest.TryReadNames(names.Text, out string first, out string nickName, out string last));
    Equal("Irma", first); Equal("Ir", nickName); Equal("Dominguez", last);
    Equal(false, PawnRequest.TryReadNames("only one", out _, out _, out _));
    Equal("Hussar", PawnXenotypeChoice.Parse(PawnXenotypeChoice.Standard("Hussar").ToText()).DefName);
    var custom = PawnXenotypeChoice.Parse(PawnRequest.Read(new PawnRequest(PawnRequestKind.SetXenotype, 7, 0,
        PawnXenotypeChoice.CustomXenotype("Blutsauger", true, "Fang", new[] { "Hemogenic", "Bloodfeeder" }).ToText()).ToPayload()).Text);
    Equal(true, custom.Custom); Equal("Blutsauger", custom.Name); Equal(true, custom.Inheritable); Equal("Fang", custom.IconDef);
    Equal("Bloodfeeder", custom.Genes[1]);
    Equal(0, PawnXenotypeChoice.Parse(PawnXenotypeChoice.CustomXenotype("Leer", false, "", Array.Empty<string>()).ToText()).Genes.Count);
    Throws<ProtocolException>(() => PawnXenotypeChoice.Parse("Hussar"));
    Throws<ProtocolException>(() => PawnXenotypeChoice.Parse("c:only-name"));
    return Task.CompletedTask;
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
