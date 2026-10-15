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
await Run("player key and hello payload", () =>
{
    string steam = PlayerKeys.Steam(76561197960287930UL), local = PlayerKeys.Local(Guid.NewGuid());
    Equal(true, PlayerKeys.IsValid(steam)); Equal(true, PlayerKeys.IsValid(local));
    foreach (string bad in new[] { "", "steam:", "steam:0", "steam:012", "steam:18446744073709551616", "local:abc",
        "local:" + new string('0', 32), "local:" + new string('A', 32), "other:1", "steam:1 " })
        Equal(false, PlayerKeys.IsValid(bad));
    var hello = LobbyPayloads.ReadHello(LobbyPayloads.Hello("Spieler Ä", steam));
    Equal("Spieler Ä", hello.Name); Equal(steam, hello.Key);
    Throws<ProtocolException>(() => LobbyPayloads.Hello("Alice", "steam:0"));
    var nameOnly = new PayloadWriter(); nameOnly.WriteString("Alice", 160);
    Throws<ProtocolException>(() => LobbyPayloads.ReadHello(nameOnly.ToArray()));
    var badKey = new PayloadWriter(); badKey.WriteString("Alice", 160); badKey.WriteString("local:xyz", 64);
    Throws<ProtocolException>(() => LobbyPayloads.ReadHello(badKey.ToArray()));
    Equal((byte)3, FrameCodec.Version);
    return Task.CompletedTask;
});
await Run("transfer payloads, pacing window and assembly", () =>
{
    byte[] data = new byte[TransferPayloads.ChunkBytes * 40 + 123];
    new Random(5).NextBytes(data);
    var source = new TransferSource(TransferKind.World, Guid.Empty, data);
    Equal(41u, source.Header.ChunkCount);
    Equal(123, source.Header.ChunkLength(40));
    TransferHeader echoed = TransferPayloads.ReadBegin(TransferPayloads.Begin(source.Header.WithSender(Guid.NewGuid())));
    Equal(source.Header.TransferId, echoed.TransferId); Equal(source.Header.Size, echoed.Size);
    Equal(true, echoed.Sha256.SequenceEqual(source.Header.Sha256));
    Equal(false, source.TryTakeChunk(out _, out _)); // no receiver known yet
    Guid a = Guid.NewGuid(), b = Guid.NewGuid();
    source.Acknowledge(a, 0); source.Acknowledge(b, 0);
    var assembler = new TransferAssembler(echoed);
    int sent = 0;
    while (source.TryTakeChunk(out uint index, out byte[] payload))
    {
        var chunk = TransferPayloads.ReadChunk(payload);
        Equal(index, chunk.Index); assembler.Add(chunk.Index, chunk.Data); sent++;
    }
    Equal((int)TransferPayloads.Window, sent); // window limits chunks in flight
    source.Acknowledge(a, 32);
    Equal(false, source.TryTakeChunk(out _, out _)); // slowest receiver b still at 0
    source.Acknowledge(b, 32);
    while (source.TryTakeChunk(out uint index, out byte[] payload)) { var c = TransferPayloads.ReadChunk(payload); assembler.Add(c.Index, c.Data); }
    Equal(true, assembler.IsComplete); Equal(true, assembler.Result.SequenceEqual(data));
    Equal(false, source.IsComplete); source.Acknowledge(a, 41); source.Acknowledge(b, 41); Equal(true, source.IsComplete);
    Throws<ProtocolException>(() => source.Acknowledge(a, 42));
    var outOfOrder = new TransferAssembler(echoed);
    Throws<ProtocolException>(() => outOfOrder.Add(1, new byte[TransferPayloads.ChunkBytes]));
    Throws<ProtocolException>(() => outOfOrder.Add(0, new byte[10]));
    byte[] small = { 1, 2, 3 };
    var tampered = new TransferHeader(Guid.NewGuid(), TransferKind.ModManifest, Guid.Empty, Guid.Empty, 3, new byte[32]);
    Throws<ProtocolException>(() => new TransferAssembler(tampered).Add(0, small));
    Throws<ProtocolException>(() => new TransferHeader(Guid.NewGuid(), TransferKind.ModManifest, Guid.Empty, Guid.Empty,
        TransferPayloads.MaximumSize(TransferKind.ModManifest) + 1, new byte[32]));
    var ack = TransferPayloads.ReadAck(TransferPayloads.Ack(echoed.TransferId, a, 7));
    Equal(a, ack.Receiver); Equal(7u, ack.Received);
    Equal("Grund", TransferPayloads.ReadAbort(TransferPayloads.Abort(echoed.TransferId, "Grund")).Reason);
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
