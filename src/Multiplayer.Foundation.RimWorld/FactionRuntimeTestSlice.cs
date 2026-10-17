using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// Checks of the opt-in faction self-test (ADR 0047). The generation itself moved to <see cref="FactionGeneration"/>,
    /// which the self-test and the host start path share (ADR 0028).
    /// </summary>
    internal static class FactionRuntimeTestSlice
    {
        internal static bool CheckCurrentGame(MultiplayerSession session, bool ignoreAnomaly = false) => Diagnostic(session, ignoreAnomaly) == null;

        internal static string? Diagnostic(MultiplayerSession session, bool ignoreAnomaly = false)
        {
            if (session.Mode?.Mode != GameMode.Factions) return "mode";
            if (session.FactionStorytellerError(ignoreAnomaly) is string error) return error;
            var entries = session.FactionSetupInSlotOrder.ToList();
            if (entries.Count != 2 || entries[0].Slot != 0 || entries[1].Slot != 1 ||
                entries[0].FactionId == entries[1].FactionId || entries[0].Storyteller == entries[1].Storyteller ||
                Find.Maps.Count < 2) return "count/order/storyteller/map count";
            if (ModsConfig.IdeologyActive)
            {
                Faction? first = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.loadID == entries[0].FactionId);
                Faction? second = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.loadID == entries[1].FactionId);
                if (first?.ideos?.PrimaryIdeo == null) return "first ideology missing";
                if (second?.ideos?.PrimaryIdeo == null) return "second ideology missing";
                if (ReferenceEquals(first.ideos.PrimaryIdeo, second.ideos.PrimaryIdeo)) return "shared ideology";
            }
            foreach (var entry in entries)
            {
                Faction? faction = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.loadID == entry.FactionId);
                if (faction?.def?.isPlayer != true) return "missing faction slot " + entry.Slot;
                if (!Find.Maps.Any(map => map.ParentFaction == faction && SiteFlags.ToSiteTile(map.Tile) == entry.Site))
                    return "missing map slot " + entry.Slot;
                if (!Find.Maps.Any(map => map.ParentFaction == faction &&
                    map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.Faction == faction && pawn.RaceProps.Humanlike)))
                    return "missing pawn slot " + entry.Slot;
                if (ModsConfig.IdeologyActive && !Find.Maps.Any(map => map.ParentFaction == faction &&
                    map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.Faction == faction && pawn.RaceProps.Humanlike &&
                        pawn.ideo?.Ideo == faction.ideos.PrimaryIdeo))) return "wrong pawn ideology slot " + entry.Slot;
            }
            return null;
        }
    }
}
