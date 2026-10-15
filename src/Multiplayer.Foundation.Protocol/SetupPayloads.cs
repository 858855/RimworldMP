using System;

namespace Multiplayer.Foundation.Protocol
{
    // Phases of the shared new-game setup (plan §6.2, ADR 0017). Later phases arrive with SETUP-005.
    public enum SetupPhase : byte
    {
        Lobby = 0,
        Scenario = 1,
        Storyteller = 2,
        WorldParams = 3,
        WorldTransfer = 4,
        SiteSelection = 5,
        Ideology = 6,
        Characters = 7,
        Generating = 8
    }

    // A world tile as RimWorld 1.6 addresses it: RimWorld.Planet.PlanetTile is (tileId, layerId), because
    // Odyssey added planet layers such as orbit. A bare tile number would be ambiguous.
    public readonly struct SiteTile : IEquatable<SiteTile>
    {
        public SiteTile(int tileId, int layerId)
        {
            if (tileId < 0) throw new ArgumentOutOfRangeException(nameof(tileId));
            if (layerId < 0) throw new ArgumentOutOfRangeException(nameof(layerId));
            TileId = tileId; LayerId = layerId;
        }
        public int TileId { get; }
        public int LayerId { get; }
        public bool Equals(SiteTile other) => TileId == other.TileId && LayerId == other.LayerId;
        public override bool Equals(object? obj) => obj is SiteTile other && Equals(other);
        public override int GetHashCode() => (TileId * 397) ^ LayerId;
        public static bool operator ==(SiteTile left, SiteTile right) => left.Equals(right);
        public static bool operator !=(SiteTile left, SiteTile right) => !left.Equals(right);
        public override string ToString() => LayerId + ":" + TileId;

        public void Write(PayloadWriter writer) { writer.WriteUInt32((uint)TileId); writer.WriteUInt32((uint)LayerId); }
        public static SiteTile Read(PayloadReader reader)
        {
            uint tile = reader.ReadUInt32(), layer = reader.ReadUInt32();
            if (tile > int.MaxValue || layer > int.MaxValue) throw new ProtocolException("Invalid world tile.");
            return new SiteTile((int)tile, (int)layer);
        }
        public static void WriteOptional(PayloadWriter writer, SiteTile? tile)
        { writer.WriteBool(tile.HasValue); tile?.Write(writer); }
        public static SiteTile? ReadOptional(PayloadReader reader) => reader.ReadBool() ? Read(reader) : (SiteTile?)null;
    }

    public static class SetupPayloads
    {
        public static SetupPhase ReadPhase(PayloadReader reader)
        {
            byte value = reader.ReadByte();
            if (!Enum.IsDefined(typeof(SetupPhase), (SetupPhase)value)) throw new ProtocolException("Unknown setup phase.");
            return (SetupPhase)value;
        }

        public static byte[] Advance(SetupPhase target)
        {
            if (!Enum.IsDefined(typeof(SetupPhase), target)) throw new ProtocolException("Unknown setup phase.");
            return new[] { (byte)target };
        }
        public static SetupPhase ReadAdvance(byte[] bytes)
        { var reader = new PayloadReader(bytes); SetupPhase phase = ReadPhase(reader); reader.EnsureComplete(); return phase; }

        public static byte[] Flag(SiteTile? tile)
        { var writer = new PayloadWriter(); SiteTile.WriteOptional(writer, tile); return writer.ToArray(); }
        public static SiteTile? ReadFlag(byte[] bytes)
        { var reader = new PayloadReader(bytes); SiteTile? tile = SiteTile.ReadOptional(reader); reader.EnsureComplete(); return tile; }

        public static byte[] Site(SiteTile tile)
        { var writer = new PayloadWriter(); tile.Write(writer); return writer.ToArray(); }
        public static SiteTile ReadSite(byte[] bytes)
        { var reader = new PayloadReader(bytes); SiteTile tile = SiteTile.Read(reader); reader.EnsureComplete(); return tile; }
    }
}
