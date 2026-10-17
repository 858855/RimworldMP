using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProtocolFactionRelation = Multiplayer.Foundation.Protocol.FactionRelation;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Host-side assignment after real player factions have been generated.</summary>
    internal sealed class FactionStorytellerAssignment
    {
        internal FactionStorytellerAssignment(PlayerKey player, byte slot, Faction faction, StorytellerDef storyteller, SiteTile site)
        { Player = player; Slot = slot; Faction = faction; Storyteller = storyteller; Site = site; }

        internal PlayerKey Player { get; }
        internal byte Slot { get; }
        internal Faction Faction { get; }
        internal StorytellerDef Storyteller { get; }
        internal SiteTile Site { get; }
    }

    internal static class FactionStorytellerCatalog
    {
        /// <summary>Only installed, player-visible choices compatible with the host's difficulty.</summary>
        internal static IReadOnlyList<StorytellerDef> AvailableFor(DifficultyDef difficulty) =>
            DefDatabase<StorytellerDef>.AllDefsListForReading
                .Where(def => def != null && DefNames.IsValid(def.defName) && def.listVisible && !def.tutorialMode &&
                    (def.forcedDifficulty == null || def.forcedDifficulty == difficulty))
                .OrderBy(def => def.listOrder).ThenBy(def => def.defName, StringComparer.Ordinal).Take(32).ToList();
    }

    /// <summary>
    /// SETUP-005: saved with the game. Says which lobby started it, who played and who owns which starting pawn
    /// (plan §7.7, §7.8). RimWorld adds one to every game; it stays empty in single-player games. OWN-001 builds on it.
    /// </summary>
    public sealed class MultiplayerSession : GameComponent
    {
        private string sessionId = "";
        private List<string> playerKeys = new List<string>();
        private List<string> playerNames = new List<string>();
        private List<int> ownedPawns = new List<int>();
        private List<string> pawnOwners = new List<string>();
        private int gameMode;
        private int coopDecisions;
        private int factionRelation;
        // FACTION-003: saved with the shared game, never in client-local mod settings.
        private string sharedDifficultyDef = "";
        private string sharedDifficultyFingerprint = "";
        private List<string> factionStorytellerOwners = new List<string>();
        private List<string> factionStorytellerDefs = new List<string>();
        private List<int> factionSlots = new List<int>();
        private List<int> factionIds = new List<int>();
        private List<int> factionSiteTileIds = new List<int>();
        private List<int> factionSiteLayerIds = new List<int>();
        // One independent incident queue and component set per slot, saved with the shared game.
        private List<Storyteller> factionStorytellers = new List<Storyteller>();
        private int lastFactionValidationTick = -1;

        public MultiplayerSession() { }
        public MultiplayerSession(Game game) { }

        internal static MultiplayerSession? ForCurrentGame => Current.Game?.GetComponent<MultiplayerSession>();

        internal bool Active => sessionId.Length > 0;
        internal string SessionId => sessionId;
        // Older saves predate MODE-001 and therefore use Coop/Host.
        internal LobbyMode? Mode => (gameMode == 0 && coopDecisions == 0 && factionRelation == 0) ||
            (gameMode == (int)GameMode.Coop && coopDecisions == (int)CoopDecisions.Host && factionRelation == 0)
            ? LobbyMode.CoopHost :
            gameMode == (int)GameMode.Coop && coopDecisions == (int)CoopDecisions.Democracy && factionRelation == 0
            ? LobbyMode.CoopDemocracy :
            gameMode == (int)GameMode.Factions && coopDecisions == 0 &&
            factionRelation >= (int)ProtocolFactionRelation.Allied && factionRelation <= (int)ProtocolFactionRelation.Hostile
            ? LobbyMode.Factions((ProtocolFactionRelation)factionRelation) : null;

        /// <summary>Host, after the vanilla start: fills the session for the colonists that actually landed.</summary>
        internal void Begin(Guid lobbyId, LobbyMode mode, IEnumerable<(PlayerKey Key, string Name)> players, IReadOnlyDictionary<int, PlayerKey> owners,
            ICollection<int> colonists, IReadOnlyList<FactionStorytellerAssignment>? factionAssignments = null)
        {
            var list = players.ToList();
            var selectedOwners = new List<string>();
            var selectedDefs = new List<string>();
            var selectedSlots = new List<int>();
            var selectedFactionIds = new List<int>();
            var selectedSiteTiles = new List<int>();
            var selectedSiteLayers = new List<int>();
            var selectedStorytellers = new List<Storyteller>();
            string selectedDifficulty = "";
            string selectedFingerprint = "";
            if (mode.Mode == GameMode.Factions)
            {
                Game? game = Current.Game;
                DifficultyDef? difficulty = game?.storyteller?.difficultyDef;
                Difficulty? values = game?.storyteller?.difficulty;
                if (difficulty == null || values == null || factionAssignments == null ||
                    factionAssignments.Count != list.Count || list.Select(p => p.Key).Distinct().Count() != list.Count ||
                    game?.World?.factionManager == null)
                    throw new InvalidOperationException("Fraktionszuordnung oder gemeinsame Schwierigkeit fehlen.");
                selectedDifficulty = difficulty.defName;
                selectedFingerprint = DifficultyFingerprint(values);
                var byKey = list.ToDictionary(player => player.Key, player => player.Name);
                var allowed = FactionStorytellerCatalog.AvailableFor(difficulty);
                var worldFactions = game.World.factionManager.AllFactionsListForReading;
                if (factionAssignments.Any(a => a == null) ||
                    factionAssignments.Select(a => a.Player).Distinct().Count() != list.Count ||
                    factionAssignments.Select(a => a.Slot).Distinct().Count() != list.Count ||
                    factionAssignments.Select(a => a.Faction?.loadID).Distinct().Count() != list.Count)
                    throw new InvalidOperationException("Fraktionsplätze oder Fraktionen sind nicht eindeutig.");
                foreach (FactionStorytellerAssignment assignment in factionAssignments.OrderBy(a => a.Slot))
                {
                    if (!byKey.ContainsKey(assignment.Player) || assignment.Slot >= 64 ||
                        assignment.Faction == null || assignment.Faction.def?.isPlayer != true ||
                        !worldFactions.Contains(assignment.Faction) || assignment.Storyteller == null ||
                        !allowed.Contains(assignment.Storyteller) || assignment.Site.TileId < 0 ||
                        assignment.Site.LayerId < 0 || !SiteFlags.ToPlanetTile(assignment.Site).Valid)
                        throw new InvalidOperationException("Ungültige Fraktion oder Erzählerwahl für Platz " + assignment.Slot + ".");
                    selectedOwners.Add(assignment.Player.ToString());
                    selectedDefs.Add(assignment.Storyteller.defName);
                    selectedSlots.Add(assignment.Slot);
                    selectedFactionIds.Add(assignment.Faction.loadID);
                    selectedSiteTiles.Add(assignment.Site.TileId);
                    selectedSiteLayers.Add(assignment.Site.LayerId);
                    selectedStorytellers.Add(new Storyteller(assignment.Storyteller, difficulty, values));
                }
                if (selectedSiteTiles.Select((tile, i) => new SiteTile(tile, selectedSiteLayers[i])).Distinct().Count() != list.Count)
                    throw new InvalidOperationException("Fraktions-Startorte sind nicht eindeutig.");
            }
            sessionId = lobbyId.ToString("N");
            gameMode = (int)mode.Mode;
            coopDecisions = (int)mode.Decisions;
            factionRelation = (int)mode.Relation;
            playerKeys = list.Select(p => p.Key.ToString()).ToList();
            playerNames = list.Select(p => p.Name).ToList();
            sharedDifficultyDef = selectedDifficulty;
            sharedDifficultyFingerprint = selectedFingerprint;
            factionStorytellerOwners = selectedOwners;
            factionStorytellerDefs = selectedDefs;
            factionSlots = selectedSlots;
            factionIds = selectedFactionIds;
            factionSiteTileIds = selectedSiteTiles;
            factionSiteLayerIds = selectedSiteLayers;
            factionStorytellers = selectedStorytellers;
            lastFactionValidationTick = -1;
            var landed = owners.Where(o => colonists.Contains(o.Key)).OrderBy(o => o.Key).ToList();
            ownedPawns = landed.Select(o => o.Key).ToList();
            pawnOwners = landed.Select(o => o.Value.ToString()).ToList();
        }

        internal IEnumerable<(int PawnId, string Owner)> Owners => ownedPawns.Zip(pawnOwners, (pawn, owner) => (pawn, owner));
        internal IEnumerable<(string Key, string Name)> Players => playerKeys.Zip(playerNames, (key, name) => (key, name));

        internal string? StorytellerDefNameFor(PlayerKey player)
        {
            int index = factionStorytellerOwners.IndexOf(player.ToString());
            return index >= 0 && index < factionStorytellerDefs.Count ? factionStorytellerDefs[index] : null;
        }

        internal int? FactionIdFor(PlayerKey player)
        {
            int index = factionStorytellerOwners.IndexOf(player.ToString());
            return index >= 0 && index < factionIds.Count ? factionIds[index] : (int?)null;
        }

        internal int? FactionSlotFor(PlayerKey player)
        {
            int index = factionStorytellerOwners.IndexOf(player.ToString());
            return index >= 0 && index < factionSlots.Count ? factionSlots[index] : (int?)null;
        }

        /// <summary>Canonical order for later per-faction storyteller ticks.</summary>
        internal IEnumerable<(int Slot, int FactionId, string Storyteller)> FactionAssignmentsInSlotOrder =>
            factionSlots.Select((slot, index) => (slot, factionIds[index], factionStorytellerDefs[index]));

        internal IEnumerable<(string PlayerKey, int Slot, int FactionId, string Storyteller, SiteTile Site)> FactionSetupInSlotOrder =>
            Enumerable.Range(0, factionSlots.Count).Select(index =>
                (factionStorytellerOwners[index], factionSlots[index], factionIds[index], factionStorytellerDefs[index],
                    new SiteTile(factionSiteTileIds[index], factionSiteLayerIds[index])));

        internal IEnumerable<(Storyteller Storyteller, Faction Owner)> LiveFactionStorytellers()
        {
            int tick = Find.TickManager.TicksGame;
            if (lastFactionValidationTick < 0 || tick < lastFactionValidationTick || tick - lastFactionValidationTick >= 60)
            {
                string? invalid = FactionStorytellerError();
                if (invalid != null) throw new InvalidOperationException(invalid);
                lastFactionValidationTick = tick;
            }
            IReadOnlyList<Faction> factions = Current.Game.World.factionManager.AllFactionsListForReading;
            for (int index = 0; index < factionSlots.Count; index++)
            {
                Faction owner = factions.Single(faction => faction.loadID == factionIds[index]);
                yield return (factionStorytellers[index], owner);
            }
        }

        internal IReadOnlyList<FactionRestoreEntry> FactionRestoreEntries()
        {
            if (Mode?.Mode != GameMode.Factions || FactionStorytellerError() != null)
                throw new InvalidOperationException("Der Fraktions-Spielstand ist unvollständig oder ungültig.");
            var entries = new List<FactionRestoreEntry>(factionSlots.Count);
            for (int index = 0; index < factionSlots.Count; index++)
            {
                if (!PlayerKey.TryParse(factionStorytellerOwners[index], out PlayerKey key))
                    throw new InvalidOperationException("Ungültiger PlayerKey im Fraktions-Spielstand.");
                entries.Add(new FactionRestoreEntry(key, (byte)factionSlots[index], factionStorytellerDefs[index]));
            }
            return entries;
        }

        internal void RebindLobby(Guid newLobbyId)
        {
            if (Mode?.Mode != GameMode.Factions || FactionStorytellerError() != null)
                throw new InvalidOperationException("Fraktions-Spielstand kann nicht fortgesetzt werden.");
            sessionId = newLobbyId.ToString("N");
        }

        /// <summary>Fail closed until every faction choice and the shared difficulty can be restored.</summary>
        internal string? FactionStorytellerError()
        {
            if (Mode?.Mode != GameMode.Factions) return null;
            if (ModsConfig.AnomalyActive)
                return "Anomaly-Ereignisse sind für getrennte Fraktions-Erzähler noch nicht zugeordnet.";
            Game? game = Current.Game;
            if (sharedDifficultyDef.Length == 0 || sharedDifficultyFingerprint.Length == 0 ||
                game?.storyteller?.difficultyDef?.defName != sharedDifficultyDef ||
                game.storyteller.difficulty == null)
                return "Gemeinsame Schwierigkeit fehlt oder weicht vom Spielstand ab.";
            try
            {
                if (DifficultyFingerprint(game.storyteller.difficulty) != sharedDifficultyFingerprint)
                    return "Gemeinsame Schwierigkeitswerte weichen vom Host-Spielstand ab.";
            }
            catch (InvalidOperationException error)
            { return "Gemeinsame Schwierigkeit kann nicht geprüft werden: " + error.Message; }
            if (factionStorytellerOwners.Count != playerKeys.Count ||
                factionStorytellerDefs.Count != playerKeys.Count ||
                factionSlots.Count != playerKeys.Count || factionIds.Count != playerKeys.Count ||
                factionSiteTileIds.Count != playerKeys.Count || factionSiteLayerIds.Count != playerKeys.Count ||
                factionStorytellers.Count != playerKeys.Count ||
                playerKeys.Distinct().Count() != playerKeys.Count ||
                factionStorytellerOwners.Distinct().Count() != playerKeys.Count ||
                factionSlots.Distinct().Count() != playerKeys.Count ||
                factionIds.Distinct().Count() != playerKeys.Count ||
                factionSiteTileIds.Select((tile, i) => (Tile: tile, Layer: factionSiteLayerIds[i])).Distinct().Count() != playerKeys.Count ||
                factionStorytellers.Any(storyteller => storyteller == null) ||
                factionStorytellers.Distinct().Count() != factionStorytellers.Count ||
                factionStorytellers.Any(storyteller =>
                    factionStorytellers.Any(other => !ReferenceEquals(storyteller, other) &&
                        ReferenceEquals(storyteller.incidentQueue, other.incidentQueue))) ||
                factionSlots.Any(slot => slot < 0 || slot >= 64) ||
                factionSiteTileIds.Any(tile => tile < 0) || factionSiteLayerIds.Any(layer => layer < 0) ||
                !factionSlots.SequenceEqual(factionSlots.OrderBy(slot => slot)) ||
                !factionStorytellerOwners.All(playerKeys.Contains))
                return "Fraktionsplätze, Fraktionen oder Erzähler sind nicht eindeutig zugeordnet.";
            DifficultyDef difficulty = game.storyteller.difficultyDef;
            var allowed = FactionStorytellerCatalog.AvailableFor(difficulty);
            var worldFactions = game.World?.factionManager?.AllFactionsListForReading;
            if (worldFactions == null || factionIds.Any(id => !worldFactions.Any(f => f.loadID == id && f.def?.isPlayer == true)))
                return "Eine gespeicherte Spielerfraktion fehlt in der Welt.";
            try
            {
                if (factionSiteTileIds.Select((tile, i) => SiteFlags.ToPlanetTile(new SiteTile(tile, factionSiteLayerIds[i])))
                    .Any(tile => !tile.Valid)) return "Ein gespeicherter Fraktions-Startort fehlt in der Welt.";
            }
            catch (Exception error) when (error is ArgumentException || error is IndexOutOfRangeException || error is NullReferenceException)
            { return "Ein gespeicherter Fraktions-Startort ist ungültig."; }
            foreach (string name in factionStorytellerDefs)
            {
                if (!allowed.Any(def => def.defName == name))
                    return "Ein Fraktions-Erzähler fehlt oder erzwingt eine andere Schwierigkeit.";
            }
            for (int index = 0; index < factionStorytellers.Count; index++)
            {
                Storyteller? storyteller = factionStorytellers[index];
                if (storyteller?.def?.defName != factionStorytellerDefs[index] ||
                    storyteller.difficultyDef?.defName != sharedDifficultyDef || storyteller.difficulty == null ||
                    storyteller.incidentQueue == null)
                    return "Eine gespeicherte Erzählerinstanz oder Ereigniswarteschlange fehlt.";
                try
                {
                    if (DifficultyFingerprint(storyteller.difficulty) != sharedDifficultyFingerprint)
                        return "Ein Fraktions-Erzähler hat abweichende Schwierigkeitswerte.";
                }
                catch (InvalidOperationException error)
                { return "Fraktions-Schwierigkeit kann nicht geprüft werden: " + error.Message; }
            }
            return null;
        }

        // Difficulty.ExposeData persists gameplay values in the host save. This checksum binds our
        // faction assignments to those loaded values, including Custom, without copying them to the lobby.
        private static string DifficultyFingerprint(Difficulty values)
        {
            var fields = typeof(Difficulty).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(field => field.Name, StringComparer.Ordinal);
            var canonical = new StringBuilder();
            foreach (FieldInfo field in fields)
            {
                object? value = field.GetValue(values);
                string encoded = value switch
                {
                    null => "null",
                    bool boolean => boolean ? "true" : "false",
                    float number => BitConverter.SingleToInt32Bits(number).ToString("X8", CultureInfo.InvariantCulture),
                    int number => number.ToString(CultureInfo.InvariantCulture),
                    Def def => def.defName,
                    _ => throw new InvalidOperationException("Nicht unterstütztes Difficulty-Feld: " + field.Name)
                };
                canonical.Append(field.Name.Length).Append(':').Append(field.Name)
                    .Append(encoded.Length).Append(':').Append(encoded);
            }
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", "");
        }

        internal string? OwnerNameOf(int pawnId)
        {
            int index = ownedPawns.IndexOf(pawnId);
            if (index < 0) return null;
            int player = playerKeys.IndexOf(pawnOwners[index]);
            return player < 0 ? pawnOwners[index] : playerNames[player];
        }

        /// <summary>SYNC-005 (plan §7.7): the saved owner of a pawn, or null for a shared pawn.</summary>
        internal PlayerKey? OwnerKeyOf(int pawnId)
        {
            int index = ownedPawns.IndexOf(pawnId);
            if (index < 0 || index >= pawnOwners.Count) return null;
            return PlayerKey.TryParse(pawnOwners[index], out PlayerKey key) ? key : (PlayerKey?)null;
        }

        internal string? PlayerNameOf(PlayerKey key)
        {
            int index = playerKeys.IndexOf(key.ToString());
            return index >= 0 && index < playerNames.Count ? playerNames[index] : null;
        }

        /// <summary>Factions mode: the player whose faction this is, by faction loadID.</summary>
        internal string? FactionOwnerNameOf(int factionId)
        {
            int index = factionIds.IndexOf(factionId);
            if (index < 0 || index >= factionStorytellerOwners.Count) return null;
            int player = playerKeys.IndexOf(factionStorytellerOwners[index]);
            return player < 0 ? factionStorytellerOwners[index] : playerNames[player];
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sessionId, "sessionId", "");
            Scribe_Collections.Look(ref playerKeys, "playerKeys", LookMode.Value);
            Scribe_Collections.Look(ref playerNames, "playerNames", LookMode.Value);
            Scribe_Collections.Look(ref ownedPawns, "ownedPawns", LookMode.Value);
            Scribe_Collections.Look(ref pawnOwners, "pawnOwners", LookMode.Value);
            Scribe_Values.Look(ref gameMode, "gameMode", 0);
            Scribe_Values.Look(ref coopDecisions, "coopDecisions", 0);
            Scribe_Values.Look(ref factionRelation, "factionRelation", 0);
            Scribe_Values.Look(ref sharedDifficultyDef, "sharedDifficultyDef", "");
            Scribe_Values.Look(ref sharedDifficultyFingerprint, "sharedDifficultyFingerprint", "");
            Scribe_Collections.Look(ref factionStorytellerOwners, "factionStorytellerOwners", LookMode.Value);
            Scribe_Collections.Look(ref factionStorytellerDefs, "factionStorytellerDefs", LookMode.Value);
            Scribe_Collections.Look(ref factionSlots, "factionSlots", LookMode.Value);
            Scribe_Collections.Look(ref factionIds, "factionIds", LookMode.Value);
            Scribe_Collections.Look(ref factionSiteTileIds, "factionSiteTileIds", LookMode.Value);
            Scribe_Collections.Look(ref factionSiteLayerIds, "factionSiteLayerIds", LookMode.Value);
            Scribe_Collections.Look(ref factionStorytellers, "factionStorytellers", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            sessionId ??= "";
            playerKeys ??= new List<string>(); playerNames ??= new List<string>();
            ownedPawns ??= new List<int>(); pawnOwners ??= new List<string>();
            sharedDifficultyDef ??= "";
            sharedDifficultyFingerprint ??= "";
            factionStorytellerOwners ??= new List<string>(); factionStorytellerDefs ??= new List<string>();
            factionSlots ??= new List<int>(); factionIds ??= new List<int>();
            factionSiteTileIds ??= new List<int>(); factionSiteLayerIds ??= new List<int>();
            factionStorytellers ??= new List<Storyteller>();
            lastFactionValidationTick = -1;
        }
    }
}
