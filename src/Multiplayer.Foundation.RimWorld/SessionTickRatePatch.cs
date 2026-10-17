using System;
using HarmonyLib;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// DET-001 finding of 2026-10-16 (ported from feat/sync-lockstep 5f1b782, ADR 0024): RimWorld 1.6 ticks pawns at a
    /// rate that depends on whether they are visible on this player's screen (<c>Pawn.UpdateRateTicks</c>,
    /// <c>Notify_BecameVisible</c>/<c>Invisible</c>). Every player looks at a different place, so the same pawn ran its
    /// interval work (needs, inspirations, …) at different ticks: the self-test on that line found an extra inspiration
    /// check in one game after 17 754 ticks. In a shared game every pawn gets the same rate on every player, its minimum
    /// interval (as if always visible).
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.UpdateRateTicks), MethodType.Getter)]
    internal static class SessionTickRatePatch
    {
        internal static int Forced;
        internal static readonly Func<Thing, int> MinRate = AccessTools.MethodDelegate<Func<Thing, int>>(AccessTools.PropertyGetter(typeof(Thing), "MinTickIntervalRate"));

        private static void Postfix(Pawn __instance, ref int __result)
        {
            if (FoundationMod.Instance?.InSharedGame != true) return;
            int rate = MinRate(__instance);
            if (__result != rate) Forced++;
            __result = rate;
        }
    }
}
