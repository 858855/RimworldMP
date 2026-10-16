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
    /// SYNC-002 (ADR 0025): player actions as lockstep commands.
    /// - Capture: a patched entry point called by the player (in a session, outside ticks and long events, not while a
    ///   command runs) queues an entry and skips the original. Entries of one frame are sent at the next frame, consecutive
    ///   entries of one kind in one command (selecting 20 items and forbidding them is one command, not 20).
    /// - Execution: every player, the issuer too, runs the entries at the command's tick through the original methods.
    /// </summary>
    internal static class LockstepCommands
    {
        internal const ushort Draft = 16, FireAtWill = 17, OrderedJob = 18, Designate = 19, Forbid = 20, WorkPriority = 21,
            Schedule = 22, Research = 23, GrowingPlant = 24;
        private const int MaximumCommandBytes = 30 * 1024;
        private const byte ModeCell = 1, ModeCells = 2, ModeThing = 3;

        /// <summary>True while a command runs: the patched entry points let the original through.</summary>
        internal static bool Executing;
        internal static int Captured, Sent, ExecutedEntries, MissingEntries, NotShared;
        internal static string LastNotShared = "";

        private static readonly List<(ushort Kind, byte[] Entry)> pending = new List<(ushort, byte[])>();
        private static float nextNotSharedMessageAt;

        internal static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> JobsPawn = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");
        internal static readonly AccessTools.FieldRef<Pawn_WorkSettings, Pawn> WorkPawn = AccessTools.FieldRefAccess<Pawn_WorkSettings, Pawn>("pawn");
        internal static readonly AccessTools.FieldRef<Pawn_TimetableTracker, Pawn> TimetablePawn = AccessTools.FieldRefAccess<Pawn_TimetableTracker, Pawn>("pawn");

        /// <summary>A call from the player's side of the game (UI), not from the simulation.</summary>
        internal static bool InPlayerContext => FoundationMod.Instance?.Lockstep != null && !LockstepDriver.Ticking && !Executing &&
            !LongEventHandler.AnyEventNowOrWaiting && Current.ProgramState == ProgramState.Playing;

        internal static int PendingCount => pending.Count;

        // ---- capture ----

        /// <summary>Queues an entry; true means the caller skips the original.</summary>
        internal static bool Capture(ushort kind, Action<SyncWriter> write)
        {
            if (!InPlayerContext) return false;
            var writer = new SyncWriter();
            write(writer);
            pending.Add((kind, writer.ToArray()));
            Captured++;
            return true;
        }

        /// <summary>An action the player took that this build cannot share yet: it runs locally, and the hash will notice.</summary>
        internal static void NoteNotShared(string what)
        {
            NotShared++;
            LastNotShared = what;
            Log.Message("Multiplayer Foundation: not shared yet, runs only here: " + what);
            if (UnityEngine.Time.realtimeSinceStartup < nextNotSharedMessageAt) return;
            nextNotSharedMessageAt = UnityEngine.Time.realtimeSinceStartup + 5f;
            Messages.Message("Wird noch nicht geteilt und gilt nur bei dir: " + what, MessageTypeDefOf.CautionInput, false);
        }

        /// <summary>Called once per frame: sends what the player did in the last frame, in order.</summary>
        internal static void Flush(LockstepDriver? driver)
        {
            if (pending.Count == 0) return;
            if (driver == null) { pending.Clear(); return; }
            int start = 0;
            while (start < pending.Count)
            {
                ushort kind = pending[start].Kind;
                int end = start, bytes = 0;
                while (end < pending.Count && pending[end].Kind == kind && end - start < ushort.MaxValue &&
                       (end == start || bytes + pending[end].Entry.Length <= MaximumCommandBytes))
                { bytes += pending[end].Entry.Length; end++; }
                var writer = new SyncWriter();
                writer.WriteUShort((ushort)(end - start));
                var combined = new List<byte>(writer.ToArray());
                for (int i = start; i < end; i++) combined.AddRange(pending[i].Entry);
                if (combined.Count <= LockstepCommand.MaximumPayloadBytes)
                {
                    driver.SendCommand(kind, combined.ToArray());
                    Sent++;
                }
                else Log.Warning("Multiplayer Foundation: a player action was too large to share (" + combined.Count + " bytes); dropped");
                start = end;
            }
            pending.Clear();
        }

        // ---- execution ----

        /// <summary>Runs every entry of a game command; an entry whose references are gone is a no-op on every player.</summary>
        internal static void Execute(ScheduledCommand command, LockstepDriver driver)
        {
            var reader = new SyncReader(command.Payload);
            int count = reader.ReadUShort();
            for (int i = 0; i < count; i++)
            {
                var read = new SyncRead(reader);
                bool ran;
                // reading counts as running: a job or designator built from the payload takes shared IDs, like the original call
                Executing = true;
                try { ran = ExecuteEntry(command, read, driver); }
                finally { Executing = false; }
                if (ran) ExecutedEntries++;
                else MissingEntries++;
            }
            reader.EnsureComplete();
        }

        private static bool ExecuteEntry(ScheduledCommand command, SyncRead r, LockstepDriver driver)
        {
            switch (command.Kind)
            {
                case Draft:
                case FireAtWill:
                {
                    Pawn? pawn = r.ReadPawn();
                    bool value = r.Reader.ReadBool();
                    if (r.Missing || pawn?.drafter == null) return false;
                    return RunOn(pawn.MapHeld, () =>
                    {
                        if (command.Kind == Draft) pawn.drafter.Drafted = value;
                        else pawn.drafter.FireAtWill = value;
                    });
                }
                case OrderedJob:
                {
                    Pawn? pawn = r.ReadPawn();
                    Job? job = r.ReadJob();
                    byte tag = r.Reader.ReadByte();
                    bool queueing = r.Reader.ReadBool();
                    if (r.Missing || pawn?.jobs == null || job == null) return false;
                    return RunOn(pawn.MapHeld, () => pawn.jobs.TryTakeOrderedJob(job, tag == byte.MaxValue ? (JobTag?)null : (JobTag)tag, queueing));
                }
                case Designate:
                {
                    Map? map = r.ReadMap();
                    var (designator, zone, area) = r.ReadDesignator();
                    byte mode = r.Reader.ReadByte();
                    IntVec3 cell = IntVec3.Invalid;
                    List<IntVec3>? cells = null;
                    Thing? thing = null;
                    if (mode == ModeCell) cell = r.ReadCell();
                    else if (mode == ModeCells) cells = r.ReadCells();
                    else if (mode == ModeThing) thing = r.ReadThing();
                    else throw new ProtocolException("Unknown designation mode.");
                    if (r.Missing || map == null || designator == null) return false;
                    return RunOn(map, () => Designation(designator, zone, area, mode, cell, cells, thing, map, command.Issuer == driver.LocalKey));
                }
                case Forbid:
                {
                    Thing? thing = r.ReadThing();
                    bool value = r.Reader.ReadBool();
                    CompForbiddable? comp = thing?.TryGetComp<CompForbiddable>();
                    if (r.Missing || comp == null) return false;
                    return RunOn(thing!.MapHeld, () => comp.Forbidden = value);
                }
                case WorkPriority:
                {
                    Pawn? pawn = r.ReadPawn();
                    WorkTypeDef? work = r.ReadDef<WorkTypeDef>();
                    int priority = r.Reader.ReadInt();
                    if (r.Missing || pawn?.workSettings == null || work == null) return false;
                    return RunOn(pawn.MapHeld, () => pawn.workSettings.SetPriority(work, priority));
                }
                case Schedule:
                {
                    Pawn? pawn = r.ReadPawn();
                    int hour = r.Reader.ReadInt();
                    TimeAssignmentDef? assignment = r.ReadDef<TimeAssignmentDef>();
                    if (r.Missing || pawn?.timetable == null || assignment == null || hour < 0 || hour > 23) return false;
                    return RunOn(pawn.MapHeld, () => pawn.timetable.SetAssignment(hour, assignment));
                }
                case Research:
                {
                    bool start = r.Reader.ReadBool();
                    ResearchProjectDef? project = r.ReadDef<ResearchProjectDef>();
                    if (r.Missing) return false;
                    return RunOn(null, () =>
                    {
                        if (start) Find.ResearchManager.SetCurrentProject(project);
                        else Find.ResearchManager.StopProject(project);
                    });
                }
                case GrowingPlant:
                {
                    var zone = r.ReadZone() as Zone_Growing;
                    ThingDef? plant = r.ReadDef<ThingDef>();
                    if (r.Missing || zone == null || plant == null) return false;
                    return RunOn(zone.Map, () => zone.SetPlantDefToGrow(plant));
                }
                default:
                    Log.Message("Multiplayer Foundation: lockstep command kind " + command.Kind + " is not supported by this build; ignored");
                    return false;
            }
        }

        // The command's map is the current map while it runs (designators and some jobs read Find.CurrentMap); the
        // player's view does not change.
        private static bool RunOn(Map? map, Action action)
        {
            sbyte before = Current.Game.currentMapIndex;
            int index = map == null ? -1 : Find.Maps.IndexOf(map);
            if (index >= 0) Current.Game.currentMapIndex = (sbyte)index;
            bool wasExecuting = Executing;
            Executing = true;
            try { action(); }
            finally
            {
                Executing = wasExecuting;
                Current.Game.currentMapIndex = before;
            }
            return true;
        }

        // Zones expand the selected zone and areas paint the selected area: both are set to the command's for the call.
        // The issuer keeps vanilla's selection afterwards (a new zone stays selected for the next drag); others get theirs back.
        private static void Designation(Designator designator, Zone? zone, Area? area, byte mode, IntVec3 cell, List<IntVec3>? cells,
            Thing? thing, Map map, bool issuedHere)
        {
            List<object>? selection = null;
            Area? areaBefore = Designator_AreaAllowed.selectedArea;
            if (designator is Designator_ZoneAdd)
            {
                selection = Find.Selector.SelectedObjects.ToList();
                Find.Selector.ClearSelection();
                if (zone != null) Find.Selector.Select(zone, false, false);
            }
            if (designator is Designator_AreaAllowed) Designator_AreaAllowed.selectedArea = area;
            try
            {
                if (mode == ModeCell) { if (cell.InBounds(map) && designator.CanDesignateCell(cell).Accepted) designator.DesignateSingleCell(cell); }
                else if (mode == ModeCells) designator.DesignateMultiCell(cells!.Where(c => c.InBounds(map)).ToList());
                else if (thing != null && designator.CanDesignateThing(thing).Accepted) designator.DesignateThing(thing);
            }
            finally
            {
                if (designator is Designator_AreaAllowed) Designator_AreaAllowed.selectedArea = areaBefore;
                if (selection != null && !issuedHere)
                {
                    Find.Selector.ClearSelection();
                    foreach (object selected in selection)
                        if (selected is Thing t ? t.Spawned : selected is Zone z && z.zoneManager != null) Find.Selector.Select(selected, false, false);
                }
            }
        }

        // ---- entries written by the capture patches ----

        internal static bool CaptureDesignation(Designator designator, string method, object argument)
        {
            if (!InPlayerContext) return false;
            string? reason = SyncWorkers.DesignatorNotShareable(designator);
            if (reason != null) { NoteNotShared(reason); return false; }
            Map map = designator.Map;
            return Capture(Designate, w =>
            {
                w.WriteMap(map);
                w.WriteDesignator(designator);
                switch (method)
                {
                    case nameof(Designator.DesignateSingleCell): w.WriteByte(ModeCell); w.WriteCell((IntVec3)argument); break;
                    case nameof(Designator.DesignateMultiCell): w.WriteByte(ModeCells); w.WriteCells(((IEnumerable<IntVec3>)argument).ToList()); break;
                    default: w.WriteByte(ModeThing); w.WriteThing((Thing)argument); break;
                }
            });
        }

        internal static bool CaptureOrder(Pawn pawn, Job job, JobTag? tag, bool requestQueueing)
        {
            if (!InPlayerContext) return false;
            string? field = SyncWorkers.JobNotShareable(job);
            if (field != null) { NoteNotShared("Befehl " + job.def.defName + " (" + field + ")"); return false; }
            return Capture(OrderedJob, w =>
            {
                w.WriteThing(pawn);
                w.WriteJob(job);
                w.WriteByte(tag.HasValue ? (byte)tag.Value : byte.MaxValue);
                w.WriteBool(requestQueueing);
            });
        }
    }
}
