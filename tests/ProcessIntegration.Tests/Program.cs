using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Server;

if (args.Length > 0 && args[0] == "worker")
{
    await RunWorkerAsync(args[1], int.Parse(args[2]), args.Length > 3 ? args[3] : "127.0.0.1");
    return;
}

await RunTestAsync();

static async Task RunTestAsync()
{
    string temporary = Path.Combine(Path.GetTempPath(), "rimworldmp-process-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temporary);
    Process? server = null;
    Process? alice = null;
    Process? bob = null;
    try
    {
        int port;
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();

        string configPath = Path.Combine(temporary, "serverconfig.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(new ServerConfig { Port = port }));
        string dotnet = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "../../..",
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        if (!File.Exists(dotnet)) throw new Exception("Cannot find .NET host: " + dotnet);
        string testDll = Assembly.GetExecutingAssembly().Location;
        string serverDll = Path.Combine(AppContext.BaseDirectory, "Multiplayer.Foundation.Server.dll");
        if (!File.Exists(serverDll)) throw new Exception("Server DLL missing from test output.");

        server = Start(dotnet, serverDll, configPath);
        string? startup = await ReadLineAsync(server, TimeSpan.FromSeconds(10));
        Require(startup?.Contains("listening on :: port " + port + " (IPv4+IPv6)", StringComparison.Ordinal) == true, "Server did not start: " + startup);
        Console.WriteLine("PASS: independent server process started");

        alice = Start(dotnet, testDll, "worker", "Alice", port.ToString());
        bob = Start(dotnet, testDll, "worker", "Bob", port.ToString(), "::1");
        Guid aliceId = Guid.Parse((await ExpectAsync(alice, "CONNECTED")).Split(' ')[2]);
        Guid bobId = Guid.Parse((await ExpectAsync(bob, "CONNECTED")).Split(' ')[2]);
        Require(aliceId != bobId, "Clients received the same session ID.");
        Console.WriteLine("PASS: two separate client processes connected over IPv4 and IPv6");

        await SendAsync(alice, "create TestLobby");
        State initial = ParseState(await ExpectAsync(alice, "STATE"));
        Require(initial.Players == "Alice" && initial.Ready == 0, "Initial lobby state incorrect.");
        await SendAsync(bob, "list");
        string[] list = (await ExpectAsync(bob, "LIST")).Split(' ');
        Require(list.Length == 4 && list[2] == initial.LobbyId.ToString() && list[3] == "TestLobby", "Lobby not visible to second client.");
        Console.WriteLine("PASS: lobby created and listed across processes");

        await SendAsync(bob, "join " + initial.LobbyId);
        State joinedBob = ParseState(await ExpectAsync(bob, "STATE"));
        State joinedAlice = ParseState(await ExpectAsync(alice, "STATE"));
        Require(joinedBob == joinedAlice && joinedBob.Players == "Alice,Bob", "Lobby snapshots differ after join.");
        Console.WriteLine("PASS: both clients received identical joined state");

        await SendAsync(bob, "ready true");
        State readyBob = ParseState(await ExpectAsync(bob, "STATE"));
        State readyAlice = ParseState(await ExpectAsync(alice, "STATE"));
        Require(readyBob == readyAlice && readyBob.Ready == 1 && readyBob.Players == "Alice,Bob", "Ready state not synchronized.");
        Console.WriteLine("PASS: Ready state synchronized across processes");

        await SendAsync(bob, "disconnect");
        await ExpectAsync(bob, "BYE");
        State remaining = ParseState(await ExpectAsync(alice, "STATE"));
        Require(remaining.Players == "Alice" && remaining.Ready == 0 && remaining.Revision > readyAlice.Revision,
            "Disconnect did not update remaining client.");
        Console.WriteLine("PASS: disconnect removed player from remaining client");

        if (!OperatingSystem.IsWindows())
        {
            Require(NativeSignals.Kill(server.Id, 15) == 0, "Could not send SIGTERM to test server.");
            await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Require(server.ExitCode == 0, "Server did not shut down cleanly after SIGTERM.");
            await ExpectAsync(alice, "DISCONNECTED");
            Console.WriteLine("PASS: SIGTERM stopped server and disconnected active client");
        }
        await SendAsync(alice, "disconnect");
        await ExpectAsync(alice, "BYE");
        Console.WriteLine("PASS: both client processes exited cleanly");
    }
    finally
    {
        await StopAsync(bob);
        await StopAsync(alice);
        await StopAsync(server);
        Directory.Delete(temporary, recursive: true);
    }
}

static async Task RunWorkerAsync(string name, int port, string host)
{
    using LobbyClient client = await LobbyClient.ConnectAsync(host, port, name, TimeSpan.FromSeconds(5));
    Console.WriteLine("EVENT CONNECTED " + client.SessionId);
    using var stopped = new CancellationTokenSource();
    Task pump = PumpAsync(client, stopped.Token);
    try
    {
        string? command;
        while ((command = await Console.In.ReadLineAsync()) != null)
        {
            string[] parts = command.Split(' ', 2);
            switch (parts[0])
            {
                case "create":
                    await client.SendAsync(MessageType.CreateLobby, 2, LobbyPayloads.Create(parts[1], 4));
                    break;
                case "list":
                    await client.SendAsync(MessageType.LobbyListRequest, 3, Array.Empty<byte>());
                    break;
                case "join":
                    await client.SendAsync(MessageType.JoinLobby, 4, LobbyPayloads.GuidValue(Guid.Parse(parts[1])));
                    break;
                case "ready":
                    await client.SendAsync(MessageType.PlayerReadyChanged, 5, LobbyPayloads.Bool(bool.Parse(parts[1])));
                    break;
                case "disconnect":
                    await client.DisconnectAsync();
                    Console.WriteLine("EVENT BYE");
                    return;
                default: throw new InvalidOperationException("Unknown command: " + command);
            }
        }
    }
    finally
    {
        stopped.Cancel();
        await pump;
    }
}

static async Task PumpAsync(LobbyClient client, CancellationToken token)
{
    try
    {
        while (!token.IsCancellationRequested && client.IsConnected)
        {
            while (client.TryDequeue(out WireMessage? message))
            {
                if (message == null) continue;
                if (message.Type == MessageType.LobbyListResponse)
                {
                    IReadOnlyList<LobbySummary> lobbies = LobbyPayloads.ReadList(message.Payload);
                    foreach (LobbySummary lobby in lobbies)
                        Console.WriteLine($"EVENT LIST {lobby.Id} {lobby.Name}");
                }
                else if (message.Type == MessageType.LobbyStateUpdate)
                {
                    LobbyView lobby = LobbyPayloads.ReadState(message.Payload);
                    string players = string.Join(',', lobby.Players.Select(player => player.Name));
                    int ready = lobby.Players.Count(player => player.Ready);
                    Console.WriteLine($"EVENT STATE {lobby.Id} {lobby.Revision} {players} {ready}");
                }
                else if (message.Type == MessageType.Error)
                    Console.WriteLine("EVENT ERROR " + message.RequestId);
            }
            await Task.Delay(20, token);
        }
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    if (!token.IsCancellationRequested) Console.WriteLine("EVENT DISCONNECTED");
}

static Process Start(string executable, params string[] arguments)
{
    var info = new ProcessStartInfo(executable)
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    foreach (string argument in arguments) info.ArgumentList.Add(argument);
    return Process.Start(info) ?? throw new Exception("Failed to start process: " + executable);
}

static async Task SendAsync(Process process, string command)
{
    await process.StandardInput.WriteLineAsync(command);
    await process.StandardInput.FlushAsync();
}

static async Task<string> ExpectAsync(Process process, string kind)
{
    while (true)
    {
        string? line = await ReadLineAsync(process, TimeSpan.FromSeconds(10));
        if (line == null) throw new Exception($"Client exited before {kind}: {await process.StandardError.ReadToEndAsync()}");
        if (line.StartsWith("EVENT ERROR", StringComparison.Ordinal)) throw new Exception("Client received server error: " + line);
        if (line.StartsWith("EVENT " + kind + " ", StringComparison.Ordinal) || line == "EVENT " + kind) return line;
    }
}

static async Task<string?> ReadLineAsync(Process process, TimeSpan timeout)
{
    try { return await process.StandardOutput.ReadLineAsync().WaitAsync(timeout); }
    catch (TimeoutException) { throw new Exception($"Process {process.Id} timed out; exit={process.HasExited}"); }
}

static State ParseState(string line)
{
    string[] parts = line.Split(' ');
    Require(parts.Length == 6, "Malformed state event: " + line);
    return new State(Guid.Parse(parts[2]), ulong.Parse(parts[3]), parts[4], int.Parse(parts[5]));
}

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static async Task StopAsync(Process? process)
{
    if (process == null) return;
    try
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
    finally { process.Dispose(); }
}

internal sealed record State(Guid LobbyId, ulong Revision, string Players, int Ready);

internal static class NativeSignals
{
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    internal static extern int Kill(int pid, int signal);
}
