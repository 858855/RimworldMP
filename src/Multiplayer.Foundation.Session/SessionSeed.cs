using System.Text;

namespace Multiplayer.Foundation.Session
{
    /// <summary>
    /// SYNC-001 (ADR 0024, plan P-6): the random seed every player uses for a tick and for a command. It depends only on
    /// the session and the tick (and the command's sequence number), never on the machine, the time or earlier draws.
    /// </summary>
    public static class SessionSeed
    {
        /// <summary>Stable 64-bit value of the session ID saved with the game (FNV-1a over UTF-8).</summary>
        public static ulong FromSessionId(string sessionId)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte value in Encoding.UTF8.GetBytes(sessionId ?? ""))
            { hash ^= value; hash *= 1099511628211UL; }
            return hash;
        }

        public static int ForTick(ulong session, int tick) => unchecked((int)Mix(session ^ ((ulong)(uint)tick * 0x9E3779B97F4A7C15UL)));

        public static int ForCommand(ulong session, int tick, uint seq) =>
            unchecked((int)Mix(Mix(session + (ulong)(uint)tick) ^ ((ulong)seq * 0xC2B2AE3D27D4EB4FUL)));

        // SplitMix64 finalizer: spreads every input bit over the whole result.
        internal static ulong Mix(ulong value)
        {
            unchecked
            {
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }
    }
}
