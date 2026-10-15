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
    await Reject<ProtocolException>(new byte[] { 0, 0, 0, 6, 2, 1, 0, 0, 0, 0 });
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
