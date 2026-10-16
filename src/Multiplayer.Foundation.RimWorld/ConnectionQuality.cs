using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Local connection telemetry. It never changes replicated game state.</summary>
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
        private byte speedCap = 3;
        private int recoverySamples;

        internal byte SpeedCap => speedCap;
        internal int? PingMs => roundTrips.Count == 0 ? (int?)null : (int)Math.Round(roundTrips.Average());
        internal int? MissingPercent => replies.Count == 0 ? (int?)null :
            100 * replies.Count(reply => !reply) / replies.Count;

        internal string Display => "Ping: " + (PingMs?.ToString() ?? "…") + " ms · Ausfälle: " +
            (MissingPercent?.ToString() ?? "…") + " %" +
            (speedCap < 3 ? " · max. " + speedCap + "×" : "");

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
            int missed = replies.Count(reply => !reply);
            int? ping = PingMs;
            byte desired = missed >= 2 || ping >= 350 ? (byte)1 :
                missed >= 1 || ping >= 180 ? (byte)2 : (byte)3;
            if (desired < speedCap) { speedCap = desired; recoverySamples = 0; }
            else if (desired > speedCap && replied && ++recoverySamples >= 3)
            { speedCap++; recoverySamples = 0; }
            else if (desired == speedCap) recoverySamples = 0;
        }
    }
}
