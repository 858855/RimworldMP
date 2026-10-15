using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Multiplayer.Foundation.Protocol
{
    public sealed class PayloadWriter
    {
        private readonly MemoryStream stream = new MemoryStream();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public void WriteByte(byte value) => stream.WriteByte(value);
        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);
        public void WriteUInt16(ushort value)
        {
            WriteByte((byte)(value >> 8)); WriteByte((byte)value);
        }
        public void WriteUInt32(uint value)
        {
            WriteByte((byte)(value >> 24)); WriteByte((byte)(value >> 16));
            WriteByte((byte)(value >> 8)); WriteByte((byte)value);
        }
        public void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)(value >> 32)); WriteUInt32((uint)value);
        }
        public void WriteString(string value, int maximumBytes = 256)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            byte[] encoded = Utf8.GetBytes(value);
            if (encoded.Length > maximumBytes || encoded.Length > ushort.MaxValue)
                throw new ProtocolException("String exceeds limit.");
            WriteUInt16((ushort)encoded.Length);
            stream.Write(encoded, 0, encoded.Length);
        }
        public void WriteGuid(Guid value)
        {
            string hex = value.ToString("N");
            for (int i = 0; i < 16; i++)
                WriteByte(byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        public void WriteBytes(byte[] value, int maximumBytes)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length > maximumBytes) throw new ProtocolException("Byte block exceeds limit.");
            WriteUInt32((uint)value.Length);
            stream.Write(value, 0, value.Length);
        }
        public void WriteFixed(byte[] value, int length)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length != length) throw new ProtocolException("Fixed-size field has the wrong length.");
            stream.Write(value, 0, value.Length);
        }
        public byte[] ToArray() => stream.ToArray();
    }

    public sealed class PayloadReader
    {
        private readonly byte[] data;
        private int offset;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public PayloadReader(byte[] data) { this.data = data ?? throw new ArgumentNullException(nameof(data)); }
        public int Remaining => data.Length - offset;
        public byte ReadByte()
        {
            Require(1); return data[offset++];
        }
        public bool ReadBool()
        {
            byte value = ReadByte();
            if (value > 1) throw new ProtocolException("Invalid boolean.");
            return value == 1;
        }
        public ushort ReadUInt16() => (ushort)((ReadByte() << 8) | ReadByte());
        public uint ReadUInt32() => ((uint)ReadByte() << 24) | ((uint)ReadByte() << 16)
            | ((uint)ReadByte() << 8) | ReadByte();
        public ulong ReadUInt64() => ((ulong)ReadUInt32() << 32) | ReadUInt32();
        public string ReadString(int maximumBytes = 256)
        {
            ushort length = ReadUInt16();
            if (length > maximumBytes) throw new ProtocolException("String exceeds limit.");
            Require(length);
            try
            {
                string value = Utf8.GetString(data, offset, length);
                offset += length;
                return value;
            }
            catch (DecoderFallbackException)
            {
                throw new ProtocolException("Invalid UTF-8.");
            }
        }
        public Guid ReadGuid()
        {
            Require(16);
            char[] chars = new char[32];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < 16; i++)
            {
                byte value = ReadByte();
                chars[i * 2] = hex[value >> 4];
                chars[i * 2 + 1] = hex[value & 15];
            }
            return Guid.ParseExact(new string(chars), "N");
        }
        public byte[] ReadBytes(int maximumBytes)
        {
            uint length = ReadUInt32();
            if (length > (uint)maximumBytes) throw new ProtocolException("Byte block exceeds limit.");
            return ReadFixed((int)length);
        }
        public byte[] ReadFixed(int length)
        {
            Require(length);
            byte[] value = new byte[length];
            Buffer.BlockCopy(data, offset, value, 0, length);
            offset += length;
            return value;
        }
        public void EnsureComplete()
        {
            if (Remaining != 0) throw new ProtocolException("Trailing payload bytes.");
        }
        private void Require(int count)
        {
            if (count < 0 || count > Remaining) throw new ProtocolException("Truncated payload.");
        }
    }
}
