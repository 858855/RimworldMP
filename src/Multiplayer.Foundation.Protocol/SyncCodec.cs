using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Multiplayer.Foundation.Protocol
{
    /// <summary>
    /// SYNC-002 (ADR 0025): writer for the payload of a game command. Values in a fixed order, no type tags; the reader
    /// must read them in the same order. Game references (pawns, defs, zones) are written by the game side on top of these.
    /// </summary>
    public sealed class SyncWriter
    {
        public const int MaximumStringBytes = 256;
        private readonly PayloadWriter writer = new PayloadWriter();

        public void WriteByte(byte value) => writer.WriteByte(value);
        public void WriteBool(bool value) => writer.WriteBool(value);
        public void WriteUShort(ushort value) => writer.WriteUInt16(value);
        public void WriteInt(int value) => writer.WriteUInt32(unchecked((uint)value));
        public void WriteFloat(float value) => writer.WriteUInt32(new FloatBits { Float = value }.Bits);
        public void WriteString(string value) => writer.WriteString(value ?? "", MaximumStringBytes);
        public void WriteBytes(byte[] value, int maximumBytes) => writer.WriteBytes(value, maximumBytes);

        /// <summary>Map cells as rows of runs: compact for the rectangles and lines players drag. Order and duplicates are dropped.</summary>
        public void WriteCells(IEnumerable<(int X, int Z)> cells)
        {
            var runs = CellRuns.Encode(cells);
            writer.WriteUInt32((uint)runs.Count);
            foreach (var (z, x0, length) in runs) { writer.WriteUInt16((ushort)z); writer.WriteUInt16((ushort)x0); writer.WriteUInt16((ushort)length); }
        }

        public byte[] ToArray() => writer.ToArray();

        [StructLayout(LayoutKind.Explicit)]
        internal struct FloatBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public uint Bits;
        }
    }

    public sealed class SyncReader
    {
        public const int MaximumCells = 250_000;
        private readonly PayloadReader reader;

        public SyncReader(byte[] payload) { reader = new PayloadReader(payload ?? throw new ArgumentNullException(nameof(payload))); }

        public int Remaining => reader.Remaining;
        public byte ReadByte() => reader.ReadByte();
        public bool ReadBool() => reader.ReadBool();
        public ushort ReadUShort() => reader.ReadUInt16();
        public int ReadInt() => unchecked((int)reader.ReadUInt32());
        public float ReadFloat() => new SyncWriter.FloatBits { Bits = reader.ReadUInt32() }.Float;
        public string ReadString() => reader.ReadString(SyncWriter.MaximumStringBytes);
        public byte[] ReadBytes(int maximumBytes) => reader.ReadBytes(maximumBytes);

        public List<(int X, int Z)> ReadCells()
        {
            uint runs = reader.ReadUInt32();
            if (runs > MaximumCells) throw new ProtocolException("Too many cell runs.");
            var cells = new List<(int X, int Z)>();
            for (uint i = 0; i < runs; i++)
            {
                int z = reader.ReadUInt16(), x0 = reader.ReadUInt16(), length = reader.ReadUInt16();
                if (length == 0 || x0 + length > 65536) throw new ProtocolException("Invalid cell run.");
                if (cells.Count + length > MaximumCells) throw new ProtocolException("Too many cells.");
                for (int x = x0; x < x0 + length; x++) cells.Add((x, z));
            }
            return cells;
        }

        public void EnsureComplete() => reader.EnsureComplete();
    }

    /// <summary>Cells sorted by row, then column, merged into runs of neighbouring columns.</summary>
    public static class CellRuns
    {
        public static List<(int Z, int X0, int Length)> Encode(IEnumerable<(int X, int Z)> cells)
        {
            var runs = new List<(int Z, int X0, int Length)>();
            foreach (var (x, z) in cells.Distinct().OrderBy(c => c.Z).ThenBy(c => c.X))
            {
                if (x < 0 || z < 0 || x > ushort.MaxValue || z > ushort.MaxValue) throw new ProtocolException("Cell outside the map range.");
                if (runs.Count > 0)
                {
                    var (lastZ, lastX0, lastLength) = runs[runs.Count - 1];
                    if (lastZ == z && lastX0 + lastLength == x && lastLength < ushort.MaxValue)
                    { runs[runs.Count - 1] = (lastZ, lastX0, lastLength + 1); continue; }
                }
                runs.Add((z, x, 1));
            }
            return runs;
        }
    }
}
