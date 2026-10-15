using System;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Profile;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-002 / VER-001 runtime check without a second player. Start RimWorld with <c>-mpWorldSelfTest</c>
    /// (optionally <c>-mpWorldSelfTestCoverage=0.3</c> and <c>-mpQuitAfterSelfTest</c>). In the main menu it
    /// generates a world like the host does, saves it with <see cref="WorldCodec"/>, drops it, loads it again
    /// like a guest and compares fingerprints. The result goes to Player.log.
    /// </summary>
    internal static class WorldSelfTest
    {
        private static bool started, quitWhenIdle, cleanupWhenIdle;

        internal static bool Requested => Environment.GetCommandLineArgs().Any(a => a == "-mpWorldSelfTest");

        internal static void TryStart()
        {
            // Clean up a frame later: FinalizeInit queues work that runs after the long event and needs the world.
            if (cleanupWhenIdle && !LongEventHandler.AnyEventNowOrWaiting)
            {
                cleanupWhenIdle = false;
                if (Current.Game != null) MemoryUtility.ClearAllMapsAndWorld();
                Current.Game = null;
            }
            if (quitWhenIdle && !cleanupWhenIdle && !LongEventHandler.AnyEventNowOrWaiting) { quitWhenIdle = false; Root.Shutdown(); return; }
            if (started || !Requested || Current.ProgramState != ProgramState.Entry || LongEventHandler.AnyEventNowOrWaiting) return;
            started = true;
            LongEventHandler.QueueLongEvent(Run, "LoadingLongEvent", false, error => Log.Error("Multiplayer Foundation world self-test: FAIL " + error));
        }

        private static void Run()
        {
            float coverage = 0.05f;
            if (GenCommandLine.TryGetCommandLineArg("mpWorldSelfTestCoverage", out string value) &&
                float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                coverage = parsed;
            try
            {
                var watch = Stopwatch.StartNew();
                Current.Game = new Game { InitData = new GameInitData() };
                Current.Game.Scenario = ScenarioDefOf.Crashlanded.scenario.CopyForEditing();
                Current.Game.Scenario.PreConfigure();
                // The host has chosen a storyteller before the world is generated; pawn generation needs one.
                Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
                var factions = FactionGenerator.ConfigurableFactions
                    .SelectMany(def => Enumerable.Repeat(def, Math.Max(0, def.startingCountAtWorldCreation))).ToList();
                World generated = WorldGenerator.GenerateWorld(coverage, "mpselftest", OverallRainfall.Normal, OverallTemperature.Normal,
                    OverallPopulation.Normal, LandmarkDensity.Normal, factions, 0.05f);
                Current.Game.World = generated;
                long generateMs = watch.ElapsedMilliseconds;
                WorldCodec.Standardize(generated); // as the host does before fingerprint and save
                byte[] before = WorldFingerprint.Compute(generated);
                var partsBefore = WorldFingerprint.Parts(generated);
                PlanetLayer generatedSurface = generated.grid.Surface;
                string[] tilesBefore = Enumerable.Range(0, generatedSurface.TilesCount).Select(i => WorldFingerprint.TileLine(generatedSurface[i])).ToArray();
                int tiles = generated.grid.Surface.TilesCount, settlements = generated.worldObjects.Settlements.Count;
                watch.Restart();
                byte[] data = WorldCodec.Save(generated);
                long saveMs = watch.ElapsedMilliseconds;
                MemoryUtility.ClearAllMapsAndWorld();
                Current.Game = null;
                watch.Restart();
                World loaded = WorldCodec.Load(data, ScenarioDefOf.Crashlanded.scenario.name);
                long loadMs = watch.ElapsedMilliseconds;
                byte[] after = WorldFingerprint.Compute(loaded);
                bool match = before.SequenceEqual(after);
                var partsAfter = WorldFingerprint.Parts(loaded);
                for (int p = 0; p < partsBefore.Length; p++)
                    if (!partsBefore[p].Hash.SequenceEqual(partsAfter[p].Hash))
                        Log.Message("Multiplayer Foundation world self-test: part differs: " + partsBefore[p].Name);
                PlanetLayer loadedSurface = loaded.grid.Surface;
                int shown = 0;
                for (int i = 0; i < Math.Min(tilesBefore.Length, loadedSurface.TilesCount) && shown < 5; i++)
                {
                    string afterLine = WorldFingerprint.TileLine(loadedSurface[i]);
                    if (afterLine == tilesBefore[i]) continue;
                    Log.Message("Multiplayer Foundation world self-test: tile " + i + " before " + tilesBefore[i] + " after " + afterLine);
                    shown++;
                }
                Log.Message("Multiplayer Foundation world self-test: " + (match ? "PASS" : "FAIL") +
                    " coverage=" + coverage + " tiles=" + tiles + " settlements=" + settlements + " loadedSettlements=" + loaded.worldObjects.Settlements.Count +
                    " gzip=" + data.Length + " bytes generate=" + generateMs + "ms save=" + saveMs + "ms load=" + loadMs + "ms" +
                    " fingerprint " + WorldFingerprint.Short(before) + " -> " + WorldFingerprint.Short(after));
            }
            finally
            {
                cleanupWhenIdle = true;
                quitWhenIdle = Environment.GetCommandLineArgs().Any(a => a == "-mpQuitAfterSelfTest");
            }
        }
    }
}
