using System.Collections.Concurrent;
using System.Diagnostics;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Session;

namespace Multiplayer.Foundation.Server;

// Runs one LockstepSession per lobby in Playing (ADR 0030). Every session call and the queueing of its messages happen
// under that session's lock, so each peer's ordered outbox receives them in the coordinator's order.
internal sealed class LockstepHost
{
    private readonly ConcurrentDictionary<Guid, Holder> sessions = new();
    private readonly Action<Guid, WireMessage> enqueue;
    private readonly LockstepOptions options;

    public LockstepHost(Action<Guid, WireMessage> enqueue, LockstepOptions? options = null)
    { this.enqueue = enqueue; this.options = options ?? new LockstepOptions(); }

    public int ActiveSessions => sessions.Count;
    public static long NowMs => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

    /// <summary>Starts the session when a lobby reaches Playing, ends it when the lobby leaves it.</summary>
    public void OnLobbyState(LobbyView state)
    {
        if (state.Phase != SetupPhase.Playing) { RemoveLobby(state.Id); return; }
        if (sessions.ContainsKey(state.Id)) return;
        var holder = new Holder(new LockstepSession(state.HostId, state.Players.Select(p => (p.Id, p.Key)), NowMs, options));
        lock (holder.Gate)
        {
            if (!sessions.TryAdd(state.Id, holder)) return;
            Deliver(holder.Session.Begin());
        }
    }

    public void Run(Guid lobbyId, Func<LockstepSession, IReadOnlyList<(Guid To, WireMessage Message)>> action)
    {
        if (!sessions.TryGetValue(lobbyId, out Holder? holder)) throw new LockstepRuleException(6, "No game session is running.");
        lock (holder.Gate) Deliver(action(holder.Session));
    }

    /// <summary>A member joined a lobby in Playing: snapshot and catch-up (ADR 0032).</summary>
    public void AddJoiner(Guid lobbyId, Guid session, PlayerKey key) =>
        Run(lobbyId, lockstep => lockstep.AddJoiner(session, key, NowMs));

    public void Tick()
    {
        long now = NowMs;
        foreach (var (lobby, holder) in sessions)
            lock (holder.Gate)
            {
                if (holder.Session.Ended) { sessions.TryRemove(lobby, out _); continue; }
                Deliver(holder.Session.Advance(now));
            }
    }

    public void RemovePeer(Guid peer)
    {
        foreach (var (lobby, holder) in sessions)
            lock (holder.Gate)
            {
                Deliver(holder.Session.RemoveParticipant(peer));
                if (holder.Session.Ended) sessions.TryRemove(lobby, out _);
            }
    }

    public void RemoveLobby(Guid lobbyId) => sessions.TryRemove(lobbyId, out _);

    private void Deliver(IReadOnlyList<(Guid To, WireMessage Message)> outgoing)
    {
        foreach (var (to, message) in outgoing) enqueue(to, message);
    }

    private sealed class Holder
    {
        public Holder(LockstepSession session) { Session = session; }
        public object Gate { get; } = new();
        public LockstepSession Session { get; }
    }
}
