using System;
using System.Collections.Generic;
using System.Linq;

namespace Multiplayer.Foundation.Protocol
{
    public enum ScheduledKind : byte
    {
        Game = 0,
        Pause = 1,
        Speed = 2,
        /// <summary>Save here (ADR 0032). Empty data: coordinated save. n × 16 bytes: snapshot for these sessions (join or resync, ADR 0033).</summary>
        Save = 3,
        /// <summary>Outcome of a Coop democracy vote (ADR 0034); data: vote id u32, option u8, event key.</summary>
        VoteResult = 4
    }

    /// <summary>A Coop democracy vote as everyone sees it (ADR 0034).</summary>
    public sealed class VoteView
    {
        public const byte NoResult = 255;
        public VoteView(uint id, byte[] eventKey, byte options, bool open, byte result, IReadOnlyList<(Guid Session, byte Option)> choices)
        { Id = id; EventKey = eventKey; Options = options; Open = open; Result = result; Choices = choices; }
        public uint Id { get; }
        /// <summary>Opaque key the games use to find the event that needs the choice.</summary>
        public byte[] EventKey { get; }
        public byte Options { get; }
        public bool Open { get; }
        /// <summary>Chosen option once closed, else <see cref="NoResult"/>. Players without a choice abstained.</summary>
        public byte Result { get; }
        public IReadOnlyList<(Guid Session, byte Option)> Choices { get; }
    }

    /// <summary>Where a joining game starts (ADR 0032): the host's save after boundary/sequence.</summary>
    public sealed class JoinSnapshot
    {
        public JoinSnapshot(int boundary, ulong sequence, Guid host, int horizon, byte speed, bool paused)
        {
            if (boundary < 0 || horizon < boundary) throw new ArgumentOutOfRangeException(nameof(boundary));
            Boundary = boundary; Sequence = sequence; Host = host; Horizon = horizon; Speed = speed; Paused = paused;
        }
        public int Boundary { get; }
        /// <summary>Sequence of the Save command the snapshot was taken at; the joiner continues with the next one.</summary>
        public ulong Sequence { get; }
        public Guid Host { get; }
        public int Horizon { get; }
        public byte Speed { get; }
        public bool Paused { get; }
    }

    /// <summary>
    /// A command with its place in the shared order (ADR 0030): run after tick <see cref="Boundary"/> and before the
    /// next tick, ordered by <see cref="Sequence"/>. The issuer is stamped by the coordinator, never by the client.
    /// </summary>
    public sealed class ScheduledCommand
    {
        public ScheduledCommand(int boundary, ulong sequence, Guid issuerSession, PlayerKey? issuerKey, ScheduledKind kind, byte[] data)
        {
            if (boundary < 0) throw new ArgumentOutOfRangeException(nameof(boundary));
            Boundary = boundary; Sequence = sequence; IssuerSession = issuerSession; IssuerKey = issuerKey; Kind = kind;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }
        public int Boundary { get; }
        public ulong Sequence { get; }
        /// <summary>Guid.Empty when the coordinator itself issued it, e.g. the pause after a desync.</summary>
        public Guid IssuerSession { get; }
        public PlayerKey? IssuerKey { get; }
        public ScheduledKind Kind { get; }
        public byte[] Data { get; }
    }

    public sealed class TickHorizon
    {
        public TickHorizon(int horizon, byte speed, bool paused, Guid slowestSession, int slowestTick)
        { Horizon = horizon; Speed = speed; Paused = paused; SlowestSession = slowestSession; SlowestTick = slowestTick; }
        /// <summary>The highest session tick any game may complete.</summary>
        public int Horizon { get; }
        public byte Speed { get; }
        public bool Paused { get; }
        /// <summary>The player whose progress limits the pace, for "Warte auf …" in the UI.</summary>
        public Guid SlowestSession { get; }
        public int SlowestTick { get; }
    }

    /// <summary>Why the games run below the host's chosen speed (pace control, ADR 0049 addendum).</summary>
    public enum PaceReason : byte
    {
        /// <summary>The speed is the host's choice.</summary>
        None = 0,
        /// <summary>The slowest game held the horizon; the coordinator stepped the speed down and steps it back up on its own.</summary>
        SlowestGame = 1
    }

