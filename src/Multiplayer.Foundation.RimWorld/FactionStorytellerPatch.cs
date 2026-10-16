using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Temporary context for the one storyteller currently being ticked on the game thread.</summary>
    internal static class FactionStorytellerScope
    {
        [ThreadStatic] private static Storyteller? current;
        [ThreadStatic] private static Faction? faction;

        internal static Storyteller? Current => current;

        internal static void Run(Storyteller storyteller, Faction owner)
        {
            if (current != null) throw new InvalidOperationException("Nested faction storyteller tick.");
            current = storyteller;
            faction = owner;
            try { storyteller.StorytellerTick(); }
            finally { faction = null; current = null; }
        }

        internal static bool Owns(IIncidentTarget? target)
        {
            if (target == null || faction == null) return false;
            // Global/world targets have no unambiguous owner (ADR 0037 R4).
            if (target is Map map) return map.ParentFaction == faction;
            if (target is WorldObject worldObject) return worldObject.Faction == faction;
            return false;
        }
    }

    /// <summary>Replace the single vanilla tick only for a complete faction game.</summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.StorytellerTick))]
    internal static class FactionStorytellerTickPatch
    {
        private static bool Prefix(Storyteller __instance)
        {
            if (FactionStorytellerScope.Current != null) return true;
            MultiplayerSession? session = MultiplayerSession.ForCurrentGame;
            if (session?.Active != true || session.Mode?.Mode != GameMode.Factions ||
                !ReferenceEquals(__instance, Current.Game?.storyteller)) return true;
            foreach ((Storyteller storyteller, Faction owner) in session.LiveFactionStorytellers())
                FactionStorytellerScope.Run(storyteller, owner);
            return false;
        }
    }

    /// <summary>Vanilla's delayed incident queue resolves its owner through Find.Storyteller.</summary>
    [HarmonyPatch(typeof(Find), nameof(Find.Storyteller), MethodType.Getter)]
    internal static class FactionStorytellerLookupPatch
    {
        private static void Postfix(ref Storyteller __result)
        {
            if (FactionStorytellerScope.Current is Storyteller scoped) __result = scoped;
        }
    }

    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.AllIncidentTargets), MethodType.Getter)]
    internal static class FactionIncidentTargetsPatch
    {
        private static void Postfix(ref List<IIncidentTarget> __result)
        {
            if (FactionStorytellerScope.Current != null)
                __result = __result.Where(FactionStorytellerScope.Owns).ToList();
        }
    }

    // The 1.6 interval iterator also visits every global quest part for every storyteller.
    // Until quests are assigned to factions, none may schedule a repeated faction incident.
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.QuestsListForReading), MethodType.Getter)]
    internal static class FactionQuestIncidentSourcePatch
    {
        private static void Postfix(ref List<Quest> __result)
        {
            if (FactionStorytellerScope.Current != null) __result = new List<Quest>();
        }
    }

    // Even if an incident obtains its target without AllIncidentTargets, it may not cross a faction boundary.
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire), new[] { typeof(FiringIncident), typeof(bool) })]
    internal static class FactionIncidentFirePatch
    {
        private static bool Prefix(FiringIncident fi)
        {
            if (FactionStorytellerScope.Current == null) return true;
            IIncidentTarget? target = fi?.parms?.target;
            if (FactionStorytellerScope.Owns(target)) return true;
            if (target is Map || target is WorldObject)
                throw new InvalidOperationException("Fraktionsereignis würde eine fremde Karte oder ein fremdes Weltobjekt treffen.");
            // World-wide incidents have no safe owner until ADR 0037 R4 is decided.
            return false;
        }
    }
}
