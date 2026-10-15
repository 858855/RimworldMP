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
        // SETUP-003: after loading, show the world on the starting-site page with two test flags like a guest.
        private static int siteStage;
        private static float siteOpenedAt;
        private static Page_SelectStartingSite? sitePage;
        private static bool screenshotTaken;

        /// <summary>True while the self-test owns the flags on the site page.</summary>
        internal static bool ShowingSitePage => siteStage != 0;

        internal static bool Requested => Environment.GetCommandLineArgs().Any(a => a == "-mpWorldSelfTest");

        internal static void TryStart()
        {
            if (siteStage == 1 && !LongEventHandler.AnyEventNowOrWaiting)
            {
                siteStage = 2; siteOpenedAt = UnityEngine.Time.realtimeSinceStartup; SiteFlags.FramesDrawn = 0;
                sitePage = new Page_SelectStartingSite();
                Find.WindowStack.Add(sitePage);
                return;
            }
            if (siteStage == 2)
            {
                float openFor = UnityEngine.Time.realtimeSinceStartup - siteOpenedAt;
                // Optional picture of the page with the test flags, for a visual check (-mpSelfTestScreenshot).
                if (openFor >= 3f && !screenshotTaken && Environment.GetCommandLineArgs().Any(a => a == "-mpSelfTestScreenshot"))
                {
                    screenshotTaken = true;
                    string shot = System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "MultiplayerFoundation", "selftest-site.png");
                    UnityEngine.ScreenCapture.CaptureScreenshot(shot);
                    Log.Message("Multiplayer Foundation world self-test: screenshot " + shot);
                }
                if (openFor < 4f) return;
                bool open = sitePage != null && sitePage.IsOpen;
                Log.Message("Multiplayer Foundation world self-test: site page " + (open && SiteFlags.FramesDrawn > 0 ? "PASS" : "FAIL") +
                    " open=" + open + " flagFrames=" + SiteFlags.FramesDrawn + " flags=" + SiteFlags.Shown.Count);
                if (sitePage != null) Find.WindowStack.TryRemove(sitePage, false);
                sitePage = null; siteStage = 0;
                SiteFlags.Shown = Array.Empty<(string, PlanetTile, UnityEngine.Color)>();
                cleanupWhenIdle = true;
                return;
            }
            // Clean up a frame later: FinalizeInit queues work that runs after the long event and needs the world.
            if (cleanupWhenIdle && !LongEventHandler.AnyEventNowOrWaiting)
            {
                cleanupWhenIdle = false;
                if (Current.Game != null) MemoryUtility.ClearAllMapsAndWorld();
                Current.Game = null;
            }
            if (quitWhenIdle && !cleanupWhenIdle && siteStage == 0 && !LongEventHandler.AnyEventNowOrWaiting)
            { quitWhenIdle = false; Root.Shutdown(); return; }
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
                // Two flags on tiles where a colony could start, then open the site page next frame.
                var scan = Stopwatch.StartNew();
                var flagTiles = Enumerable.Range(0, loadedSurface.TilesCount).Select(i => loadedSurface.PlanetTileForID(i))
                    .Where(t => TileFinder.IsValidTileForNewSettlement(t, new System.Text.StringBuilder(), false)).Take(2).ToList();
                Log.Message("Multiplayer Foundation world self-test: flag tiles " + flagTiles.Count + " found in " + scan.ElapsedMilliseconds + "ms");
                SiteFlags.Shown = flagTiles.Select((t, i) => (i == 0 ? "Test-Host" : "Test-Gast", t, SiteFlags.ColorFor(i))).ToList();
                if (flagTiles.Count > 0) Find.WorldCameraDriver.JumpTo(flagTiles[0]);
                siteStage = match ? 1 : 0;
                Log.Message("Multiplayer Foundation world self-test: " + (match ? "PASS" : "FAIL") +
                    " coverage=" + coverage + " tiles=" + tiles + " settlements=" + settlements + " loadedSettlements=" + loaded.worldObjects.Settlements.Count +
                    " gzip=" + data.Length + " bytes generate=" + generateMs + "ms save=" + saveMs + "ms load=" + loadMs + "ms" +
                    " fingerprint " + WorldFingerprint.Short(before) + " -> " + WorldFingerprint.Short(after));
            }
            finally
            {
                cleanupWhenIdle = siteStage == 0;
                quitWhenIdle = Environment.GetCommandLineArgs().Any(a => a == "-mpQuitAfterSelfTest");
            }
        }
    }
}
