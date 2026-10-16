using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Session;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SYNC-001 (ADR 0024, minimum of DESYNC-001): hash of the simulated state every player compares every 60 ticks.
    /// Tick and maps by ID; per map every spawned thing, sorted by ID: ID, def index, cell, hit points, stack;
    /// per pawn: job def, number of hediffs, each need's level as raw float bits. No caches, no drawing state.
    /// </summary>
    internal static class GameStateHash
    {
        private static Thing[] buffer = Array.Empty<Thing>();
        private static readonly IComparer<Thing> ById = Comparer<Thing>.Create((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));

        /// <returns>The hash and a short summary for Player.log (no player identities).</returns>
        internal static (ulong Hash, string Summary) Compute(Game game)
        {
            var hasher = new StateHasher();
            hasher.Add(game.tickManager.TicksGame);
            var parts = new List<string> { "tick " + game.tickManager.TicksGame };
            foreach (Map map in game.Maps.OrderBy(m => m.uniqueID))
            {
                List<Thing> things = map.listerThings.AllThings;
                int count = things.Count;
                if (buffer.Length < count) buffer = new Thing[count + count / 2];
                things.CopyTo(buffer);
                Array.Sort(buffer, 0, count, ById);
                hasher.Add(map.uniqueID); hasher.Add(count);
                int pawns = 0;
                for (int i = 0; i < count; i++)
                {
                    Thing thing = buffer[i];
                    hasher.Add(thing.thingIDNumber); hasher.Add(thing.def.index);
                    IntVec3 cell = thing.Position;
                    hasher.Add(cell.x); hasher.Add(cell.z);
                    hasher.Add(thing.HitPoints); hasher.Add(thing.stackCount);
                    if (thing is Pawn pawn) { pawns++; AddPawn(hasher, pawn); }
                }
                Array.Clear(buffer, 0, count);
                parts.Add("map " + map.uniqueID + ": " + count + " things, " + pawns + " pawns");
                parts.AddRange(map.mapPawns.FreeColonistsSpawned.OrderBy(p => p.thingIDNumber)
                    .Select(p => "colonist " + p.thingIDNumber + " at " + p.Position.x + "," + p.Position.z + " " + (p.CurJobDef?.defName ?? "no job")));
            }
            parts.Add("values " + hasher.Count);
            return (hasher.Value, string.Join("; ", parts));
        }

        /// <summary>One thing's hashed values, compact enough to keep for every hash tick (self-test diagnosis).</summary>
        internal readonly struct ThingRecord
        {
            public ThingRecord(Thing thing, ulong pawnValue)
            {
                Id = thing.thingIDNumber; Def = thing.def; X = thing.Position.x; Z = thing.Position.z; Hp = thing.HitPoints;
                Stack = thing.stackCount; PawnValue = pawnValue;
            }
            public int Id { get; }
            public ThingDef Def { get; }
            public int X { get; }
            public int Z { get; }
            public int Hp { get; }
            public int Stack { get; }
            public ulong PawnValue { get; }
            public bool SameAs(ThingRecord other) => Id == other.Id && Def == other.Def && X == other.X && Z == other.Z &&
                Hp == other.Hp && Stack == other.Stack && PawnValue == other.PawnValue;
            public override string ToString() => Def.defName + " at " + X + "," + Z + " hp " + Hp + " x" + Stack;
        }

        /// <summary>
        /// The same values as <see cref="Compute"/> for every spawned thing, sorted by ID, plus a readable line per pawn
        /// (self-test diagnosis: which thing and which value differ first).
        /// </summary>
        internal static (ThingRecord[] Things, Dictionary<int, string> Pawns) Records(Game game)
        {
            var things = new List<ThingRecord>();
            var pawns = new Dictionary<int, string>();
            foreach (Map map in game.Maps)
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    ulong pawnValue = 0;
                    if (thing is Pawn pawn)
                    {
                        var hasher = new StateHasher();
                        AddPawn(hasher, pawn);
                        pawnValue = hasher.Value;
                        pawns[pawn.thingIDNumber] = pawn.LabelShort + " job " + (pawn.CurJobDef?.defName ?? "-") + " hediffs " +
                            (pawn.health?.hediffSet?.hediffs?.Count ?? -1) + " needs " +
                            string.Join(",", pawn.needs?.AllNeeds?.Select(n => n.def.defName + "=" + n.CurLevel.ToString("R")) ?? Enumerable.Empty<string>());
                    }
                    things.Add(new ThingRecord(thing, pawnValue));
                }
            things.Sort((a, b) => a.Id.CompareTo(b.Id));
            return (things.ToArray(), pawns);
        }

        private static void AddPawn(StateHasher hasher, Pawn pawn)
        {
            hasher.Add(pawn.CurJobDef?.index ?? -1);
            hasher.Add(pawn.health?.hediffSet?.hediffs?.Count ?? -1);
            List<Need>? needs = pawn.needs?.AllNeeds;
            if (needs == null) { hasher.Add(-1); return; }
            hasher.Add(needs.Count);
            foreach (Need need in needs) { hasher.Add(need.def.index); hasher.Add(need.CurLevel); }
        }
    }
}
