using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Session
{
    /// <summary>A lobby whose shared game runs (phase <c>Playing</c>), as the lobby registry sees it now.</summary>
    public sealed class LockstepRoom
    {
        public LockstepRoom(Guid sessionId, Guid host, IReadOnlyList<(Guid Id, PlayerKey Key)> members)
        {
            if (sessionId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(sessionId));
            SessionId = sessionId; Host = host; Members = members ?? throw new ArgumentNullException(nameof(members));
        }
        public Guid SessionId { get; }
        public Guid Host { get; }
        public IReadOnlyList<(Guid Id, PlayerKey Key)> Members { get; }
    }

    /// <summary>
    /// SYNC-001 (ADR 0024): every running lockstep session of one server, behind one lock. The server's clock loop calls
    /// <see cref="Step"/>; connection tasks report ticks, hashes and commands at any time.
    /// </summary>
    public sealed class LockstepSessions
    {
        private readonly object gate = new object();
        private readonly Dictionary<Guid, LockstepSequencer> sessions = new Dictionary<Guid, LockstepSequencer>();

        public int Count { get { lock (gate) return sessions.Count; } }

        /// <summary>One clock step for all games in <paramref name="playing"/>; games no longer listed end.</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> Step(IReadOnlyList<LockstepRoom> playing, long nowMs)
        {
            if (playing == null) throw new ArgumentNullException(nameof(playing));
            lock (gate)
            {
                foreach (Guid ended in sessions.Keys.Where(id => playing.All(r => r.SessionId != id)).ToList()) sessions.Remove(ended);
                var outgoing = new List<(Guid To, WireMessage Message)>();
                foreach (LockstepRoom room in playing) outgoing.AddRange(Ensure(room).Step(nowMs));
                return outgoing;
            }
        }

        public void Submit(LockstepRoom room, Guid member, LockstepCommand command, long nowMs)
        { lock (gate) Ensure(room).Submit(member, command, nowMs); }

        public void ReportTick(LockstepRoom room, Guid member, ClientTick tick)
        { lock (gate) Ensure(room).ReportTick(member, tick); }

        public void ReportHash(LockstepRoom room, Guid member, StateHash hash)
        { lock (gate) Ensure(room).ReportHash(member, hash); }

        private LockstepSequencer Ensure(LockstepRoom room)
        {
            if (!sessions.TryGetValue(room.SessionId, out LockstepSequencer? sequencer))
                sessions[room.SessionId] = sequencer = new LockstepSequencer(room.SessionId, room.Host, room.Members);
            else sequencer.SetMembers(room.Host, room.Members.ToList());
            return sequencer;
        }
    }
}
