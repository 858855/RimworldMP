using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    // Runs immediately before RimWorld lays out its native main-menu option list.
    [HarmonyPatch(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing))]
    internal static class MainMenuOptionPatch
    {
        private static void Prefix(List<ListableOption> __1)
        {
            if (Current.ProgramState != ProgramState.Entry || __1.Count < 2) return;
            string newColony = Translator.Translate("NewColony");
            if (__1[1].label != newColony || FoundationMod.Instance == null) return;
            __1.Insert(1, new ListableOption("Multiplayer", FoundationMod.Instance.OpenWindow, null));
        }
    }
}
