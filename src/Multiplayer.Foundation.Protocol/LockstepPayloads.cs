using System;
using System.Collections.Generic;

namespace Multiplayer.Foundation.Protocol
{
    public enum ScheduledKind : byte
    {
        Game = 0,
        Pause = 1,
        Speed = 2,
        /// <summary>Save here (ADR 0032). Empty data: coordinated save. 16 bytes: snapshot for that joining session.</summary>
        Save = 3
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
