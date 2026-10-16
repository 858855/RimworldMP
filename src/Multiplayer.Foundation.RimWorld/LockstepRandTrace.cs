using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// Self-test diagnosis for DET-001 (plan §7.9 trace mode, first step): every Rand draw goes through
    /// <c>MurmurHash.GetInt(seed, iterations)</c>. A prefix there, patched only while the lockstep self-test runs,
    /// counts draws from threads other than the main thread (they would race on Rand's shared state) and, while
    /// <see cref="Recording"/>, notes the call site of each main-thread draw during a lockstep tick.
    /// </summary>
    internal static class LockstepRandTrace
    {
        private const string HarmonyId = "rimworldmp.foundation.randtrace";
        private static Harmony? harmony;
        private static int mainThread;
        private static readonly FieldInfo SeedField = AccessTools.Field(typeof(Rand), "seed");
        private static readonly FieldInfo IterationsField = AccessTools.Field(typeof(Rand), "iterations");
        private static readonly Dictionary<string, int> tokens = new Dictionary<string, int>();
        private static readonly List<string> names = new List<string>();
        private static readonly object gate = new object();

        internal static bool Recording;
        internal static int OffThreadDraws;
        internal static readonly List<string> OffThreadSamples = new List<string>();
        /// <summary>Call-site tokens of the current tick's draws, in order.</summary>
        internal static readonly List<int> CurrentTick = new List<int>();

        internal static uint Iterations => (uint)IterationsField.GetValue(null);
        internal static string Name(int token) => token >= 0 && token < names.Count ? names[token] : "?";

        internal static void Install()
        {
            if (harmony != null) return;
            mainThread = Thread.CurrentThread.ManagedThreadId;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(MurmurHash), nameof(MurmurHash.GetInt)),
                prefix: new HarmonyMethod(typeof(LockstepRandTrace), nameof(Prefix)));
        }

        internal static void Uninstall()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null; Recording = false;
        }

        private static void Prefix(uint seed, uint input)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
            {
                Interlocked.Increment(ref OffThreadDraws);
                lock (gate)
                    if (OffThreadSamples.Count < 3) OffThreadSamples.Add(new StackTrace(1, false).ToString());
                return;
            }
            if (!Recording || !LockstepDriver.Ticking || LockstepVisualRandPatch.Depth > 0) return;
            // a Rand draw uses Rand's own seed and stream position; other MurmurHash uses are seeded and deterministic
            uint currentSeed = (uint)SeedField.GetValue(null), iterations = Iterations;
            if (seed != currentSeed || (input != iterations && input + 1 != iterations)) return;
            CurrentTick.Add(Token(CallSite()));
        }

        // The first two callers outside Rand and the patch machinery, e.g. "RimWorld.CellFinder.X < RimWorld.JobGiver_Wander.Y".
        private static string CallSite()
        {
            var trace = new StackTrace(1, false);
            var found = new List<string>(2);
            for (int i = 0; i < trace.FrameCount && i < 14 && found.Count < 2; i++)
            {
                MethodBase? method = trace.GetFrame(i)?.GetMethod();
                Type? type = method?.DeclaringType;
                string space = type?.Namespace ?? "";
                if (method == null || type == null || type == typeof(Rand) || type == typeof(MurmurHash) || type == typeof(LockstepRandTrace) ||
                    space.StartsWith("HarmonyLib", StringComparison.Ordinal) || space.StartsWith("MonoMod", StringComparison.Ordinal)) continue;
                found.Add(type.FullName + "." + method.Name);
            }
            return found.Count == 0 ? "(unknown)" : string.Join(" < ", found);
        }

        private static int Token(string name)
        {
            if (tokens.TryGetValue(name, out int token)) return token;
            token = names.Count; names.Add(name); tokens[name] = token;
            return token;
        }
    }
}
