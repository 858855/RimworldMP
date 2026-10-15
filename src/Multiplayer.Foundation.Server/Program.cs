using Multiplayer.Foundation.Server;
using System.Runtime.InteropServices;

try
{
    string configPath = args.Length == 0 ? "serverconfig.json" : args[0];
    var config = ServerConfig.Load(configPath);
    await using var server = new FoundationServer(config);
    await server.StartAsync();
    Console.WriteLine($"RimWorld Multiplayer Foundation lobby server listening on {config.BindAddress}:{server.BoundPort}");
    Console.WriteLine("Press Ctrl+C to stop.");
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
    using var termination = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(
        PosixSignal.SIGTERM, context => { context.Cancel = true; stop.Cancel(); });
    try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); }
    catch (OperationCanceledException) { }
    await server.StopAsync();
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Server startup failed: {error.GetType().Name}: {error.Message}");
    return 1;
}
