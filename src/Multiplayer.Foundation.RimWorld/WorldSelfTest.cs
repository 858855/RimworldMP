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
        // SETUP-004: the guest's character page (stage 4).
        private static GuestPageTestWindow? guestPageWindow;
        private static bool guestPageOk, guestShotTaken;
        private static string guestPageInfo = "";
        // SETUP-004: vanilla's xenotype editor on a game without starting pawns (stage 5), and errors logged meanwhile.
        private static Dialog_CreateXenotype? editorProbe;
        private static bool editorCallback;
        private static System.Collections.Generic.HashSet<string> xenotypeFilesBefore = new System.Collections.Generic.HashSet<string>();
        private static int loggedErrors;
        private const int ProbeSlot = 2;
        private const string ProbeXenotype = "MP Selbsttest";

        private static void CountErrors(string message, string stack, UnityEngine.LogType type)
        {
            if (type == UnityEngine.LogType.Error || type == UnityEngine.LogType.Exception) loggedErrors++;
        }

        private static void OpenEditorProbe()
        {
            loggedErrors = 0;
            UnityEngine.Application.logMessageReceived += CountErrors;
            if (!ModsConfig.BiotechActive) return;
            try
            {
                xenotypeFilesBefore = new System.Collections.Generic.HashSet<string>(GenFilePaths.AllCustomXenotypeFiles.Select(f => f.FullName));
                editorCallback = false;
                editorProbe = new Dialog_CreateXenotype(ProbeSlot, () => editorCallback = true);
                Find.WindowStack.Add(editorProbe);
            }
            catch (Exception error) { Log.Error("Multiplayer Foundation world self-test: xenotype editor FAIL opening " + error); }
        }

        // Fills in a name and one gene, accepts like the button does, and checks the result reached this game's request for
        // the slot (where the guest page picks it up). Files the editor saved for the test are removed again.
        private static void FinishEditorProbe()
        {
            if (!ModsConfig.BiotechActive) { Log.Message("Multiplayer Foundation world self-test: xenotype editor skipped (no Biotech)"); return; }
            string result;
            try
            {
                int pawnsBefore = Find.GameInitData.startingAndOptionalPawns.Count;
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail("Hemogenic") ?? DefDatabase<GeneDef>.AllDefs.First();
                AccessTools.Field(typeof(GeneCreationDialogBase), "xenotypeName").SetValue(editorProbe, ProbeXenotype);
                var genes = (System.Collections.Generic.List<GeneDef>)AccessTools.Field(typeof(Dialog_CreateXenotype), "selectedGenes").GetValue(editorProbe);
                genes.Clear(); genes.Add(gene);
                AccessTools.Method(typeof(Dialog_CreateXenotype), "AcceptInner").Invoke(editorProbe, null);
                CustomXenotype? made = StartingPawnUtility.GetGenerationRequest(ProbeSlot).ForcedCustomXenotype;
                var choice = made == null ? null : Protocol.PawnXenotypeChoice.Parse(GuestCharacterPage.Choice(made).ToText());
                bool ok = editorCallback && made != null && made.name == ProbeXenotype && choice != null && choice.Genes.Contains(gene.defName);
                result = (ok ? "PASS" : "FAIL") + " callback=" + editorCallback + " made=" + (made?.name ?? "-") + " genes=" + (choice?.Genes.Count ?? 0) +
                    " pawnsBefore=" + pawnsBefore;
            }
            catch (Exception error) { result = "FAIL " + (error.InnerException ?? error).Message; }
            finally
            {
                // Clean up: the slot's request, the window, and only files the editor wrote during this test.
                try
                {
                    PawnGenerationRequest request = StartingPawnUtility.GetGenerationRequest(ProbeSlot);
                    request.ForcedCustomXenotype = null;
                    StartingPawnUtility.SetGenerationRequest(ProbeSlot, request);
                }
                catch (Exception) { }
                if (editorProbe != null) Find.WindowStack.TryRemove(editorProbe, false);
                editorProbe = null;
                RemoveProbeFiles();
            }
            Log.Message("Multiplayer Foundation world self-test: xenotype editor " + result + " loggedErrors=" + loggedErrors);
        }

        // The editor saves the test xenotype a little after accepting (seen 2026-10-16), so this also runs later and at the end.
        private static void RemoveProbeFiles()
        {
            try
            {
                foreach (System.IO.FileInfo file in GenFilePaths.AllCustomXenotypeFiles.ToList())
                    if (!xenotypeFilesBefore.Contains(file.FullName) && file.Name.StartsWith(ProbeXenotype))
                        file.Delete();
            }
            catch (Exception error) { Log.Warning("Multiplayer Foundation world self-test: could not remove the test xenotype: " + error.Message); }
        }

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
                // SETUP-004: vanilla's xenotype editor as a guest uses it, while this game has no starting pawns yet (like a
                // guest's). Accepted a second later.
                OpenEditorProbe();
                siteStage = 5; siteOpenedAt = UnityEngine.Time.realtimeSinceStartup;
                return;
            }
            if (siteStage == 5)
            {
                if (UnityEngine.Time.realtimeSinceStartup - siteOpenedAt < 1f) return;
                FinishEditorProbe();
                pawnsPassed = RunPawnTest();
                // SETUP-004: the guest's character page with these pawns, as a guest receives them (opened before the site
                // page closes, so a window stays open).
                guestPageWindow = OpenGuestPageTest();
                if (sitePage != null) Find.WindowStack.TryRemove(sitePage, false);
                sitePage = null;
                siteStage = 4; siteOpenedAt = UnityEngine.Time.realtimeSinceStartup;
                return;
            }
            if (siteStage == 4)
            {
                float openFor = UnityEngine.Time.realtimeSinceStartup - siteOpenedAt;
                if (openFor >= 2f && !guestShotTaken && Environment.GetCommandLineArgs().Any(a => a == "-mpSelfTestScreenshot"))
                {
                    guestShotTaken = true;
                    string shot = System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "MultiplayerFoundation", "selftest-guest-page.png");
                    UnityEngine.ScreenCapture.CaptureScreenshot(shot);
                    Log.Message("Multiplayer Foundation world self-test: screenshot " + shot);
                }
                if (openFor < 3f) return;
                bool shown = guestPageWindow != null && guestPageWindow.Frames > 0 && guestPageWindow.Errors == 0 && loggedErrors == 0;
                Log.Message("Multiplayer Foundation world self-test: guest page " + (guestPageOk && shown ? "PASS" : "FAIL") + " " + guestPageInfo +
                    " frames=" + (guestPageWindow?.Frames ?? 0) + " errors=" + (guestPageWindow?.Errors ?? 0) + " loggedErrors=" + loggedErrors);
                UnityEngine.Application.logMessageReceived -= CountErrors;
                RemoveProbeFiles();
                // Like a guest after the site page: no setup page, only the multiplayer window. Does the world stay?
                if (FoundationMod.Instance != null) Find.WindowStack.Add(new FoundationWindow(FoundationMod.Instance));
                if (guestPageWindow != null) Find.WindowStack.TryRemove(guestPageWindow, false);
                guestPageWindow = null;
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
                // Like a guest when the host's game arrives (LoadGuestGame): the setup world goes, and nothing of it may
                // stay current, or drawing the main menu throws (guest log, 2026-10-16).
                WorldCodec.Discard();
                bool detached = Current.Game?.World == null;
                Log.Message("Multiplayer Foundation world self-test: world discard " + (detached ? "PASS" : "FAIL") + " (game=" + (Current.Game != null) +
                    " world=" + (Current.Game?.World != null) + " grid=" + (Current.Game?.World?.grid != null) + ")");
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
            // Without focus the game stops updating and the test stalls (2026-10-16); this run only, the prefs stay as they are.
            UnityEngine.Application.runInBackground = true;
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
                        // SYNC-001: lockstep on the loaded copy, then the determinism runs on the same save
                        LockstepSelfTest.Begin(GuestSave, gameResults);
                        gameStage = 11;
                        return;
                    case 11:
                        if (LockstepSelfTest.Step()) Finish();
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
            if (Current.Game != null) MemoryUtility.ClearAllMapsAndWorld();
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
            RemoveProbeFiles();
            gameStage = 0; hostGame = null; guestFile = null; hostFile = null; startPage = null;
            if (!quitWhenIdle && Current.ProgramState == ProgramState.Playing) GenScene.GoToMainMenu();
        }

        // SETUP-004: cards with details and portraits of the real starting pawns, through the wire formats like a guest gets
        // them, drawn on the guest page. The player claims the first pawn so the page shows its own-pawn actions.
        private static GuestPageTestWindow? OpenGuestPageTest()
        {
            try
            {
                GameInitData init = Find.GameInitData;
                var host = new CharacterHost();
                Protocol.PlayerKey me = Protocol.PlayerKey.Local(Guid.NewGuid()), other = Protocol.PlayerKey.Local(Guid.NewGuid());
                var keys = new System.Collections.Generic.List<Protocol.PlayerKey> { other, me };
                Pawn first = init.startingAndOptionalPawns[0];
                host.Apply(new Protocol.PawnRequest(Protocol.PawnRequestKind.Claim, first.thingIDNumber, 0, "", me), init);
                Protocol.PawnRoster roster = Protocol.PawnRoster.Read(host.Build(init, keys).ToPayload());
                var page = new GuestCharacterPage();
                int images = 0, bytes = 0, largest = 0;
                // Every view option of every pawn, as the host sends them.
                foreach (Pawn pawn in init.startingAndOptionalPawns.Take(Protocol.PawnRoster.MaximumCards))
                    for (byte variant = 0; variant < Protocol.PawnPortrait.Variants; variant++)
                    {
                        byte[]? png = CharacterHost.RenderPortrait(pawn, variant);
                        if (png == null) continue;
                        page.SetPortrait(Protocol.PawnPortrait.Read(new Protocol.PawnPortrait(pawn.thingIDNumber, png, variant).ToPayload()));
                        images++; bytes += png.Length; largest = Math.Max(largest, png.Length);
                    }
                bool details = roster.Cards.All(c => c.Details != null && c.Details.AgeBiological > 0 && (c.Details.First.Length > 0 || c.Details.Nick.Length > 0));
                Protocol.PawnCardDetails? sample = roster.Cards[0].Details;
                guestPageOk = images == roster.Cards.Count * Protocol.PawnPortrait.Variants && page.PortraitCount == images && details;
                guestPageInfo = "cards=" + roster.Cards.Count + " portraits=" + images + " png=" + bytes / 1024 + "KiB max=" + largest + "B details=" + details +
                    " sample=\"" + sample?.First + " '" + sample?.Nick + "' " + sample?.Last + ", " + sample?.AgeBiological + "(" + sample?.AgeChronological + "), " +
                    sample?.Xenotype + " [" + sample?.XenotypeIcon + "], health " + sample?.Health.Count + ", relations " + sample?.Relations.Count +
                    ", possessions " + sample?.Possessions.Count + "\"";
                var players = new[]
                {
                    new Protocol.PlayerView(Guid.NewGuid(), "Test-Host", false, other),
                    new Protocol.PlayerView(Guid.NewGuid(), "Test-Gast", false, me)
                };
                var window = new GuestPageTestWindow(page, roster, players, me);
                Find.WindowStack.Add(window);
                return window;
            }
            catch (Exception error)
            {
                guestPageOk = false; guestPageInfo = "error " + error.Message;
                Log.Error("Multiplayer Foundation world self-test: guest page FAIL " + error);
                return null;
            }
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
                // The host page's functions for guests (2026-10-16): all three names, xenotype (standard and custom), development stage.
                Pawn PawnOf(int id) => init.startingAndOptionalPawns.First(p => p.thingIDNumber == id);
                int MyPawn() => host.Build(init, present).Cards.First(c => c.Owner == a && c.Selected).PawnId;
                int mine = MyPawn();
                Check("setName", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.SetName, mine, 0,
                    Multiplayer.Foundation.Protocol.PawnRequest.Names("Vorname", "Spitz", "Nachname")), init) == null &&
                    PawnOf(mine).Name is NameTriple named && named.First == "Vorname" && named.Nick == "Spitz" && named.Last == "Nachname");
                Check("setNameNeedsFirst", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.SetName, mine, 0,
                    Multiplayer.Foundation.Protocol.PawnRequest.Names("", "x", "y")), init) != null);
                if (ModsConfig.BiotechActive)
                {
                    XenotypeDef other = DefDatabase<XenotypeDef>.AllDefs.First(x => x != XenotypeDefOf.Baseliner);
                    Check("foreignXenotypeRefused", host.Apply(new Multiplayer.Foundation.Protocol.PawnRequest(Multiplayer.Foundation.Protocol.PawnRequestKind.SetXenotype,
                        mine, 0, Multiplayer.Foundation.Protocol.PawnXenotypeChoice.Standard(other.defName).ToText(), b), init) != null);
                    Check("setXenotype", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.SetXenotype, mine, 0,
                        Multiplayer.Foundation.Protocol.PawnXenotypeChoice.Standard(other.defName).ToText()), init) == null);
                    int xenoPawn = MyPawn();
                    Check("xenotypeApplied", xenoPawn != mine && PawnOf(xenoPawn).genes?.Xenotype == other);
                    GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail("Hemogenic") ?? DefDatabase<GeneDef>.AllDefs.First();
                    Check("setCustomXenotype", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.SetXenotype, xenoPawn, 0,
                        Multiplayer.Foundation.Protocol.PawnXenotypeChoice.CustomXenotype("MP Testtyp", false, "", new[] { gene.defName }).ToText()), init) == null);
                    int customPawn = MyPawn();
                    Check("customApplied", PawnOf(customPawn).genes != null && PawnOf(customPawn).genes.XenotypeLabelCap.ToLower().Contains("mp testtyp") &&
                        PawnOf(customPawn).genes.HasActiveGene(gene));
                    Check("unknownGeneRefused", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.SetXenotype, customPawn, 0,
                        Multiplayer.Foundation.Protocol.PawnXenotypeChoice.CustomXenotype("X", false, "", new[] { "NoSuchGene" }).ToText()), init) != null);
                    Check("setChild", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.SetDevelopmentStage, customPawn,
                        (int)DevelopmentalStage.Child), init) == null && PawnOf(MyPawn()).DevelopmentalStage == DevelopmentalStage.Child);
                    Check("backToAdult", host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.SetDevelopmentStage, MyPawn(),
                        (int)DevelopmentalStage.Adult), init) == null && PawnOf(MyPawn()).DevelopmentalStage == DevelopmentalStage.Adult &&
                        host.Build(init, present).Cards.First(c => c.PawnId == MyPawn()).Details?.DevelopmentStage == (byte)DevelopmentalStage.Adult);
                    host.Apply(Req(Multiplayer.Foundation.Protocol.PawnRequestKind.SetXenotype, MyPawn(), 0,
                        Multiplayer.Foundation.Protocol.PawnXenotypeChoice.Standard(XenotypeDefOf.Baseliner.defName).ToText()), init);
                }
                else results.Add("xenotype=skipped(no Biotech)");
                var tipCard = host.Build(init, present).Cards[0];
                Check("tips", tipCard.Details != null && tipCard.Details.TraitTips.Count == tipCard.Traits.Count &&
                    (tipCard.Childhood.Length == 0 || tipCard.Details.ChildhoodTip.Length > 0));
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

    /// <summary>SETUP-004 self-test: the guest's character page in a window; counts drawn frames and drawing errors.</summary>
    internal sealed class GuestPageTestWindow : Window
    {
        private readonly GuestCharacterPage page;
        private readonly Protocol.PawnRoster roster;
        private readonly Protocol.PlayerView[] players;
        private readonly Protocol.PlayerKey me;
        private int? chosen;
        internal int Frames, Errors;

        internal GuestPageTestWindow(GuestCharacterPage page, Protocol.PawnRoster roster, Protocol.PlayerView[] players, Protocol.PlayerKey me)
        {
            this.page = page; this.roster = roster; this.players = players; this.me = me;
            doCloseX = false; closeOnCancel = false; closeOnAccept = false;
        }

        public override UnityEngine.Vector2 InitialSize => new UnityEngine.Vector2(
            UnityEngine.Mathf.Min(Page.StandardSize.x, UI.screenWidth - 24f), UnityEngine.Mathf.Min(Page.StandardSize.y, UI.screenHeight - 24f));

        public override void DoWindowContents(UnityEngine.Rect inRect)
        {
            try
            {
                page.Draw(inRect, roster, players, me, false, "Fertig: 0/2 · Warte auf: Test-Host, Test-Gast", ref chosen, _ => { }, _ => { }, () => { });
                Frames++;
            }
            catch (Exception error)
            {
                if (Errors++ == 0) Log.Error("Multiplayer Foundation world self-test: guest page draw FAIL " + error);
                Text.Font = GameFont.Small; Text.Anchor = UnityEngine.TextAnchor.UpperLeft; UnityEngine.GUI.color = UnityEngine.Color.white;
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            page.Clear();
        }
    }
}
