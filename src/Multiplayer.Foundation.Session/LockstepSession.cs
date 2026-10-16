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

    public sealed class LockstepOptions
    {
        /// <summary>Ticks per real second for speed 1..3. Plan §7.1 values, not verified in game (WAHRSCHEINLICH).</summary>
        public int[] TicksPerSecond { get; set; } = { 60, 180, 360 };
        /// <summary>How far ahead of the slowest game the horizon may run, in seconds at the current speed.</summary>
        public double MaximumLeadSeconds { get; set; } = 2.0;
        public int MinimumLeadTicks { get; set; } = 60;
        public int HashInterval { get; set; } = 60;
        public double CommandsPerSecond { get; set; } = 30;
        public double CommandBurst { get; set; } = 60;
        /// <summary>Coordinated save every this many ticks; 60000 = RimWorld.GenDate.TicksPerDay (checked by reflection).</summary>
        public int AutosaveIntervalTicks { get; set; } = 60000;
        /// <summary>After this many resyncs of one game without a matching comparison in between, give up (ADR 0033).</summary>
        public int MaximumResyncsInARow { get; set; } = 3;
    }

    /// <summary>
    /// The lockstep coordinator in the host's embedded server (ADR 0030). Pure state machine: callers pass the clock and
    /// deliver the returned messages in order, per recipient. Not thread-safe; the server holds one lock per session.
    /// </summary>
    public sealed class LockstepSession
    {
        private readonly LockstepOptions options;
        private readonly Dictionary<Guid, Participant> participants = new Dictionary<Guid, Participant>();
        private readonly SortedDictionary<int, Dictionary<Guid, ulong>> pendingHashes = new SortedDictionary<int, Dictionary<Guid, ulong>>();
        private ulong nextSequence = 1;
        private int clockBaseTick;
        private long clockBaseMs;
        private int lastComparedHashTick = -1;
        private int nextAutosave;
        // Paused by the coordinator itself (desync repair, vote); it resumes only when every such reason is done (ADR 0034).
        private bool systemPaused;
        private int resyncTick = -1;
        private readonly SortedDictionary<uint, Vote> votes = new SortedDictionary<uint, Vote>();
        private uint nextVoteId = 1;
        private readonly CoopDecisions decisions;

        /// <param name="decisions">Coop decision rule from the lobby mode (ADR 0031); votes exist only with Democracy.</param>
        public LockstepSession(Guid host, IEnumerable<(Guid Session, PlayerKey Key)> members, long nowMs, LockstepOptions? options = null,
            CoopDecisions decisions = CoopDecisions.Host)
        {
            this.decisions = decisions;
            this.options = options ?? new LockstepOptions();
            if (this.options.TicksPerSecond.Length != LockstepPayloads.MaximumSpeed) throw new ArgumentException("Need one tick rate per speed.");
            foreach (var (session, key) in members) participants[session] = new Participant(session, key, this.options, nowMs);
            if (!participants.ContainsKey(host)) throw new ArgumentException("The host must take part.", nameof(host));
            Host = host;
            clockBaseMs = nowMs;
            nextAutosave = this.options.AutosaveIntervalTicks;
        }

        public Guid Host { get; }
        public int Horizon { get; private set; }
        public byte Speed { get; private set; } = 1;
        public bool Paused { get; private set; } = true;
        public bool Ended { get; private set; }
        public IReadOnlyCollection<Guid> Participants => participants.Keys;
        /// <summary>Participants that count for pace and hash comparison; joiners are not active until caught up.</summary>
        public IReadOnlyCollection<Guid> ActiveParticipants => participants.Values.Where(p => p.Active).Select(p => p.Session).ToList();

        /// <summary>First message for everyone: the starting horizon (tick 0, paused).</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> Begin() => BroadcastHorizon();

        /// <summary>A game command. It runs everywhere after tick Horizon, in sequence order.</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> Submit(Guid issuer, byte[] data, long nowMs)
        {
            Participant who = Require(issuer);
            LockstepPayloads.ReadCommand(data);
            if (!who.Bucket.TryTake(nowMs)) throw new LockstepRuleException(7, "Too many commands.");
            return Schedule(issuer, who.Key, ScheduledKind.Game, data);
        }

        /// <summary>
        /// Pause, resume, speed or save. Anyone may pause or resume; only the host sets the speed (plan §7.1 default)
        /// and saves on demand (ADR 0032).
        /// </summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> Control(Guid issuer, ScheduledKind kind, byte value, long nowMs)
        {
            Participant who = Require(issuer);
            LockstepPayloads.Control(kind, value);
            if (kind == ScheduledKind.Speed && issuer != Host) throw new LockstepRuleException(3, "Only the host sets the speed.");
            if (kind == ScheduledKind.Save && issuer != Host) throw new LockstepRuleException(3, "Only the host saves.");
            if (!who.Bucket.TryTake(nowMs)) throw new LockstepRuleException(7, "Too many commands.");
            if (kind == ScheduledKind.Save) return Schedule(issuer, who.Key, ScheduledKind.Save, Array.Empty<byte>());
            if (kind == ScheduledKind.Pause)
            {
                // Resuming would undo the shared pause of a running vote; pausing is always allowed (ADR 0034).
                if (value == 0 && votes.Count > 0) throw new LockstepRuleException(6, "A vote is running.");
                systemPaused = false;   // a player's own pause or resume takes over from the coordinator's
            }
            var outgoing = new List<(Guid, WireMessage)>(Schedule(issuer, who.Key, kind, new[] { value }));
            Apply(kind, value, nowMs);
            outgoing.AddRange(BroadcastHorizon());
            return outgoing;
        }

        public IReadOnlyList<(Guid To, WireMessage Message)> ReportTick(Guid session, int tick, long nowMs)
        {
            Participant who = Require(session);
            if (tick > Horizon) throw new LockstepRuleException(1, "Reported a tick beyond the horizon.");
            if (tick > who.Tick) who.Tick = tick;
            var outgoing = new List<(Guid, WireMessage)>();
            outgoing.AddRange(TryActivate(who, nowMs));
            outgoing.AddRange(Advance(nowMs));
            return outgoing;
        }

        /// <summary>A joining or resyncing game confirms it loaded the host's snapshot (ADR 0033).</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> SnapshotLoaded(Guid session, long nowMs)
        {
            Participant who = Require(session);
            if (who.Active) throw new LockstepRuleException(6, "No snapshot was expected.");
            who.Loaded = true;
            return TryActivate(who, nowMs);
        }

        // A joiner or resynced game goes live once it loaded the snapshot and caught up with the slowest active game.
        private IReadOnlyList<(Guid To, WireMessage Message)> TryActivate(Participant who, long nowMs)
        {
            if (who.Active || !who.Loaded || who.Tick < participants.Values.Where(p => p.Active).Min(p => p.Tick))
                return Array.Empty<(Guid, WireMessage)>();
            who.Active = true; who.Resyncing = false;
            var synced = new WireMessage(MessageType.PlayerSynced, 0, LobbyPayloads.GuidValue(who.Session));
            var outgoing = new List<(Guid, WireMessage)>(participants.Keys.Select(to => (to, synced)));
            outgoing.AddRange(FinishResyncIfDone());
            outgoing.AddRange(TryResume(nowMs));
            return outgoing;
        }

        // Every repaired game is back (plan §7.9 step 5), whoever paused. A catching-up joiner does not hold this up.
        private IReadOnlyList<(Guid To, WireMessage Message)> FinishResyncIfDone()
        {
            if (resyncTick < 0 || participants.Values.Any(p => p.Resyncing)) return Array.Empty<(Guid, WireMessage)>();
            var resolved = new WireMessage(MessageType.DesyncResolved, 0, LockstepPayloads.Tick(resyncTick));
            resyncTick = -1;
            return participants.Keys.Select(to => (to, resolved)).ToList();
        }

        private IReadOnlyList<(Guid To, WireMessage Message)> PauseBySystem()
        {
            if (Paused) return Array.Empty<(Guid, WireMessage)>();
            var outgoing = new List<(Guid, WireMessage)>(Schedule(Guid.Empty, null, ScheduledKind.Pause, new byte[] { 1 }));
            Paused = true; clockBaseTick = Horizon; systemPaused = true;
            outgoing.AddRange(BroadcastHorizon());
            return outgoing;
        }

        // Resume at one shared point once nothing the coordinator paused for is left (plan §7.9 step 4, ADR 0034).
        private IReadOnlyList<(Guid To, WireMessage Message)> TryResume(long nowMs)
        {
            // Without the real time (nowMs < 0) the clock cannot be rebased; resuming then would count the whole pause as
            // elapsed and make every game catch up in one burst. The next call with a time resumes instead.
            if (nowMs < 0 || !systemPaused || !Paused || votes.Count > 0 || participants.Values.Any(p => p.Resyncing))
                return Array.Empty<(Guid, WireMessage)>();
            systemPaused = false;
            var outgoing = new List<(Guid, WireMessage)>(Schedule(Guid.Empty, null, ScheduledKind.Pause, new byte[] { 0 }));
            Apply(ScheduledKind.Pause, 0, nowMs);
            outgoing.AddRange(BroadcastHorizon());
            return outgoing;
        }

        public IReadOnlyList<(Guid To, WireMessage Message)> ReportHash(Guid session, int tick, ulong hash)
        {
            Participant who = Require(session);
            // Reports from a game's state before it reloaded belong to the diverged state (ADR 0033).
            if (who.ResyncFrom != null && tick <= who.ResyncFrom.Value) return Array.Empty<(Guid, WireMessage)>();
            if (tick % options.HashInterval != 0) throw new LockstepRuleException(1, "Hashes are reported every " + options.HashInterval + " ticks.");
            if (tick > Horizon) throw new LockstepRuleException(1, "Reported a tick beyond the horizon.");
            if (tick <= lastComparedHashTick) return Array.Empty<(Guid, WireMessage)>();
            if (!pendingHashes.TryGetValue(tick, out var reports)) pendingHashes[tick] = reports = new Dictionary<Guid, ulong>();
            reports[session] = hash;
            return CompareReadyHashes();
        }

        /// <summary>Moves the horizon with the clock, limited by the slowest game. Call about every 50 ms.</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> Advance(long nowMs)
        {
            if (Ended || Paused) return Array.Empty<(Guid, WireMessage)>();
            int rate = options.TicksPerSecond[Speed - 1];
            long target = clockBaseTick + (nowMs - clockBaseMs) * rate / 1000;
            int lead = Math.Max(options.MinimumLeadTicks, (int)(options.MaximumLeadSeconds * rate));
            long limit = (long)participants.Values.Where(p => p.Active).Min(p => p.Tick) + lead;
            if (target > limit)
            {
                // The slowest game sets the pace; rebase the clock so no backlog builds up and bursts later.
                target = limit; clockBaseTick = (int)limit; clockBaseMs = nowMs;
            }
            if (target <= Horizon) return Array.Empty<(Guid, WireMessage)>();
            Horizon = (int)Math.Min(target, int.MaxValue);
            var outgoing = new List<(Guid, WireMessage)>();
            if (Horizon >= nextAutosave)
            {
                // Everyone saves at the same point, once per interval (SAVE-001).
                outgoing.AddRange(Schedule(Guid.Empty, null, ScheduledKind.Save, Array.Empty<byte>()));
                nextAutosave = (Horizon / options.AutosaveIntervalTicks + 1) * options.AutosaveIntervalTicks;
            }
            outgoing.AddRange(BroadcastHorizon());
            return outgoing;
        }

        /// <summary>
        /// A player joins the running game (JOIN-001). The host saves a snapshot here for them; from the very next command
        /// on they receive everything, load the snapshot, catch up and go live (ADR 0032).
        /// </summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> AddJoiner(Guid session, PlayerKey key, long nowMs)
        {
            if (Ended) throw new LockstepRuleException(6, "The session has ended.");
            if (participants.ContainsKey(session)) throw new LockstepRuleException(6, "Already part of this session.");
            // Sent before the joiner is added, so it never receives its own snapshot command.
            var outgoing = new List<(Guid, WireMessage)>(Schedule(Guid.Empty, null, ScheduledKind.Save, session.ToByteArray()));
            var snapshot = new JoinSnapshot(Horizon, nextSequence - 1, Host, Horizon, Speed, Paused);
            participants[session] = new Participant(session, key, options, nowMs) { Active = false, Loaded = false, Tick = Horizon };
            outgoing.Add((session, new WireMessage(MessageType.JoinSnapshot, 0, LockstepPayloads.Join(snapshot))));
            outgoing.Add(BroadcastHorizon().First(o => o.To == session));
            return outgoing;
        }

        public IReadOnlyList<(Guid To, WireMessage Message)> RemoveParticipant(Guid session, long nowMs = -1)
        {
            if (!participants.Remove(session)) return Array.Empty<(Guid, WireMessage)>();
            if (session == Host || participants.Count == 0) { Ended = true; return Array.Empty<(Guid, WireMessage)>(); }
            foreach (var reports in pendingHashes.Values) reports.Remove(session);
            var outgoing = new List<(Guid, WireMessage)>(CompareReadyHashes());
            // Disconnected players do not count in votes (ADR 0031); a vote may now be complete.
            foreach (Vote vote in votes.Values.ToList())
            {
                vote.Choices.Remove(session);
                outgoing.AddRange(CloseIfEveryoneVoted(vote, nowMs));
            }
            outgoing.AddRange(FinishResyncIfDone());
            outgoing.AddRange(TryResume(nowMs));
            return outgoing;
        }

        /// <summary>The host's game opens a vote for an event that needs a choice (ADR 0034). Pauses for everyone.</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> OpenVote(Guid issuer, byte options, byte[] eventKey, long nowMs)
        {
            Require(issuer);
            if (decisions != CoopDecisions.Democracy) throw new LockstepRuleException(6, "Votes are only used in the Coop democracy mode.");
            if (issuer != Host) throw new LockstepRuleException(3, "Only the host opens a vote.");
            LockstepPayloads.VoteOpen(options, eventKey);
            var vote = new Vote(nextVoteId++, (byte[])eventKey.Clone(), options);
            votes[vote.Id] = vote;
            var outgoing = new List<(Guid, WireMessage)>(PauseBySystem());
            outgoing.AddRange(BroadcastVote(vote, open: true, VoteView.NoResult));
            return outgoing;
        }

        /// <summary>An active player picks or changes an option. The vote ends once every active player has chosen.</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> CastVote(Guid voter, uint voteId, byte option, long nowMs)
        {
            Participant who = Require(voter);
            Vote vote = RequireVote(voteId);
            if (!who.Active) throw new LockstepRuleException(6, "Only active players vote.");
            if (option >= vote.Options) throw new LockstepRuleException(1, "No such option.");
            vote.Choices[voter] = option;
            var outgoing = new List<(Guid, WireMessage)>(BroadcastVote(vote, open: true, VoteView.NoResult));
            outgoing.AddRange(CloseIfEveryoneVoted(vote, nowMs));
            return outgoing;
        }

        /// <summary>The host ends a vote early; players without a choice abstain (ADR 0031).</summary>
        public IReadOnlyList<(Guid To, WireMessage Message)> CloseVote(Guid issuer, uint voteId, long nowMs)
        {
            Require(issuer);
            if (issuer != Host) throw new LockstepRuleException(3, "Only the host ends a vote.");
            Vote vote = RequireVote(voteId);
            byte? result = Decide(vote) ?? throw new LockstepRuleException(6, "Tie: the host has to vote first.");
            return Close(vote, result.Value, nowMs);
        }

        private IReadOnlyList<(Guid To, WireMessage Message)> CloseIfEveryoneVoted(Vote vote, long nowMs)
        {
            if (!votes.ContainsKey(vote.Id) || participants.Values.Where(p => p.Active).Any(p => !vote.Choices.ContainsKey(p.Session)))
                return Array.Empty<(Guid, WireMessage)>();
            byte? result = Decide(vote);
            // A tie the host's choice does not break stays open until someone changes their vote or the host does.
            return result == null ? Array.Empty<(Guid, WireMessage)>() : Close(vote, result.Value, nowMs);
        }

        // Majority of the cast votes; a tie goes to the host's option if it is among the tied ones.
        private byte? Decide(Vote vote)
        {
            var counts = new int[vote.Options];
            foreach (byte choice in vote.Choices.Values) counts[choice]++;
            int best = counts.Max();
            List<byte> tied = Enumerable.Range(0, vote.Options).Where(o => counts[o] == best).Select(o => (byte)o).ToList();
            if (tied.Count == 1) return tied[0];
            return vote.Choices.TryGetValue(Host, out byte hostChoice) && tied.Contains(hostChoice) ? hostChoice : (byte?)null;
        }

        private IReadOnlyList<(Guid To, WireMessage Message)> Close(Vote vote, byte result, long nowMs)
        {
            votes.Remove(vote.Id);
            // Every game applies the outcome at the same point.
            var outgoing = new List<(Guid, WireMessage)>(Schedule(Guid.Empty, null, ScheduledKind.VoteResult,
                LockstepPayloads.VoteResultData(vote.Id, result, vote.EventKey)));
            outgoing.AddRange(BroadcastVote(vote, open: false, result));
            outgoing.AddRange(TryResume(nowMs));
            return outgoing;
        }

        private IReadOnlyList<(Guid To, WireMessage Message)> BroadcastVote(Vote vote, bool open, byte result)
        {
            var view = new VoteView(vote.Id, vote.EventKey, vote.Options, open, result,
                vote.Choices.OrderBy(c => c.Key).Select(c => (c.Key, c.Value)).ToList());
            var message = new WireMessage(MessageType.VoteState, 0, LockstepPayloads.VoteState(view));
            return participants.Keys.Select(to => (to, message)).ToList();
        }

        private Vote RequireVote(uint voteId)
        {
            if (!votes.TryGetValue(voteId, out Vote? vote)) throw new LockstepRuleException(4, "No such vote.");
            return vote;
        }

        private sealed class Vote
        {
            public Vote(uint id, byte[] eventKey, byte options) { Id = id; EventKey = eventKey; Options = options; }
            public uint Id { get; }
            public byte[] EventKey { get; }
            public byte Options { get; }
            public Dictionary<Guid, byte> Choices { get; } = new Dictionary<Guid, byte>();
        }

        private IReadOnlyList<(Guid To, WireMessage Message)> Schedule(Guid issuer, PlayerKey? key, ScheduledKind kind, byte[] data)
        {
            if (Ended) throw new LockstepRuleException(6, "The session has ended.");
            var command = new ScheduledCommand(Horizon, nextSequence++, issuer, key, kind, data);
            var message = new WireMessage(MessageType.ScheduledCommand, 0, LockstepPayloads.Scheduled(command));
            return participants.Keys.Select(to => (to, message)).ToList();
        }

        private void Apply(ScheduledKind kind, byte value, long nowMs)
        {
            // The clock changes from the current horizon on, where every game applies the same command.
            clockBaseTick = Horizon; clockBaseMs = nowMs;
            if (kind == ScheduledKind.Pause) Paused = value == 1;
            else Speed = value;
        }

        private IReadOnlyList<(Guid To, WireMessage Message)> CompareReadyHashes()
        {
            var outgoing = new List<(Guid, WireMessage)>();
            foreach (int tick in pendingHashes.Keys.ToList())
            {
                var reports = pendingHashes[tick];
                // Only active games are waited for; a joiner's reports still count when present.
                if (!participants.Values.Where(p => p.Active).All(p => reports.ContainsKey(p.Session))) break;
                pendingHashes.Remove(tick);
                lastComparedHashTick = tick;
                ulong reference = reports[Host];
                List<Guid> differing = reports.Where(r => participants.ContainsKey(r.Key) && r.Value != reference)
                    .Select(r => r.Key).OrderBy(p => p).ToList();
                foreach (KeyValuePair<Guid, ulong> report in reports)
                    if (report.Value == reference && participants.TryGetValue(report.Key, out Participant? matching)) matching.ResyncsInARow = 0;
                if (differing.Count == 0) continue;
                // Absolutely in sync means never run on silently: tell everyone and stop time at one shared point.
                var alarm = new WireMessage(MessageType.DesyncDetected, 0, LockstepPayloads.Desync(tick, differing));
                outgoing.AddRange(participants.Keys.Select(to => (to, alarm)));
                outgoing.AddRange(PauseBySystem());
                outgoing.AddRange(Resync(tick, differing));
            }
            return outgoing;
        }

        // ADR 0033: the differing games reload the host's state from a snapshot taken right here.
        private IReadOnlyList<(Guid To, WireMessage Message)> Resync(int tick, IReadOnlyList<Guid> differing)
        {
            var outgoing = new List<(Guid, WireMessage)>();
            var targets = new List<Participant>();
            var gaveUp = new List<Guid>();
            foreach (Guid id in differing)
            {
                Participant p = participants[id];
                if (!p.Active) continue;                                       // already reloading
                if (p.ResyncsInARow >= options.MaximumResyncsInARow) { gaveUp.Add(id); continue; }
                targets.Add(p);
            }
            if (gaveUp.Count > 0)
            {
                // Stays paused; the host decides, e.g. by removing the player.
                systemPaused = false;
                var message = new WireMessage(MessageType.ResyncGaveUp, 0, LockstepPayloads.Desync(tick, gaveUp));
                outgoing.AddRange(participants.Keys.Select(to => (to, message)));
            }
            if (targets.Count == 0) return outgoing;
            resyncTick = tick;
            byte[] ids = targets.SelectMany(p => p.Session.ToByteArray()).ToArray();
            outgoing.AddRange(Schedule(Guid.Empty, null, ScheduledKind.Save, ids));
            var snapshot = new JoinSnapshot(Horizon, nextSequence - 1, Host, Horizon, Speed, Paused);
            foreach (Participant p in targets)
            {
                p.ResyncsInARow++;
                p.Active = false; p.Loaded = false; p.Resyncing = true; p.Tick = Horizon; p.ResyncFrom = Horizon;
                foreach (var reports in pendingHashes.Values) reports.Remove(p.Session);
                outgoing.Add((p.Session, new WireMessage(MessageType.JoinSnapshot, 0, LockstepPayloads.Join(snapshot))));
            }
            return outgoing;
        }

        private IReadOnlyList<(Guid To, WireMessage Message)> BroadcastHorizon()
        {
            Participant slowest = participants.Values.Where(p => p.Active).OrderBy(p => p.Tick).ThenBy(p => p.Session).First();
            var message = new WireMessage(MessageType.TickHorizon, 0, LockstepPayloads.Horizon(
                new TickHorizon(Horizon, Speed, Paused, slowest.Session, slowest.Tick)));
            return participants.Keys.Select(to => (to, message)).ToList();
        }

        private Participant Require(Guid session)
        {
            if (Ended) throw new LockstepRuleException(6, "The session has ended.");
            if (!participants.TryGetValue(session, out Participant? who)) throw new LockstepRuleException(3, "Not part of this session.");
            return who;
        }

        private sealed class Participant
        {
            public Participant(Guid session, PlayerKey key, LockstepOptions options, long nowMs)
            { Session = session; Key = key; Bucket = new TokenBucket(options.CommandsPerSecond, options.CommandBurst, nowMs); }
            public Guid Session { get; }
            public PlayerKey Key { get; }
            public int Tick { get; set; }
            public bool Active { get; set; } = true;
            /// <summary>False while a joining or resyncing game has not confirmed loading its snapshot.</summary>
            public bool Loaded { get; set; } = true;
            /// <summary>Hash reports at or before this tick come from the state before a resync and are dropped.</summary>
            public int? ResyncFrom { get; set; }
            public int ResyncsInARow { get; set; }
            /// <summary>Reloading after a desync; until done the coordinator does not resume (ADR 0033).</summary>
            public bool Resyncing { get; set; }
            public TokenBucket Bucket { get; }
        }

        private sealed class TokenBucket
        {
            private readonly double rate, capacity;
            private double tokens;
            private long lastMs;
            public TokenBucket(double rate, double capacity, long nowMs) { this.rate = rate; this.capacity = capacity; tokens = capacity; lastMs = nowMs; }
            public bool TryTake(long nowMs)
            {
                tokens = Math.Min(capacity, tokens + Math.Max(0, nowMs - lastMs) * rate / 1000.0);
                lastMs = nowMs;
                if (tokens < 1) return false;
                tokens -= 1;
                return true;
            }
        }
    }
}
