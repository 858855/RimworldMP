using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    // Acknowledge only after a chunk is consumed, not when it is read from the socket. LobbyClient buffers
    // incoming messages without limit until the next game frame; consuming first keeps that buffer inside
    // the sender's window too (ADR 0014).
    public sealed class TransferReceiver
    {
        private readonly byte[] buffer;
        private uint received;

        /// <param name="begin">TransferBegin as forwarded by the server; its Peer is the sender's session ID.</param>
        public TransferReceiver(TransferBegin begin)
        {
            Begin = begin ?? throw new ArgumentNullException(nameof(begin));
            TransferPayloads.Validate(begin);
            buffer = new byte[checked((int)begin.TotalLength)];
        }

        public TransferBegin Begin { get; }
        public Guid TransferId => Begin.TransferId;
        public Guid Sender => Begin.Peer;
        public uint ReceivedChunks => received;
        public TransferState State { get; private set; } = TransferState.Active;
        public TransferAbort? Failure { get; private set; }
        /// <summary>The verified payload, set once State is Completed.</summary>
        public byte[]? Data { get; private set; }

        public bool Owns(WireMessage message) =>
            (message.Type == MessageType.TransferChunk || message.Type == MessageType.TransferAbort) &&
            TransferPayloads.ReadTransferId(message.Payload) == TransferId;

        public IReadOnlyList<WireMessage> Handle(WireMessage message)
        {
            if (!Owns(message) || State == TransferState.Failed) return Array.Empty<WireMessage>();
            if (message.Type == MessageType.TransferAbort)
            {
                if (State == TransferState.Active) { State = TransferState.Failed; Failure = TransferPayloads.ReadAbort(message.Payload); }
                return Array.Empty<WireMessage>();
            }
            TransferChunk chunk = TransferPayloads.ReadChunk(message.Payload);
            // A duplicate means our acknowledgement went missing: repeat it, including the final one.
            if (State == TransferState.Completed || chunk.Index < received) return new[] { Ack() };
            // A gap is repaired by the sender's timeout; never acknowledge past it.
            if (chunk.Index > received) return Array.Empty<WireMessage>();
            if (chunk.Index >= Begin.ChunkCount ||
                chunk.Data.Length != TransferPayloads.ChunkLength(Begin.TotalLength, chunk.Index))
                return Fail(TransferAbortCode.InvalidData, "Chunk has the wrong length.");
            Buffer.BlockCopy(chunk.Data, 0, buffer, (int)chunk.Index * TransferPayloads.ChunkSize, chunk.Data.Length);
            received++;
            if (received < Begin.ChunkCount) return new[] { Ack() };
            byte[] actual;
            using (SHA256 hash = SHA256.Create()) actual = hash.ComputeHash(buffer);
            if (!actual.SequenceEqual(Begin.Sha256))
                return Fail(TransferAbortCode.HashMismatch, "SHA-256 does not match.");
            State = TransferState.Completed;
            Data = buffer;
            return new[] { Ack() };
        }

        public IReadOnlyList<WireMessage> Cancel(string reason)
        {
            if (State != TransferState.Active) return Array.Empty<WireMessage>();
            return Fail(TransferAbortCode.Cancelled, reason);
        }

        private WireMessage Ack() =>
            new WireMessage(MessageType.TransferAck, 0, TransferPayloads.Ack(TransferId, received));

        private IReadOnlyList<WireMessage> Fail(TransferAbortCode code, string reason)
        {
            State = TransferState.Failed;
            Failure = new TransferAbort(TransferId, code, reason);
            return new[] { new WireMessage(MessageType.TransferAbort, 0, TransferPayloads.Abort(Failure)) };
        }
    }
}
