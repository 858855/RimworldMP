using System;
using System.Collections.Generic;
using System.Linq;

namespace Multiplayer.Foundation.Protocol
{
    // SYNC-001 (ADR 0024): the game speed of a lockstep session. Same numbers as vanilla's Verse.TimeSpeed;
    // Ultrafast (dev only) is not allowed in a session.
    public enum SessionSpeed : byte
    {
        Paused = 0,
        Normal = 1,
        Fast = 2,
        Superfast = 3
    }

    // Kinds of lockstep commands. The server reads only SetSpeed. Marker has no game effect and lets players compare
    // the tick a command ran at. From FirstGameKind on the server orders commands without reading them (SYNC-002).
    public static class LockstepCommandKind
    {
        public const ushort SetSpeed = 1;
        public const ushort Marker = 2;
        public const ushort FirstGameKind = 16;
        public const int MaximumMarkerBytes = 64;

        public static byte[] Speed(SessionSpeed speed) => new[] { (byte)speed };
        public static SessionSpeed ReadSpeed(byte[] payload)
        {
            if (payload == null || payload.Length != 1 || payload[0] > (byte)SessionSpeed.Superfast)
                throw new ProtocolException("Invalid session speed.");
            return (SessionSpeed)payload[0];
        }
        public static byte[] MarkerText(string text)
        {
            var writer = new PayloadWriter(); writer.WriteString(text, MaximumMarkerBytes); return writer.ToArray();
        }
        public static string ReadMarker(byte[] payload)
        {
            var reader = new PayloadReader(payload); string text = reader.ReadString(MaximumMarkerBytes); reader.EnsureComplete(); return text;
        }
    }

    internal static class LockstepWire
    {
        public static void WriteTick(PayloadWriter writer, int tick)
        {
            if (tick < 0) throw new ProtocolException("Invalid tick.");
            writer.WriteUInt32((uint)tick);
        }
        public static int ReadTick(PayloadReader reader)
        {
            uint tick = reader.ReadUInt32();
            if (tick > int.MaxValue) throw new ProtocolException("Invalid tick.");
            return (int)tick;
        }
    }

    /// <summary>Client to server: a command for every player's game. The server stamps the issuer and the tick.</summary>
    public sealed class LockstepCommand
    {
        public const int MaximumPayloadBytes = 32 * 1024;

