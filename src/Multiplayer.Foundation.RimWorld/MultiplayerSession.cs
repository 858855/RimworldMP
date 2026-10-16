using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;
using ProtocolFactionRelation = Multiplayer.Foundation.Protocol.FactionRelation;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Host-side assignment after real player factions have been generated.</summary>
    internal sealed class FactionStorytellerAssignment
    {
        internal FactionStorytellerAssignment(PlayerKey player, byte slot, Faction faction, StorytellerDef storyteller)
        { Player = player; Slot = slot; Faction = faction; Storyteller = storyteller; }

        internal PlayerKey Player { get; }
        internal byte Slot { get; }
        internal Faction Faction { get; }
        internal StorytellerDef Storyteller { get; }
    }

    internal static class FactionStorytellerCatalog
    {
        /// <summary>Only installed, player-visible choices compatible with the host's difficulty.</summary>
        internal static IReadOnlyList<StorytellerDef> AvailableFor(DifficultyDef difficulty) =>
            DefDatabase<StorytellerDef>.AllDefsListForReading
                .Where(def => def != null && def.listVisible && !def.tutorialMode &&
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
                        !allowed.Contains(assignment.Storyteller))
                        throw new InvalidOperationException("Ungültige Fraktion oder Erzählerwahl für Platz " + assignment.Slot + ".");
                    selectedOwners.Add(assignment.Player.ToString());
                    selectedDefs.Add(assignment.Storyteller.defName);
                    selectedSlots.Add(assignment.Slot);
                    selectedFactionIds.Add(assignment.Faction.loadID);
                }
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

        /// <summary>Fail closed until every faction choice and the shared difficulty can be restored.</summary>
        internal string? FactionStorytellerError()
        {
            if (Mode?.Mode != GameMode.Factions) return null;
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
                playerKeys.Distinct().Count() != playerKeys.Count ||
                factionStorytellerOwners.Distinct().Count() != playerKeys.Count ||
                factionSlots.Distinct().Count() != playerKeys.Count ||
                factionIds.Distinct().Count() != playerKeys.Count ||
                factionSlots.Any(slot => slot < 0 || slot >= 64) ||
                !factionSlots.SequenceEqual(factionSlots.OrderBy(slot => slot)) ||
                !factionStorytellerOwners.All(playerKeys.Contains))
                return "Fraktionsplätze, Fraktionen oder Erzähler sind nicht eindeutig zugeordnet.";
            DifficultyDef difficulty = game.storyteller.difficultyDef;
            var allowed = FactionStorytellerCatalog.AvailableFor(difficulty);
            var worldFactions = game.World?.factionManager?.AllFactionsListForReading;
            if (worldFactions == null || factionIds.Any(id => !worldFactions.Any(f => f.loadID == id && f.def?.isPlayer == true)))
                return "Eine gespeicherte Spielerfraktion fehlt in der Welt.";
            foreach (string name in factionStorytellerDefs)
            {
                if (!allowed.Any(def => def.defName == name))
                    return "Ein Fraktions-Erzähler fehlt oder erzwingt eine andere Schwierigkeit.";
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
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            sessionId ??= "";
            playerKeys ??= new List<string>(); playerNames ??= new List<string>();
            ownedPawns ??= new List<int>(); pawnOwners ??= new List<string>();
            sharedDifficultyDef ??= "";
            sharedDifficultyFingerprint ??= "";
            factionStorytellerOwners ??= new List<string>(); factionStorytellerDefs ??= new List<string>();
            factionSlots ??= new List<int>(); factionIds ??= new List<int>();
        }
    }
}
