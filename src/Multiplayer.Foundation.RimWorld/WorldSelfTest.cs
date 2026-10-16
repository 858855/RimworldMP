using System;
using HarmonyLib;
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
    /// SETUP-004 and SETUP-005 continue on that world: pawn logic, then a real vanilla game start, saved like the host
    /// does, loaded like a guest does, fingerprints compared and the pause checked.
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

        // SETUP-005: 1 prepare, 8 character page, 9 start asked, 10 answer and start, 2 save as host, 3 copy for the guest
        // and reload as host, 4 host fingerprint and pause probe, 5 load as guest, 6 compare and pause probe, 7 done.
        private static int gameStage;
        private static Page_ConfigureStartingPawns? startPage;
        private static int[] chosenPawns = Array.Empty<int>();
        private static PlanetTile startTile = PlanetTile.Invalid;
        private static float gameStageAt;
        private static int pausedTick;
        private static Game? hostGame;
        private static byte[]? guestFile, hostFile, hostFingerprint;
        private static int hostTick;
        private static long reloadMs;
        private static int probeCalls, probeResets, probeRunning, probeBlocked;
        private static string[] hostLines = Array.Empty<string>();
        private static DateTime savedAtUtc;
        private static readonly Stopwatch gameWatch = new Stopwatch();
        private static long generateMs, saveMs;
        private static bool saveSynchronous, pawnsPassed;
        private static readonly System.Collections.Generic.List<string> gameResults = new System.Collections.Generic.List<string>();
        private const string HostSave = "Multiplayer - Selbsttest Host", GuestSave = "Multiplayer - Selbsttest Gast";

        /// <summary>The self-test's shared game is paused like a multiplayer session (SETUP-005).</summary>
        internal static bool HoldsPause => gameStage >= 2 && gameStage <= 7 && Current.ProgramState == ProgramState.Playing;

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
                SiteFlags.Shown = Array.Empty<(string, PlanetTile, UnityEngine.Color)>();
                pawnsPassed = RunPawnTest();
                // Like a guest after the site page: no setup page, only the multiplayer window. Does the world stay?
                if (sitePage != null) Find.WindowStack.TryRemove(sitePage, false);
                sitePage = null;
                if (FoundationMod.Instance != null) Find.WindowStack.Add(new FoundationWindow(FoundationMod.Instance));
                siteStage = 3; siteOpenedAt = UnityEngine.Time.realtimeSinceStartup;
                return;
            }
            if (siteStage == 3)
            {
                if (UnityEngine.Time.realtimeSinceStartup - siteOpenedAt < 1f) return;
                bool kept = Current.Game?.World != null && Current.Game.InitData != null;
                Log.Message("Multiplayer Foundation world self-test: world without a setup page " + (kept ? "kept" : "dropped") +
                    " (game=" + (Current.Game != null) + " world=" + (Current.Game?.World != null) + " initData=" + (Current.Game?.InitData != null) + ")");
                // The window stays open: without any window the main menu drops the game (found by this check).
                siteStage = 0;
                if (kept && pawnsPassed && startTile.Valid) gameStage = 1;
                else cleanupWhenIdle = true;
                return;
            }
            if (gameStage != 0) { RunGameTest(); return; }
            // Clean up a frame later: FinalizeInit queues work that runs after the long event and needs the world.
            if (cleanupWhenIdle && !LongEventHandler.AnyEventNowOrWaiting)
            {
                cleanupWhenIdle = false;
                if (Current.Game != null) MemoryUtility.ClearAllMapsAndWorld();
                Current.Game = null;
            }
            if (quitWhenIdle && !cleanupWhenIdle && siteStage == 0 && gameStage == 0 && !LongEventHandler.AnyEventNowOrWaiting)
            { quitWhenIdle = false; Root.Shutdown(); return; }
            if (started || !Requested || Current.ProgramState != ProgramState.Entry || LongEventHandler.AnyEventNowOrWaiting) return;
            started = true;
            LongEventHandler.QueueLongEvent(Run, "LoadingLongEvent", false, error => Log.Error("Multiplayer Foundation world self-test: FAIL " + error));
        }

        // SETUP-005: one stage per frame (or per long event); every check goes into one log line.
        private static void RunGameTest()
        {
            void Check(string name, bool ok) => gameResults.Add(name + "=" + (ok ? "ok" : "FAIL"));
            bool Loaded() => Current.ProgramState == ProgramState.Playing && !LongEventHandler.AnyEventNowOrWaiting &&
                Find.Maps.Count > 0 && !ReferenceEquals(Current.Game, hostGame);
            try
            {
                switch (gameStage)
                {
                    case 1:
                        // The host starts from its own generated world, never from a guest's copy (the copy has no ID
                        // counters, so a game started from it reuses load IDs; self-test 2026-10-15).
                        gameStage = 8;
                        LongEventHandler.QueueLongEvent(PrepareHostGame, "LoadingLongEvent", false, error =>
                        {
                            Log.Error("Multiplayer Foundation game self-test: FAIL preparing " + error);
                            gameStage = 0; cleanupWhenIdle = true;
                        });
                        return;
                    case 8:
                        // Like the host after everyone is done: the vanilla character page on the prepared GameInitData,
                        // and no multiplayer window (the host only has its overlay there).
                        if (LongEventHandler.AnyEventNowOrWaiting) return;
                        if (Current.Game?.InitData == null || !startTile.Valid) { Check("hostGame", false); Finish(); return; }
                        Find.GameInitData.startingTile = startTile;
                        chosenPawns = Find.GameInitData.startingAndOptionalPawns.Take(Find.GameInitData.startingPawnCount)
                            .Select(p => p.thingIDNumber).OrderBy(id => id).ToArray();
                        startPage = new Page_ConfigureStartingPawns { nextAct = PageUtility.InitGameStart };
                        Find.WindowStack.Add(startPage);
                        Find.WindowStack.TryRemove(typeof(FoundationWindow), false);
                        gameStage = 9; gameStageAt = UnityEngine.Time.realtimeSinceStartup;
                        return;
                    case 9:
                        // Start pressed: the mod asks the server and holds the game with its window (as in
                        // TryBeginSharedStart), then vanilla closes the page.
                        if (UnityEngine.Time.realtimeSinceStartup - gameStageAt < 1f) return;
                        FoundationMod.Instance?.OpenWindow();
                        if (startPage != null) Find.WindowStack.TryRemove(startPage, false);
                        startPage = null;
                        gameStage = 10; gameStageAt = UnityEngine.Time.realtimeSinceStartup;
                        return;
                    case 10:
                        // The server answers later; until then the main menu must not drop the chosen pawns and site.
                        if (UnityEngine.Time.realtimeSinceStartup - gameStageAt < 1.5f) return;
                        bool kept = Current.Game?.InitData != null && Find.GameInitData.startingTile == startTile &&
                            Find.GameInitData.startingAndOptionalPawns.Take(Find.GameInitData.startingPawnCount)
                                .Select(p => p.thingIDNumber).OrderBy(id => id).SequenceEqual(chosenPawns);
                        Check("startKept", kept);
                        if (!kept) { Finish(); return; }
                        gameStage = 2; gameWatch.Restart(); hostGame = null;
                        PageUtility.InitGameStart();
                        return;
                    case 2:
                        // Host: the generated game. Fill the session and save it.
                        if (!Loaded()) return;
                        generateMs = gameWatch.ElapsedMilliseconds;
                        var colonists = GameFingerprint.Colonists();
                        Check("colonists", colonists.Count >= 2);
                        // The game must land the chosen pawns on the chosen site, not a quick-test game (2026-10-16).
                        Check("landedChosen", colonists.Select(p => p.thingIDNumber).OrderBy(id => id).SequenceEqual(chosenPawns));
                        Check("site", Find.Maps[0].Tile == startTile);
                        Protocol.PlayerKey a = Protocol.PlayerKey.Local(Guid.NewGuid()), b = Protocol.PlayerKey.Local(Guid.NewGuid());
                        var owners = new System.Collections.Generic.Dictionary<int, Protocol.PlayerKey>();
                        if (colonists.Count >= 2) { owners[colonists[0].thingIDNumber] = a; owners[colonists[1].thingIDNumber] = b; }
                        owners[-1] = b; // a pawn that did not land must not be saved
                        MultiplayerSession? session = MultiplayerSession.ForCurrentGame;
                        Check("component", session != null);
                        session?.Begin(Guid.NewGuid(), new[] { (a, "Test-Host"), (b, "Test-Gast") }, owners,
                            colonists.Select(p => p.thingIDNumber).ToList());
                        Check("ownersLanded", session != null && session.Owners.Count() == Math.Min(2, colonists.Count));
                        Map map = Find.Maps[0];
                        gameResults.Add("generated(tick=" + Find.TickManager.TicksGame + " tile=" + map.Tile.tileId + " parent=" +
                            (map.Parent?.Tile.tileId.ToString() ?? "-") + " start=" + startTile.tileId + ")");
                        savedAtUtc = DateTime.UtcNow; gameWatch.Restart();
                        GameDataSaveLoader.SaveGame(HostSave);
                        saveMs = gameWatch.ElapsedMilliseconds;
                        saveSynchronous = GameCodec.ReadFinishedSave(HostSave, savedAtUtc) != null;
                        gameStage = 3; gameStageAt = UnityEngine.Time.realtimeSinceStartup;
                        return;
                    case 3:
                        // Host: the written save becomes the guest's copy (gzip both ways); then the host reloads its own file.
                        if (LongEventHandler.AnyEventNowOrWaiting) return;
                        byte[]? raw = GameCodec.ReadFinishedSave(HostSave, savedAtUtc);
                        if (raw == null)
                        {
                            if (UnityEngine.Time.realtimeSinceStartup - gameStageAt < 30f) return;
                            Check("saved", false); Finish(); return;
                        }
                        hostFile = raw;
                        byte[] gzipped = GameCodec.Gzip(raw);
                        guestFile = GameCodec.Gunzip(gzipped);
                        Check("gzip", guestFile.SequenceEqual(raw));
                        gameResults.Add("raw=" + raw.Length + " gzip=" + gzipped.Length);
                        System.IO.File.WriteAllBytes(GameCodec.PathFor(GuestSave), guestFile);
                        hostGame = Current.Game; gameWatch.Restart(); gameStageAt = UnityEngine.Time.realtimeSinceStartup;
                        gameStage = 4;
                        GameDataSaveLoader.LoadGame(HostSave);
                        return;
                    case 4:
                        // Host: reloaded. Its fingerprint; then let time run through a path the setter patch never sees.
                        if (!Loaded()) { TimeOut("hostReloaded"); return; }
                        reloadMs = gameWatch.ElapsedMilliseconds;
                        (hostFingerprint, hostLines) = GameFingerprint.Compute(hostFile!, MultiplayerSession.ForCurrentGame!);
                        hostTick = Find.TickManager.TicksGame;
                        StartPauseProbe();
                        gameStage = 5;
                        return;
                    case 5:
                        if (UnityEngine.Time.realtimeSinceStartup - gameStageAt < 2f) return;
                        Check("ticksHeld", Find.TickManager.TicksGame == pausedTick && Find.TickManager.Paused);
                        gameResults.Add(ProbeText());
                        hostGame = Current.Game; gameWatch.Restart(); gameStageAt = UnityEngine.Time.realtimeSinceStartup;
                        gameStage = 6;
                        GameDataSaveLoader.LoadGame(GuestSave);
                        return;
                    case 6:
                        // Guest: loaded the received copy. Same fingerprint, same tick, session and owners present.
                        if (!Loaded()) { TimeOut("guestLoaded"); return; }
                        long loadMs = gameWatch.ElapsedMilliseconds;
                        MultiplayerSession? loaded = MultiplayerSession.ForCurrentGame;
                        Check("sessionLoaded", loaded?.Active == true && loaded.Owners.Count() == 2);
                        var (guestFingerprint, guestLines) = GameFingerprint.Compute(guestFile!, loaded!);
                        bool same = guestFingerprint.SequenceEqual(hostFingerprint!);
                        Check("fingerprint", same);
                        if (!same)
                        {
                            foreach (string line in hostLines.Except(guestLines).Take(5)) Log.Message("Multiplayer Foundation game self-test: host only: " + line);
                            foreach (string line in guestLines.Except(hostLines).Take(5)) Log.Message("Multiplayer Foundation game self-test: guest only: " + line);
                        }
                        Check("tick", Find.TickManager.TicksGame == hostTick);
                        gameResults.Add("generate=" + generateMs + "ms save=" + saveMs + "ms(sync=" + saveSynchronous + ") hostReload=" + reloadMs +
                            "ms guestLoad=" + loadMs + "ms");
                        gameResults.Add("fingerprint " + GameFingerprint.Short(hostFingerprint!) + " -> " + GameFingerprint.Short(guestFingerprint));
                        gameResults.Add(GameFingerprint.LogSummary(guestLines));
                        StartPauseProbe();
                        gameStage = 7;
                        return;
                    case 7:
                        if (UnityEngine.Time.realtimeSinceStartup - gameStageAt < 2f) return;
                        Check("ticksHeldAfterLoad", Find.TickManager.TicksGame == pausedTick && Find.TickManager.Paused);
                        gameResults.Add(ProbeText());
                        Finish();
                        return;
                }
            }
            catch (Exception error)
            {
                Log.Error("Multiplayer Foundation game self-test: FAIL " + error);
                Finish();
            }
        }

        private static void TimeOut(string what)
        {
            if (UnityEngine.Time.realtimeSinceStartup - gameStageAt < 120f) return;
            gameResults.Add(what + "=FAIL");
            Finish();
        }

        // Closes windows that pause on their own (intro letters) and sets the speed field directly, as any path the
        // setter patch cannot see would; the per-frame check must pause the game again before a tick runs.
        private static void StartPauseProbe()
        {
            foreach (Window window in Find.WindowStack.Windows.Where(w => w.forcePause).ToList()) Find.WindowStack.TryRemove(window, false);
            pausedTick = Find.TickManager.TicksGame;
            AccessTools.Field(typeof(TickManager), "curTimeSpeed").SetValue(Find.TickManager, TimeSpeed.Normal);
            probeCalls = SessionPauseUpdatePatch.Calls; probeResets = SessionPauseUpdatePatch.Resets;
            probeRunning = SessionPauseUpdatePatch.RunningAfter; probeBlocked = SessionPauseTickPatch.Blocked;
            gameStageAt = UnityEngine.Time.realtimeSinceStartup;
        }

        private static string ProbeText() =>
            "(ticks " + pausedTick + "->" + Find.TickManager.TicksGame + " speed=" + Find.TickManager.CurTimeSpeed +
            " checks=" + (SessionPauseUpdatePatch.Calls - probeCalls) + " resets=" + (SessionPauseUpdatePatch.Resets - probeResets) +
            " runningAfter=" + (SessionPauseUpdatePatch.RunningAfter - probeRunning) + " ticksBlocked=" + (SessionPauseTickPatch.Blocked - probeBlocked) + ")";

        // Host side of the game test: a fresh game with a generated world and the scenario's starting pawns.
        private static void PrepareHostGame()
        {
            MemoryUtility.ClearAllMapsAndWorld();
            Current.Game = new Game { InitData = new GameInitData() };
            Current.Game.Scenario = ScenarioDefOf.Crashlanded.scenario.CopyForEditing();
            Current.Game.Scenario.PreConfigure();
            Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
            var factions = FactionGenerator.ConfigurableFactions
                .SelectMany(def => Enumerable.Repeat(def, Math.Max(0, def.startingCountAtWorldCreation))).ToList();
            Current.Game.World = WorldGenerator.GenerateWorld(0.05f, "mpselftestgame", OverallRainfall.Normal, OverallTemperature.Normal,
                OverallPopulation.Normal, LandmarkDensity.Normal, factions, 0.05f);
            GameInitData init = Find.GameInitData;
            if (init.playerFaction == null) init.playerFaction = Faction.OfPlayer;
            Find.Scenario.PostIdeoChosen();
            PlanetLayer surface = Find.World.grid.Surface;
            startTile = Enumerable.Range(0, surface.TilesCount).Select(i => surface.PlanetTileForID(i))
                .FirstOrDefault(t => TileFinder.IsValidTileForNewSettlement(t, new System.Text.StringBuilder(), false));
        }

        private static void Finish()
        {
            bool pass = gameResults.Count > 0 && !gameResults.Any(r => r.EndsWith("FAIL"));
            Log.Message("Multiplayer Foundation game self-test: " + (pass ? "PASS" : "FAIL") + " " + string.Join(" ", gameResults));
            foreach (string name in new[] { HostSave, GuestSave })
            {
                // only the two files this test wrote
                try { if (System.IO.File.Exists(GameCodec.PathFor(name))) System.IO.File.Delete(GameCodec.PathFor(name)); }
                catch (Exception error) { Log.Warning("Multiplayer Foundation game self-test: could not remove " + name + ": " + error.Message); }
            }
            if (startPage != null) Find.WindowStack.TryRemove(startPage, false);
            gameStage = 0; hostGame = null; guestFile = null; hostFile = null; startPage = null;
            if (!quitWhenIdle && Current.ProgramState == ProgramState.Playing) GenScene.GoToMainMenu();
        }

        // SETUP-004: the host's pawn logic on real starting pawns generated by vanilla. True when every check passed.
        private static bool RunPawnTest()
        {
            try
            {
                GameInitData init = Find.GameInitData;
                if (init.playerFaction == null) init.playerFaction = Faction.OfPlayer;
                // Like the host: a storyteller is chosen before pawns are generated (pawn generation reads it).
                if (Current.Game.storyteller?.def == null) Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
                Find.Scenario.PostIdeoChosen();
                int startCount = init.startingPawnCount, total = init.startingAndOptionalPawns.Count;
                var host = new CharacterHost();
                Multiplayer.Foundation.Protocol.PlayerKey a = Multiplayer.Foundation.Protocol.PlayerKey.Local(Guid.NewGuid()),
                    b = Multiplayer.Foundation.Protocol.PlayerKey.Local(Guid.NewGuid());
                var present = new System.Collections.Generic.List<Multiplayer.Foundation.Protocol.PlayerKey> { a, b };
                var results = new System.Collections.Generic.List<string>();
                void Check(string name, bool ok) => results.Add(name + "=" + (ok ? "ok" : "FAIL"));
                Multiplayer.Foundation.Protocol.PawnRequest Req(Multiplayer.Foundation.Protocol.PawnRequestKind kind, int id, int other = 0, string text = "")
                    => new Multiplayer.Foundation.Protocol.PawnRequest(kind, id, other, text, a);
                host.EnsurePawnCount(init, startCount + 1);
                Check("raise", init.startingPawnCount == startCount + 1);
                var roster = host.Build(init, present);
                var card = roster.Cards[0];
                Check("card", card.Name.Length > 0 && card.Skills.Count > 0 && card.Summary.Length > 0);
                Check("claim", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.Claim, card.PawnId), init) == null &&
                    host.Build(init, present).Cards[0].Owner == a);
                Check("secondClaimRefused", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.Claim, roster.Cards[1].PawnId), init) != null);
                Check("reroll", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.Reroll, card.PawnId), init) == null);
                var rerolled = host.Build(init, present).Cards[0];
                Check("rerollKeepsOwner", rerolled.PawnId != card.PawnId && rerolled.Owner == a);
                Check("rename", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.Rename, rerolled.PawnId, 0, "Testi"), init) == null &&
                    host.Build(init, present).Cards[0].Name.Contains("Testi"));
                Check("foreignRefused", host.Apply(new Multiplayer.Foundation.Protocol.PawnRequest(
                    Multiplayer.Foundation.Protocol.PawnRequestKind.Release, rerolled.PawnId, 0, "", b), init) != null);
                var left = host.Build(init, present).Cards.FirstOrDefault(c => !c.Selected);
                if (left != null)
                {
                    Check("swap", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.Swap, rerolled.PawnId, left.PawnId), init) == null);
                    var swapped = host.Build(init, present);
                    Check("swapResult", swapped.Cards.Any(c => c.PawnId == left.PawnId && c.Selected && c.Owner == a) &&
                        swapped.Cards.Any(c => c.PawnId == rerolled.PawnId && !c.Selected && c.Owner == null));
                }
                else results.Add("swap=skipped(no left-behind pawn)");
                Check("leaveFrees", host.Build(init, new System.Collections.Generic.List<Multiplayer.Foundation.Protocol.PlayerKey> { b }).Cards.All(c => c.Owner != a));
                Check("payload", Multiplayer.Foundation.Protocol.PawnRoster.Read(host.Build(init, present).ToPayload()).Cards.Count == init.startingAndOptionalPawns.Count);
                bool pass = results.All(r => !r.EndsWith("FAIL"));
                Log.Message("Multiplayer Foundation world self-test: pawns " + (pass ? "PASS" : "FAIL") + " start=" + startCount + "/" + total +
                    " now=" + init.startingPawnCount + "/" + init.startingAndOptionalPawns.Count + " " + string.Join(" ", results) +
                    " example=\"" + card.Name + " | " + card.Summary + " | " + card.Childhood + "/" + card.Adulthood + " | " + string.Join(",", card.Traits) + "\"");
                return pass;
            }
            catch (Exception error) { Log.Error("Multiplayer Foundation world self-test: pawns FAIL " + error); return false; }
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
                if (flagTiles.Count > 0) { Find.WorldCameraDriver.JumpTo(flagTiles[0]); startTile = flagTiles[0]; }
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