        public LockstepCommand(ushort kind, byte[] payload)
        {
            if (kind == 0) throw new ProtocolException("Invalid command kind.");
            if (payload == null || payload.Length > MaximumPayloadBytes) throw new ProtocolException("Command too large.");
            Kind = kind; Payload = payload;
        }
        public ushort Kind { get; }
        public byte[] Payload { get; }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter();
            writer.WriteUInt16(Kind); writer.WriteBytes(Payload, MaximumPayloadBytes);
            return writer.ToArray();
        }
        public static LockstepCommand Read(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            ushort kind = reader.ReadUInt16();
            byte[] data = reader.ReadBytes(MaximumPayloadBytes);
            reader.EnsureComplete();
            return new LockstepCommand(kind, data);
        }
    }

    /// <summary>
    /// Server to every player: run this command when your game is at <see cref="Tick"/> − 1, before tick
    /// <see cref="Tick"/>, after the commands with a smaller <see cref="Seq"/>.
    /// </summary>
    public sealed class ScheduledCommand
    {
        public ScheduledCommand(int tick, uint seq, PlayerKey issuer, LockstepCommand command)
        {
            if (tick < 1) throw new ProtocolException("Invalid tick.");
            if (seq == 0) throw new ProtocolException("Invalid sequence number.");
            if (!issuer.IsValid) throw new ProtocolException("Invalid player key.");
            Tick = tick; Seq = seq; Issuer = issuer; Command = command ?? throw new ArgumentNullException(nameof(command));
        }
        public int Tick { get; }
        public uint Seq { get; }
        public PlayerKey Issuer { get; }
        public LockstepCommand Command { get; }
        public ushort Kind => Command.Kind;
        public byte[] Payload => Command.Payload;

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter();
            LockstepWire.WriteTick(writer, Tick); writer.WriteUInt32(Seq); Issuer.Write(writer);
            writer.WriteUInt16(Command.Kind); writer.WriteBytes(Command.Payload, LockstepCommand.MaximumPayloadBytes);
            return writer.ToArray();
        }
        public static ScheduledCommand Read(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            int tick = LockstepWire.ReadTick(reader); uint seq = reader.ReadUInt32(); PlayerKey issuer = PlayerKey.Read(reader);
            ushort kind = reader.ReadUInt16(); byte[] data = reader.ReadBytes(LockstepCommand.MaximumPayloadBytes);
            reader.EnsureComplete();
            return new ScheduledCommand(tick, seq, issuer, new LockstepCommand(kind, data));
        }
    }

    /// <summary>
    /// Server to every player: you may simulate until your game is at <see cref="Horizon"/>. <see cref="Speed"/> is the
    /// pace the server runs at; <see cref="WaitingFor"/> is the player who holds it back (Guid.Empty: nobody), at
    /// <see cref="WaitingTick"/>. <see cref="CheckedTick"/> is the last tick whose state hashes matched for everyone (−1: none yet).
    /// </summary>
    public sealed class TickHorizon
    {
        public TickHorizon(int horizon, SessionSpeed speed, Guid waitingFor, int waitingTick, int checkedTick)
        {
            if (horizon < 0 || waitingTick < 0 || checkedTick < -1) throw new ProtocolException("Invalid tick.");
            if (!Enum.IsDefined(typeof(SessionSpeed), speed)) throw new ProtocolException("Invalid session speed.");
            Horizon = horizon; Speed = speed; WaitingFor = waitingFor; WaitingTick = waitingTick; CheckedTick = checkedTick;
        }
        public int Horizon { get; }
        public SessionSpeed Speed { get; }
        public Guid WaitingFor { get; }
        public int WaitingTick { get; }
        public int CheckedTick { get; }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter();
            LockstepWire.WriteTick(writer, Horizon); writer.WriteByte((byte)Speed); writer.WriteGuid(WaitingFor);
            LockstepWire.WriteTick(writer, WaitingTick); writer.WriteUInt32(unchecked((uint)CheckedTick));
            return writer.ToArray();
        }
        public static TickHorizon Read(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            int horizon = LockstepWire.ReadTick(reader);
            byte speed = reader.ReadByte();
            if (speed > (byte)SessionSpeed.Superfast) throw new ProtocolException("Invalid session speed.");
            Guid waiting = reader.ReadGuid(); int waitingTick = LockstepWire.ReadTick(reader);
            uint checkedRaw = reader.ReadUInt32();
            if (checkedRaw > int.MaxValue && checkedRaw != uint.MaxValue) throw new ProtocolException("Invalid tick.");
            reader.EnsureComplete();
            return new TickHorizon(horizon, (SessionSpeed)speed, waiting, waitingTick, checkedRaw == uint.MaxValue ? -1 : (int)checkedRaw);
        }
    }

    [Flags]
    public enum ClientTickFlags : byte
    {
        None = 0,
        // vanilla's TimeSlower.ForcedNormalSpeed (danger nearby): the server paces at Normal
        ForcedNormalSpeed = 1
    }

    /// <summary>Client to server: the tick this player's game has reached.</summary>
    public sealed class ClientTick
    {
        public ClientTick(int tick, ClientTickFlags flags)
        {
            if (tick < 0) throw new ProtocolException("Invalid tick.");
            if (((byte)flags & ~(byte)ClientTickFlags.ForcedNormalSpeed) != 0) throw new ProtocolException("Unknown tick flags.");
            Tick = tick; Flags = flags;
        }
        public int Tick { get; }
        public ClientTickFlags Flags { get; }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter(); LockstepWire.WriteTick(writer, Tick); writer.WriteByte((byte)Flags); return writer.ToArray();
        }
        public static ClientTick Read(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            int tick = LockstepWire.ReadTick(reader); byte flags = reader.ReadByte(); reader.EnsureComplete();
            return new ClientTick(tick, (ClientTickFlags)flags);
        }
    }

    /// <summary>Client to server: the hash of this player's game state right after <see cref="Tick"/>.</summary>
    public sealed class StateHash
    {
        public StateHash(int tick, ulong hash)
        {
            if (tick < 0) throw new ProtocolException("Invalid tick.");
            Tick = tick; Hash = hash;
        }
        public int Tick { get; }
        public ulong Hash { get; }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter(); LockstepWire.WriteTick(writer, Tick); writer.WriteUInt64(Hash); return writer.ToArray();
        }
        public static StateHash Read(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            int tick = LockstepWire.ReadTick(reader); ulong hash = reader.ReadUInt64(); reader.EnsureComplete();
            return new StateHash(tick, hash);
        }
    }

    /// <summary>Server to every player: at <see cref="Tick"/> these players' state differs from the host's.</summary>
    public sealed class DesyncDetected
    {
        public const int MaximumPlayers = 64;

        public DesyncDetected(int tick, IReadOnlyList<Guid> players)
        {
            if (tick < 0) throw new ProtocolException("Invalid tick.");
            if (players == null || players.Count == 0 || players.Count > MaximumPlayers) throw new ProtocolException("Invalid player list.");
            Tick = tick; Players = players;
        }
        public int Tick { get; }
        public IReadOnlyList<Guid> Players { get; }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter();
            LockstepWire.WriteTick(writer, Tick); writer.WriteByte((byte)Players.Count);
            foreach (Guid player in Players) writer.WriteGuid(player);
            return writer.ToArray();
        }
        public static DesyncDetected Read(byte[] payload)
        {
            var reader = new PayloadReader(payload);
            int tick = LockstepWire.ReadTick(reader); byte count = reader.ReadByte();
            if (count == 0 || count > MaximumPlayers) throw new ProtocolException("Invalid player list.");
            Guid[] players = Enumerable.Range(0, count).Select(_ => reader.ReadGuid()).ToArray();
            reader.EnsureComplete();
            return new DesyncDetected(tick, players);
        }
    }
}
