using HarmonyLib;
using RimWorld;
using Verse;

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
}
