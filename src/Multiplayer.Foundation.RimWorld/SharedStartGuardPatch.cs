using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-001: during a shared setup the host's final "Start" on the character page must not start a
    /// single-player game. The shared start (game transfer to every player) comes with SETUP-005.
    /// Skips the original only while a shared setup runs; normal single-player starts are untouched.
    /// </summary>
    [HarmonyPatch(typeof(Page_ConfigureStartingPawns), "DoNext")]
    internal static class SharedStartGuardPatch
    {
        private static bool Prefix()
        {
            if (FoundationMod.Instance?.HostSetupActive != true) return true;
            Messages.Message(FoundationMod.Instance.StartGuardText(), MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }
}
