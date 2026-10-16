using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Server;

// One sender per peer, so messages leave in the order they were queued. Lockstep needs this: a TickHorizon from the
// clock must never overtake a ScheduledCommand queued before it, or that game runs past the command (ADR 0030).
internal sealed class OrderedOutbox
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<WireMessage> queue = new();
    private readonly SemaphoreSlim signal = new(0);

    public OrderedOutbox(NetworkConnection connection, CancellationToken token) =>
        Pump = Task.Run(() => PumpAsync(connection, token));

    public Task Pump { get; }

    public void Enqueue(WireMessage message) { queue.Enqueue(message); signal.Release(); }

    private async Task PumpAsync(NetworkConnection connection, CancellationToken token)
    {
        try
        {
            while (true)
            {
                await signal.WaitAsync(token).ConfigureAwait(false);
                if (queue.TryDequeue(out WireMessage? message)) await connection.SendAsync(message, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { connection.Dispose(); }
        catch (ObjectDisposedException) { }
    }
}
