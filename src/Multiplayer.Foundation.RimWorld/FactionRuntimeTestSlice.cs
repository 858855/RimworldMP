using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// Opt-in, isolated game self-test. Never called by the lobby or the normal game-start path.
    /// It probes the real 1.6 generator and save path before multi-faction generation is enabled.
    /// </summary>
    internal static class FactionRuntimeTestSlice
    {
        internal static Faction CreateFactionBeforeLanding()
        {
            if (Find.GameInitData == null || Current.Game?.World == null)
                throw new InvalidOperationException("Die zweite Fraktion muss vor PrepForMapGen erzeugt werden.");
            Faction firstFaction = Faction.OfPlayer;
            var ideology = FactionIdeosTracker.IdeoGenerationParmsForFaction_BackCompatibility(firstFaction.def, true);
            var parameters = new FactionGeneratorParms(firstFaction.def, ideology, true);
            Faction secondFaction = FactionGenerator.NewGeneratedFaction(parameters);
            secondFaction.Name = "Testfraktion Zwei";
            Find.FactionManager.Add(secondFaction);
            if (!secondFaction.def.isPlayer || secondFaction == firstFaction)
                throw new InvalidOperationException("Zweite Spielerfraktion wurde nicht erzeugt.");
            if (ModsConfig.IdeologyActive &&
                (secondFaction.ideos?.PrimaryIdeo == null ||
                 ReferenceEquals(secondFaction.ideos.PrimaryIdeo, firstFaction.ideos?.PrimaryIdeo)))
            {
                Ideo separate = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(secondFaction.def));
                if (!Find.IdeoManager.IdeosListForReading.Contains(separate)) Find.IdeoManager.Add(separate);
                AccessTools.Field(typeof(FactionIdeosTracker), "primaryIdeo").SetValue(secondFaction.ideos, separate);
            }
            Log.Message("Multiplayer Foundation faction self-test: pre-start ideos first=" +
                (firstFaction.ideos?.PrimaryIdeo?.name ?? "null") + " second=" +
                (secondFaction.ideos?.PrimaryIdeo?.name ?? "null") + " same=" +
                ReferenceEquals(firstFaction.ideos?.PrimaryIdeo, secondFaction.ideos?.PrimaryIdeo));
            return secondFaction;
        }

        internal static IReadOnlyList<FactionStorytellerAssignment> Create(PlayerKey first, PlayerKey second,
            IReadOnlyList<Pawn> colonists, Faction secondFaction)
        {
            if (colonists.Count < 2 || Current.Game?.World == null || Find.Maps.Count != 1)
                throw new InvalidOperationException("Zwei gelandete Figuren und die Vanilla-Startkarte fehlen.");
            Map firstMap = Find.Maps[0];
            Faction firstFaction = Faction.OfPlayer;
            if (firstMap.ParentFaction != firstFaction || !firstFaction.def.isPlayer)
                throw new InvalidOperationException("Die Vanilla-Startkarte gehört nicht zur Spielerfraktion.");

            PlanetLayer surface = Find.World.grid.Surface;
            PlanetTile secondTile = Enumerable.Range(0, surface.TilesCount).Select(surface.PlanetTileForID)
                .FirstOrDefault(tile => tile != firstMap.Tile &&
                    Find.WorldGrid.ApproxDistanceInTiles(firstMap.Tile, tile) >= 20f &&
                    TileFinder.IsValidTileForNewSettlement(tile, new StringBuilder(), false));
            if (!secondTile.Valid) throw new InvalidOperationException("Kein zweiter gültiger Startort gefunden.");

            if (!Find.FactionManager.AllFactionsListForReading.Contains(secondFaction))
                throw new InvalidOperationException("Die vor dem Spielstart erzeugte Fraktion fehlt.");

            WorldObjectDef settlementDef = DefDatabase<WorldObjectDef>.GetNamed("Settlement");
            if (!(WorldObjectMaker.MakeWorldObject(settlementDef) is Settlement settlement))
                throw new InvalidOperationException("Die zweite Siedlung konnte nicht erzeugt werden.");
            settlement.Tile = secondTile;
            settlement.SetFaction(secondFaction);
            Find.WorldObjects.Add(settlement);
            Map secondMap = MapGenerator.GenerateMap(firstMap.Size, settlement, settlement.MapGeneratorDef,
                null, null, false, false);
            if (secondMap.ParentFaction != secondFaction)
                throw new InvalidOperationException("Die zweite Karte gehört nicht zur zweiten Fraktion.");

            Pawn moved = colonists[1];
            moved.DeSpawn(DestroyMode.Vanish);
            moved.SetFaction(secondFaction);
            if (ModsConfig.IdeologyActive)
            {
                Ideo ideology = secondFaction.ideos?.PrimaryIdeo
                    ?? throw new InvalidOperationException("Die zweite Fraktion hat keine eigene Ideologie.");
                moved.ideo.SetIdeo(ideology);
            }
            IntVec3 spawn = CellFinder.RandomNotEdgeCell(10, secondMap);
            GenSpawn.Spawn(moved, spawn, secondMap, WipeMode.Vanish);
            if (!moved.Spawned || moved.Map != secondMap || moved.Faction != secondFaction)
                throw new InvalidOperationException("Die zweite Startfigur wurde nicht getrennt angesiedelt.");

            var storytellers = FactionStorytellerCatalog.AvailableFor(Current.Game.storyteller.difficultyDef);
            StorytellerDef firstDef = Current.Game.storyteller.def;
            StorytellerDef secondDef = storytellers.FirstOrDefault(def => def != firstDef)
                ?? throw new InvalidOperationException("Kein zweiter kompatibler Erzähler installiert.");
            Log.Message("Multiplayer Foundation faction self-test: generated sites=" + firstMap.Tile.tileId + "," + secondTile.tileId +
                " factions=" + firstFaction.loadID + "," + secondFaction.loadID +
                " storytellers=" + firstDef.defName + "," + secondDef.defName);
            return new[]
            {
                new FactionStorytellerAssignment(first, 0, firstFaction, firstDef, SiteFlags.ToSiteTile(firstMap.Tile)),
                new FactionStorytellerAssignment(second, 1, secondFaction, secondDef, SiteFlags.ToSiteTile(secondTile))
            };
        }

        internal static bool CheckCurrentGame(MultiplayerSession session) => Diagnostic(session) == null;

        internal static string? Diagnostic(MultiplayerSession session)
        {
            if (session.Mode?.Mode != GameMode.Factions) return "mode";
            if (session.FactionStorytellerError() is string error) return error;
            var entries = session.FactionSetupInSlotOrder.ToList();
            if (entries.Count != 2 || entries[0].Slot != 0 || entries[1].Slot != 1 ||
                entries[0].FactionId == entries[1].FactionId || entries[0].Storyteller == entries[1].Storyteller ||
                Find.Maps.Count < 2) return "count/order/storyteller/map count";
            if (ModsConfig.IdeologyActive)
            {
                Faction? first = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.loadID == entries[0].FactionId);
                Faction? second = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.loadID == entries[1].FactionId);
                if (first?.ideos?.PrimaryIdeo == null) return "first ideology missing";
                if (second?.ideos?.PrimaryIdeo == null) return "second ideology missing";
                if (ReferenceEquals(first.ideos.PrimaryIdeo, second.ideos.PrimaryIdeo)) return "shared ideology";
            }
            foreach (var entry in entries)
            {
                Faction? faction = Find.FactionManager.AllFactionsListForReading.SingleOrDefault(f => f.loadID == entry.FactionId);
                if (faction?.def?.isPlayer != true) return "missing faction slot " + entry.Slot;
                if (!Find.Maps.Any(map => map.ParentFaction == faction && SiteFlags.ToSiteTile(map.Tile) == entry.Site))
                    return "missing map slot " + entry.Slot;
                if (!Find.Maps.Any(map => map.ParentFaction == faction &&
                    map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.Faction == faction && pawn.RaceProps.Humanlike)))
                    return "missing pawn slot " + entry.Slot;
                if (ModsConfig.IdeologyActive && !Find.Maps.Any(map => map.ParentFaction == faction &&
                    map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.Faction == faction && pawn.RaceProps.Humanlike &&
                        pawn.ideo?.Ideo == faction.ideos.PrimaryIdeo))) return "wrong pawn ideology slot " + entry.Slot;
            }
            return null;
        }
    }
}
