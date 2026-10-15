using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    public enum TransferState
    {
        Active,
        Completed,
        Failed
    }

    // Pure state machine: the caller feeds incoming messages and the clock, and sends what comes back,
    // in order. No threads or I/O, so it fits the once-per-frame game pump and tests deterministically.
    public sealed class TransferSender
    {
        public static readonly TimeSpan DefaultAckTimeout = TimeSpan.FromSeconds(10);
        public const int MaximumRetransmits = 3;
        public const int MaximumHashRetries = 1;

        private readonly byte[] data;
        private readonly byte[] sha256;
        private readonly long ackTimeoutMs;
        private uint acknowledged;
        private uint nextToSend;
        private long lastProgressMs;
        private int retransmits;
        private int hashRetries;

        /// <param name="recipient">Session ID of one lobby member, or Guid.Empty for all other members.</param>
        /// <param name="data">Sent as is; the caller must not modify it while the transfer runs.</param>
        public TransferSender(TransferKind kind, Guid recipient, byte[] data, TimeSpan? ackTimeout = null)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (!Enum.IsDefined(typeof(TransferKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (data.Length == 0 || (ulong)data.Length > TransferPayloads.MaximumLength(kind))
                throw new ArgumentException("Data length is outside the limit for this transfer kind.", nameof(data));
            TimeSpan timeout = ackTimeout ?? DefaultAckTimeout;
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ackTimeout));
            Kind = kind;
            Recipient = recipient;
            ackTimeoutMs = (long)timeout.TotalMilliseconds;
            ChunkCount = TransferPayloads.ChunkCountFor((ulong)data.Length);
            using (SHA256 hash = SHA256.Create()) sha256 = hash.ComputeHash(data);
            TransferId = Guid.NewGuid();
        }

        public Guid TransferId { get; private set; }
        public TransferKind Kind { get; }
        public Guid Recipient { get; }
        public uint ChunkCount { get; }
        public uint AcknowledgedChunks => acknowledged;
        public uint UnacknowledgedChunks => nextToSend - acknowledged;
        public TransferState State { get; private set; } = TransferState.Active;
        public TransferAbort? Failure { get; private set; }
        public bool Started { get; private set; }

        public bool Owns(WireMessage message) =>
            (message.Type == MessageType.TransferAck || message.Type == MessageType.TransferAbort) &&
            TransferPayloads.ReadTransferId(message.Payload) == TransferId;

        public IReadOnlyList<WireMessage> Start(long nowMs)
        {
            if (Started) throw new InvalidOperationException("Transfer already started.");
            Started = true;
            return Restart(nowMs);
        }

        public IReadOnlyList<WireMessage> Handle(WireMessage message, long nowMs)
        {
            if (!Started) throw new InvalidOperationException("Transfer not started.");
            if (State != TransferState.Active || !Owns(message)) return Array.Empty<WireMessage>();
            if (message.Type == MessageType.TransferAbort)
            {
                TransferAbort abort = TransferPayloads.ReadAbort(message.Payload);
                if (abort.Code == TransferAbortCode.HashMismatch && hashRetries < MaximumHashRetries)
                {
                    hashRetries++;
                    TransferId = Guid.NewGuid();
                    return Restart(nowMs);
                }
                State = TransferState.Failed; Failure = abort;
                return Array.Empty<WireMessage>();
            }
            uint received = TransferPayloads.ReadAck(message.Payload).Received;
            if (received > nextToSend)
                return Fail(TransferAbortCode.InvalidData, "Acknowledged chunks that were never sent.");
            if (received > acknowledged)
            {
                acknowledged = received; lastProgressMs = nowMs; retransmits = 0;
            }
            if (acknowledged == ChunkCount)
            {
                State = TransferState.Completed;
                return Array.Empty<WireMessage>();
            }
            return FillWindow();
        }

        /// <summary>Call regularly. Without acknowledgement progress it resends from the last acknowledged chunk.</summary>
        public IReadOnlyList<WireMessage> Poll(long nowMs)
        {
            if (!Started || State != TransferState.Active || nowMs - lastProgressMs < ackTimeoutMs)
                return Array.Empty<WireMessage>();
            if (retransmits >= MaximumRetransmits)
                return Fail(TransferAbortCode.Timeout, "No acknowledgement progress.");
            retransmits++;
            lastProgressMs = nowMs;
            nextToSend = acknowledged;
            return FillWindow();
        }

        public IReadOnlyList<WireMessage> Cancel(string reason)
        {
            if (State != TransferState.Active) return Array.Empty<WireMessage>();
            return Fail(TransferAbortCode.Cancelled, reason);
        }

        private IReadOnlyList<WireMessage> Restart(long nowMs)
        {
            acknowledged = 0; nextToSend = 0; retransmits = 0; lastProgressMs = nowMs;
            var messages = new List<WireMessage>
            {
                new WireMessage(MessageType.TransferBegin, 0, TransferPayloads.Begin(new TransferBegin(
                    TransferId, Kind, Recipient, (ulong)data.Length, ChunkCount, sha256)))
            };
            messages.AddRange(FillWindow());
            return messages;
        }

        private IReadOnlyList<WireMessage> FillWindow()
        {
            var messages = new List<WireMessage>();
            while (nextToSend < ChunkCount && nextToSend - acknowledged < TransferPayloads.WindowChunks)
            {
                int length = TransferPayloads.ChunkLength((ulong)data.Length, nextToSend);
                byte[] slice = new byte[length];
                Buffer.BlockCopy(data, (int)nextToSend * TransferPayloads.ChunkSize, slice, 0, length);
                messages.Add(new WireMessage(MessageType.TransferChunk, 0,
                    TransferPayloads.Chunk(new TransferChunk(TransferId, nextToSend, slice))));
                nextToSend++;
            }
            return messages;
        }

        private IReadOnlyList<WireMessage> Fail(TransferAbortCode code, string reason)
        {
            State = TransferState.Failed;
            Failure = new TransferAbort(TransferId, code, reason);
            return new[] { new WireMessage(MessageType.TransferAbort, 0, TransferPayloads.Abort(Failure)) };
        }
    }
}
