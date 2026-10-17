using System;
using HarmonyLib;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Isolates gameplay random draws from frame/UI draws in RimWorld 1.6.</summary>
    internal sealed class SessionRandom
    {
        [ThreadStatic] private static SessionRandom? active;

        /// <summary>True inside a lockstep tick or a scheduled command on this thread (see the Session*Patch classes).</summary>
        internal static bool Active => active != null;
        private readonly Guid sessionId;
        private ulong draws, valueSum, windowDraws, windowValueSum;

        internal SessionRandom(Guid sessionId) { this.sessionId = sessionId; }

        internal void BeginTick(int nextTick) => Begin(Seed(sessionId, nextTick, 0, false));
        internal void BeginCommand(int boundary, ulong sequence) => Begin(Seed(sessionId, boundary, sequence, true));

        private void Begin(int seed)
        {
            if (active != null) throw new InvalidOperationException("Nested multiplayer Rand scope.");
            Rand.PushState(seed);
            draws = 0; valueSum = 0; active = this;
        }

        internal void End()
        {
            if (active != this) throw new InvalidOperationException("Multiplayer Rand scope mismatch.");
            active = null;
            unchecked { windowDraws += draws; windowValueSum += valueSum; }
            Rand.PopState();
        }

        internal (ulong Draws, ulong ValueSum) TakeWindow()
        {
            var value = (windowDraws, windowValueSum);
            windowDraws = 0; windowValueSum = 0;
            return value;
        }

        internal static void RecordInt(int value)
        {
            // Draws inside an isolated visual/sound method run on their own Rand state and must not enter the hash window.
            if (active == null || SessionVisualRandPatch.Depth > 0) return;
            unchecked { active.draws++; active.valueSum += (uint)value; }
        }

        internal static void RecordFloat(float value) => RecordInt(BitConverter.SingleToInt32Bits(value));

        private static int Seed(Guid session, int tick, ulong sequence, bool command)
        {
            // FNV-1a over explicit bytes: no runtime-dependent string or object hash codes.
            uint hash = 2166136261;
            void Add(byte value) { unchecked { hash = (hash ^ value) * 16777619; } }
            foreach (byte value in session.ToByteArray()) Add(value);
            for (int i = 0; i < 4; i++) Add((byte)(tick >> (i * 8)));
            Add(command ? (byte)1 : (byte)0);
            if (command) for (int i = 0; i < 8; i++) Add((byte)(sequence >> (i * 8)));
            return unchecked((int)hash);
        }
    }

    // Local 1.6 IL inspection: Rand.Value and Rand.Int each advance Rand.iterations once;
    // Bool/Range/Chance delegate to them. Only draws inside a session scope enter the window.
    [HarmonyPatch(typeof(Rand), nameof(Rand.Value), MethodType.Getter)]
    internal static class SessionRandValuePatch
    {
        private static void Postfix(float __result) => SessionRandom.RecordFloat(__result);
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Int), MethodType.Getter)]
    internal static class SessionRandIntPatch
    {
        private static void Postfix(int __result) => SessionRandom.RecordInt(__result);
    }
}
