using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Multiplayer.Foundation.Protocol
{
    public enum TransferKind : byte { ModManifest = 1, World = 2, Game = 3 }

    /// <summary>Announces a chunked transfer. The server fills in <see cref="Sender"/>.</summary>
    public sealed class TransferHeader
    {
        public TransferHeader(Guid transferId, TransferKind kind, Guid sender, Guid target, long size, byte[] sha256)
        {
            if (transferId == Guid.Empty) throw new ProtocolException("Transfer ID is required.");
            if (!Enum.IsDefined(typeof(TransferKind), kind)) throw new ProtocolException("Unknown transfer kind.");
            if (size < 1 || size > TransferPayloads.MaximumSize(kind)) throw new ProtocolException("Transfer size exceeds limit.");
            if (sha256 == null || sha256.Length != 32) throw new ProtocolException("SHA-256 must have 32 bytes.");
            TransferId = transferId; Kind = kind; Sender = sender; Target = target; Size = size; Sha256 = sha256;
        }
        public Guid TransferId { get; }
        public TransferKind Kind { get; }
        public Guid Sender { get; }
        /// <summary><see cref="Guid.Empty"/> sends to every other member of the sender's lobby.</summary>
        public Guid Target { get; }
        public long Size { get; }
        public byte[] Sha256 { get; }
        public uint ChunkCount => TransferPayloads.ChunkCountFor(Size);
        public int ChunkLength(uint index)
        {
            if (index >= ChunkCount) throw new ProtocolException("Chunk index out of range.");
            return index + 1 < ChunkCount ? TransferPayloads.ChunkBytes : (int)(Size - (long)index * TransferPayloads.ChunkBytes);
        }
        public TransferHeader WithSender(Guid sender) => new TransferHeader(TransferId, Kind, sender, Target, Size, Sha256);
    }

    public static class TransferPayloads
    {
        /// <summary>Chunk data per frame; leaves room for envelope and chunk header inside the 64 KiB frame.</summary>
        public const int ChunkBytes = 60 * 1024;
        /// <summary>Chunks a sender may have in flight beyond the slowest receiver's acknowledgement.</summary>
        public const uint Window = 32;

        public static long MaximumSize(TransferKind kind) => kind switch
        {
            TransferKind.ModManifest => 4L * 1024 * 1024,
            TransferKind.World => 64L * 1024 * 1024,
            TransferKind.Game => 256L * 1024 * 1024,
            _ => 0
        };

        public static uint ChunkCountFor(long size) => (uint)((size + ChunkBytes - 1) / ChunkBytes);

        public static byte[] Hash(byte[] data, int offset, int count)
        {
            using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(data, offset, count);
        }

        public static byte[] Begin(TransferHeader header)
        {
            var writer = new PayloadWriter();
            writer.WriteGuid(header.TransferId); writer.WriteByte((byte)header.Kind);
            writer.WriteGuid(header.Sender); writer.WriteGuid(header.Target);
            writer.WriteUInt64((ulong)header.Size); WriteHash(writer, header.Sha256);
            return writer.ToArray();
        }
        public static TransferHeader ReadBegin(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            Guid id = reader.ReadGuid(); var kind = (TransferKind)reader.ReadByte();
            Guid sender = reader.ReadGuid(), target = reader.ReadGuid();
            ulong size = reader.ReadUInt64(); byte[] hash = ReadHash(reader);
            reader.EnsureComplete();
            if (size > long.MaxValue) throw new ProtocolException("Transfer size exceeds limit.");
            return new TransferHeader(id, kind, sender, target, (long)size, hash);
        }

        // Chunk: transfer ID, index, then the raw chunk bytes to the end of the payload.
        public static byte[] Chunk(Guid transferId, uint index, byte[] data, int offset, int count)
        {
            if (count < 1 || count > ChunkBytes) throw new ProtocolException("Invalid chunk length.");
            var head = new PayloadWriter(); head.WriteGuid(transferId); head.WriteUInt32(index);
            byte[] payload = new byte[20 + count];
            Buffer.BlockCopy(head.ToArray(), 0, payload, 0, 20);
            Buffer.BlockCopy(data, offset, payload, 20, count);
            return payload;
        }
        public static (Guid TransferId, uint Index, byte[] Data) ReadChunk(byte[] bytes)
        {
            if (bytes.Length < 21 || bytes.Length > 20 + ChunkBytes) throw new ProtocolException("Invalid chunk length.");
            var reader = new PayloadReader(bytes);
            Guid id = reader.ReadGuid(); uint index = reader.ReadUInt32();
            byte[] data = new byte[bytes.Length - 20];
            Buffer.BlockCopy(bytes, 20, data, 0, data.Length);
            return (id, index, data);
        }

        /// <summary>Receiver reports how many chunks it holds. Clients send <see cref="Guid.Empty"/>; the server fills in the receiver.</summary>
        public static byte[] Ack(Guid transferId, Guid receiver, uint received)
        {
            var writer = new PayloadWriter(); writer.WriteGuid(transferId); writer.WriteGuid(receiver); writer.WriteUInt32(received);
            return writer.ToArray();
        }
        public static (Guid TransferId, Guid Receiver, uint Received) ReadAck(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            Guid id = reader.ReadGuid(), receiver = reader.ReadGuid(); uint received = reader.ReadUInt32();
            reader.EnsureComplete(); return (id, receiver, received);
        }

        public static byte[] Abort(Guid transferId, string reason)
        {
            var writer = new PayloadWriter(); writer.WriteGuid(transferId); writer.WriteString(reason, 160);
            return writer.ToArray();
        }
        public static (Guid TransferId, string Reason) ReadAbort(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            Guid id = reader.ReadGuid(); string reason = reader.ReadString(160);
            reader.EnsureComplete(); return (id, reason);
        }

        internal static void WriteHash(PayloadWriter writer, byte[] hash)
        { foreach (byte value in hash) writer.WriteByte(value); }
        internal static byte[] ReadHash(PayloadReader reader)
        {
            byte[] hash = new byte[32];
            for (int i = 0; i < 32; i++) hash[i] = reader.ReadByte();
            return hash;
        }
    }

    /// <summary>Sender side: splits data into chunks and limits chunks in flight by the slowest receiver.</summary>
    public sealed class TransferSource
    {
        private readonly byte[] data;
        private readonly Dictionary<Guid, uint> acknowledged = new Dictionary<Guid, uint>();

        public TransferSource(TransferKind kind, Guid target, byte[] data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            Header = new TransferHeader(Guid.NewGuid(), kind, Guid.Empty, target, data.Length,
                TransferPayloads.Hash(data, 0, data.Length));
        }

        public TransferHeader Header { get; }
        public uint NextIndex { get; private set; }
        public bool AllSent => NextIndex >= Header.ChunkCount;
        public int ReceiverCount => acknowledged.Count;
        /// <summary>True once every known receiver confirmed every chunk.</summary>
        public bool IsComplete => AllSent && acknowledged.Count > 0 && acknowledged.Values.All(r => r >= Header.ChunkCount);

        public void Acknowledge(Guid receiver, uint received)
        {
            if (received > Header.ChunkCount || received > NextIndex) throw new ProtocolException("Acknowledged unsent chunk.");
            acknowledged[receiver] = acknowledged.TryGetValue(receiver, out uint old) ? Math.Max(old, received) : received;
        }

        public bool TryTakeChunk(out uint index, out byte[] payload)
        {
            index = NextIndex; payload = Array.Empty<byte>();
            if (AllSent || acknowledged.Count == 0) return false;
            if (NextIndex >= acknowledged.Values.Min() + TransferPayloads.Window) return false;
            int length = Header.ChunkLength(index);
            payload = TransferPayloads.Chunk(Header.TransferId, index, data, (int)((long)index * TransferPayloads.ChunkBytes), length);
            NextIndex++;
            return true;
        }
    }

    /// <summary>Receiver side: accepts chunks strictly in order and checks the SHA-256 at the end.</summary>
    public sealed class TransferAssembler
    {
        private readonly byte[] buffer;

        public TransferAssembler(TransferHeader header)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            buffer = new byte[header.Size];
        }

        public TransferHeader Header { get; }
        public uint Received { get; private set; }
        public bool IsComplete => Received == Header.ChunkCount;
        public float Progress => Header.ChunkCount == 0 ? 1f : (float)Received / Header.ChunkCount;

        public void Add(uint index, byte[] data)
        {
            if (IsComplete) throw new ProtocolException("Transfer already complete.");
            if (index != Received) throw new ProtocolException("Chunk out of order.");
            if (data.Length != Header.ChunkLength(index)) throw new ProtocolException("Chunk has wrong length.");
            Buffer.BlockCopy(data, 0, buffer, (int)((long)index * TransferPayloads.ChunkBytes), data.Length);
            Received++;
            if (IsComplete && !TransferPayloads.Hash(buffer, 0, buffer.Length).SequenceEqual(Header.Sha256))
                throw new ProtocolException("Transfer hash mismatch.");
        }

        public byte[] Result
        {
            get
            {
                if (!IsComplete) throw new InvalidOperationException("Transfer is not complete.");
                return buffer;
            }
        }
    }
}
