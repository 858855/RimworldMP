using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;
using Verse.AI;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SYNC-002 (ADR 0025) part of the lockstep self-test: shared player actions. Every action is called the way the
    /// game's UI calls it; it must be captured (no local change yet), come back from the in-process sequencer and then
    /// show its effect. The determinism runs replay a fixed script of the same commands.
    /// </summary>
    internal static partial class LockstepSelfTest
    {
        private static readonly AccessTools.FieldRef<Designator_Build, ThingDef> TestBuildStuff = AccessTools.FieldRefAccess<Designator_Build, ThingDef>("stuffDef");
        private static readonly AccessTools.FieldRef<Designator_Place, Rot4> TestPlaceRotation = AccessTools.FieldRefAccess<Designator_Place, Rot4>("placingRot");

        private static int cmdStep;
        private static Map? cmdMap;
        private static Pawn? cmdPawn;
        private static IntVec3 cmdDest, mineCell, buildCell;
        private static List<IntVec3> stockCells = new List<IntVec3>(), expandCells = new List<IntVec3>(), growCells = new List<IntVec3>();
        private static Zone_Stockpile? stockZone;
        private static Zone_Growing? growZone;
        private static ThingDef? plant;
        private static Thing? forbidThing;
        private static WorkTypeDef? work;
        private static TimeAssignmentDef? hourAssignment;
        private static ResearchProjectDef? project;
        private static int capturedBefore, missingBefore, notSharedBefore, executedBefore;

        /// <summary>One step per frame while the session is paused; true when the command stage is done.</summary>
        private static bool RunCommands(LockstepDriver d)
        {
            switch (cmdStep)
            {
                case 0:
                {
                    cmdMap = Find.CurrentMap;
                    cmdPawn = cmdMap.mapPawns.FreeColonistsSpawned.OrderBy(p => p.thingIDNumber).First();
                    Find.Selector.ClearSelection();
                    capturedBefore = LockstepCommands.Captured; missingBefore = LockstepCommands.MissingEntries;
                    notSharedBefore = LockstepCommands.NotShared; executedBefore = LockstepCommands.ExecutedEntries;
                    cmdPawn.drafter.Drafted = true; // the draft button
                    Check("cmdCapturedNotLocal", !cmdPawn.Drafted && LockstepCommands.Captured == capturedBefore + 1);
                    return Advance();
                }
                case 1:
                {
                    if (!cmdPawn!.Drafted) return Timeout("cmdDraft");
                    Check("cmdDraft", true);
                    cmdDest = Around(cmdPawn.Position, cmdMap!, 8).First(c => c.Standable(cmdMap!) && Distance(c, cmdPawn.Position) >= 16);
                    int idsBefore = LocalIdsPatch.Given;
                    Job menuJob = JobMaker.MakeJob(JobDefOf.Goto, cmdDest); // what a right-click menu builds
                    Check("localIds", menuJob.loadID < 0 && LocalIdsPatch.Given > idsBefore);
                    cmdPawn.jobs.TryTakeOrderedJob(menuJob, JobTag.Misc, false);
                    return Advance();
                }
                case 2:
                {
                    if (cmdPawn!.CurJobDef != JobDefOf.Goto || cmdPawn.CurJob.targetA.Cell != cmdDest) return Timeout("cmdOrder");
                    Check("cmdOrder", true);
                    Check("sharedJobId", cmdPawn.CurJob.loadID >= 0); // made while the command ran: a shared ID
                    PickCells(cmdMap!, cmdPawn.Position, cmdDest);
                    new Designator_Mine().DesignateSingleCell(mineCell);
                    var build = new Designator_Build(ThingDefOf.Bed);
                    TestBuildStuff(build) = ThingDefOf.WoodLog;
                    TestPlaceRotation(build) = Rot4.East;
                    build.DesignateSingleCell(buildCell);
                    Find.Selector.ClearSelection();
                    new Designator_ZoneAddStockpile_Resources().DesignateMultiCell(stockCells);
                    Check("designationsNotLocal", cmdMap!.designationManager.DesignationAt(mineCell, DesignationDefOf.Mine) == null &&
                        Blueprint(buildCell) == null && cmdMap.zoneManager.ZoneAt(stockCells[0]) == null);
                    return Advance();
                }
                case 3:
                {
                    bool mined = cmdMap!.designationManager.DesignationAt(mineCell, DesignationDefOf.Mine) != null;
                    Blueprint_Build? blueprint = Blueprint(buildCell);
                    stockZone = cmdMap.zoneManager.ZoneAt(stockCells[0]) as Zone_Stockpile;
                    if (!mined || blueprint == null || stockZone == null) return Timeout("cmdDesignations");
                    Check("cmdMine", true);
                    Check("cmdBuild", blueprint.def.entityDefToBuild == ThingDefOf.Bed && blueprint.stuffToUse == ThingDefOf.WoodLog &&
                        blueprint.Rotation == Rot4.East);
                    Check("cmdZone", stockCells.All(c => stockZone.ContainsCell(c)));
                    // expanding: the selected zone, as in the game
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(stockZone, false, false);
                    new Designator_ZoneAddStockpile_Resources().DesignateMultiCell(expandCells);
                    return Advance();
                }
                case 4:
                {
                    if (!expandCells.All(c => stockZone!.ContainsCell(c))) return Timeout("cmdZoneExpand");
                    Check("cmdZoneExpand", cmdMap!.zoneManager.ZoneAt(expandCells[0]) == stockZone);
                    Find.Selector.ClearSelection();
                    new Designator_ZoneAdd_Growing().DesignateMultiCell(growCells);
                    return Advance();
                }
                case 5:
                {
                    growZone = cmdMap!.zoneManager.ZoneAt(growCells[0]) as Zone_Growing;
                    if (growZone == null) return Timeout("cmdGrowingZone");
                    plant = growZone.GetPlantDefToGrow() == ThingDefOf.Plant_Potato
                        ? DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Rice") ?? ThingDefOf.Plant_Potato : ThingDefOf.Plant_Potato;
                    growZone.SetPlantDefToGrow(plant);
                    return Advance();
                }
                case 6:
                {
                    if (growZone!.GetPlantDefToGrow() != plant) return Timeout("cmdGrowing");
                    Check("cmdGrowing", true);
                    forbidThing = cmdMap!.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                        .FirstOrDefault(t => t.TryGetComp<CompForbiddable>() != null && !t.IsForbidden(Faction.OfPlayer));
                    forbidThing?.SetForbidden(true, false);
                    work = DefDatabase<WorkTypeDef>.AllDefsListForReading.FirstOrDefault(w => cmdPawn!.workSettings.GetPriority(w) > 0);
                    if (work != null) cmdPawn!.workSettings.SetPriority(work, 0);
                    hourAssignment = cmdPawn!.timetable.GetAssignment(5) == TimeAssignmentDefOf.Joy ? TimeAssignmentDefOf.Work : TimeAssignmentDefOf.Joy;
                    cmdPawn.timetable.SetAssignment(5, hourAssignment);
                    project = DefDatabase<ResearchProjectDef>.AllDefsListForReading.FirstOrDefault(r => r.CanStartNow && !Find.ResearchManager.IsCurrentProject(r));
                    if (project != null) Find.ResearchManager.SetCurrentProject(project);
                    cmdPawn.drafter.Drafted = false;
                    return Advance();
                }
                case 7:
                {
                    bool forbidden = forbidThing == null || forbidThing.IsForbidden(Faction.OfPlayer);
                    bool priority = work == null || cmdPawn!.workSettings.GetPriority(work) == 0;
                    bool schedule = cmdPawn!.timetable.GetAssignment(5) == hourAssignment;
                    bool research = project == null || Find.ResearchManager.IsCurrentProject(project);
                    if (!(forbidden && priority && schedule && research && !cmdPawn.Drafted) && Since < 5f) return false;
                    Check("cmdForbid", forbidThing != null && forbidden);
                    Check("cmdPriority", work != null && priority);
                    Check("cmdSchedule", schedule);
                    Check("cmdResearch", project != null && research);
                    Check("cmdUndraft", !cmdPawn.Drafted);
                    // a command whose pawn does not exist: a no-op for everyone
                    sequencer!.Submit(HostId, new LockstepCommand(LockstepCommands.Draft, Entry(w =>
                    { w.WriteInt(999_999_999); w.WriteMap(cmdMap); w.WriteBool(true); })), clock.ElapsedMilliseconds);
                    return Advance();
                }
                case 8:
                {
                    if (LockstepCommands.MissingEntries == missingBefore) return Timeout("cmdMissingNoop");
                    Check("cmdMissingNoop", true);
                    var hostGame = ran.Where(r => r.Kind >= LockstepCommandKind.FirstGameKind).Select(r => (r.Seq, r.Tick)).ToList();
                    var guestGame = toGuest.Where(c => c.Kind >= LockstepCommandKind.FirstGameKind).Select(c => (c.Seq, c.Tick)).ToList();
                    Check("cmdSameForGuest", hostGame.Count > 0 && hostGame.SequenceEqual(guestGame));
                    Check("cmdAllShared", LockstepCommands.NotShared == notSharedBefore);
                    results.Add("commands(captured=" + (LockstepCommands.Captured - capturedBefore) + " sent=" + LockstepCommands.Sent +
                        " entries=" + (LockstepCommands.ExecutedEntries - executedBefore) + " missing=" + (LockstepCommands.MissingEntries - missingBefore) +
                        " localIds=" + LocalIdsPatch.Given + ")");
                    return true;
                }
            }
            return true;
        }

        private static bool Advance() { cmdStep++; stepAt = UnityEngine.Time.realtimeSinceStartup; return false; }

        private static bool Timeout(string name)
        {
            if (Since < 5f) return false;
            Check(name, false);
            cmdStep = 99;
            return true;
        }

        private static Blueprint_Build? Blueprint(IntVec3 cell) => cell.GetThingList(cmdMap!).OfType<Blueprint_Build>().FirstOrDefault();

        private static int Distance(IntVec3 a, IntVec3 b) => (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z);

        private static IEnumerable<IntVec3> Around(IntVec3 center, Map map, int radius)
        {
            var cells = new List<IntVec3>();
            for (int x = center.x - radius; x <= center.x + radius; x++)
                for (int z = center.z - radius; z <= center.z + radius; z++)
                {
                    var c = new IntVec3(x, 0, z);
                    if (c.InBounds(map)) cells.Add(c);
                }
            return cells.OrderBy(c => Distance(c, center)).ThenBy(c => c.z).ThenBy(c => c.x);
        }

        private static List<IntVec3> Block(IntVec3 origin, int width, int height) =>
            Enumerable.Range(0, width).SelectMany(dx => Enumerable.Range(0, height).Select(dz => new IntVec3(origin.x + dx, 0, origin.z + dz))).ToList();

        // Cells for the designations, apart from each other: a rock to mine, a bed (1 x 2), a 3 x 3 stockpile with a
        // column to expand it, a 2 x 2 growing zone.
        private static void PickCells(Map map, IntVec3 near, IntVec3 dest)
        {
            var taken = new List<IntVec3> { dest };
            bool Free(IntVec3 c) => taken.All(t => Distance(t, c) > 16);
            var mine = new Designator_Mine();
            mineCell = Around(near, map, 60).First(c => mine.CanDesignateCell(c).Accepted);
            taken.Add(mineCell);
            var build = new Designator_Build(ThingDefOf.Bed);
            TestBuildStuff(build) = ThingDefOf.WoodLog;
            TestPlaceRotation(build) = Rot4.East;
            buildCell = Around(near, map, 30).First(c => Free(c) && build.CanDesignateCell(c).Accepted);
            taken.Add(buildCell);
            var stockpile = new Designator_ZoneAddStockpile_Resources();
            IntVec3 stockOrigin = Around(near, map, 40).First(o => Block(o, 4, 3).All(c => Free(c) && stockpile.CanDesignateCell(c).Accepted));
            stockCells = Block(stockOrigin, 3, 3);
            expandCells = Block(new IntVec3(stockOrigin.x + 3, 0, stockOrigin.z), 1, 3);
            taken.AddRange(Block(stockOrigin, 4, 3));
            var growing = new Designator_ZoneAdd_Growing();
            IntVec3 growOrigin = Around(near, map, 50).First(o => Block(o, 2, 2).All(c => Free(c) && growing.CanDesignateCell(c).Accepted));
            growCells = Block(growOrigin, 2, 2);
        }

        /// <summary>A game command payload with one entry, as LockstepCommands.Flush writes it.</summary>
        private static byte[] Entry(Action<SyncWriter> write)
        {
            var writer = new SyncWriter();
            writer.WriteUShort(1);
            write(writer);
            return writer.ToArray();
        }

        // ---- the script of the determinism runs ----

        private static List<(int AtTick, ushort Kind, byte[] Payload)> script = new List<(int, ushort, byte[])>();
        private static IntVec3 scriptStockCell;

        /// <summary>
        /// Built once in the first run, from the loaded save (the same IDs in every run). Built while the test driver is
        /// installed, so the job it serializes takes a local ID and the game's shared counters stay as in the other runs.
        /// </summary>
        private static void BuildScript(Map map)
        {
            Pawn pawn = map.mapPawns.FreeColonistsSpawned.OrderBy(p => p.thingIDNumber).First();
            IntVec3 dest = Around(pawn.Position, map, 8).First(c => c.Standable(map) && Distance(c, pawn.Position) >= 16);
            PickCells(map, pawn.Position, dest);
            scriptStockCell = stockCells[0];
            Find.Selector.ClearSelection();
            var build = new Designator_Build(ThingDefOf.Bed);
            TestBuildStuff(build) = ThingDefOf.WoodLog;
            TestPlaceRotation(build) = Rot4.East;
            Thing? item = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                .FirstOrDefault(t => t.TryGetComp<CompForbiddable>() != null && !t.IsForbidden(Faction.OfPlayer));
            WorkTypeDef? workType = DefDatabase<WorkTypeDef>.AllDefsListForReading.FirstOrDefault(w => pawn.workSettings.GetPriority(w) > 0);
            ResearchProjectDef? research = DefDatabase<ResearchProjectDef>.AllDefsListForReading.FirstOrDefault(r => r.CanStartNow);
            Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
            script = new List<(int, ushort, byte[])>
            {
                (100, LockstepCommands.Draft, Entry(w => { w.WriteThing(pawn); w.WriteBool(true); })),
                (100, LockstepCommands.OrderedJob, Entry(w => { w.WriteThing(pawn); w.WriteJob(job); w.WriteByte((byte)JobTag.Misc); w.WriteBool(false); })),
                (300, LockstepCommands.Designate, Entry(w => { w.WriteMap(map); w.WriteDesignator(new Designator_Mine()); w.WriteByte(1); w.WriteCell(mineCell); })),
                (300, LockstepCommands.Designate, Entry(w => { w.WriteMap(map); w.WriteDesignator(build); w.WriteByte(1); w.WriteCell(buildCell); })),
                (300, LockstepCommands.Designate, Entry(w =>
                    { w.WriteMap(map); w.WriteDesignator(new Designator_ZoneAddStockpile_Resources()); w.WriteByte(2); w.WriteCells(stockCells); })),
                (1500, LockstepCommands.Draft, Entry(w => { w.WriteThing(pawn); w.WriteBool(false); }))
            };
            if (item != null) script.Add((600, LockstepCommands.Forbid, Entry(w => { w.WriteThing(item); w.WriteBool(true); })));
            if (workType != null) script.Add((600, LockstepCommands.WorkPriority, Entry(w => { w.WriteThing(pawn); w.WriteDef(workType); w.WriteInt(0); })));
            script.Add((600, LockstepCommands.Schedule, Entry(w => { w.WriteThing(pawn); w.WriteInt(5); w.WriteDef(TimeAssignmentDefOf.Joy); })));
            if (research != null) script.Add((600, LockstepCommands.Research, Entry(w => { w.WriteBool(true); w.WriteDef(research); })));
            script = script.OrderBy(s => s.AtTick).ToList();
        }

        /// <summary>The script as scheduled commands after the speed command (seq 1).</summary>
        private static void QueueScript(LockstepDriver driver, int start)
        {
            uint seq = 2;
            foreach (var (atTick, kind, payload) in script)
                driver.State.OnScheduled(new ScheduledCommand(start + atTick, seq++, HostKey, new LockstepCommand(kind, payload)));
        }

        private static bool ScriptApplied(Map map) => map.zoneManager.ZoneAt(scriptStockCell) is Zone_Stockpile;
    }
}
