using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Session;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SYNC-001 (ADR 0024) runtime check without a second player. The world self-test (<c>-mpWorldSelfTest</c>) runs it
    /// after the guest copy of the game is loaded. <c>-mpLockstepTicks=N</c> sets the length of each determinism run
    /// (default 6000; 0 skips them).
    /// - speeds: vanilla's TickRateMultiplier for Normal, Fast and Superfast matches the session table.
    /// - driver: the real driver against a sequencer in this process and a fake guest that follows, stops and goes on.
    ///   Ticks never pass the horizon, the slowest sets the pace, vanilla's speed controls become requests, speed and
    ///   pause act at their tick, a marker reaches both at the same tick, autosave is skipped, a wrong guest hash is reported.
    /// - determinism: the same save loaded four times, N ticks each. With the per-tick seed, random draws between the
    ///   ticks (as UI code makes them) must not change any hash; without it they must (the control).
    /// </summary>
    internal static partial class LockstepSelfTest
    {
        private const int DefaultTicks = 6000;
        private static readonly Stopwatch clock = new Stopwatch();

        private static int stage, driverStep, runIndex, ticks;
        private static float stepAt;
        private static List<string> results = new List<string>();
        private static string saveName = "";

        // driver part
        private static LockstepSequencer? sequencer;
        private static LockstepDriver? driver;
        private static readonly Guid HostId = Guid.NewGuid(), GuestId = Guid.NewGuid();
        private static readonly PlayerKey HostKey = PlayerKey.Local(Guid.NewGuid()), GuestKey = PlayerKey.Local(Guid.NewGuid());
        private static int guestTick, guestHorizon, startTick, mark, violations, submitted, refused;
        private static bool guestFrozen, wrongGuestHashes;
        private static readonly List<StateHash> hostHashes = new List<StateHash>();
        private static readonly List<(uint Seq, ushort Kind, int Tick, int RanAt)> ran = new List<(uint, ushort, int, int)>();
        private static readonly List<ScheduledCommand> toGuest = new List<ScheduledCommand>();

        // determinism part: A and B with the per-tick seed and different random draws between ticks; C and D the same
        // without the seed (the control); A2 repeats A (the same inputs, only the frames differ). When runs differ, two
        // more runs record every thing at every hash tick, and the first thing that differs between them is logged.
        private sealed class RunSpec
        {
            public RunSpec(string name, bool reseed, int noise, bool record = false) { Name = name; Reseed = reseed; Noise = noise; Record = record; }
            public string Name { get; }
            public bool Reseed { get; }
            public int Noise { get; }
            public bool Record { get; }
        }
        private sealed class RunResult
        {
            public List<StateHash> Hashes { get; } = new List<StateHash>();
            public long Ms { get; set; }
            public int Frames { get; set; }
            // pawns did move, paths were answered at once (with the path patch on)
            public Dictionary<int, IntVec3> StartCells { get; } = new Dictionary<int, IntVec3>();
            public int Moved { get; set; }
            public int Answered { get; set; }
            public int Found { get; set; }
            public int NotReady { get; set; }
            public int OffThread { get; set; }
            public bool ScriptApplied { get; set; }
            public List<(int Tick, uint Iterations, int[] Calls)> Draws { get; } = new List<(int, uint, int[])>();
            public Dictionary<int, (GameStateHash.ThingRecord[] Things, Dictionary<int, string> Pawns)> Records { get; } =
                new Dictionary<int, (GameStateHash.ThingRecord[], Dictionary<int, string>)>();
        }
        private static readonly List<RunSpec> runs = new List<RunSpec>();
        private static readonly List<RunResult> finished = new List<RunResult>();
        private static RunResult current = new RunResult();
        private static bool asyncPaths, diagnosed;
        private static (int Answered, int Found, int NotReady) pathsBefore;
        private static int ticksPerFrame;
        private static int offThreadBefore;
        private static Game? before;
        private static LockstepDriver? runDriver;
        private static int runStart;
        private static readonly Stopwatch runWatch = new Stopwatch();
        private static readonly System.Reflection.PropertyInfo RandState = AccessTools.Property(typeof(Rand), "StateCompressed");
        // thing records cost about 2 MB per hash tick: kept only this far into a run (the draw trace covers all ticks)
        private const int RecordThingsUpTo = 10000;

        internal static bool HoldsPause => stage != 0 && Current.ProgramState == ProgramState.Playing;

        internal static void Begin(string guestSave, List<string> into)
        {
            saveName = guestSave; results = into; stage = 1; driverStep = 0; runIndex = 0; diagnosed = false;
            ticks = GenCommandLine.TryGetCommandLineArg("mpLockstepTicks", out string value) && int.TryParse(value, out int n) ? Math.Max(0, n) : DefaultTicks;
            asyncPaths = Environment.GetCommandLineArgs().Any(a => a == "-mpLockstepAsyncPaths");
            LockstepPathRequestPatch.Disabled = asyncPaths;
            LockstepVisualRandPatch.Disabled = Environment.GetCommandLineArgs().Any(a => a == "-mpLockstepNoVisualIsolation");
            LockstepTickRatePatch.Disabled = Environment.GetCommandLineArgs().Any(a => a == "-mpLockstepVisibleTickRates");
            ticksPerFrame = GenCommandLine.TryGetCommandLineArg("mpLockstepTicksPerFrame", out string fixedValue) && int.TryParse(fixedValue, out int perFrame) ? Math.Max(0, perFrame) : 0;
            runs.Clear(); finished.Clear(); clock.Restart();
            LockstepRandTrace.Install();
            runs.AddRange(new[] { new RunSpec("A", true, 0), new RunSpec("B", true, 3), new RunSpec("C", false, 0), new RunSpec("D", false, 3),
                new RunSpec("A2", true, 0) });
        }

        /// <summary>One step per frame; true once the test is over (its checks are in the shared result list).</summary>
        internal static bool Step()
        {
            try
            {
                switch (stage)
                {
                    case 1: CheckSpeeds(); stage = 2; return false;
                    case 2: if (RunDriver()) stage = 3; return false;
                    case 3: return LoadForRun();
                    case 4: WaitForLoad(); return false;
                    case 5: return WaitForRun();
                }
                return true;
            }
            catch (Exception error)
            {
                Log.Error("Multiplayer Foundation lockstep self-test: FAIL " + error);
                Check("lockstep", false);
                Cleanup();
                return true;
            }
        }

        private static void Check(string name, bool ok) => results.Add(name + "=" + (ok ? "ok" : "FAIL"));
        private static float Since => UnityEngine.Time.realtimeSinceStartup - stepAt;
        private static void Next() { driverStep++; stepAt = UnityEngine.Time.realtimeSinceStartup; }

        // Vanilla 1.6 speeds against SessionSpeeds (60/180/360 ticks per second).
        private static void CheckSpeeds()
        {
            TickManager tm = Find.TickManager;
            var field = AccessTools.Field(typeof(TickManager), "curTimeSpeed");
            var seen = new List<string>();
            bool ok = true;
            foreach (SessionSpeed speed in new[] { SessionSpeed.Normal, SessionSpeed.Fast, SessionSpeed.Superfast })
            {
                field.SetValue(tm, LockstepDriver.ToTimeSpeed(speed));
                float multiplier = tm.TickRateMultiplier;
                seen.Add(speed + "=" + multiplier);
                ok &= Math.Abs(multiplier - SessionSpeeds.Multiplier(speed)) < 0.001f;
            }
            field.SetValue(tm, TimeSpeed.Paused);
            Check("speedTable", ok);
            results.Add("speeds(" + string.Join(" ", seen) + ")");
            // 1.6 pawn tick rates as vanilla gives them here (no driver yet): the session uses each pawn's minimum
            var pawns = Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned).ToList();
            string Span(IEnumerable<int> values) { var list = values.ToList(); return list.Count == 0 ? "-" : list.Min() + ".." + list.Max(); }
            results.Add("pawnRates(" + pawns.Count + " pawns, update " + Span(pawns.Select(p => p.UpdateRateTicks)) + ", min " +
                Span(pawns.Select(p => LockstepTickRatePatch.MinRate(p))) + ", max " + Span(pawns.Select(p => LockstepTickRatePatch.MaxRate(p))) + ")");
        }

        private static void Send(MessageType type, byte[] payload)
        {
            if (sequencer == null) return;
            switch (type)
            {
                case MessageType.LockstepCommand:
                    submitted++;
                    try { sequencer.Submit(HostId, LockstepCommand.Read(payload), clock.ElapsedMilliseconds); }
                    catch (LockstepRuleException) { refused++; }
                    break;
                case MessageType.ClientTick: sequencer.ReportTick(HostId, ClientTick.Read(payload)); break;
                case MessageType.StateHash:
                    StateHash hash = StateHash.Read(payload);
                    hostHashes.Add(hash);
                    sequencer.ReportHash(HostId, hash);
                    break;
            }
        }

        // The fake guest follows the host's game up to its own horizon unless frozen, and echoes the host's hashes.
        private static void Pump(LockstepDriver d)
        {
            int tick = d.Game.tickManager.TicksGame;
            if (d.State.Started && tick > d.State.Horizon) violations++;
            if (!guestFrozen) guestTick = Math.Max(guestTick, Math.Min(tick, guestHorizon));
            sequencer!.ReportTick(GuestId, new ClientTick(guestTick, 0));
            foreach (StateHash hash in hostHashes) sequencer.ReportHash(GuestId, new StateHash(hash.Tick, wrongGuestHashes ? hash.Hash ^ 1 : hash.Hash));
            hostHashes.Clear();
            foreach (var (to, message) in sequencer.Step(clock.ElapsedMilliseconds))
            {
                if (to == GuestId)
                {
                    if (message.Type == MessageType.TickHorizon) guestHorizon = TickHorizon.Read(message.Payload).Horizon;
                    else if (message.Type == MessageType.ScheduledCommand) toGuest.Add(ScheduledCommand.Read(message.Payload));
                    continue;
                }
                if (message.Type == MessageType.TickHorizon) d.State.OnHorizon(TickHorizon.Read(message.Payload));
                else if (message.Type == MessageType.ScheduledCommand) d.State.OnScheduled(ScheduledCommand.Read(message.Payload));
                else if (message.Type == MessageType.DesyncDetected) d.OnDesync(DesyncDetected.Read(message.Payload), "Test-Gast");
            }
        }

        private static bool RunDriver()
        {
            TickManager tm = Find.TickManager;
            if (driverStep == 0)
            {
                startTick = guestTick = guestHorizon = tm.TicksGame;
                sequencer = new LockstepSequencer(Guid.NewGuid(), HostId, new[] { (HostId, HostKey), (GuestId, GuestKey) });
                driver = new LockstepDriver(Current.Game, SessionSeed.FromSessionId("selftest"), true, HostKey, Send,
                    key => key == HostKey ? "Test-Host" : "Test-Gast");
                driver.CommandRan = (command, at) => ran.Add((command.Seq, command.Kind, command.Tick, at));
                ran.Clear(); toGuest.Clear(); hostHashes.Clear(); violations = submitted = refused = 0; guestFrozen = wrongGuestHashes = false;
                FoundationMod.Instance!.TestLockstep = driver;
                driver.Start();
                Next();
                return false;
            }
            LockstepDriver d = driver!;
            Pump(d);
            switch (driverStep)
            {
                case 1: // started, paused, nothing runs
                    if (Since < 1f) return false;
                    Check("startsPaused", d.State.Started && tm.TicksGame == startTick && tm.CurTimeSpeed == TimeSpeed.Paused);
                    tm.CurTimeSpeed = TimeSpeed.Normal; // vanilla's speed button
                    Check("setterIsRequest", tm.CurTimeSpeed == TimeSpeed.Paused && submitted == 1);
                    Next(); return false;
                case 2: // the speed command ran at its tick; time runs at about 60 ticks per second
                    if (d.Speed != SessionSpeed.Normal) return Since > 5f && Fail("speedApplied");
                    var go = ran.FirstOrDefault(r => r.Kind == LockstepCommandKind.SetSpeed);
                    Check("speedAtItsTick", go.Seq != 0 && go.RanAt == go.Tick - 1 && tm.CurTimeSpeed == TimeSpeed.Normal);
                    mark = tm.TicksGame; Next(); return false;
                case 3:
                    if (Since < 2f) return false;
                    float rate = (tm.TicksGame - mark) / Since;
                    results.Add("normalRate=" + rate.ToString("0") + "/s");
                    Check("runsAtNormal", rate > 40f && rate < 90f);
                    guestFrozen = true; mark = guestTick; Next(); return false;
                case 4: // the guest stops: the host stops two seconds of ticks ahead of it
                    if (Since < 3.5f) return false;
                    int ahead = tm.TicksGame - mark;
                    results.Add("aheadOfFrozenGuest=" + ahead);
                    Check("slowestGates", ahead >= 100 && ahead <= 120 && sequencer!.WaitingFor == GuestId && d.State.WaitingFor == GuestId);
                    guestFrozen = false; mark = tm.TicksGame; Next(); return false;
                case 5:
                    if (Since < 1f) return false;
                    Check("resumes", tm.TicksGame > mark + 20 && d.State.WaitingFor == Guid.Empty);
                    tm.TogglePaused(); // vanilla's space key
                    Next(); return false;
                case 6:
                    if (d.Speed != SessionSpeed.Paused) return Since > 5f && Fail("pauseApplied");
                    mark = tm.TicksGame; Next(); return false;
                case 7: // pause holds; marker; autosave; Ultrafast refused
                    if (Since < 1.5f) return false;
                    Check("pauseHolds", tm.TicksGame == mark && tm.CurTimeSpeed == TimeSpeed.Paused);
                    var stop = ran.LastOrDefault(r => r.Kind == LockstepCommandKind.SetSpeed);
                    Check("pauseAtItsTick", stop.RanAt == stop.Tick - 1 && stop.RanAt == mark);
                    d.SendMarker("selftest");
                    var ticksSinceSave = AccessTools.Field(typeof(Autosaver), "ticksSinceSave");
                    int savedBefore = (int)ticksSinceSave.GetValue(Current.Game.autosaver), skippedBefore = SessionAutosavePatch.Skipped;
                    Current.Game.autosaver.AutosaverTick();
                    Check("autosaveSkipped", (int)ticksSinceSave.GetValue(Current.Game.autosaver) == savedBefore && SessionAutosavePatch.Skipped == skippedBefore + 1);
                    int submittedBefore = submitted;
                    tm.CurTimeSpeed = TimeSpeed.Ultrafast;
                    Check("ultrafastRefused", submitted == submittedBefore && tm.CurTimeSpeed == TimeSpeed.Paused);
                    Next(); return false;
                case 8: // the marker ran for the host at its tick, and the guest got the same tick and seq
                    var marker = ran.FirstOrDefault(r => r.Kind == LockstepCommandKind.Marker);
                    if (marker.Seq == 0) return Since > 5f && Fail("markerRan");
                    ScheduledCommand? guestCopy = toGuest.FirstOrDefault(c => c.Seq == marker.Seq);
                    Check("markerTick", marker.RanAt == marker.Tick - 1 && guestCopy != null && guestCopy.Tick == marker.Tick &&
                        guestCopy.Kind == LockstepCommandKind.Marker && d.LastMarker.Contains("Test-Host"));
                    Check("hashesMatched", sequencer!.CheckedTick >= startTick && !sequencer.DesyncReported && d.DesyncTick == null);
                    results.Add("checkedTick=" + sequencer.CheckedTick + " hashes=" + (sequencer.CheckedTick - startTick) / LockstepDriver.HashInterval);
                    wrongGuestHashes = true;
                    tm.CurTimeSpeed = TimeSpeed.Superfast; // the host may pick the level
                    Next(); return false;
                case 9: // a guest hash that differs is reported within one hash interval
                    if (d.DesyncTick == null) return Since > 5f && Fail("desyncDetected");
                    Check("desyncDetected", d.DesyncTick > mark && d.Speed == SessionSpeed.Superfast);
                    tm.TogglePaused();
                    Next(); return false;
                case 10:
                    if (d.Speed != SessionSpeed.Paused) return Since > 5f && Fail("pausedAgain");
                    Check("neverPastHorizon", violations == 0);
                    Check("noMissedCommands", d.MissedCommands == 0 && refused == 0);
                    results.Add("driver(ticks=" + d.TicksRun + " commands=" + d.CommandsRun + " submitted=" + submitted + ")");
                    cmdStep = 0;
                    Next(); return false;
                case 11: // shared player actions (ADR 0025), while paused
                    if (!RunCommands(d)) return false;
                    StopDriver();
                    return true;
            }
            return false;
        }

        private static bool Fail(string name)
        {
            Check(name, false);
            StopDriver();
            driverStep = 0; stage = 3; runIndex = runs.Count; diagnosed = true; // skip the rest
            return false;
        }

        private static void StopDriver()
        {
            driver?.Stop();
            if (FoundationMod.Instance != null) FoundationMod.Instance.TestLockstep = null;
            driver = null; sequencer = null;
        }

        private static bool LoadForRun()
        {
            if (ticks > 0 && runIndex >= runs.Count && !diagnosed && finished.Count == runs.Count)
            {
                // the five runs are done; when two runs that should agree differ, record a pair like them in full
                diagnosed = true;
                int repeat = FirstDifference(finished[0].Hashes, finished[4].Hashes), noise = FirstDifference(finished[0].Hashes, finished[1].Hashes);
                if (repeat >= 0) runs.AddRange(new[] { new RunSpec("A-rec", true, 0, true), new RunSpec("A2-rec", true, 0, true) });
                else if (noise >= 0) runs.AddRange(new[] { new RunSpec("A-rec", true, 0, true), new RunSpec("B-rec", true, 3, true) });
            }
            if (ticks == 0 || runIndex >= runs.Count)
            {
                if (ticks > 0 && finished.Count >= 5) CompareRuns();
                Cleanup();
                return true;
            }
            before = Current.Game;
            stepAt = UnityEngine.Time.realtimeSinceStartup;
            GameDataSaveLoader.LoadGame(saveName);
            stage = 4;
            return false;
        }

        private static void WaitForLoad()
        {
            bool loaded = Current.ProgramState == ProgramState.Playing && !LongEventHandler.AnyEventNowOrWaiting && Find.Maps.Count > 0 &&
                !ReferenceEquals(Current.Game, before);
            if (!loaded)
            {
                if (Since > 120f) { Check("runLoaded", false); runIndex = runs.Count; diagnosed = true; stage = 3; }
                return;
            }
            RunSpec spec = runs[runIndex];
            RunResult result = current = new RunResult();
            runStart = Find.TickManager.TicksGame;
            foreach (Pawn pawn in Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned)) result.StartCells[pawn.thingIDNumber] = pawn.Position;
            pathsBefore = (LockstepPathRequestPatch.Answered, LockstepPathRequestPatch.Found, LockstepPathRequestPatch.NotReady);
            runDriver = new LockstepDriver(Current.Game, SessionSeed.FromSessionId("selftest"), true, HostKey, (type, payload) =>
            {
                if (type != MessageType.StateHash) return;
                StateHash hash = StateHash.Read(payload);
                result.Hashes.Add(hash);
                if (spec.Record && hash.Tick - runStart <= RecordThingsUpTo) result.Records[hash.Tick] = GameStateHash.Records(Current.Game);
            }, _ => "Test");
            runDriver.ReseedRand = spec.Reseed;
            runDriver.FixedTicksPerFrame = ticksPerFrame;
            LockstepRandTrace.Recording = spec.Record;
            LockstepRandTrace.CurrentTick.Clear();
            offThreadBefore = LockstepRandTrace.OffThreadDraws;
            if (spec.Record) runDriver.AfterTick = tick =>
            {
                result.Draws.Add((tick, LockstepRandTrace.Iterations, LockstepRandTrace.CurrentTick.ToArray()));
                LockstepRandTrace.CurrentTick.Clear();
            };
            int noise = spec.Noise;
            if (noise > 0) runDriver.BetweenTicks = () => { for (int i = 0; i < noise; i++) _ = Rand.Value; };
            // the control runs start from the same global state (the state setter, not Rand.Seed: vanilla logs an error for that)
            RandState.SetValue(null, 12345UL);
            // like a session: the speed command first, then the horizon for the whole run
            runDriver.State.OnScheduled(new ScheduledCommand(runStart + 1, 1, HostKey,
                new LockstepCommand(LockstepCommandKind.SetSpeed, LockstepCommandKind.Speed(SessionSpeed.Superfast))));
            runDriver.State.OnHorizon(new TickHorizon(runStart + ticks, SessionSpeed.Superfast, Guid.Empty, 0, -1));
            FoundationMod.Instance!.TestLockstep = runDriver;
            // the same player actions at the same ticks in every run (ADR 0025); built once, with the test driver installed
            if (runIndex == 0) BuildScript(Find.CurrentMap);
            QueueScript(runDriver, runStart);
            runDriver.Start();
            runWatch.Restart(); stepAt = UnityEngine.Time.realtimeSinceStartup;
            stage = 5;
        }

        private static bool WaitForRun()
        {
            LockstepDriver d = runDriver!;
            current.Frames++;
            if (Find.TickManager.TicksGame < runStart + ticks)
            {
                if (Since < 900f) return false;
                Check("runFinished", false); runIndex = runs.Count; diagnosed = true;
            }
            current.ScriptApplied = ScriptApplied(Find.CurrentMap);
            d.Stop();
            FoundationMod.Instance!.TestLockstep = null;
            current.Ms = runWatch.ElapsedMilliseconds;
            current.Moved = Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned).Count(p => current.StartCells.TryGetValue(p.thingIDNumber, out IntVec3 cell) && cell != p.Position);
            current.Answered = LockstepPathRequestPatch.Answered - pathsBefore.Answered; current.Found = LockstepPathRequestPatch.Found - pathsBefore.Found;
            current.NotReady = LockstepPathRequestPatch.NotReady - pathsBefore.NotReady;
            current.OffThread = LockstepRandTrace.OffThreadDraws - offThreadBefore;
            LockstepRandTrace.Recording = false;
            finished.Add(current);
            runDriver = null; runIndex++; stage = 3;
            return false;
        }

        private static void CompareRuns()
        {
            List<StateHash> a = finished[0].Hashes, b = finished[1].Hashes, c = finished[2].Hashes, dRun = finished[3].Hashes, a2 = finished[4].Hashes;
            int expected = ticks / LockstepDriver.HashInterval + 1;
            Check("hashesPerRun", finished.Take(5).All(f => f.Hashes.Count >= expected - 1 && f.Hashes.Count <= expected + 1));
            Check("startHashEqual", finished.All(f => f.Hashes.Count > 0 && f.Hashes[0].Hash == a[0].Hash));
            int repeatDiff = FirstDifference(a, a2), noiseDiff = FirstDifference(a, b), controlDiff = FirstDifference(c, dRun);
            string At(List<StateHash> run, int index) => index < 0 ? "-" : index < run.Count ? run[index].Tick.ToString() : "end";
            Check("repeatable", repeatDiff < 0);
            Check("deterministic", noiseDiff < 0);
            Check("controlDiffers", controlDiff >= 0);
            // the path patch must not stop the game: pawns move, every request is answered and ready at once
            Check("pawnsMove", finished[0].Moved > 0);
            if (ticks >= 1500) Check("scriptApplied", finished[0].ScriptApplied && finished[1].ScriptApplied);
            if (!asyncPaths) Check("pathsAnswered", finished[0].Answered > 0 && finished.All(f => f.NotReady == 0));
            results.Add("determinism(" + ticks + " ticks, " + a.Count + " hashes per run, script " + script.Count + " commands, asyncPaths=" + asyncPaths +
                ", visualIsolation=" + !LockstepVisualRandPatch.Disabled + " (" + LockstepVisualRandPatch.Isolated + " calls)" +
                ", tickRates=" + (LockstepTickRatePatch.Disabled ? "vanilla" : "fixed (" + LockstepTickRatePatch.Forced + " changed)") +
                ", ticksPerFrame=" + (ticksPerFrame == 0 ? "pacer" : ticksPerFrame.ToString()) +
                ", A vs A2 first differs at " + At(a, repeatDiff) + ", A vs B at " + At(a, noiseDiff) + ", control C vs D at " + At(c, controlDiff) +
                ", run A: " + finished[0].Moved + " of " + finished[0].StartCells.Count + " pawns moved, paths answered/found " + finished[0].Answered +
                "/" + finished[0].Found + ", with area " + LockstepPathRequestPatch.WithArea + ", draws from other threads " + string.Join("/", finished.Select(f => f.OffThread)) +
                ", ms/frames " + string.Join(" ", finished.Select((f, i) => runs[i].Name + "=" + f.Ms + "/" + f.Frames)) + ")");
            if (finished.Count >= 7) Diagnose(finished[5], finished[6], runs[5].Name, runs[6].Name);
        }

        // The first hash tick where the recorded pair differs, and which things differ there (with their values one hash
        // interval before, from the first run).
        private static void Diagnose(RunResult x, RunResult y, string xName, string yName)
        {
            DiagnoseDraws(x, y, xName, yName);
            int index = FirstDifference(x.Hashes, y.Hashes);
            if (index < 0 || index >= x.Hashes.Count || index >= y.Hashes.Count)
            {
                results.Add("diagnosis(" + xName + " vs " + yName + ": " + (index < 0 ? "no difference this time" : "different hash counts") + ")");
                return;
            }
            int tick = x.Hashes[index].Tick;
            if (!x.Records.TryGetValue(tick, out var left) || !y.Records.TryGetValue(tick, out var right)) { results.Add("diagnosis(no records at " + tick + ")"); return; }
            x.Records.TryGetValue(tick - LockstepDriver.HashInterval, out var earlier);
            var lines = new List<string>();
            int differing = 0, i = 0, j = 0;
            string Show(GameStateHash.ThingRecord? record, Dictionary<int, string>? pawns) => record == null ? "(missing)" :
                record.Value + (pawns != null && pawns.TryGetValue(record.Value.Id, out string? pawn) ? " " + pawn : "");
            GameStateHash.ThingRecord? Before(int id)
            {
                if (earlier.Things == null) return null;
                int at = Array.FindIndex(earlier.Things, r => r.Id == id);
                return at < 0 ? (GameStateHash.ThingRecord?)null : earlier.Things[at];
            }
            while (i < left.Things.Length || j < right.Things.Length)
            {
                GameStateHash.ThingRecord? a = i < left.Things.Length ? left.Things[i] : (GameStateHash.ThingRecord?)null;
                GameStateHash.ThingRecord? b = j < right.Things.Length ? right.Things[j] : (GameStateHash.ThingRecord?)null;
                int id;
                if (b == null || (a != null && a.Value.Id < b.Value.Id)) { id = a!.Value.Id; b = null; i++; }
                else if (a == null || b.Value.Id < a.Value.Id) { id = b.Value.Id; a = null; j++; }
                else { id = a.Value.Id; i++; j++; if (a.Value.SameAs(b.Value)) continue; }
                differing++;
                if (lines.Count < 20)
                    lines.Add(id + ": " + Show(a, left.Pawns) + "  <>  " + Show(b, right.Pawns) + "  | before: " + Show(Before(id), earlier.Pawns));
            }
            Log.Warning("Multiplayer Foundation lockstep self-test: " + xName + " vs " + yName + " first differ at tick " + tick + ": " + differing +
                " of " + left.Things.Length + " things differ" + (lines.Count == 0 ? " (records equal: the tick or map values differ)" : ":\n" + string.Join("\n", lines)));
            results.Add("diagnosis(" + xName + " vs " + yName + " at tick " + tick + ": " + differing + " things differ)");
        }

        // The first tick whose Rand draws differ (count or call sites), and the call sites around the first differing draw.
        private static void DiagnoseDraws(RunResult x, RunResult y, string xName, string yName)
        {
            int n = Math.Min(x.Draws.Count, y.Draws.Count);
            for (int i = 0; i < n; i++)
            {
                var (tick, xIterations, xCalls) = x.Draws[i];
                var (yTick, yIterations, yCalls) = y.Draws[i];
                int k = 0;
                while (k < xCalls.Length && k < yCalls.Length && xCalls[k] == yCalls[k]) k++;
                if (tick == yTick && xIterations == yIterations && k == xCalls.Length && k == yCalls.Length) continue;
                string Around(int[] calls) => string.Join(" | ", Enumerable.Range(Math.Max(0, k - 3), Math.Min(calls.Length, k + 4) - Math.Max(0, k - 3))
                    .Select(c => (c == k ? ">> " : "") + LockstepRandTrace.Name(calls[c])));
                Log.Warning("Multiplayer Foundation lockstep self-test: Rand draws of " + xName + " and " + yName + " first differ in tick " + tick +
                    ": stream positions " + xIterations + " / " + yIterations + ", traced draws " + xCalls.Length + " / " + yCalls.Length +
                    ", first different draw #" + k + "\n" + xName + ": " + Around(xCalls) + "\n" + yName + ": " + Around(yCalls));
                results.Add("randDiagnosis(tick " + tick + ", draw #" + k + ", " + (k < xCalls.Length ? LockstepRandTrace.Name(xCalls[k]) : "end") +
                    " vs " + (k < yCalls.Length ? LockstepRandTrace.Name(yCalls[k]) : "end") + ")");
                return;
            }
            results.Add("randDiagnosis(" + (x.Draws.Count == 0 ? "not recorded" : "draws equal in all " + n + " ticks") + ")");
        }

        private static int FirstDifference(List<StateHash> x, List<StateHash> y)
        {
            int n = Math.Min(x.Count, y.Count);
            for (int i = 0; i < n; i++) if (x[i].Tick != y[i].Tick || x[i].Hash != y[i].Hash) return i;
            return x.Count == y.Count ? -1 : n;
        }

        private static void Cleanup()
        {
            StopDriver();
            runDriver?.Stop();
            runDriver = null;
            LockstepPathRequestPatch.Disabled = false;
            LockstepVisualRandPatch.Disabled = false;
            LockstepTickRatePatch.Disabled = false;
            // game loading draws from its own thread; only draws while the runs tick would race with the game
            if (finished.Any(f => f.OffThread > 0))
                Log.Warning("Multiplayer Foundation lockstep self-test: " + finished.Sum(f => f.OffThread) + " Rand draws from other threads " +
                    "during the runs; first samples of the whole test:\n" + string.Join("\n---\n", LockstepRandTrace.OffThreadSamples));
            LockstepRandTrace.Uninstall();
            if (FoundationMod.Instance != null) FoundationMod.Instance.TestLockstep = null;
            Log.Message("Multiplayer Foundation lockstep self-test: " +
                (results.Any(r => r.EndsWith("FAIL")) ? "FAIL" : "PASS") + " (see the game self-test line)");
            stage = 0;
        }
    }
}
