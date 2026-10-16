using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// Time in a multiplayer session (single-player games are untouched).
    /// - SETUP-005: while the shared game starts and loads (no lockstep yet), it stays paused for everyone. Before each
    ///   frame's ticks the game is set back to paused; asking for another speed or toggling pause only shows why.
    ///   Resetting the speed alone is not enough: inside the update the game sometimes runs again (self-test 2026-10-15:
    ///   50 of 239 frames), so <see cref="SessionPauseTickPatch"/> stops the tick itself.
    /// - SYNC-001 (ADR 0024): once the lockstep driver runs, it runs the ticks from this prefix. Vanilla's update still
    ///   runs afterwards but cannot tick; skipping its ticks is intended (AGENTS.md rule 12).
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
            FoundationMod? mod = FoundationMod.Instance;
            LockstepDriver? driver = mod?.Lockstep;
            if (driver != null) { driver.Update(__instance); return; }
            if (mod?.SessionPaused != true || __instance.Paused) return;
            Resets++;
            __instance.CurTimeSpeed = TimeSpeed.Paused;
        }

        private static void Postfix(TickManager __instance)
        {
            if (FoundationMod.Instance?.SessionPaused == true && !__instance.Paused) RunningAfter++;
        }
    }

    /// <summary>The hard stop: in a session no game tick runs unless the lockstep driver runs it.</summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    internal static class SessionPauseTickPatch
    {
        internal static int Blocked;

        private static bool Prefix()
        {
            if (LockstepDriver.Ticking) return true;
            FoundationMod? mod = FoundationMod.Instance;
            if (mod?.Lockstep == null && mod?.SessionPaused != true) return true;
            Blocked++;
            return false;
        }
    }

    /// <summary>In lockstep the speed changes only when the server's command runs; a local change becomes a request.</summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.CurTimeSpeed), MethodType.Setter)]
    internal static class SessionPauseSpeedPatch
    {
        private static bool Prefix(ref TimeSpeed value)
        {
            if (LockstepDriver.ApplyingSpeed) return true;
            FoundationMod? mod = FoundationMod.Instance;
            LockstepDriver? driver = mod?.Lockstep;
            if (driver != null) { driver.RequestSpeed(value); return false; }
            if (value == TimeSpeed.Paused || mod?.SessionPaused != true) return true;
            value = TimeSpeed.Paused;
            mod.NotifySessionPaused();
            return true;
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TogglePaused))]
    internal static class SessionPauseTogglePatch
    {
        private static bool Prefix()
        {
            FoundationMod? mod = FoundationMod.Instance;
            LockstepDriver? driver = mod?.Lockstep;
            if (driver != null) { driver.RequestTogglePause(); return false; }
            if (mod?.SessionPaused != true) return true;
            mod.NotifySessionPaused();
            return false;
        }
    }

    /// <summary>SYNC-001: vanilla's auto-pause (letters, threats) pauses the session for everyone.</summary>
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.Pause))]
    internal static class SessionPausePausePatch
    {
        private static bool Prefix()
        {
            LockstepDriver? driver = FoundationMod.Instance?.Lockstep;
            if (driver == null) return true;
            driver.RequestSpeed(TimeSpeed.Paused);
            return false;
        }
    }

    /// <summary>
    /// SYNC-001: no local autosave in lockstep. Each player has an own interval, and coordinated saves come with SAVE-001.
    /// Saving by hand stays possible.
    /// </summary>
    [HarmonyPatch(typeof(Autosaver), nameof(Autosaver.AutosaverTick))]
    internal static class SessionAutosavePatch
    {
        internal static int Skipped;

        private static bool Prefix()
        {
            if (FoundationMod.Instance?.Lockstep == null) return true;
            Skipped++;
            return false;
        }
    }
}
