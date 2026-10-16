using System;
using HarmonyLib;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SYNC-001 (ADR 0024, DET-001 finding of 2026-10-16): RimWorld 1.6 ticks pawns at a rate that depends on whether they
    /// are visible on this player's screen (<c>Pawn.UpdateRateTicks</c>, <c>Notify_BecameVisible</c>/<c>Invisible</c>).
    /// Every player looks at a different place, so the same pawn ran its interval work (needs, inspirations, …) at
    /// different ticks: the self-test found an extra inspiration check in one game after 17 754 ticks. In a lockstep
    /// session every pawn gets the same rate on every player, its minimum interval (as if always visible).
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.UpdateRateTicks), MethodType.Getter)]
    internal static class LockstepTickRatePatch
    {
        /// <summary>Self-test switch (-mpLockstepVisibleTickRates): keep vanilla's view-dependent rates, to show the drift.</summary>
        internal static bool Disabled;
        internal static int Forced;
        internal static readonly Func<Thing, int> MinRate = AccessTools.MethodDelegate<Func<Thing, int>>(AccessTools.PropertyGetter(typeof(Thing), "MinTickIntervalRate"));
        internal static readonly Func<Thing, int> MaxRate = AccessTools.MethodDelegate<Func<Thing, int>>(AccessTools.PropertyGetter(typeof(Thing), "MaxTickIntervalRate"));

        private static void Postfix(Pawn __instance, ref int __result)
        {
            if (Disabled || FoundationMod.Instance?.Lockstep == null) return;
            int rate = MinRate(__instance);
            if (__result != rate) Forced++;
            __result = rate;
        }
    }
}
