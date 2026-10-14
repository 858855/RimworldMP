// Harmony2/1.6 instructional demonstration; NOT BUILT OR TESTED.
// Verify Verse.TickManager.TickManagerUpdate in the installed game assembly.
using HarmonyLib;
using Verse;

namespace CodexResearch.HarmonyExample
{
    [HarmonyPatch(typeof(TickManager), "TickManagerUpdate")]
    internal static class TickManagerObservationalPatch
    {
        // Keep the original function running: NEVER return false here.
        private static void Prefix() { /* no game state change */ }
        private static void Postfix() { /* no game state change */ }
    }

    [StaticConstructorOnStartup]
    internal static class RegisterExampleHarmony
    {
        static RegisterExampleHarmony()
        {
            new Harmony("com.codexresearch.rimworld.observer").PatchAll();
        }
    }
}
