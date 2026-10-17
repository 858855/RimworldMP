using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProtocolFactionRelation = Multiplayer.Foundation.Protocol.FactionRelation;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>One faction slot as the lobby decided it (ADR 0037/0038): who, which storyteller, which site, which ideology.</summary>
    internal sealed class FactionSlotPlan
    {
        internal FactionSlotPlan(byte slot, PlayerKey player, string playerName, StorytellerDef storyteller, SiteTile site, string ideologyLabel, byte[] ideologyData)
        { Slot = slot; Player = player; PlayerName = playerName; Storyteller = storyteller; Site = site; IdeologyLabel = ideologyLabel; IdeologyData = ideologyData; }

        internal byte Slot { get; }
        internal PlayerKey Player { get; }
        internal string PlayerName { get; }
        internal StorytellerDef Storyteller { get; }
        internal SiteTile Site { get; }
        /// <summary>Version-1 bytes of ADR 0046 (fixed choice, preset or edited Ideo).</summary>
        internal string IdeologyLabel { get; }
        internal byte[] IdeologyData { get; }
    }

    /// <summary>
    /// FACTION-005 (ADR 0046, ADR 0028): the host's generation step for the Factions mode. Before vanilla's own start it
    /// creates one player faction per slot with that slot's ideology; after the landing it gives each of those factions a
    /// settlement at the slot's site, generates its map and moves the slot owner's pawns there. Slot order throughout, so
    /// the host's save holds one deterministic result that every guest loads. The host's own slot keeps vanilla's faction,
    /// start site and map. Nothing here is reachable without <see cref="StartEnabled"/>; the server lock stays as well.
    /// </summary>
    internal static class FactionGeneration
    {
        /// <summary>Debug switch for the game side of the Factions start; the server keeps its own lock (ADR 0037).</summary>
        internal static bool StartEnabled => Environment.GetCommandLineArgs().Any(a => a == "-mpFactionStart") || WorldSelfTest.Requested;

        internal sealed class Prepared
        {
            internal Prepared(FactionSlotPlan host, IReadOnlyList<(FactionSlotPlan Plan, Faction Faction)> others, ProtocolFactionRelation relation)
            { Host = host; Others = others; Relation = relation; }
            internal FactionSlotPlan Host { get; }
            internal IReadOnlyList<(FactionSlotPlan Plan, Faction Faction)> Others { get; }
            internal ProtocolFactionRelation Relation { get; }
        }

        /// <summary>
        /// Before <c>PageUtility.InitGameStart</c> (ADR 0046): the host's start tile becomes its own site; every other slot
        /// gets a player faction with its chosen ideology. The factions exist in the world before the map is generated.
        /// </summary>
        internal static Prepared PrepareBeforeLanding(IReadOnlyList<FactionSlotPlan> plans, PlayerKey hostKey, ProtocolFactionRelation relation)
        {
            if (Find.GameInitData == null || Current.Game?.World == null)
                throw new InvalidOperationException("Die Fraktionen müssen vor PrepForMapGen erzeugt werden.");
            if (plans == null || plans.Count == 0 || plans.Select(p => p.Slot).Distinct().Count() != plans.Count ||
                plans.Select(p => p.Player).Distinct().Count() != plans.Count || plans.Select(p => p.Site).Distinct().Count() != plans.Count)
                throw new InvalidOperationException("Fraktionsplätze, Spieler oder Startorte sind nicht eindeutig.");
            FactionSlotPlan host = plans.FirstOrDefault(p => p.Player == hostKey)
                ?? throw new InvalidOperationException("Der Host hat keinen Fraktionsplatz.");
            Faction hostFaction = Faction.OfPlayer;
            PlanetTile hostTile = SiteFlags.ToPlanetTile(host.Site);
            if (!hostTile.Valid) throw new InvalidOperationException("Der Startort des Hosts ist ungültig.");
            Find.GameInitData.startingTile = hostTile;
            if (ModsConfig.IdeologyActive && hostFaction.ideos?.PrimaryIdeo == null)
            {
                // Without the host's ideology page (classic start, self-test) vanilla picks an ideology at PrepForMapGen and
                // took the first slot's one in the self-test of 2026-10-17 ("shared ideology"). The host gets its own first.
                Ideo own = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(hostFaction.def));
                if (!Find.IdeoManager.IdeosListForReading.Contains(own)) Find.IdeoManager.Add(own);
                hostFaction.ideos!.SetPrimary(own);
            }
            var others = new List<(FactionSlotPlan, Faction)>();
            foreach (FactionSlotPlan plan in plans.Where(p => p.Player != hostKey).OrderBy(p => p.Slot))
            {
                var ideologyParms = FactionIdeosTracker.IdeoGenerationParmsForFaction_BackCompatibility(hostFaction.def, true);
                Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(hostFaction.def, ideologyParms, true));
                faction.Name = "Kolonie " + plan.PlayerName;
                Find.FactionManager.Add(faction);
                if (!faction.def.isPlayer || ReferenceEquals(faction, hostFaction))
                    throw new InvalidOperationException("Spielerfraktion für Platz " + plan.Slot + " wurde nicht erzeugt.");
                ApplyIdeology(faction, plan, hostFaction);
                others.Add((plan, faction));
            }
            var prepared = new Prepared(host, others, relation);
            ApplyRelations(prepared, hostFaction);
            Log.Message("Multiplayer Foundation: Fraktionen vor der Landung erzeugt: Host " + hostFaction.Name + " (" + hostFaction.loadID +
                ", Ideologie " + (hostFaction.ideos?.PrimaryIdeo?.name ?? "-") + "); " + string.Join(", ",
                others.Select(o => "Platz " + o.Item1.Slot + " " + o.Item2.Name + " (" + o.Item2.loadID + ", Ideologie " + (o.Item2.ideos?.PrimaryIdeo?.name ?? "-") + ")")));
            return prepared;
        }

        /// <summary>
        /// After vanilla landed the host's colony: one settlement and map per other slot at its site, the slot owner's pawns
        /// moved there with the faction's ideology; the assignments for <see cref="MultiplayerSession.Begin"/>.
        /// </summary>
        internal static IReadOnlyList<FactionStorytellerAssignment> AfterLanding(Prepared prepared, IReadOnlyDictionary<int, PlayerKey> owners,
            IReadOnlyList<Pawn> colonists)
        {
            if (Current.Game?.World == null || Find.Maps.Count != 1) throw new InvalidOperationException("Die Vanilla-Startkarte fehlt oder es gibt schon mehrere Karten.");
            Map hostMap = Find.Maps[0];
            Faction hostFaction = Faction.OfPlayer;
            if (hostMap.ParentFaction != hostFaction) throw new InvalidOperationException("Die Startkarte gehört nicht zur Spielerfraktion des Hosts.");
            if (ModsConfig.IdeologyActive)
            {
                foreach ((FactionSlotPlan plan, Faction faction) in prepared.Others)
                    if (faction.ideos?.PrimaryIdeo == null || ReferenceEquals(faction.ideos.PrimaryIdeo, hostFaction.ideos?.PrimaryIdeo))
                        throw new InvalidOperationException("Vanilla hat beim Start die Ideologie von Platz " + plan.Slot + " mit der des Hosts zusammengelegt.");
            }
            WorldObjectDef settlementDef = DefDatabase<WorldObjectDef>.GetNamed("Settlement");
            var assignments = new List<FactionStorytellerAssignment>
            {
                new FactionStorytellerAssignment(prepared.Host.Player, prepared.Host.Slot, hostFaction, prepared.Host.Storyteller, SiteFlags.ToSiteTile(hostMap.Tile))
            };
            foreach ((FactionSlotPlan plan, Faction faction) in prepared.Others)
            {
                if (!Find.FactionManager.AllFactionsListForReading.Contains(faction))
                    throw new InvalidOperationException("Die vor der Landung erzeugte Fraktion für Platz " + plan.Slot + " fehlt.");
                PlanetTile tile = SiteFlags.ToPlanetTile(plan.Site);
                if (!tile.Valid || tile == hostMap.Tile || Find.WorldObjects.AnySettlementAt(tile))
                    throw new InvalidOperationException("Der Startort von Platz " + plan.Slot + " ist ungültig oder belegt.");
                if (!(WorldObjectMaker.MakeWorldObject(settlementDef) is Settlement settlement))
                    throw new InvalidOperationException("Die Siedlung für Platz " + plan.Slot + " konnte nicht erzeugt werden.");
                settlement.Tile = tile;
                settlement.SetFaction(faction);
                settlement.Name = faction.Name;
                Find.WorldObjects.Add(settlement);
                Map map = MapGenerator.GenerateMap(hostMap.Size, settlement, settlement.MapGeneratorDef, null, null, false, false);
                if (map.ParentFaction != faction) throw new InvalidOperationException("Die Karte von Platz " + plan.Slot + " gehört nicht zur Fraktion.");
                Pawn[] movers = colonists.Where(pawn => owners.TryGetValue(pawn.thingIDNumber, out PlayerKey owner) && owner == plan.Player).ToArray();
                if (movers.Length == 0) throw new InvalidOperationException("Platz " + plan.Slot + " (" + plan.PlayerName + ") hat keine übernommene Figur.");
                foreach (Pawn pawn in movers)
                {
                    pawn.DeSpawn(DestroyMode.Vanish);
                    pawn.SetFaction(faction);
                    if (ModsConfig.IdeologyActive)
                    {
                        Ideo ideology = faction.ideos?.PrimaryIdeo ?? throw new InvalidOperationException("Fraktion von Platz " + plan.Slot + " hat keine Ideologie.");
                        pawn.ideo?.SetIdeo(ideology);
                    }
                    GenSpawn.Spawn(pawn, CellFinder.RandomNotEdgeCell(10, map), map, WipeMode.Vanish);
                    if (!pawn.Spawned || pawn.Map != map || pawn.Faction != faction)
                        throw new InvalidOperationException("Figur " + pawn.LabelShort + " wurde nicht auf der Karte von Platz " + plan.Slot + " angesiedelt.");
                }
                assignments.Add(new FactionStorytellerAssignment(plan.Player, plan.Slot, faction, plan.Storyteller, plan.Site));
            }
            Log.Message("Multiplayer Foundation: Fraktionskolonien angelegt: " + string.Join(", ",
                assignments.Select(a => "Platz " + a.Slot + " " + a.Faction.Name + " Feld " + a.Site.TileId + " Erzähler " + a.Storyteller.defName)));
            return assignments;
        }

        // ADR 0031: the relation chosen when the lobby was created holds between every pair of player factions.
        private static void ApplyRelations(Prepared prepared, Faction hostFaction)
        {
            FactionRelationKind kind = prepared.Relation == ProtocolFactionRelation.Allied ? FactionRelationKind.Ally
                : prepared.Relation == ProtocolFactionRelation.Hostile ? FactionRelationKind.Hostile : FactionRelationKind.Neutral;
            var all = new List<Faction> { hostFaction };
            all.AddRange(prepared.Others.Select(o => o.Faction));
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    all[i].SetRelationDirect(all[j], kind, false);
                    all[j].SetRelationDirect(all[i], kind, false);
                }
        }

        // ADR 0046 data format v1: 0 fixed (no Ideology DLC), 1 installed preset, 2 edited Ideo as Scribe XML.
        private static void ApplyIdeology(Faction faction, FactionSlotPlan plan, Faction hostFaction)
        {
            if (!ModsConfig.IdeologyActive) return;
            string? invalid = FactionIdeologyCodec.InvalidReason(plan.IdeologyLabel, plan.IdeologyData);
            if (invalid != null) throw new InvalidOperationException("Ideologie von Platz " + plan.Slot + ": " + invalid);
            Ideo ideology;
            if (plan.IdeologyData[1] == 1)
            {
                string name = Encoding.UTF8.GetString(plan.IdeologyData, 2, plan.IdeologyData.Length - 2);
                IdeoPresetDef preset = DefDatabase<IdeoPresetDef>.GetNamed(name);
                var parms = new IdeoGenerationParms(faction.def);
                parms.forcedMemes = new List<MemeDef>(preset.memes);
                ideology = IdeoGenerator.GenerateIdeo(parms);
            }
            else if (plan.IdeologyData[1] == 2)
                ideology = LoadEditedIdeology(plan.IdeologyData);
            else
                throw new InvalidOperationException("Ideologie von Platz " + plan.Slot + ": feste Wahl mit aktivem Ideology-DLC.");
            if (!Find.IdeoManager.IdeosListForReading.Contains(ideology)) Find.IdeoManager.Add(ideology);
            faction.ideos.SetPrimary(ideology);
            if (faction.ideos.PrimaryIdeo == null || ReferenceEquals(faction.ideos.PrimaryIdeo, hostFaction.ideos?.PrimaryIdeo))
                throw new InvalidOperationException("Platz " + plan.Slot + " teilt die Ideologie des Hosts oder hat keine.");
        }

        private static Ideo LoadEditedIdeology(byte[] data)
        {
            string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "MultiplayerFoundation");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "faction-ideo-in.xml");
            using (var input = new MemoryStream(data, 2, data.Length - 2))
            using (var zip = new GZipStream(input, CompressionMode.Decompress))
            using (var file = File.Create(path)) zip.CopyTo(file);
            Ideo? ideology = null;
            Scribe.loader.InitLoading(path);
            try
            {
                Scribe_Deep.Look(ref ideology, "ideo");
                Scribe.loader.FinalizeLoading();
            }
            catch { Scribe.ForceStop(); throw; }
            if (ideology == null) throw new InvalidOperationException("Die bearbeitete Ideologie konnte nicht geladen werden.");
            // Loaded from another game: a fresh id keeps it apart from the host's own ideologies.
            ideology.id = Find.UniqueIDsManager.GetNextIdeoID();
            return ideology;
        }
    }
}
