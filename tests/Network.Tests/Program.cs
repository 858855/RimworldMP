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

static void Assert(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
}
static async Task Expect<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
