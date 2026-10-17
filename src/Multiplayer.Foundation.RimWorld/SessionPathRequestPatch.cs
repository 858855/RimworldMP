using HarmonyLib;
using Verse;
using Verse.AI;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// DET-001 finding of 2026-10-16 (ported from feat/sync-lockstep 5f1b782, ADR 0024): RimWorld 1.6 computes pawn paths
    /// in background jobs. When a path request is answered depends on the worker threads, not on the tick, so two games
    /// with the same ticks drifted apart: the self-test on that line found wandering animals one cell apart after 2 000
    /// to 6 000 ticks, also with the jobs completed after every tick. During lockstep ticks and commands this prefix
    /// answers each request at once on the main thread with vanilla's synchronous <c>PathFinder.FindPathNow</c> and skips
    /// the queue; skipping the original is intended (AGENTS.md rule 12). Outside a session nothing changes.
    /// Not passed to FindPathNow: the request's area and exact destination (counted for diagnostics).
    /// </summary>
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.PushRequest))]
    internal static class SessionPathRequestPatch
    {
        internal static int Answered, Found, NotReady, WithArea;

        private static bool Prefix(PathFinder __instance, PathRequest request)
        {
            if (!SessionRandom.Active || request == null) return true;
            PawnPath path = __instance.FindPathNow(request.Start, request.Target, request.TraverseParms, request.Tuning, request.EndMode,
                request.customizer);
            request.Resolve(path);
            Answered++;
            if (path.Found) Found++;
            if (!request.ResultIsReady) NotReady++;
            if (request.area != null) WithArea++;
            return false;
        }
    }
}
