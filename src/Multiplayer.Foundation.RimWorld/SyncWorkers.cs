using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;
using Verse.AI;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SYNC-002 (ADR 0025, plan §7.3): game references in command payloads. Things and pawns go by ID and map, defs by
    /// name, zones and areas by ID. Reading notes a reference that no longer resolves in <see cref="SyncRead.Missing"/>;
    /// the command is then a no-op on every player.
    /// </summary>
    internal sealed class SyncRead
    {
        public SyncRead(SyncReader reader) { Reader = reader; }
        public SyncReader Reader { get; }
        public bool Missing { get; set; }
    }

    internal static class SyncWorkers
    {
        // ---- defs, maps, cells ----

        internal static void WriteDef<T>(this SyncWriter w, T? def) where T : Def => w.WriteString(def?.defName ?? "");

        internal static T? ReadDef<T>(this SyncRead r) where T : Def
        {
            string name = r.Reader.ReadString();
            if (name.Length == 0) return null;
            T? def = DefDatabase<T>.GetNamedSilentFail(name);
            if (def == null) r.Missing = true;
            return def;
        }

        // ThingDef or TerrainDef (what a blueprint builds)
        internal static void WriteBuildable(this SyncWriter w, BuildableDef? def)
        {
            w.WriteByte(def switch { null => (byte)0, ThingDef _ => (byte)1, TerrainDef _ => (byte)2, _ => throw new ProtocolException("Unknown buildable def.") });
            if (def != null) w.WriteString(def.defName);
        }

        internal static BuildableDef? ReadBuildable(this SyncRead r)
        {
            byte kind = r.Reader.ReadByte();
            if (kind == 0) return null;
            string name = r.Reader.ReadString();
            BuildableDef? def = kind switch
            {
                1 => DefDatabase<ThingDef>.GetNamedSilentFail(name),
                2 => DefDatabase<TerrainDef>.GetNamedSilentFail(name),
                _ => throw new ProtocolException("Unknown buildable def.")
            };
            if (def == null) r.Missing = true;
            return def;
        }

        internal static void WriteMap(this SyncWriter w, Map? map) => w.WriteInt(map?.uniqueID ?? -1);

        internal static Map? ReadMap(this SyncRead r)
        {
            int id = r.Reader.ReadInt();
            if (id < 0) return null;
            Map? map = Find.Maps.FirstOrDefault(m => m.uniqueID == id);
            if (map == null) r.Missing = true;
            return map;
        }

        internal static void WriteCell(this SyncWriter w, IntVec3 c) { w.WriteInt(c.x); w.WriteInt(c.y); w.WriteInt(c.z); }
        internal static IntVec3 ReadCell(this SyncRead r) => new IntVec3(r.Reader.ReadInt(), r.Reader.ReadInt(), r.Reader.ReadInt());

        internal static void WriteCells(this SyncWriter w, IEnumerable<IntVec3> cells) => w.WriteCells(cells.Select(c => (c.x, c.z)));
        internal static List<IntVec3> ReadCells(this SyncRead r) => r.Reader.ReadCells().Select(c => new IntVec3(c.X, 0, c.Z)).ToList();

        // ---- things ----

        internal static void WriteThing(this SyncWriter w, Thing? thing)
        {
            if (thing == null) { w.WriteInt(-1); return; }
            w.WriteInt(thing.thingIDNumber);
            w.WriteMap(thing.MapHeld);
        }

        internal static Thing? ReadThing(this SyncRead r)
        {
            int id = r.Reader.ReadInt();
            if (id < 0) return null;
            Map? map = r.ReadMap();
            Thing? thing = map == null ? null : FindThing(map, id);
            if (thing == null) r.Missing = true;
            return thing;
        }

        internal static Pawn? ReadPawn(this SyncRead r)
        {
            Thing? thing = r.ReadThing();
            if (thing != null && thing is not Pawn) r.Missing = true;
            return thing as Pawn;
        }

        /// <summary>A thing on the map by ID: spawned, or held by a pawn there (inventory, equipment, apparel, carried).</summary>
        internal static Thing? FindThing(Map map, int id)
        {
            foreach (Thing thing in map.listerThings.AllThings) if (thing.thingIDNumber == id) return thing;
            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                if (pawn.thingIDNumber == id) return pawn;
                if (pawn.carryTracker?.CarriedThing is Thing carried && carried.thingIDNumber == id) return carried;
                if (pawn.inventory != null) foreach (Thing held in pawn.inventory.innerContainer.InnerListForReading) if (held.thingIDNumber == id) return held;
                if (pawn.equipment != null) foreach (Thing held in pawn.equipment.AllEquipmentListForReading) if (held.thingIDNumber == id) return held;
                if (pawn.apparel != null) foreach (Thing held in pawn.apparel.WornApparel) if (held.thingIDNumber == id) return held;
            }
            return null;
        }

        internal static void WriteTarget(this SyncWriter w, LocalTargetInfo target)
        {
            if (target.HasThing) { w.WriteByte(1); w.WriteThing(target.Thing); }
            else if (target.IsValid) { w.WriteByte(2); w.WriteCell(target.Cell); }
            else w.WriteByte(0);
        }

        internal static LocalTargetInfo ReadTarget(this SyncRead r) => r.Reader.ReadByte() switch
        {
            0 => LocalTargetInfo.Invalid,
            1 => r.ReadThing() is Thing thing ? new LocalTargetInfo(thing) : LocalTargetInfo.Invalid,
            2 => new LocalTargetInfo(r.ReadCell()),
            _ => throw new ProtocolException("Unknown target kind.")
        };

        // ---- zones and areas ----

        internal static void WriteZone(this SyncWriter w, Zone? zone)
        {
            w.WriteMap(zone?.Map);
            w.WriteInt(zone?.ID ?? -1);
        }

        internal static Zone? ReadZone(this SyncRead r)
        {
            Map? map = r.ReadMap();
            int id = r.Reader.ReadInt();
            if (id < 0) return null;
            Zone? zone = map?.zoneManager.AllZones.FirstOrDefault(z => z.ID == id);
            if (zone == null) r.Missing = true;
            return zone;
        }

        internal static void WriteArea(this SyncWriter w, Area? area)
        {
            w.WriteMap(area?.Map);
            w.WriteInt(area?.ID ?? -1);
        }

        internal static Area? ReadArea(this SyncRead r)
        {
            Map? map = r.ReadMap();
            int id = r.Reader.ReadInt();
            if (id < 0) return null;
            Area? area = map?.areaManager.AllAreas.FirstOrDefault(a => a.ID == id);
            if (area == null) r.Missing = true;
            return area;
        }

        // ---- verbs: the owner (a pawn or an equipment) and the index in its verb list ----

        internal static bool CanWriteVerb(Verb? verb) => verb == null || VerbOwnerThing(verb) != null;

        private static Thing? VerbOwnerThing(Verb verb) => verb.verbTracker?.directOwner switch
        {
            Pawn pawn => pawn,
            CompEquippable equippable => equippable.parent,
            _ => null
        };

        internal static void WriteVerb(this SyncWriter w, Verb? verb)
        {
            Thing? owner = verb == null ? null : VerbOwnerThing(verb);
            w.WriteThing(owner);
            if (owner != null) w.WriteInt(verb!.verbTracker.AllVerbs.IndexOf(verb));
        }

        internal static Verb? ReadVerb(this SyncRead r)
        {
            Thing? owner = r.ReadThing();
            if (owner == null) return null;
            int index = r.Reader.ReadInt();
            List<Verb>? verbs = owner is Pawn pawn ? pawn.verbTracker?.AllVerbs : owner.TryGetComp<CompEquippable>()?.AllVerbs;
            if (verbs == null || index < 0 || index >= verbs.Count) { r.Missing = true; return null; }
            return verbs[index];
        }

        // ---- jobs (right-click orders) ----

        internal static void WriteJob(this SyncWriter w, Job job)
        {
            w.WriteDef(job.def);
            w.WriteTarget(job.targetA); w.WriteTarget(job.targetB); w.WriteTarget(job.targetC);
            WriteTargets(w, job.targetQueueA); WriteTargets(w, job.targetQueueB);
            w.WriteInt(job.count);
            w.WriteInt(job.countQueue?.Count ?? -1);
            if (job.countQueue != null) foreach (int count in job.countQueue) w.WriteInt(count);
            w.WriteBool(job.playerForced);
            w.WriteByte((byte)job.haulMode);
            w.WriteByte((byte)job.locomotionUrgency);
            w.WriteDef(job.plantDefToSow);
            w.WriteDef(job.thingDefToCarry);
            w.WriteDef(job.workGiverDef);
            w.WriteVerb(job.verbToUse);
            w.WriteInt(job.maxNumMeleeAttacks); w.WriteInt(job.maxNumStaticAttacks);
            w.WriteInt(job.expiryInterval); w.WriteBool(job.checkOverrideOnExpire);
            w.WriteBool(job.killIncappedTarget); w.WriteBool(job.ignoreForbidden); w.WriteBool(job.ignoreDesignations);
            w.WriteBool(job.canBashDoors); w.WriteBool(job.canBashFences); w.WriteBool(job.canUseRangedWeapon);
            w.WriteInt(job.takeExtraIngestibles); w.WriteBool(job.ingestTotalCount); w.WriteBool(job.overeat);
            w.WriteBool(job.restUntilHealed); w.WriteBool(job.endIfCantShootTargetFromCurPos); w.WriteBool(job.endIfCantShootInMelee);
            w.WriteFloat(job.followRadius); w.WriteBool(job.draftedTend); w.WriteBool(job.exitMapOnArrival);
            w.WriteBool(job.attackDoorIfTargetLost); w.WriteBool(job.haulOpportunisticDuplicates); w.WriteBool(job.haulDroppedApparel);
            w.WriteBool(job.expireRequiresEnemiesNearby);
        }

        internal static Job? ReadJob(this SyncRead r)
        {
            JobDef? def = r.ReadDef<JobDef>();
            Job job = JobMaker.MakeJob(def ?? JobDefOf.Wait);
            job.targetA = r.ReadTarget(); job.targetB = r.ReadTarget(); job.targetC = r.ReadTarget();
            job.targetQueueA = ReadTargets(r); job.targetQueueB = ReadTargets(r);
            job.count = r.Reader.ReadInt();
            int counts = r.Reader.ReadInt();
            if (counts > 4096) throw new ProtocolException("Too many counts.");
            if (counts >= 0)
            {
                job.countQueue = new List<int>(counts);
                for (int i = 0; i < counts; i++) job.countQueue.Add(r.Reader.ReadInt());
            }
            else job.countQueue = null;
            job.playerForced = r.Reader.ReadBool();
            job.haulMode = (HaulMode)r.Reader.ReadByte();
            job.locomotionUrgency = (LocomotionUrgency)r.Reader.ReadByte();
            job.plantDefToSow = r.ReadDef<ThingDef>();
            job.thingDefToCarry = r.ReadDef<ThingDef>();
            job.workGiverDef = r.ReadDef<WorkGiverDef>();
            job.verbToUse = r.ReadVerb();
            job.maxNumMeleeAttacks = r.Reader.ReadInt(); job.maxNumStaticAttacks = r.Reader.ReadInt();
            job.expiryInterval = r.Reader.ReadInt(); job.checkOverrideOnExpire = r.Reader.ReadBool();
            job.killIncappedTarget = r.Reader.ReadBool(); job.ignoreForbidden = r.Reader.ReadBool(); job.ignoreDesignations = r.Reader.ReadBool();
            job.canBashDoors = r.Reader.ReadBool(); job.canBashFences = r.Reader.ReadBool(); job.canUseRangedWeapon = r.Reader.ReadBool();
            job.takeExtraIngestibles = r.Reader.ReadInt(); job.ingestTotalCount = r.Reader.ReadBool(); job.overeat = r.Reader.ReadBool();
            job.restUntilHealed = r.Reader.ReadBool(); job.endIfCantShootTargetFromCurPos = r.Reader.ReadBool(); job.endIfCantShootInMelee = r.Reader.ReadBool();
            job.followRadius = r.Reader.ReadFloat(); job.draftedTend = r.Reader.ReadBool(); job.exitMapOnArrival = r.Reader.ReadBool();
            job.attackDoorIfTargetLost = r.Reader.ReadBool(); job.haulOpportunisticDuplicates = r.Reader.ReadBool(); job.haulDroppedApparel = r.Reader.ReadBool();
            job.expireRequiresEnemiesNearby = r.Reader.ReadBool();
            if (def == null) r.Missing = true;
            return r.Missing ? null : job;
        }

        private static void WriteTargets(SyncWriter w, List<LocalTargetInfo>? targets)
        {
            w.WriteInt(targets?.Count ?? -1);
            if (targets != null) foreach (LocalTargetInfo target in targets) w.WriteTarget(target);
        }

        private static List<LocalTargetInfo>? ReadTargets(SyncRead r)
        {
            int count = r.Reader.ReadInt();
            if (count > 4096) throw new ProtocolException("Too many targets.");
            if (count < 0) return null;
            var targets = new List<LocalTargetInfo>(count);
            for (int i = 0; i < count; i++) targets.Add(r.ReadTarget());
            return targets;
        }

        // Fields the job system sets for itself, not the order: they may differ after the round trip.
        private static readonly HashSet<string> JobFieldsNotSent = new HashSet<string>
        {
            "loadID", "startTick", "cachedDriver", "lastJobDriverMade", "jobGiver", "jobGiverThinkTree", "jobGiverKey",
            "playerInterruptedForced", "psyfocusTargetLast", "wasOnMeditationTimeAssignment", "mote"
        };
        private static readonly FieldInfo[] JobFields = typeof(Job).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => !JobFieldsNotSent.Contains(f.Name)).ToArray();

        /// <summary>
        /// Null when the order survives the trip through the payload unchanged; otherwise the name of the first field that
        /// would be lost (e.g. a bill): such an order is not shared yet and runs only locally.
        /// </summary>
        internal static string? JobNotShareable(Job job)
        {
            if (!CanWriteVerb(job.verbToUse)) return "verbToUse";
            var writer = new SyncWriter();
            writer.WriteJob(job);
            var read = new SyncRead(new SyncReader(writer.ToArray()));
            Job? copy = read.ReadJob();
            if (copy == null) return "a target that cannot be found by ID";
            foreach (FieldInfo field in JobFields)
                if (!SameValue(field.GetValue(job), field.GetValue(copy))) return field.Name;
            return null;
        }

        private static bool SameValue(object? a, object? b)
        {
            if (a == null || b == null) return a == null && b == null || IsEmptyList(a ?? b);
            if (a is IList listA && b is IList listB)
                return listA.Count == listB.Count && Enumerable.Range(0, listA.Count).All(i => Equals(listA[i], listB[i]));
            return Equals(a, b);
        }

        private static bool IsEmptyList(object? value) => value is IList list && list.Count == 0;

        // ---- designators ----

        private static readonly AccessTools.FieldRef<Designator_Build, BuildableDef> BuildEntDef = AccessTools.FieldRefAccess<Designator_Build, BuildableDef>("entDef");
        private static readonly AccessTools.FieldRef<Designator_Build, ThingDef> BuildStuffDef = AccessTools.FieldRefAccess<Designator_Build, ThingDef>("stuffDef");
        private static readonly AccessTools.FieldRef<Designator_Place, Rot4> PlaceRotation = AccessTools.FieldRefAccess<Designator_Place, Rot4>("placingRot");
        private static readonly Dictionary<string, Type> DesignatorTypes = typeof(Designator).Assembly.GetTypes()
            .Where(t => typeof(Designator).IsAssignableFrom(t) && !t.IsAbstract).ToDictionary(t => t.FullName!, t => t);

        /// <summary>Null when the designator can be rebuilt on every player from its type and the state sent; otherwise why not.</summary>
        internal static string? DesignatorNotShareable(Designator designator)
        {
            Type type = designator.GetType();
            if (type.Assembly != typeof(Designator).Assembly) return "designators of other mods";
            if (designator is Designator_Build build) return build.sourcePrecept != null ? "ideoligion building styles" : null;
            if (designator is Designator_Place) return "installing";
            string name = type.Name;
            if (name.StartsWith("Designator_Paint", StringComparison.Ordinal)) return "painting";
            if (name.StartsWith("Designator_Plan", StringComparison.Ordinal)) return "plans";
            if (type.GetConstructor(Type.EmptyTypes) == null) return type.Name;
            return null;
        }

        internal static void WriteDesignator(this SyncWriter w, Designator designator)
        {
            w.WriteString(designator.GetType().FullName!);
            switch (designator)
            {
                case Designator_Build build:
                    w.WriteBuildable(BuildEntDef(build));
                    w.WriteDef(BuildStuffDef(build));
                    w.WriteByte(PlaceRotation(build).AsByte);
                    w.WriteDef(build.styleDef);
                    break;
                case Designator_ZoneAdd _:
                    // the zone it expands is the selected one (vanilla), or none: a new zone
                    w.WriteZone(Find.Selector.SelectedZone);
                    break;
                case Designator_AreaAllowed _:
                    w.WriteArea(Designator_AreaAllowed.selectedArea);
                    break;
            }
        }

        /// <summary>The rebuilt designator with the zone or area it works on (null when none applies).</summary>
        internal static (Designator? Designator, Zone? Zone, Area? Area) ReadDesignator(this SyncRead r)
        {
            string typeName = r.Reader.ReadString();
            if (!DesignatorTypes.TryGetValue(typeName, out Type? type)) throw new ProtocolException("Unknown designator " + typeName);
            if (type == typeof(Designator_Build) || type.IsSubclassOf(typeof(Designator_Build)))
            {
                BuildableDef? entDef = r.ReadBuildable();
                ThingDef? stuff = r.ReadDef<ThingDef>();
                var rotation = new Rot4(r.Reader.ReadByte());
                ThingStyleDef? style = r.ReadDef<ThingStyleDef>();
                if (entDef == null) { r.Missing = true; return (null, null, null); }
                var build = new Designator_Build(entDef);
                BuildStuffDef(build) = stuff!;
                PlaceRotation(build) = rotation;
                build.styleDef = style;
                return (build, null, null);
            }
            if (type.GetConstructor(Type.EmptyTypes) == null || typeof(Designator_Place).IsAssignableFrom(type))
                throw new ProtocolException("Designator not supported: " + typeName);
            var designator = (Designator)Activator.CreateInstance(type);
            if (designator is Designator_ZoneAdd) return (designator, r.ReadZone(), null);
            if (designator is Designator_AreaAllowed) return (designator, null, r.ReadArea());
            return (designator, null, null);
        }
    }
}
