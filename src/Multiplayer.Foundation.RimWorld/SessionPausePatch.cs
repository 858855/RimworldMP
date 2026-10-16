using HarmonyLib;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Frames process commands even while paused; vanilla still owns tick timing.</summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickManagerUpdate))]
    internal static class SessionPauseUpdatePatch
    {
        // Kept for the existing game self-test's pause probe.
        internal static int Calls, Resets, RunningAfter;

        private static void Prefix(TickManager __instance)
        {
            Calls++;
            if (FoundationMod.Instance?.PrepareSessionFrame(__instance) == true) Resets++;
        }

        private static void Postfix(TickManager __instance)
        {
            FoundationMod.Instance?.RestoreSessionSpeed(__instance);
            if (FoundationMod.Instance?.SessionPaused == true && !__instance.Paused) RunningAfter++;
        }
    }

    /// <summary>Hard boundary: no tick runs before its commands and coordinator horizon.</summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    internal static class SessionPauseTickPatch
    {
        internal static int Blocked;

        private static bool Prefix(TickManager __instance, out bool __state)
        {
            __state = FoundationMod.Instance?.MayRunSessionTick(__instance) ?? true;
            if (!__state) Blocked++;
            return __state;
        }

        private static void Postfix(TickManager __instance, bool __state)
        {
            if (__state) FoundationMod.Instance?.AfterSessionTick(__instance);
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.CurTimeSpeed), MethodType.Setter)]
    internal static class SessionPauseSpeedPatch
    {
        private static void Prefix(ref TimeSpeed value)
        {
            FoundationMod.Instance?.InterceptSessionSpeed(ref value);
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TogglePaused))]
    internal static class SessionPauseTogglePatch
    {
        private static bool Prefix()
        {
            return FoundationMod.Instance?.InterceptSessionToggle() ?? true;
        }
    }
}
