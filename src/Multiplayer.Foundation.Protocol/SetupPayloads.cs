using System;
using System.Collections.Generic;
using System.Linq;

namespace Multiplayer.Foundation.Protocol
{
    // Phases of the shared new-game setup (plan §6.2, ADR 0017). SETUP-005 (ADR 0023): in Generating the host starts
    // the game and sends it; Playing means every player loaded it. Time stays paused until lockstep (SYNC-001).
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
        Generating = 8,
        Playing = 9
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

    /// <summary>
    /// SETUP-001: what the host currently sees on its setup page, for the other players' setup view.
    /// Display text only; the binding values arrive with the world and the game save (plan §6.3).
    /// </summary>
    public sealed class SetupSettings
    {
        public const int MaximumEntries = 24;
        public const int MaximumLabelBytes = 64;
        public const int MaximumValueBytes = 256;

        public SetupSettings(string page, IReadOnlyList<(string Label, string Value)> entries)
        {
            if (entries == null || entries.Count > MaximumEntries) throw new ProtocolException("Too many setup entries.");
            Page = page ?? ""; Entries = entries;
        }
        public string Page { get; }
        public IReadOnlyList<(string Label, string Value)> Entries { get; }

        public bool SameAs(SetupSettings? other) =>
            other != null && other.Page == Page && other.Entries.SequenceEqual(Entries);

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter();
            writer.WriteString(Page, MaximumLabelBytes); writer.WriteByte((byte)Entries.Count);
            foreach (var (label, value) in Entries)
            { writer.WriteString(label, MaximumLabelBytes); writer.WriteString(value, MaximumValueBytes); }
            return writer.ToArray();
        }
        public static SetupSettings Read(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            string page = reader.ReadString(MaximumLabelBytes); byte count = reader.ReadByte();
            if (count > MaximumEntries) throw new ProtocolException("Too many setup entries.");
            var entries = new List<(string, string)>(count);
            for (int i = 0; i < count; i++) entries.Add((reader.ReadString(MaximumLabelBytes), reader.ReadString(MaximumValueBytes)));
            reader.EnsureComplete();
            return new SetupSettings(page, entries);
        }
    }

    /// <summary>
    /// SETUP-002: has this member loaded the host's world, and does its fingerprint match? From Generating on
    /// (SETUP-005) the same status reports the host's starting game instead.
    /// </summary>
    public enum WorldStatus : byte
    {
        /// <summary>Outside world transfer and the game start.</summary>
        None = 0,
        Waiting = 1,
        Match = 2,
        Mismatch = 3
    }

    /// <summary>
    /// Builds a SHA-256 over canonical values in a fixed order. Callers quantize floats themselves,
    /// so values that survive a save/load round trip hash the same on every machine.
    /// </summary>
    public sealed class FingerprintBuilder
    {
        private readonly System.IO.MemoryStream data = new System.IO.MemoryStream();
        private static readonly System.Text.UTF8Encoding Utf8 = new System.Text.UTF8Encoding(false);

        public FingerprintBuilder Add(int value)
        {
            for (int shift = 24; shift >= 0; shift -= 8) data.WriteByte((byte)(value >> shift));
            return this;
        }
        public FingerprintBuilder Add(string? value)
        {
            byte[] bytes = Utf8.GetBytes(value ?? "");
            Add(bytes.Length); data.Write(bytes, 0, bytes.Length);
            return this;
        }
        /// <summary>Adds a float rounded to the given number of decimals.</summary>
        public FingerprintBuilder Add(float value, int decimals) => Add((int)Math.Round(value * Math.Pow(10, decimals)));
        public byte[] Finish()
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) return sha.ComputeHash(data.ToArray());
        }
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

        public static byte[] Fingerprint(byte[] hash)
        {
            if (hash == null || hash.Length != 32) throw new ProtocolException("Fingerprint must have 32 bytes.");
            return (byte[])hash.Clone();
        }
        public static byte[] ReadFingerprint(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 32) throw new ProtocolException("Fingerprint must have 32 bytes.");
            return (byte[])bytes.Clone();
        }

        public static byte[] Site(SiteTile tile)
        { var writer = new PayloadWriter(); tile.Write(writer); return writer.ToArray(); }
        public static SiteTile ReadSite(byte[] bytes)
        { var reader = new PayloadReader(bytes); SiteTile tile = SiteTile.Read(reader); reader.EnsureComplete(); return tile; }
    }
}
