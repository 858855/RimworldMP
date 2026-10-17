using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.Sound;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// DET-001 finding of 2026-10-16 (ported from feat/sync-lockstep 5f1b782, ADR 0024): some tick code makes visual or
    /// sound effects only, and only on some players (it depends on their view and settings), but draws from the game's
    /// random stream. One extra draw shifts every later draw of that tick, and the games drift apart minutes later
    /// (self-test on that line: wild animals wandering differently). During lockstep ticks and commands these methods run
    /// inside their own <c>Rand.PushState()</c>/<c>PopState()</c>: they still get random numbers, but the game's stream
    /// is the same afterwards whatever they did, and their draws stay out of the state hash (<see cref="SessionRandom"/>).
    /// Only methods whose effects are purely visual or audible belong here.
    /// </summary>
    [HarmonyPatch]
    internal static class SessionVisualRandPatch
    {
        internal static int Isolated;
        /// <summary>Inside an isolated method: its draws do not count for the game (the Rand window skips them).</summary>
        internal static int Depth;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            // 1.6 fish shadows in water: flecks only
            yield return AccessTools.Method(typeof(FishShadowComponent), nameof(FishShadowComponent.MapComponentTick));
            // sounds: played or not depending on the player's camera and settings; grain, volume and pitch are random
            yield return AccessTools.Method(typeof(SoundStarter), nameof(SoundStarter.PlayOneShot));
            yield return AccessTools.Method(typeof(SoundStarter), nameof(SoundStarter.PlayOneShotOnCamera));
            yield return AccessTools.Method(typeof(SoundStarter), nameof(SoundStarter.TrySpawnSustainer));
            yield return AccessTools.Method(typeof(SubSustainer), "StartSample");
        }

        private static void Prefix(out bool __state)
        {
            __state = SessionRandom.Active;
            if (!__state) return;
            Rand.PushState();
            Isolated++; Depth++;
        }

        private static Exception? Finalizer(Exception? __exception, bool __state)
        {
            if (__state) { Rand.PopState(); Depth--; }
            return __exception;
        }
    }
}
