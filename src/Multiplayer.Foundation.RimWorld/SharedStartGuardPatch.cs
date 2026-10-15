using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-004/005: during a shared setup the host's final "Start" on the character page only goes through when
    /// every player is done. Vanilla's own checks (and their dialogs) then run as usual and end in
    /// <see cref="PageUtility.InitGameStart"/>, where <see cref="SharedGameStartPatch"/> takes over.
    /// Normal single-player starts are untouched.
    /// </summary>
    [HarmonyPatch(typeof(Page_ConfigureStartingPawns), "DoNext")]
    internal static class SharedStartGuardPatch
    {
        private static bool Prefix()
        {
            if (FoundationMod.Instance?.HostSetupActive != true) return true;
            string? blocked = FoundationMod.Instance.StartGuardText();
            if (blocked == null) return true;
            Messages.Message(blocked, MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }

    /// <summary>
    /// SETUP-005: the vanilla game start waits for the server. The first call asks the server to move to
    /// Generating; once it has, the mod calls <see cref="PageUtility.InitGameStart"/> again and lets it run.
    /// </summary>
    [HarmonyPatch(typeof(PageUtility), nameof(PageUtility.InitGameStart))]
    internal static class SharedGameStartPatch
    {
        private static bool Prefix()
        {
            if (FoundationMod.Instance?.HostSetupActive != true) return true;
            return FoundationMod.Instance.TryBeginSharedStart();
        }
    }
}
