using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// Local connection telemetry: round trips of application pings and probes without an answer. It never changes
    /// replicated game state. The pace is not decided here: the coordinator lowers the shared speed for everyone when a
    /// game holds the horizon (ADR 0049 addendum, PaceNotice), a local cap would only make this player the slowest.
    /// </summary>
    internal sealed class ConnectionQuality
    {
        private const long IntervalMs = 3000;
        private const long TimeoutMs = 5000;
        private readonly Queue<bool> replies = new Queue<bool>();
        private readonly Queue<int> roundTrips = new Queue<int>();
        private uint pendingRequest;
        private ulong pendingNonce;
        private long sentAt;
        private long nextAt;

        internal int? PingMs => roundTrips.Count == 0 ? (int?)null : (int)Math.Round(roundTrips.Average());
        internal int? MissingPercent => replies.Count == 0 ? (int?)null :
            100 * replies.Count(reply => !reply) / replies.Count;

        internal string Display => "Ping: " + (PingMs?.ToString() ?? "…") + " ms · Ausfälle: " +
            (MissingPercent?.ToString() ?? "…") + " %";

        internal void Update(long nowMs)
        {
            if (pendingRequest == 0 || nowMs - sentAt < TimeoutMs) return;
            pendingRequest = 0;
            Record(false, null);
        }

        internal WireMessage? StartProbe(long nowMs, uint requestId)
        {
            Update(nowMs);
            if (pendingRequest != 0 || nowMs < nextAt || requestId == 0) return null;
            pendingRequest = requestId;
            pendingNonce = unchecked(((ulong)nowMs << 32) | requestId);
            if (pendingNonce == 0) pendingNonce = 1;
            sentAt = nowMs;
            nextAt = nowMs + IntervalMs;
            return new WireMessage(MessageType.Ping, requestId, LobbyPayloads.Ping(pendingNonce));
        }

        internal void SendFailed(uint requestId)
        {
            if (pendingRequest != requestId) return;
            pendingRequest = 0;
            Record(false, null);
        }

        internal bool Receive(WireMessage message, long nowMs)
        {
            if (message.Type != MessageType.Pong) return false;
            if (pendingRequest == 0 || message.RequestId != pendingRequest) return true;
            if (LobbyPayloads.ReadPing(message.Payload) != pendingNonce) return true;
            int roundTrip = (int)Math.Min(int.MaxValue, Math.Max(0, nowMs - sentAt));
            pendingRequest = 0;
            Record(true, roundTrip);
            return true;
        }

        private void Record(bool replied, int? roundTrip)
        {
            replies.Enqueue(replied);
            while (replies.Count > 20) replies.Dequeue();
            if (roundTrip != null)
            {
                roundTrips.Enqueue(roundTrip.Value);
                while (roundTrips.Count > 8) roundTrips.Dequeue();
            }
        }
    }
}