    /// <summary>
    /// Code 104: the coordinator's pace. <see cref="Speed"/> is what every game runs now, <see cref="ChosenSpeed"/> what
    /// the host asked for; they differ only with a <see cref="Reason"/>. Sent whenever one of them changes, so the UI can
    /// say why the game is slower than chosen. Never changes simulation state: the speed itself travels as a scheduled command.
    /// </summary>
    public sealed class PaceNotice
    {
        public PaceNotice(byte speed, byte chosenSpeed, PaceReason reason, Guid slowestSession, int slowestTick)
        { Speed = speed; ChosenSpeed = chosenSpeed; Reason = reason; SlowestSession = slowestSession; SlowestTick = slowestTick; }
        public byte Speed { get; }
        public byte ChosenSpeed { get; }
        public PaceReason Reason { get; }
        /// <summary>The game whose progress limited the pace when the notice was sent.</summary>
        public Guid SlowestSession { get; }
        public int SlowestTick { get; }
    }

    public static class LockstepPayloads
    {
        public const int MaximumCommandBytes = 32 * 1024;
        public const byte MaximumSpeed = 3;

        public static byte[] Command(byte[] data)
        {
            if (data == null || data.Length == 0) throw new ProtocolException("Command must contain data.");
            if (data.Length > MaximumCommandBytes) throw new ProtocolException("Command exceeds 32 KiB.");
            return (byte[])data.Clone();
        }
        public static byte[] ReadCommand(byte[] payload)
        {
            if (payload.Length == 0) throw new ProtocolException("Command must contain data.");
            if (payload.Length > MaximumCommandBytes) throw new ProtocolException("Command exceeds 32 KiB.");
            return payload;
        }

