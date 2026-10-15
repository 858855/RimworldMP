using System;

namespace Multiplayer.Foundation.Protocol
{
    public enum TransferKind : byte
    {
        ModManifest = 1,
        World = 2,
        Game = 3,
        Snapshot = 4
    }

    public enum TransferAbortCode : byte
    {
        Rejected = 1,
        Cancelled = 2,
        HashMismatch = 3,
        Timeout = 4,
        PeerLeft = 5,
        InvalidData = 6
    }

    public sealed class TransferBegin
    {
        public TransferBegin(Guid transferId, TransferKind kind, Guid peer, ulong totalLength, uint chunkCount, byte[] sha256)
        {
            TransferId = transferId; Kind = kind; Peer = peer;
            TotalLength = totalLength; ChunkCount = chunkCount; Sha256 = sha256;
        }
        public Guid TransferId { get; }
        public TransferKind Kind { get; }
        /// <summary>Client to server: recipient, Guid.Empty for all other lobby members. Server to client: sender.</summary>
        public Guid Peer { get; }
        public ulong TotalLength { get; }
        public uint ChunkCount { get; }
        public byte[] Sha256 { get; }
        public TransferBegin WithPeer(Guid peer) => new TransferBegin(TransferId, Kind, peer, TotalLength, ChunkCount, Sha256);
    }

    public sealed class TransferChunk
    {
        public TransferChunk(Guid transferId, uint index, byte[] data) { TransferId = transferId; Index = index; Data = data; }
        public Guid TransferId { get; }
        public uint Index { get; }
        public byte[] Data { get; }
    }

    public sealed class TransferAbort
    {
        public TransferAbort(Guid transferId, TransferAbortCode code, string reason) { TransferId = transferId; Code = code; Reason = reason; }
        public Guid TransferId { get; }
        public TransferAbortCode Code { get; }
        public string Reason { get; }
    }

    // Steam limits drive these numbers (ADR 0014): the in-flight window must stay well below
    // SteamPeerStream's 1 MiB receive queue and Steam's reliable send buffer.
    public static class TransferPayloads
    {
        public const int ChunkSize = 60 * 1024;
        public const int WindowChunks = 6;
        public const int MaximumWindowChunks = 8;
        public const int HashLength = 32;

        public static ulong MaximumLength(TransferKind kind)
        {
            switch (kind)
            {
                case TransferKind.ModManifest: return 4UL * 1024 * 1024;
                case TransferKind.World: return 64UL * 1024 * 1024;
                case TransferKind.Game:
                case TransferKind.Snapshot: return 256UL * 1024 * 1024;
                default: throw new ProtocolException("Unknown transfer kind.");
            }
        }

        public static uint ChunkCountFor(ulong totalLength) => (uint)((totalLength + ChunkSize - 1) / ChunkSize);

        public static int ChunkLength(ulong totalLength, uint index)
        {
            uint count = ChunkCountFor(totalLength);
            if (index >= count) throw new ProtocolException("Chunk index out of range.");
            if (index < count - 1) return ChunkSize;
            return (int)(totalLength - (ulong)index * ChunkSize);
        }

        public static void Validate(TransferBegin begin)
        {
            if (begin.TransferId == Guid.Empty) throw new ProtocolException("Transfer ID is required.");
            if (!Enum.IsDefined(typeof(TransferKind), begin.Kind)) throw new ProtocolException("Unknown transfer kind.");
            if (begin.TotalLength == 0 || begin.TotalLength > MaximumLength(begin.Kind))
                throw new ProtocolException("Transfer length outside the limit for its kind.");
            if (begin.ChunkCount != ChunkCountFor(begin.TotalLength))
                throw new ProtocolException("Chunk count does not match the transfer length.");
            if (begin.Sha256 == null || begin.Sha256.Length != HashLength) throw new ProtocolException("SHA-256 must be 32 bytes.");
        }

        public static byte[] Begin(TransferBegin begin)
        {
            Validate(begin);
            var writer = new PayloadWriter();
            writer.WriteGuid(begin.TransferId); writer.WriteByte((byte)begin.Kind); writer.WriteGuid(begin.Peer);
            writer.WriteUInt64(begin.TotalLength); writer.WriteUInt32(begin.ChunkCount); writer.WriteFixed(begin.Sha256, HashLength);
            return writer.ToArray();
        }

        public static TransferBegin ReadBegin(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            var begin = new TransferBegin(reader.ReadGuid(), (TransferKind)reader.ReadByte(), reader.ReadGuid(),
                reader.ReadUInt64(), reader.ReadUInt32(), reader.ReadFixed(HashLength));
            reader.EnsureComplete(); Validate(begin);
            return begin;
        }

        public static byte[] Chunk(TransferChunk chunk)
        {
            if (chunk.Data == null || chunk.Data.Length == 0) throw new ProtocolException("Chunk must contain data.");
            var writer = new PayloadWriter();
            writer.WriteGuid(chunk.TransferId); writer.WriteUInt32(chunk.Index); writer.WriteBytes(chunk.Data, ChunkSize);
            return writer.ToArray();
        }

        public static TransferChunk ReadChunk(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            var chunk = new TransferChunk(reader.ReadGuid(), reader.ReadUInt32(), reader.ReadBytes(ChunkSize));
            reader.EnsureComplete();
            if (chunk.Data.Length == 0) throw new ProtocolException("Chunk must contain data.");
            return chunk;
        }

        public static byte[] Ack(Guid transferId, uint received)
        { var writer = new PayloadWriter(); writer.WriteGuid(transferId); writer.WriteUInt32(received); return writer.ToArray(); }

        public static (Guid TransferId, uint Received) ReadAck(byte[] bytes)
        { var reader = new PayloadReader(bytes); Guid id = reader.ReadGuid(); uint received = reader.ReadUInt32(); reader.EnsureComplete(); return (id, received); }

        public static byte[] Abort(TransferAbort abort)
        {
            if (!Enum.IsDefined(typeof(TransferAbortCode), abort.Code)) throw new ProtocolException("Unknown abort code.");
            var writer = new PayloadWriter();
            writer.WriteGuid(abort.TransferId); writer.WriteByte((byte)abort.Code); writer.WriteString(abort.Reason);
            return writer.ToArray();
        }

        public static TransferAbort ReadAbort(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            var abort = new TransferAbort(reader.ReadGuid(), (TransferAbortCode)reader.ReadByte(), reader.ReadString());
            reader.EnsureComplete();
            if (!Enum.IsDefined(typeof(TransferAbortCode), abort.Code)) throw new ProtocolException("Unknown abort code.");
            return abort;
        }

        /// <summary>Transfer ID of any transfer message, for routing without decoding the whole payload.</summary>
        public static Guid ReadTransferId(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            return reader.ReadGuid();
        }
    }
}
