using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Session
{
    public sealed class LockstepRuleException : Exception
    {
        public LockstepRuleException(byte code, string message) : base(message) { Code = code; }
        public byte Code { get; }
    }

    /// <summary>
    /// SYNC-001 (ADR 0024): the server side of one lockstep session (one lobby in <c>Playing</c>). It gives every command
    /// the next tick and a sequence number, runs the clock at the session speed and moves the horizon, never more than
    /// <see cref="MaximumLeadSeconds"/> past the slowest player. It compares the players' state hashes.
    /// Not thread-safe: <see cref="LockstepSessions"/> serializes the calls. Only <see cref="Step"/> produces messages, so a
    /// caller that sends them in order delivers every command for tick t before any horizon at or past t.
    /// </summary>
    public sealed class LockstepSequencer
    {
        public const double MaximumLeadSeconds = 2;
        public const int MaximumCommandsPerSecond = 30;
        public const int MaximumPendingHashTicks = 64;
        /// <summary>An unchanged horizon is repeated this often, so a player's view never goes stale.</summary>
        public const long HorizonRepeatMilliseconds = 1000;
        private const long MaximumStepMilliseconds = 1000;

        private readonly Dictionary<Guid, Member> members = new Dictionary<Guid, Member>();
        private readonly List<Guid> order = new List<Guid>();
        private readonly List<(PlayerKey Issuer, LockstepCommand Command)> queue = new List<(PlayerKey, LockstepCommand)>();
        private readonly SortedDictionary<int, Dictionary<Guid, ulong>> hashes = new SortedDictionary<int, Dictionary<Guid, ulong>>();
        private Guid host;
        private uint seq;
        private SessionSpeed requested = SessionSpeed.Paused, lastRunning = SessionSpeed.Normal;
        private double carry;
        private long lastStepMs, lastHorizonSentMs;
        private TickHorizon? lastSent;

        public LockstepSequencer(Guid sessionId, Guid host, IEnumerable<(Guid Id, PlayerKey Key)> players)
        {
            SessionId = sessionId;
            SetMembers(host, players?.ToList() ?? throw new ArgumentNullException(nameof(players)));
        }

        /// <summary>Identifies this shared game; a new game in the same lobby gets a new sequencer.</summary>
        public Guid SessionId { get; }
        public bool Started { get; private set; }
        public int Horizon { get; private set; }
        /// <summary>Speed after the last scheduled command: the pace of the clock.</summary>
        public SessionSpeed Speed { get; private set; } = SessionSpeed.Paused;
        public uint LastSeq => seq;
        public int CheckedTick { get; private set; } = -1;
        public bool DesyncReported { get; private set; }
        public Guid WaitingFor { get; private set; }
        public int MemberCount => members.Count;

        /// <summary>Follows the lobby: players who left no longer count, for the start, the pace or the hashes.</summary>
        public void SetMembers(Guid hostId, IReadOnlyCollection<(Guid Id, PlayerKey Key)> current)
        {
            host = hostId;
            foreach (Guid gone in order.Where(id => current.All(c => c.Id != id)).ToList())
            {
                members.Remove(gone); order.Remove(gone);
                foreach (var byMember in hashes.Values) byMember.Remove(gone);
            }
            foreach (var (id, key) in current)
                if (!members.ContainsKey(id)) { members.Add(id, new Member(key)); order.Add(id); }
        }

        /// <summary>
        /// Queues a player's command for the next step. Pause and continue are open to everyone; only the host picks
        /// the speed level (plan §7.1). A request that changes nothing is accepted and dropped.
        /// </summary>
        public void Submit(Guid memberId, LockstepCommand command, long nowMs)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            Member member = Get(memberId);
            while (member.RecentCommands.Count > 0 && nowMs - member.RecentCommands.Peek() >= 1000) member.RecentCommands.Dequeue();
            if (member.RecentCommands.Count >= MaximumCommandsPerSecond)
                throw new LockstepRuleException(7, "Too many commands: at most " + MaximumCommandsPerSecond + " per second.");
            if (command.Kind == LockstepCommandKind.SetSpeed)
            {
                SessionSpeed result = SpeedFor(memberId, LockstepCommandKind.ReadSpeed(command.Payload));
                member.RecentCommands.Enqueue(nowMs);
                if (result == requested) return;
                requested = result;
                if (result != SessionSpeed.Paused) lastRunning = result;
                queue.Add((member.Key, new LockstepCommand(LockstepCommandKind.SetSpeed, LockstepCommandKind.Speed(result))));
                return;
            }
            member.RecentCommands.Enqueue(nowMs);
            queue.Add((member.Key, command));
        }

        /// <summary>The tick a player's game has reached. Nobody counts as being past the horizon.</summary>
        public void ReportTick(Guid memberId, ClientTick report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            Member member = Get(memberId);
            int tick = Started ? Math.Min(report.Tick, Horizon) : report.Tick;
            if (member.Tick == null || tick > member.Tick) member.Tick = tick;
            member.Flags = report.Flags;
        }

        public void ReportHash(Guid memberId, StateHash report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            Get(memberId);
            if (DesyncReported || report.Tick <= CheckedTick) return;
            if (!hashes.TryGetValue(report.Tick, out var byMember))
            {
                if (hashes.Count >= MaximumPendingHashTicks) hashes.Remove(hashes.Keys.First());
                hashes[report.Tick] = byMember = new Dictionary<Guid, ulong>();
            }
            byMember[memberId] = report.Hash;
        }

        /// <summary>One clock step (the server calls it about every 50 ms). Returns the messages to send, in order.</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> Step(long nowMs)
        {
            var outgoing = new List<(Guid To, WireMessage Message)>();
            if (!Started)
            {
                CompareHashes(outgoing);
                if (members.Count == 0 || members.Values.Any(m => m.Tick == null)) return outgoing;
                Started = true;
                Horizon = members.Values.Min(m => m.Tick!.Value);
                lastStepMs = nowMs; carry = 0;
            }
            long elapsed = Math.Max(0, Math.Min(MaximumStepMilliseconds, nowMs - lastStepMs));
            lastStepMs = nowMs;

            // Commands first: they get the tick after the horizon every player has seen.
            foreach (var (issuer, command) in queue)
            {
                if (command.Kind == LockstepCommandKind.SetSpeed) Speed = LockstepCommandKind.ReadSpeed(command.Payload);
                Broadcast(outgoing, MessageType.ScheduledCommand, new ScheduledCommand(Horizon + 1, ++seq, issuer, command).ToPayload());
            }
            queue.Clear();

            SessionSpeed pace = Speed > SessionSpeed.Normal && members.Values.Any(m => (m.Flags & ClientTickFlags.ForcedNormalSpeed) != 0)
                ? SessionSpeed.Normal : Speed;
            int ticksPerSecond = SessionSpeeds.TicksPerSecond(pace);
            Guid slowestId = order.Where(id => members[id].Tick != null).OrderBy(id => members[id].Tick).FirstOrDefault();
            WaitingFor = Guid.Empty;
            int waitingTick = 0;
            if (ticksPerSecond == 0) carry = 0;
            else
            {
                carry += elapsed * ticksPerSecond / 1000.0;
                int due = (int)Math.Floor(carry);
                carry -= due;
                long wanted = (long)Horizon + due;
                if (slowestId != Guid.Empty)
                {
                    long limit = (long)members[slowestId].Tick!.Value + (long)(ticksPerSecond * MaximumLeadSeconds);
                    if (wanted > limit)
                    {
                        // the slowest player sets the pace: hold back and do not make up the time later
                        wanted = Math.Max(Horizon, limit); carry = 0;
                        WaitingFor = slowestId; waitingTick = members[slowestId].Tick!.Value;
                    }
                }
                Horizon = (int)Math.Min(int.MaxValue, wanted);
            }

            CompareHashes(outgoing);
            var horizon = new TickHorizon(Horizon, pace, WaitingFor, waitingTick, CheckedTick);
            if (lastSent == null || !SameAs(lastSent, horizon) || nowMs - lastHorizonSentMs >= HorizonRepeatMilliseconds)
            {
                Broadcast(outgoing, MessageType.TickHorizon, horizon.ToPayload());
                lastSent = horizon; lastHorizonSentMs = nowMs;
            }
            return outgoing;
        }

        // A tick is compared once every current player has reported it. The host's hash is the reference; the first
        // difference is reported once (recovery is DESYNC-003).
        private void CompareHashes(List<(Guid To, WireMessage Message)> outgoing)
        {
            if (DesyncReported) { hashes.Clear(); return; }
            foreach (int tick in hashes.Keys.ToList())
            {
                Dictionary<Guid, ulong> byMember = hashes[tick];
                if (members.Count == 0 || order.Any(id => !byMember.ContainsKey(id))) continue;
                hashes.Remove(tick);
                ulong reference = byMember.TryGetValue(host, out ulong hostHash) ? hostHash : byMember[order[0]];
                Guid[] differing = order.Where(id => byMember[id] != reference).ToArray();
                if (differing.Length == 0) { CheckedTick = Math.Max(CheckedTick, tick); continue; }
                DesyncReported = true; hashes.Clear();
                Broadcast(outgoing, MessageType.DesyncDetected, new DesyncDetected(tick, differing).ToPayload());
                return;
            }
        }

        private SessionSpeed SpeedFor(Guid memberId, SessionSpeed wanted)
        {
            if (wanted == SessionSpeed.Paused || memberId == host) return wanted;
            if (requested == SessionSpeed.Paused) return lastRunning; // a guest continues at the host's last speed
            if (wanted == requested) return requested;
            throw new LockstepRuleException(3, "Only the host changes the speed.");
        }

        private void Broadcast(List<(Guid To, WireMessage Message)> outgoing, MessageType type, byte[] payload)
        {
            foreach (Guid id in order) outgoing.Add((id, new WireMessage(type, 0, payload)));
        }

        private Member Get(Guid id)
        {
            if (!members.TryGetValue(id, out Member? member)) throw new LockstepRuleException(6, "Not part of this game.");
            return member;
        }

        private static bool SameAs(TickHorizon a, TickHorizon b) => a.Horizon == b.Horizon && a.Speed == b.Speed &&
            a.WaitingFor == b.WaitingFor && a.WaitingTick == b.WaitingTick && a.CheckedTick == b.CheckedTick;

        private sealed class Member
        {
            public Member(PlayerKey key) { Key = key; }
            public PlayerKey Key { get; }
            public int? Tick { get; set; }
            public ClientTickFlags Flags { get; set; }
            public Queue<long> RecentCommands { get; } = new Queue<long>();
        }
    }
}
