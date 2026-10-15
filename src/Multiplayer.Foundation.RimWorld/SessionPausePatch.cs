using HarmonyLib;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-005: until lockstep exists (SYNC-001) the shared game stays paused for everyone (plan §7.7). Before each
    /// frame's ticks the game is set back to paused; asking for another speed or toggling pause only shows why.
    /// Resetting the speed alone is not enough: inside the update the game sometimes runs again (self-test 2026-10-15:
    /// 50 of 239 frames), so <see cref="SessionPauseTickPatch"/> stops the tick itself.
    /// Only while a multiplayer session runs; single-player games are untouched.
    /// </summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickManagerUpdate))]
    internal static class SessionPauseUpdatePatch
    {
        // Counted for the self-test: how often the check ran, how often it had to pause again, and how often the
        // game was running again right after the update.
        internal static int Calls, Resets, RunningAfter;

        private static void Prefix(TickManager __instance)
        {
            Calls++;
            if (FoundationMod.Instance?.SessionPaused != true || __instance.Paused) return;
            Resets++;
            __instance.CurTimeSpeed = TimeSpeed.Paused;
        }

        private static void Postfix(TickManager __instance)
        {
            if (FoundationMod.Instance?.SessionPaused == true && !__instance.Paused) RunningAfter++;
        }
    }

    /// <summary>The hard stop: no game tick runs while the session is paused, whatever set the speed.</summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    internal static class SessionPauseTickPatch
    {
        internal static int Blocked;

        private static bool Prefix()
        {
            if (FoundationMod.Instance?.SessionPaused != true) return true;
            Blocked++;
            return false;
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.CurTimeSpeed), MethodType.Setter)]
    internal static class SessionPauseSpeedPatch
    {
        private static void Prefix(ref TimeSpeed value)
        {
            if (value == TimeSpeed.Paused || FoundationMod.Instance?.SessionPaused != true) return;
            value = TimeSpeed.Paused;
            FoundationMod.Instance.NotifySessionPaused();
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TogglePaused))]
    internal static class SessionPauseTogglePatch
    {
        private static bool Prefix()
        {
            if (FoundationMod.Instance?.SessionPaused != true) return true;
            FoundationMod.Instance.NotifySessionPaused();
            return false;
        }
    }
}
