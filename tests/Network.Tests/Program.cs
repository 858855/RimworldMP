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
    var helloReader = new PayloadReader(hello.Payload);
    Assert(helloReader.ReadString(160) == "Alice", "Hello name");
    helloReader.EnsureComplete();
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

static void Assert(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
}
static async Task Expect<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
