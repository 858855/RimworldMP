using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Server;

await using var server = new FoundationServer(new ServerConfig
{ Port = 0, HeartbeatIntervalSeconds = 1, ClientTimeoutSeconds = 3 });
await server.StartAsync();
using var client = await LobbyClient.ConnectAsync("127.0.0.1", server.BoundPort, "UI Test", TimeSpan.FromSeconds(3));
Assert(client.IsConnected && client.SessionId != Guid.Empty, "connected session");
await client.SendAsync(MessageType.CreateLobby, 2, LobbyPayloads.Create("Client room", 3));
LobbyView state = LobbyPayloads.ReadState((await WaitFor(client, MessageType.LobbyStateUpdate)).Payload);
Assert(state.Players.Count == 1 && state.Players[0].Id == client.SessionId, "lobby state queued");
Console.WriteLine("PASS: client pump receives lobby snapshot");

await Task.Delay(4200);
Assert(client.IsConnected && server.ConnectedClients == 1, "automatic pong keeps session alive");
await client.SendAsync(MessageType.LobbyListRequest, 3, Array.Empty<byte>());
Assert(LobbyPayloads.ReadList((await WaitFor(client, MessageType.LobbyListResponse)).Payload).Count == 1,
    "lobby list after heartbeats");
Console.WriteLine("PASS: automatic heartbeat response keeps client connected");

await client.DisconnectAsync();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
while (server.ConnectedClients != 0 && !timeout.IsCancellationRequested) await Task.Delay(20);
Assert(server.ConnectedClients == 0, "clean client disconnect");
Console.WriteLine("PASS: client disconnect cleans server session");

static async Task<WireMessage> WaitFor(LobbyClient client, MessageType type)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    while (!timeout.IsCancellationRequested)
    {
        if (client.TryDequeue(out WireMessage? message) && message != null)
        {
            if (message.Type == type) return message;
            if (message.Type == MessageType.Error) throw new Exception("Server error");
        }
        await Task.Delay(20);
    }
    throw new TimeoutException("Missing " + type);
}
static void Assert(bool condition, string message)
{ if (!condition) throw new Exception("FAIL: " + message); }
