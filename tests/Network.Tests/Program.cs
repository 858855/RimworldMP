using System.Net;
using System.Net.Sockets;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;

var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
int port = ((IPEndPoint)listener.LocalEndpoint).Port;
Guid session = Guid.NewGuid();
Task server = Task.Run(async () =>
{
    using var accepted = await listener.AcceptTcpClientAsync();
    using var connection = NetworkConnection.FromAcceptedClient(accepted);
    WireMessage hello = await connection.ReceiveAsync() ?? throw new Exception("missing Hello");
    Assert(hello.Type == MessageType.Hello && hello.RequestId == 1, "Hello envelope");
    var (helloName, helloKey) = LobbyPayloads.ReadHello(hello.Payload);
    Assert(helloName == "Alice" && helloKey.IsValid, "Hello name and player key");
    var reply = new PayloadWriter();
    reply.WriteGuid(session); reply.WriteUInt16(2);
    await connection.SendAsync(new WireMessage(MessageType.HandshakeAccepted, 1, reply.ToArray()));
    for (int i = 0; i < 20; i++)
    {
        WireMessage packet = await connection.ReceiveAsync() ?? throw new Exception("early close");
        Assert(packet.Type == MessageType.Ping, "concurrent send frame");
    }
    await connection.SendAsync(new WireMessage(MessageType.Pong, 0, Array.Empty<byte>()));
    Assert(await connection.ReceiveAsync() == null, "clean EOF");
});
var result = await HandshakeClient.ConnectAsync("127.0.0.1", port, "Alice", TimeSpan.FromSeconds(3));
Assert(result.SessionId == session && result.HeartbeatSeconds == 2, "accepted handshake");
await Task.WhenAll(Enumerable.Range(0, 20).Select(i => result.Connection.SendAsync(
    new WireMessage(MessageType.Ping, (uint)(i + 2), Array.Empty<byte>()))));
Assert((await result.Connection.ReceiveAsync())?.Type == MessageType.Pong, "Pong");
result.Connection.Dispose();
await server;
listener.Stop();
Console.WriteLine("PASS: real TCP handshake, concurrent sends, clean disconnect");

listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
port = ((IPEndPoint)listener.LocalEndpoint).Port;
server = Task.Run(async () =>
{
    using var accepted = await listener.AcceptTcpClientAsync();
    using var connection = NetworkConnection.FromAcceptedClient(accepted);
    await connection.ReceiveAsync();
    var reply = new PayloadWriter(); reply.WriteByte(2); reply.WriteString("version");
    await connection.SendAsync(new WireMessage(MessageType.HandshakeRejected, 1, reply.ToArray()));
});
await Expect<ProtocolException>(() => HandshakeClient.ConnectAsync("127.0.0.1", port, "Bob", TimeSpan.FromSeconds(3)));
await server; listener.Stop();
Console.WriteLine("PASS: handshake rejection");

listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
port = ((IPEndPoint)listener.LocalEndpoint).Port;
server = Task.Run(async () => { using var accepted = await listener.AcceptTcpClientAsync(); await Task.Delay(300); });
await Expect<TimeoutException>(() => HandshakeClient.ConnectAsync("127.0.0.1", port, "Bob", TimeSpan.FromMilliseconds(100)));
await server; listener.Stop();
Console.WriteLine("PASS: handshake timeout");

Assert(HostEndpoint.TryParse(" 203.0.113.5 ", 28491, out string h, out int p) && h == "203.0.113.5" && p == 28491, "IPv4 default port");
Assert(HostEndpoint.TryParse("example.org:1234", 28491, out h, out p) && h == "example.org" && p == 1234, "name with port");
Assert(HostEndpoint.TryParse("[2001:db8::5]:4000", 28491, out h, out p) && h == "2001:db8::5" && p == 4000, "bracketed IPv6 with port");
Assert(HostEndpoint.TryParse("[2001:db8::5]", 28491, out h, out p) && h == "2001:db8::5" && p == 28491, "bracketed IPv6");
Assert(HostEndpoint.TryParse("2001:db8::5", 28491, out h, out p) && h == "2001:db8::5" && p == 28491, "bare IPv6");
Assert(!HostEndpoint.TryParse("", 28491, out _, out _) && !HostEndpoint.TryParse("host:0", 28491, out _, out _) &&
    !HostEndpoint.TryParse("host:70000", 28491, out _, out _) && !HostEndpoint.TryParse("a b", 28491, out _, out _) &&
    !HostEndpoint.TryParse("[1.2.3.4]:5", 28491, out _, out _) && !HostEndpoint.TryParse("x:y:z", 28491, out _, out _),
    "invalid endpoints rejected");
Console.WriteLine("PASS: server address parsing");

