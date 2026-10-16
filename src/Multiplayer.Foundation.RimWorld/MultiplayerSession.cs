using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;
using ProtocolFactionRelation = Multiplayer.Foundation.Protocol.FactionRelation;

namespace Multiplayer.Foundation.RimWorld
{
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
        private List<string> factionStorytellerOwners = new List<string>();
        private List<string> factionStorytellerDefs = new List<string>();

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
            ICollection<int> colonists, IReadOnlyDictionary<PlayerKey, StorytellerDef>? storytellerChoices = null)
        {
            var list = players.ToList();
            var selectedOwners = new List<string>();
            var selectedDefs = new List<string>();
            string selectedDifficulty = "";
            if (mode.Mode == GameMode.Factions)
            {
                DifficultyDef? difficulty = Current.Game?.storyteller?.difficultyDef;
                if (difficulty == null || storytellerChoices == null || storytellerChoices.Count != list.Count ||
                    list.Select(p => p.Key).Distinct().Count() != list.Count)
                    throw new InvalidOperationException("Fraktions-Erzähler oder gemeinsame Schwierigkeit fehlen.");
                selectedDifficulty = difficulty.defName;
                foreach (var player in list)
                {
                    if (!storytellerChoices.TryGetValue(player.Key, out StorytellerDef? chosen) || chosen == null ||
                        !DefDatabase<StorytellerDef>.AllDefsListForReading.Contains(chosen) ||
                        !chosen.listVisible || chosen.tutorialMode ||
                        (chosen.forcedDifficulty != null && chosen.forcedDifficulty != difficulty))
                        throw new InvalidOperationException("Ungültiger Fraktions-Erzähler für " + player.Name + ".");
                    selectedOwners.Add(player.Key.ToString());
                    selectedDefs.Add(chosen.defName);
                }
            }
            sessionId = lobbyId.ToString("N");
            gameMode = (int)mode.Mode;
            coopDecisions = (int)mode.Decisions;
            factionRelation = (int)mode.Relation;
            playerKeys = list.Select(p => p.Key.ToString()).ToList();
            playerNames = list.Select(p => p.Name).ToList();
            sharedDifficultyDef = selectedDifficulty;
            factionStorytellerOwners = selectedOwners;
            factionStorytellerDefs = selectedDefs;
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

        /// <summary>Fail closed until every faction choice and the shared difficulty can be restored.</summary>
        internal string? FactionStorytellerError()
        {
            if (Mode?.Mode != GameMode.Factions) return null;
            if (sharedDifficultyDef.Length == 0 || Current.Game?.storyteller?.difficultyDef?.defName != sharedDifficultyDef)
                return "Gemeinsame Schwierigkeit fehlt oder weicht vom Spielstand ab.";
            if (factionStorytellerOwners.Count != playerKeys.Count ||
                factionStorytellerDefs.Count != playerKeys.Count ||
                playerKeys.Distinct().Count() != playerKeys.Count ||
                !factionStorytellerOwners.SequenceEqual(playerKeys))
                return "Fraktions-Erzähler sind nicht jedem Spieler eindeutig zugeordnet.";
            DifficultyDef difficulty = Current.Game.storyteller.difficultyDef;
            foreach (string name in factionStorytellerDefs)
            {
                StorytellerDef? chosen = DefDatabase<StorytellerDef>.AllDefsListForReading
                    .FirstOrDefault(def => def.defName == name);
                if (chosen == null || !chosen.listVisible || chosen.tutorialMode ||
                    (chosen.forcedDifficulty != null && chosen.forcedDifficulty != difficulty))
                    return "Ein Fraktions-Erzähler fehlt oder erzwingt eine andere Schwierigkeit.";
            }
            return null;
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
            Scribe_Collections.Look(ref factionStorytellerOwners, "factionStorytellerOwners", LookMode.Value);
            Scribe_Collections.Look(ref factionStorytellerDefs, "factionStorytellerDefs", LookMode.Value);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            sessionId ??= "";
            playerKeys ??= new List<string>(); playerNames ??= new List<string>();
            ownedPawns ??= new List<int>(); pawnOwners ??= new List<string>();
            sharedDifficultyDef ??= "";
            factionStorytellerOwners ??= new List<string>(); factionStorytellerDefs ??= new List<string>();
        }
    }
}
