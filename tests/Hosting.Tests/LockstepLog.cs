using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;

// SYNC-001 test helper: the lockstep messages one client received, in arrival order.
sealed class LockstepLog
{
    private readonly LobbyClient client;
    public LockstepLog(LobbyClient client) { this.client = client; }
    public List<WireMessage> Seen { get; } = new();
    public TickHorizon? Latest { get; private set; }

    public async Task<WireMessage> UntilAsync(Func<WireMessage, bool> wanted, double seconds = 5)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        while (true)
        {
            while (client.TryDequeue(out WireMessage? message) && message != null)
            {
                if (message.Type is not (MessageType.ScheduledCommand or MessageType.TickHorizon or MessageType.DesyncDetected)) continue;
                Seen.Add(message);
                if (message.Type == MessageType.TickHorizon) Latest = TickHorizon.Read(message.Payload);
                if (wanted(message)) return message;
            }
            await Task.Delay(10, timeout.Token);
        }
    }

    public async Task<TickHorizon> HorizonAsync(Func<TickHorizon, bool> wanted, double seconds = 5) => TickHorizon.Read((await UntilAsync(
        m => m.Type == MessageType.TickHorizon && wanted(TickHorizon.Read(m.Payload)), seconds)).Payload);

    public async Task<ScheduledCommand> CommandAsync() =>
        ScheduledCommand.Read((await UntilAsync(m => m.Type == MessageType.ScheduledCommand)).Payload);

    /// <summary>True if every command arrived before the first horizon that reached its tick.</summary>
    public bool CommandsBeforeHorizons()
    {
        int horizon = -1;
        foreach (WireMessage message in Seen)
        {
            if (message.Type == MessageType.TickHorizon) horizon = Math.Max(horizon, TickHorizon.Read(message.Payload).Horizon);
            else if (message.Type == MessageType.ScheduledCommand && ScheduledCommand.Read(message.Payload).Tick <= horizon) return false;
        }
        return true;
    }

    public void Clear() { Seen.Clear(); }
}