listener = new TcpListener(IPAddress.IPv6Loopback, 0);
listener.Start();
port = ((IPEndPoint)listener.LocalEndpoint).Port;
server = Task.Run(async () => { using var accepted = await listener.AcceptTcpClientAsync(); });
using (var overIPv6 = await NetworkConnection.ConnectAsync("[::1]", port, TimeSpan.FromSeconds(3)))
    Assert(!overIPv6.IsClosed, "IPv6 connection");
await server; listener.Stop();
listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
port = ((IPEndPoint)listener.LocalEndpoint).Port;
server = Task.Run(async () => { using var accepted = await listener.AcceptTcpClientAsync(); });
using (var byName = await NetworkConnection.ConnectAsync("localhost", port, TimeSpan.FromSeconds(3)))
    Assert(!byName.IsClosed, "host name resolved, refused address skipped");
await server; listener.Stop();
await Expect<SocketException>(() => NetworkConnection.ConnectAsync("127.0.0.1", port, TimeSpan.FromSeconds(3)));
Console.WriteLine("PASS: IPv6 literal, DNS name with fallback, refused connection reported");

// Chunk transfer state machines over a simulated channel with a virtual clock (ADR 0014).
var random = new Random(1234);
byte[] payload = new byte[TransferPayloads.ChunkSize * 20 + 777];
random.NextBytes(payload);

var clean = SimulateTransfer(payload);
Assert(clean.Sender.State == TransferState.Completed, "clean transfer completes");
Assert(clean.Receiver!.Data!.AsSpan().SequenceEqual(payload), "receiver holds identical bytes");
Assert(clean.MaxInFlight <= TransferPayloads.WindowChunks && clean.MaxInFlight == TransferPayloads.WindowChunks,
    "window filled but never exceeded");
Assert(clean.Retransmits == 0, "no retransmits without loss");
Console.WriteLine("PASS: transfer completes with identical SHA-256 and a bounded window");

// Lose every acknowledgement for a while: the window fills and stalls, then resumes after the timeout.
int acks = 0;
var lostAcks = SimulateTransfer(payload, towardSender: m => m.Type == MessageType.TransferAck && ++acks is >= 3 and <= 9 ? null : m);
Assert(lostAcks.Sender.State == TransferState.Completed && lostAcks.Receiver!.Data!.AsSpan().SequenceEqual(payload),
    "transfer resumes after lost acknowledgements");
Assert(lostAcks.Retransmits >= 1, "resume happened through a retransmit");
// Lose only the final acknowledgement: the retransmitted last chunk is answered again.
int finalAcks = 0;
var lostFinal = SimulateTransfer(payload, towardSender: m =>
    m.Type == MessageType.TransferAck && TransferPayloads.ReadAck(m.Payload).Received == 21 && ++finalAcks == 1 ? null : m);
Assert(lostFinal.Sender.State == TransferState.Completed, "completed receiver repeats its final acknowledgement");
Console.WriteLine("PASS: resume after lost acknowledgements, including the final one");

// Swap two chunks: the receiver never acknowledges past a gap, the timeout repairs it.
WireMessage? held = null;
bool heldOnce = false;
var reordered = SimulateTransfer(payload, towardReceiver: m =>
{
    if (m.Type != MessageType.TransferChunk || heldOnce) return m;
    if (TransferPayloads.ReadChunk(m.Payload).Index == 4) { held = m; heldOnce = true; return null; }
    return m;
}, reinject: () => { var h = held; held = null; return h; });
Assert(reordered.Sender.State == TransferState.Completed && reordered.Receiver!.Data!.AsSpan().SequenceEqual(payload),
    "out-of-order chunk does not corrupt the result");
Console.WriteLine("PASS: out-of-order chunk is ignored and repaired");

// Corrupt one byte once: the receiver reports a hash mismatch, the sender restarts once with a new ID.
int corruptions = 0;
Func<WireMessage, WireMessage?> corruptOnce = m =>
{
    if (m.Type != MessageType.TransferChunk || corruptions >= 1) return m;
    TransferChunk c = TransferPayloads.ReadChunk(m.Payload);
    if (c.Index != 2) return m;
    corruptions++;
    byte[] bad = (byte[])c.Data.Clone(); bad[10] ^= 0xFF;
    return new WireMessage(m.Type, 0, TransferPayloads.Chunk(new TransferChunk(c.TransferId, c.Index, bad)));
};
var retried = SimulateTransfer(payload, towardReceiver: corruptOnce);
Assert(retried.Sender.State == TransferState.Completed && retried.Receivers == 2, "hash mismatch restarts once with a new transfer");
Assert(retried.Receiver!.Data!.AsSpan().SequenceEqual(payload), "second attempt delivers the original bytes");
int alwaysCorrupt = 0;
var hopeless = SimulateTransfer(payload, towardReceiver: m =>
{
    if (m.Type != MessageType.TransferChunk) return m;
    TransferChunk c = TransferPayloads.ReadChunk(m.Payload);
    if (c.Index != 2) return m;
    alwaysCorrupt++;
    byte[] bad = (byte[])c.Data.Clone(); bad[0] ^= 1;
    return new WireMessage(m.Type, 0, TransferPayloads.Chunk(new TransferChunk(c.TransferId, c.Index, bad)));
});
Assert(hopeless.Sender.State == TransferState.Failed && hopeless.Sender.Failure!.Code == TransferAbortCode.HashMismatch,
    "a second hash mismatch fails the transfer");
