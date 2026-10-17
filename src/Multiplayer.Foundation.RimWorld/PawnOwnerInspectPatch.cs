using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// OWN-001, first step: the inspect pane of a player pawn names the player who controls it (Coop: the saved owner or
    /// "gemeinsame Figur"; Factions: the player of its faction). Display only, nothing in this patch changes game state.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    internal static class PawnOwnerInspectPatch
    {
        private static void Postfix(Pawn __instance, ref string __result)
        {
            MultiplayerSession? session = MultiplayerSession.ForCurrentGame;
            if (session == null || !session.Active || __instance.Faction?.IsPlayer != true) return;
            string? owner = session.Mode?.Mode == GameMode.Factions
                ? session.FactionOwnerNameOf(__instance.Faction.loadID)
                : session.OwnerNameOf(__instance.thingIDNumber);
            string line = owner == null ? "Multiplayer: gemeinsame Figur" : "Multiplayer: Figur von " + owner;
            __result = string.IsNullOrEmpty(__result) ? line : __result + "\n" + line;
        }
    }
}
