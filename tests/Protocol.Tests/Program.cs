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
