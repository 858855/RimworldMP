using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Multiplayer.Foundation.RimWorld
{
    // SYNC-002 (ADR 0025): the entry points of shared player actions. Each prefix hands a player's call to
    // LockstepCommands.Capture and then skips the original; the action runs for everyone when the command comes back.
    // Skipping is intended (AGENTS.md rule 12). Calls from the simulation, long events and running commands pass through.

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    internal static class CaptureDraftPatch
    {
        private static bool Prefix(Pawn_DraftController __instance, bool value) => __instance.Drafted == value ||
            !LockstepCommands.Capture(LockstepCommands.Draft, w => { w.WriteThing(__instance.pawn); w.WriteBool(value); });
    }

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.FireAtWill), MethodType.Setter)]
    internal static class CaptureFireAtWillPatch
    {
        private static bool Prefix(Pawn_DraftController __instance, bool value) => __instance.FireAtWill == value ||
            !LockstepCommands.Capture(LockstepCommands.FireAtWill, w => { w.WriteThing(__instance.pawn); w.WriteBool(value); });
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    internal static class CaptureOrderedJobPatch
    {
        private static bool Prefix(Pawn_JobTracker __instance, Job job, JobTag? tag, bool requestQueueing, ref bool __result)
        {
            if (job == null || !LockstepCommands.CaptureOrder(LockstepCommands.JobsPawn(__instance), job, tag, requestQueueing)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompForbiddable), nameof(CompForbiddable.Forbidden), MethodType.Setter)]
    internal static class CaptureForbidPatch
    {
        private static bool Prefix(CompForbiddable __instance, bool value) => __instance.Forbidden == value ||
            !LockstepCommands.Capture(LockstepCommands.Forbid, w => { w.WriteThing(__instance.parent); w.WriteBool(value); });
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority))]
    internal static class CaptureWorkPriorityPatch
    {
        private static bool Prefix(Pawn_WorkSettings __instance, WorkTypeDef w, int priority)
        {
            if (!LockstepCommands.InPlayerContext || w == null || __instance.GetPriority(w) == priority) return true;
            Pawn pawn = LockstepCommands.WorkPawn(__instance);
            return !LockstepCommands.Capture(LockstepCommands.WorkPriority, writer => { writer.WriteThing(pawn); writer.WriteDef(w); writer.WriteInt(priority); });
        }
    }

    [HarmonyPatch(typeof(Pawn_TimetableTracker), nameof(Pawn_TimetableTracker.SetAssignment))]
    internal static class CaptureSchedulePatch
    {
        private static bool Prefix(Pawn_TimetableTracker __instance, int hour, TimeAssignmentDef ta)
        {
            if (!LockstepCommands.InPlayerContext || ta == null || __instance.GetAssignment(hour) == ta) return true;
            Pawn pawn = LockstepCommands.TimetablePawn(__instance);
            return !LockstepCommands.Capture(LockstepCommands.Schedule, w => { w.WriteThing(pawn); w.WriteInt(hour); w.WriteDef(ta); });
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject))]
    internal static class CaptureResearchPatch
    {
        private static bool Prefix(ResearchProjectDef proj) =>
            !LockstepCommands.Capture(LockstepCommands.Research, w => { w.WriteBool(true); w.WriteDef(proj); });
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.StopProject))]
    internal static class CaptureStopResearchPatch
    {
        private static bool Prefix(ResearchProjectDef proj) =>
            !LockstepCommands.Capture(LockstepCommands.Research, w => { w.WriteBool(false); w.WriteDef(proj); });
    }

    [HarmonyPatch(typeof(Zone_Growing), nameof(Zone_Growing.SetPlantDefToGrow))]
    internal static class CaptureGrowingPlantPatch
    {
        private static bool Prefix(Zone_Growing __instance, ThingDef plantDef) => __instance.GetPlantDefToGrow() == plantDef ||
            !LockstepCommands.Capture(LockstepCommands.GrowingPlant, w => { w.WriteZone(__instance); w.WriteDef(plantDef); });
    }

    /// <summary>
    /// Every DesignateSingleCell, DesignateMultiCell and DesignateThing that RimWorld's designators declare (an override
    /// does not call the patched base method, so each one is patched). Designators of other mods are not patched yet.
    /// </summary>
    [HarmonyPatch]
    internal static class CaptureDesignatorPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var signatures = new (string Name, Type Parameter)[]
            {
                (nameof(Designator.DesignateSingleCell), typeof(IntVec3)),
                (nameof(Designator.DesignateMultiCell), typeof(IEnumerable<IntVec3>)),
                (nameof(Designator.DesignateThing), typeof(Thing))
            };
            foreach (Type type in typeof(Designator).Assembly.GetTypes().Where(t => typeof(Designator).IsAssignableFrom(t)))
                foreach (var (name, parameter) in signatures)
                {
                    MethodInfo? method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                        new[] { parameter }, null);
                    if (method != null && !method.IsAbstract) yield return method;
                }
        }

        private static bool Prefix(Designator __instance, MethodBase __originalMethod, object[] __args) =>
            !LockstepCommands.CaptureDesignation(__instance, __originalMethod.Name, __args[0]);
    }

    /// <summary>
    /// SYNC-002 (ADR 0025): objects the player's side creates in a session (the job of a right-click menu before it is
    /// sent, a bill being set up) take local IDs, so the game's shared ID counters stay equal on every player. Objects
    /// created by the simulation, by long events and by running commands take shared IDs as before.
    /// </summary>
    [HarmonyPatch(typeof(UniqueIDsManager), "GetNextID")]
    internal static class LocalIdsPatch
    {
        private static int nextLocalId = -1_000_000;
        internal static int Given;

        private static bool Prefix(ref int __result)
        {
            if (!LockstepCommands.InPlayerContext) return true;
            __result = nextLocalId--;
            Given++;
            return false;
        }
    }
}
