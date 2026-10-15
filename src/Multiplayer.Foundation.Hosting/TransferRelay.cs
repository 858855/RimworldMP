using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Server;

// Relays chunk transfers between lobby members. Holds metadata and acknowledgement counts only, never
// payload. Returns the messages to deliver so FoundationServer keeps all socket I/O in one place.
internal sealed class TransferRelay
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Transfer> transfers = new();
    private readonly Dictionary<Guid, Guid> activeBySender = new();

    public int ActiveTransfers { get { lock (gate) return transfers.Count; } }

    public IReadOnlyList<(Guid To, WireMessage Message)> Begin(Guid sender, TransferBegin begin, IReadOnlyList<Guid> lobbyMembers)
    {
        lock (gate)
        {
            if (transfers.ContainsKey(begin.TransferId)) return Reject(sender, begin.TransferId, "Transfer ID already in use.");
            if (activeBySender.ContainsKey(sender))
                return Reject(sender, begin.TransferId, "Another transfer from this player is still running.");
            List<Guid> others = lobbyMembers.Where(member => member != sender).ToList();
            if (others.Count == 0) return Reject(sender, begin.TransferId, "No recipient in your lobby.");
            if (begin.Peer != Guid.Empty)
            {
                if (!others.Contains(begin.Peer)) return Reject(sender, begin.TransferId, "Recipient is not in your lobby.");
                others = new List<Guid> { begin.Peer };
            }
            var transfer = new Transfer(begin.TransferId, sender, begin.TotalLength, begin.ChunkCount, others);
            transfers[transfer.Id] = transfer;
            activeBySender[sender] = transfer.Id;
            var forward = new WireMessage(MessageType.TransferBegin, 0, TransferPayloads.Begin(begin.WithPeer(sender)));
            return others.Select(recipient => (recipient, forward)).ToList();
        }
    }

    public IReadOnlyList<(Guid To, WireMessage Message)> Chunk(Guid sender, WireMessage message)
    {
        TransferChunk chunk = TransferPayloads.ReadChunk(message.Payload);
        lock (gate)
        {
            // Chunks still in flight after an abort are dropped quietly.
            if (!transfers.TryGetValue(chunk.TransferId, out Transfer? transfer) || transfer.Sender != sender)
                return Array.Empty<(Guid, WireMessage)>();
            if (chunk.Index >= transfer.ChunkCount ||
                chunk.Data.Length != TransferPayloads.ChunkLength(transfer.TotalLength, chunk.Index))
                return Remove(transfer, TransferAbortCode.Rejected, "Invalid chunk.", null);
            uint everyone = transfer.MinimumAcknowledged;
            if (chunk.Index >= everyone + TransferPayloads.MaximumWindowChunks)
                return Remove(transfer, TransferAbortCode.Rejected, "Transfer window exceeded.", null);
            // Every recipient has it already, so the sender missed our acknowledgement: repeat it.
            if (chunk.Index < everyone)
                return new[] { (sender, AckMessage(transfer.Id, everyone)) };
            return transfer.Acknowledged.Where(entry => entry.Value <= chunk.Index)
                .Select(entry => (entry.Key, message)).ToList();
        }
    }

    public IReadOnlyList<(Guid To, WireMessage Message)> Ack(Guid recipient, WireMessage message)
    {
        var (transferId, received) = TransferPayloads.ReadAck(message.Payload);
        lock (gate)
        {
            if (!transfers.TryGetValue(transferId, out Transfer? transfer) ||
                !transfer.Acknowledged.TryGetValue(recipient, out uint previous))
                return Array.Empty<(Guid, WireMessage)>();
            if (received > transfer.ChunkCount)
                return Remove(transfer, TransferAbortCode.Rejected, "Acknowledged more chunks than exist.", null);
            if (received > previous) transfer.Acknowledged[recipient] = received;
            uint everyone = transfer.MinimumAcknowledged;
            // Always forward, even when unchanged: a repeated acknowledgement is how a lost one is recovered.
            var forward = new[] { (transfer.Sender, AckMessage(transfer.Id, everyone)) };
            if (everyone == transfer.ChunkCount) Forget(transfer);
            return forward;
        }
    }

    public IReadOnlyList<(Guid To, WireMessage Message)> Abort(Guid actor, WireMessage message)
    {
        TransferAbort abort = TransferPayloads.ReadAbort(message.Payload);
        lock (gate)
        {
            if (!transfers.TryGetValue(abort.TransferId, out Transfer? transfer) || !transfer.Involves(actor))
                return Array.Empty<(Guid, WireMessage)>();
            return Remove(transfer, abort.Code, abort.Reason, actor);
        }
    }

    /// <summary>A participant left the lobby or disconnected: end every transfer it took part in.</summary>
    public IReadOnlyList<(Guid To, WireMessage Message)> PeerGone(Guid peer)
    {
        lock (gate)
        {
            var messages = new List<(Guid, WireMessage)>();
            foreach (Transfer transfer in transfers.Values.Where(t => t.Involves(peer)).ToList())
                messages.AddRange(Remove(transfer, TransferAbortCode.PeerLeft, "A participant left.", peer));
            return messages;
        }
    }

    private List<(Guid, WireMessage)> Remove(Transfer transfer, TransferAbortCode code, string reason, Guid? skip)
    {
        Forget(transfer);
        var abort = new WireMessage(MessageType.TransferAbort, 0,
            TransferPayloads.Abort(new TransferAbort(transfer.Id, code, reason)));
        return transfer.Participants.Where(p => p != skip).Select(p => (p, abort)).ToList();
    }

    private void Forget(Transfer transfer)
    {
        transfers.Remove(transfer.Id);
        activeBySender.Remove(transfer.Sender);
    }

    private static IReadOnlyList<(Guid, WireMessage)> Reject(Guid sender, Guid transferId, string reason) =>
        new[] { (sender, new WireMessage(MessageType.TransferAbort, 0,
            TransferPayloads.Abort(new TransferAbort(transferId, TransferAbortCode.Rejected, reason)))) };

    private static WireMessage AckMessage(Guid transferId, uint received) =>
        new(MessageType.TransferAck, 0, TransferPayloads.Ack(transferId, received));

    private sealed class Transfer
    {
        public Transfer(Guid id, Guid sender, ulong totalLength, uint chunkCount, IEnumerable<Guid> recipients)
        {
            Id = id; Sender = sender; TotalLength = totalLength; ChunkCount = chunkCount;
            Acknowledged = recipients.ToDictionary(recipient => recipient, _ => 0u);
        }
        public Guid Id { get; }
        public Guid Sender { get; }
        public ulong TotalLength { get; }
        public uint ChunkCount { get; }
        public Dictionary<Guid, uint> Acknowledged { get; }
        public uint MinimumAcknowledged => Acknowledged.Values.Min();
        public IEnumerable<Guid> Participants => Acknowledged.Keys.Append(Sender);
        public bool Involves(Guid peer) => peer == Sender || Acknowledged.ContainsKey(peer);
    }
}
