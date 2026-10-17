using System;
using System.Linq;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Canonical gameplay fields from plan §7.9; excludes rendering and cached data.</summary>
    internal static class SessionStateHash
    {
        internal static ulong Compute(TickManager manager, ulong randDraws, ulong randValueSum)
        {
            ulong hash = 14695981039346656037UL;
            void Add(ulong value) { unchecked { hash = (hash ^ value) * 1099511628211UL; } }
            void AddInt(int value) => Add((uint)value);
            AddInt(manager.TicksGame);
            foreach (Map map in Find.Maps.OrderBy(m => m.uniqueID))
            {
                AddInt(map.uniqueID);
                var things = map.listerThings.AllThings.Where(t => t.Spawned).OrderBy(t => t.thingIDNumber).ToList();
                AddInt(things.Count);
                foreach (Thing thing in things)
                {
                    AddInt(thing.thingIDNumber); AddInt(thing.def.index);
                    AddInt(thing.Position.x); AddInt(thing.Position.y); AddInt(thing.Position.z);
                    AddInt(thing.HitPoints); AddInt(thing.stackCount);
                    if (thing is Pawn pawn)
                    {
                        AddInt(pawn.CurJob?.def?.index ?? -1);
                        var needs = pawn.needs?.AllNeeds.OrderBy(n => n.def.index).ToList();
                        AddInt(needs?.Count ?? -1);
                        if (needs != null) foreach (Need need in needs)
                        { AddInt(need.def.index); AddInt(BitConverter.SingleToInt32Bits(need.CurLevel)); }
                    }
                }
            }
            var factions = Current.Game?.World?.factionManager?.AllFactionsListForReading.OrderBy(f => f.loadID).ToList();
            AddInt(factions?.Count ?? -1);
            if (factions != null) foreach (Faction faction in factions)
            {
                AddInt(faction.loadID);
                foreach (Faction other in factions)
                {
                    if (other == faction) continue;
                    // The stored relation, not GoodwillWith: vanilla's situation manager only knows Faction.OfPlayer and
                    // throws for a second player faction (Factions mode, self-test 2026-10-17).
                    AddInt(other.loadID); AddInt(faction.RelationWith(other, true)?.baseGoodwill ?? int.MinValue);
                }
            }
            var quests = Current.Game?.questManager?.QuestsListForReading.OrderBy(q => q.id).ToList();
            AddInt(quests?.Count ?? -1);
            if (quests != null) foreach (Quest quest in quests) { AddInt(quest.id); AddInt((int)quest.State); }
            SyncHashes.Contribute(Add);   // ADR 0029: other mods' synchronized state, in id order
            Add(randDraws); Add(randValueSum);
            return hash;
        }
    }
}
