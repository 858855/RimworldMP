using System;

namespace Multiplayer.Foundation.Session
{
    /// <summary>
    /// SYNC-001 (ADR 0024): how many ticks a player's game runs in one frame. It runs at the session's pace and spreads the
    /// server's 50 ms horizon steps over the frames. When it falls behind by more than a cushion it runs faster, so the
    /// extra lag halves about every <see cref="CatchUpSeconds"/>. It never runs past the horizon and saves no time while
    /// it waits for one.
    /// </summary>
    public sealed class TickPacer
    {
        /// <summary>Lag that counts as normal: the horizon moves in steps of about 50 ms.</summary>
        public const double CushionSeconds = 0.1;
        public const double CatchUpSeconds = 0.5;
        /// <summary>A longer frame (a hitch, loading) counts only this much; catching up covers the rest.</summary>
        public const double MaximumFrameSeconds = 0.25;

        private double carry;

        /// <param name="frameSeconds">Real time since the last frame.</param>
        /// <param name="behind">Ticks the game may still run (horizon minus its tick).</param>
        /// <param name="ticksPerSecond">The session's pace; 0 while paused. A game behind a paused horizon catches up at the normal pace.</param>
        public int Next(double frameSeconds, int behind, int ticksPerSecond)
        {
            if (behind <= 0) { carry = 0; return 0; }
            double frame = Math.Max(0, Math.Min(MaximumFrameSeconds, frameSeconds));
            double pace = ticksPerSecond > 0 ? ticksPerSecond : SessionSpeeds.NormalTicksPerSecond;
            double extra = Math.Max(0, behind - pace * CushionSeconds) / CatchUpSeconds;
            carry += frame * (pace + extra);
            int ticks = (int)Math.Min(Math.Floor(carry), behind);
            carry -= ticks;
            if (ticks == behind) carry = Math.Min(carry, 1);
            return ticks;
        }
    }
}