        public static byte[] Scheduled(ScheduledCommand command)
        {
            var writer = new PayloadWriter();
            writer.WriteUInt32((uint)command.Boundary); writer.WriteUInt64(command.Sequence); writer.WriteGuid(command.IssuerSession);
            writer.WriteBool(command.IssuerKey.HasValue); command.IssuerKey?.Write(writer);
            writer.WriteByte((byte)command.Kind); writer.WriteBytes(command.Data, MaximumCommandBytes);
            return writer.ToArray();
        }
        public static ScheduledCommand ReadScheduled(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            int boundary = ReadTick(reader); ulong sequence = reader.ReadUInt64(); Guid issuer = reader.ReadGuid();
            PlayerKey? key = reader.ReadBool() ? PlayerKey.Read(reader) : (PlayerKey?)null;
            byte kind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(ScheduledKind), (ScheduledKind)kind)) throw new ProtocolException("Unknown command kind.");
            byte[] data = reader.ReadBytes(MaximumCommandBytes);
            reader.EnsureComplete();
            return new ScheduledCommand(boundary, sequence, issuer, key, (ScheduledKind)kind, data);
        }

        public static byte[] Horizon(TickHorizon horizon)
        {
            var writer = new PayloadWriter();
            writer.WriteUInt32((uint)horizon.Horizon); writer.WriteByte(horizon.Speed); writer.WriteBool(horizon.Paused);
            writer.WriteGuid(horizon.SlowestSession); writer.WriteUInt32((uint)horizon.SlowestTick);
            return writer.ToArray();
        }
        public static TickHorizon ReadHorizon(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            var horizon = new TickHorizon(ReadTick(reader), reader.ReadByte(), reader.ReadBool(), reader.ReadGuid(), ReadTick(reader));
            reader.EnsureComplete();
            if (horizon.Speed < 1 || horizon.Speed > MaximumSpeed) throw new ProtocolException("Invalid speed.");
            return horizon;
        }

        public static byte[] Pace(PaceNotice pace)
        {
            ValidatePace(pace);
            var writer = new PayloadWriter();
            writer.WriteByte(pace.Speed); writer.WriteByte(pace.ChosenSpeed); writer.WriteByte((byte)pace.Reason);
            writer.WriteGuid(pace.SlowestSession); WriteTick(writer, pace.SlowestTick);
            return writer.ToArray();
        }
        public static PaceNotice ReadPace(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            var pace = new PaceNotice(reader.ReadByte(), reader.ReadByte(), (PaceReason)reader.ReadByte(), reader.ReadGuid(), ReadTick(reader));
            reader.EnsureComplete();
            ValidatePace(pace);
            return pace;
        }
        private static void ValidatePace(PaceNotice pace)
        {
            if (pace.Speed < 1 || pace.Speed > MaximumSpeed || pace.ChosenSpeed < 1 || pace.ChosenSpeed > MaximumSpeed)
                throw new ProtocolException("Invalid speed.");
            if (pace.Speed > pace.ChosenSpeed) throw new ProtocolException("The pace never exceeds the chosen speed.");
            if (!Enum.IsDefined(typeof(PaceReason), pace.Reason)) throw new ProtocolException("Unknown pace reason.");
            if ((pace.Reason == PaceReason.None) != (pace.Speed == pace.ChosenSpeed))
                throw new ProtocolException("A pace below the chosen speed needs a reason; the chosen speed has none.");
        }

        public static byte[] Join(JoinSnapshot join)
        {
            var writer = new PayloadWriter();
            WriteTick(writer, join.Boundary); writer.WriteUInt64(join.Sequence); writer.WriteGuid(join.Host);
            WriteTick(writer, join.Horizon); writer.WriteByte(join.Speed); writer.WriteBool(join.Paused);
            return writer.ToArray();
        }
        public static JoinSnapshot ReadJoin(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            int boundary = ReadTick(reader); ulong sequence = reader.ReadUInt64(); Guid host = reader.ReadGuid();
            int horizon = ReadTick(reader); byte speed = reader.ReadByte(); bool paused = reader.ReadBool();
            reader.EnsureComplete();
            if (speed < 1 || speed > MaximumSpeed) throw new ProtocolException("Invalid speed.");
            if (horizon < boundary) throw new ProtocolException("Horizon before the snapshot.");
            return new JoinSnapshot(boundary, sequence, host, horizon, speed, paused);
        }

        /// <summary>Session IDs listed in a Save command's data (ADR 0032/0033).</summary>
        public static IReadOnlyList<Guid> SaveTargets(byte[] data)
        {
            if (data.Length % 16 != 0) throw new ProtocolException("Save data must list whole session IDs.");
            var targets = new List<Guid>(data.Length / 16);
            for (int i = 0; i < data.Length; i += 16) { byte[] id = new byte[16]; Buffer.BlockCopy(data, i, id, 0, 16); targets.Add(new Guid(id)); }
            return targets;
        }

        public const int MaximumEventKeyBytes = 256;
        public const byte MinimumVoteOptions = 2, MaximumVoteOptions = 10;

        public static byte[] VoteOpen(byte options, byte[] eventKey)
        {
            ValidateVote(options, eventKey);
            var writer = new PayloadWriter(); writer.WriteByte(options); writer.WriteBytes(eventKey, MaximumEventKeyBytes);
            return writer.ToArray();
        }
        public static (byte Options, byte[] EventKey) ReadVoteOpen(byte[] payload)
        {
            var reader = new PayloadReader(payload); byte options = reader.ReadByte(); byte[] key = reader.ReadBytes(MaximumEventKeyBytes);
            reader.EnsureComplete(); ValidateVote(options, key);
            return (options, key);
        }
        public static byte[] VoteCast(uint voteId, byte option)
        { var writer = new PayloadWriter(); writer.WriteUInt32(voteId); writer.WriteByte(option); return writer.ToArray(); }
        public static (uint VoteId, byte Option) ReadVoteCast(byte[] payload)
        { var reader = new PayloadReader(payload); uint id = reader.ReadUInt32(); byte option = reader.ReadByte(); reader.EnsureComplete(); return (id, option); }
        public static byte[] VoteClose(uint voteId) { var writer = new PayloadWriter(); writer.WriteUInt32(voteId); return writer.ToArray(); }
        public static uint ReadVoteClose(byte[] payload) { var reader = new PayloadReader(payload); uint id = reader.ReadUInt32(); reader.EnsureComplete(); return id; }

        public static byte[] VoteState(VoteView vote)
        {
            if (vote.Choices.Count > 64) throw new ProtocolException("Too many voters.");
            var writer = new PayloadWriter();
            writer.WriteUInt32(vote.Id); writer.WriteBytes(vote.EventKey, MaximumEventKeyBytes); writer.WriteByte(vote.Options);
            writer.WriteBool(vote.Open); writer.WriteByte(vote.Result); writer.WriteByte((byte)vote.Choices.Count);
            foreach (var (session, option) in vote.Choices) { writer.WriteGuid(session); writer.WriteByte(option); }
            return writer.ToArray();
        }
        public static VoteView ReadVoteState(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            uint id = reader.ReadUInt32(); byte[] key = reader.ReadBytes(MaximumEventKeyBytes); byte options = reader.ReadByte();
            bool open = reader.ReadBool(); byte result = reader.ReadByte(); byte count = reader.ReadByte();
            if (count > 64) throw new ProtocolException("Too many voters.");
            var choices = new List<(Guid, byte)>(count);
            for (int i = 0; i < count; i++) choices.Add((reader.ReadGuid(), reader.ReadByte()));
            reader.EnsureComplete(); ValidateVote(options, key);
            if (open ? result != VoteView.NoResult : result >= options) throw new ProtocolException("Invalid vote result.");
            if (choices.Any(c => c.Item2 >= options)) throw new ProtocolException("Invalid vote option.");
            return new VoteView(id, key, options, open, result, choices);
        }

        public static byte[] VoteResultData(uint voteId, byte option, byte[] eventKey)
        { var writer = new PayloadWriter(); writer.WriteUInt32(voteId); writer.WriteByte(option); writer.WriteBytes(eventKey, MaximumEventKeyBytes); return writer.ToArray(); }
        public static (uint VoteId, byte Option, byte[] EventKey) ReadVoteResultData(byte[] data)
        { var reader = new PayloadReader(data); uint id = reader.ReadUInt32(); byte option = reader.ReadByte(); byte[] key = reader.ReadBytes(MaximumEventKeyBytes); reader.EnsureComplete(); return (id, option, key); }

        private static void ValidateVote(byte options, byte[] eventKey)
        {
            if (options < MinimumVoteOptions || options > MaximumVoteOptions) throw new ProtocolException("A vote needs 2 to 10 options.");
            if (eventKey == null || eventKey.Length == 0 || eventKey.Length > MaximumEventKeyBytes) throw new ProtocolException("Invalid event key.");
        }

        public static byte[] Tick(int tick) { var writer = new PayloadWriter(); WriteTick(writer, tick); return writer.ToArray(); }
        public static int ReadTickPayload(byte[] payload) { var reader = new PayloadReader(payload); int tick = ReadTick(reader); reader.EnsureComplete(); return tick; }

        public static byte[] Hash(int tick, ulong hash) { var writer = new PayloadWriter(); WriteTick(writer, tick); writer.WriteUInt64(hash); return writer.ToArray(); }
        public static (int Tick, ulong Hash) ReadHash(byte[] payload)
        { var reader = new PayloadReader(payload); int tick = ReadTick(reader); ulong hash = reader.ReadUInt64(); reader.EnsureComplete(); return (tick, hash); }

        public static byte[] Desync(int tick, IReadOnlyList<Guid> sessions)
        {
            if (sessions.Count == 0 || sessions.Count > 64) throw new ProtocolException("Invalid desync list.");
            var writer = new PayloadWriter(); WriteTick(writer, tick); writer.WriteByte((byte)sessions.Count);
            foreach (Guid session in sessions) writer.WriteGuid(session);
            return writer.ToArray();
        }
        public static (int Tick, IReadOnlyList<Guid> Sessions) ReadDesync(byte[] payload)
        {
            var reader = new PayloadReader(payload); int tick = ReadTick(reader); byte count = reader.ReadByte();
            if (count == 0 || count > 64) throw new ProtocolException("Invalid desync list.");
            var sessions = new List<Guid>(count);
            for (int i = 0; i < count; i++) sessions.Add(reader.ReadGuid());
            reader.EnsureComplete();
            return (tick, sessions);
        }

        public static byte[] Control(ScheduledKind kind, byte value)
        {
            ValidateControl(kind, value);
            return new[] { (byte)kind, value };
        }
        public static (ScheduledKind Kind, byte Value) ReadControl(byte[] payload)
        {
            var reader = new PayloadReader(payload); var kind = (ScheduledKind)reader.ReadByte(); byte value = reader.ReadByte();
            reader.EnsureComplete(); ValidateControl(kind, value);
            return (kind, value);
        }
        private static void ValidateControl(ScheduledKind kind, byte value)
        {
            if (kind == ScheduledKind.Pause) { if (value > 1) throw new ProtocolException("Pause value must be 0 or 1."); return; }
            if (kind == ScheduledKind.Speed) { if (value < 1 || value > MaximumSpeed) throw new ProtocolException("Invalid speed."); return; }
            if (kind == ScheduledKind.Save) { if (value != 0) throw new ProtocolException("Save takes no value."); return; }
            throw new ProtocolException("Unknown control kind.");
        }

        // Session ticks are non-negative ints, as RimWorld's tick counter is an int.
        private static void WriteTick(PayloadWriter writer, int tick)
        {
            if (tick < 0) throw new ProtocolException("Tick must not be negative.");
            writer.WriteUInt32((uint)tick);
        }
        private static int ReadTick(PayloadReader reader)
        {
            uint tick = reader.ReadUInt32();
            if (tick > int.MaxValue) throw new ProtocolException("Tick out of range.");
            return (int)tick;
        }
    }
}
