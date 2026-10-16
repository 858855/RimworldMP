using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Player GUI draft toggles enter the shared command stream instead of mutating this game early.</summary>
    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    internal static class SessionDraftPatch
    {
        private static bool Prefix(Pawn_DraftController __instance, bool value) =>
            FoundationMod.Instance?.CaptureDraft(__instance.pawn, value) ?? true;
    }

    /// <summary>Forbid/Unforbid from a player's GUI enters the same ordered command stream.</summary>
    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.SetForbidden),
        new[] { typeof(Thing), typeof(bool), typeof(bool) })]
    internal static class SessionForbidPatch
    {
        private static bool Prefix(Thing t, bool value) =>
            FoundationMod.Instance?.CaptureForbidden(t, value) ?? true;
    }

    /// <summary>Only a simple Goto order is encoded yet. Other UI orders stay blocked in shared games.</summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob),
        new[] { typeof(Job), typeof(JobTag?), typeof(bool) })]
    internal static class SessionOrderedJobPatch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnOf =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        private static bool Prefix(Pawn_JobTracker __instance, Job job, JobTag? tag,
            bool requestQueueing, ref bool __result)
        {
            FoundationMod? mod = FoundationMod.Instance;
            if (mod == null) return true;
            bool runOriginal = mod.CaptureOrderedJob(PawnOf(__instance), job, tag, requestQueueing, out bool accepted);
            if (!runOriginal) __result = accepted;
            return runOriginal;
        }
    }
}