Console.WriteLine("PASS: hash mismatch retried once, then reported");

// A receiver that never answers: three retransmits, then the sender gives up with Timeout.
var silent = SimulateTransfer(payload, towardSender: _ => null);
Assert(silent.Sender.State == TransferState.Failed && silent.Sender.Failure!.Code == TransferAbortCode.Timeout,
    "silent receiver times out");
Assert(silent.Retransmits == TransferSender.MaximumRetransmits, "exactly the allowed retransmits");
Assert(silent.Receiver!.State == TransferState.Failed, "receiver learns about the timeout");
Console.WriteLine("PASS: unresponsive receiver times out after bounded retransmits");

// Malformed and cancelled transfers.
byte[] small = new byte[TransferPayloads.ChunkSize + 10];
var shortChunk = SimulateTransfer(small, towardReceiver: m =>
{
    if (m.Type != MessageType.TransferChunk) return m;
    TransferChunk c = TransferPayloads.ReadChunk(m.Payload);
    return c.Index != 0 ? m : new WireMessage(m.Type, 0, TransferPayloads.Chunk(new TransferChunk(c.TransferId, 0, new byte[100])));
});
Assert(shortChunk.Receiver!.Failure!.Code == TransferAbortCode.InvalidData && shortChunk.Sender.State == TransferState.Failed,
    "wrong chunk length aborts both sides");
var cancelled = new TransferSender(TransferKind.Game, Guid.Empty, small);
cancelled.Start(0);
Assert(cancelled.Cancel("user").Single().Type == MessageType.TransferAbort && cancelled.State == TransferState.Failed,
    "sender cancel emits abort");
Assert(cancelled.Cancel("again").Count == 0, "cancel is idempotent");
Expect<ArgumentException>(() => Task.FromResult(new TransferSender(TransferKind.ModManifest, Guid.Empty,
    new byte[(int)TransferPayloads.MaximumLength(TransferKind.ModManifest) + 1]))).GetAwaiter().GetResult();
Expect<ArgumentException>(() => Task.FromResult(new TransferSender(TransferKind.World, Guid.Empty, Array.Empty<byte>()))).GetAwaiter().GetResult();
Console.WriteLine("PASS: invalid chunk, cancel and size limits");

static TransferSim SimulateTransfer(byte[] data, Func<WireMessage, WireMessage?>? towardReceiver = null,
    Func<WireMessage, WireMessage?>? towardSender = null, Func<WireMessage?>? reinject = null)
{
    Guid senderSession = Guid.NewGuid();
    var sender = new TransferSender(TransferKind.World, Guid.Empty, data);
    var toReceiver = new Queue<WireMessage>(sender.Start(0));
    var toSender = new Queue<WireMessage>();
    TransferReceiver? receiver = null;
    int receivers = 0, maxInFlight = 0, retransmits = 0;
    long now = 0;
    for (int step = 0; step < 200_000 && (sender.State == TransferState.Active || toReceiver.Count > 0); step++)
    {
        maxInFlight = Math.Max(maxInFlight, (int)sender.UnacknowledgedChunks);
        if (toReceiver.Count > 0)
        {
            WireMessage? message = Filter(towardReceiver, toReceiver.Dequeue());
            if (message == null) continue;
            if (message.Type == MessageType.TransferBegin)
            {
                // The server stamps the sender's session ID before forwarding.
                receiver = new TransferReceiver(TransferPayloads.ReadBegin(message.Payload).WithPeer(senderSession));
                receivers++;
                continue;
            }
            if (receiver != null) foreach (WireMessage reply in receiver.Handle(message)) toSender.Enqueue(reply);
        }
        else if (toSender.Count > 0)
        {
            WireMessage? message = Filter(towardSender, toSender.Dequeue());
            if (message != null) foreach (WireMessage next in sender.Handle(message, now)) toReceiver.Enqueue(next);
        }
        else
        {
            if (reinject?.Invoke() is WireMessage late) { toReceiver.Enqueue(late); continue; }
            now += 1000;
            var resent = sender.Poll(now);
            if (resent.Any(m => m.Type == MessageType.TransferChunk)) retransmits++;
            foreach (WireMessage next in resent) toReceiver.Enqueue(next);
        }
    }
    return new TransferSim(sender, receiver, receivers, maxInFlight, retransmits);
}

static WireMessage? Filter(Func<WireMessage, WireMessage?>? filter, WireMessage message) => filter == null ? message : filter(message);

static void Assert(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
}
static async Task Expect<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

sealed record TransferSim(TransferSender Sender, TransferReceiver? Receiver, int Receivers, int MaxInFlight, int Retransmits);
