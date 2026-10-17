using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// FACTION-005 (ADR 0028): vanilla recomputes a player faction's primary ideology from every "colonist", and a colonist
    /// is any humanlike of any faction with <c>def.isPlayer</c>. With a second player faction the host's majority ideology
    /// therefore overwrote the second faction's own one right after the landing (self-test 2026-10-17: "shared ideology").
    /// With more than one player faction each faction counts only its own pawns; with one, vanilla runs unchanged.
    /// </summary>
    [HarmonyPatch(typeof(FactionIdeosTracker), nameof(FactionIdeosTracker.RecalculateIdeosBasedOnPlayerPawns))]
    internal static class FactionIdeosRecalculatePatch
    {
        private static readonly FieldInfo? FactionField = AccessTools.Field(typeof(FactionIdeosTracker), "faction");
        internal static int Scoped;

        private static bool Prefix(FactionIdeosTracker __instance)
        {
            if (FactionField == null || Current.Game?.World?.factionManager == null) return true;
            if (Find.FactionManager.AllFactionsListForReading.Count(f => f.def?.isPlayer == true) < 2) return true;
            if (FactionField.GetValue(__instance) is not Faction faction) return true;
            var counts = new Dictionary<Ideo, int>();
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
            {
                if (pawn.Faction != faction || !pawn.RaceProps.Humanlike || pawn.ideo?.Ideo == null) continue;
                counts[pawn.ideo.Ideo] = counts.TryGetValue(pawn.ideo.Ideo, out int count) ? count + 1 : 1;
            }
            Scoped++;
            if (counts.Count == 0) return false;   // no pawn yet: keep what the generation set
            // Deterministic: most pawns, then the lower ideology id.
            Ideo best = counts.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key.id).First().Key;
            if (!ReferenceEquals(__instance.PrimaryIdeo, best)) __instance.SetPrimary(best);
            return false;
        }
    }
}
