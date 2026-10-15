using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;
using Verse;

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

        public MultiplayerSession() { }
        public MultiplayerSession(Game game) { }

        internal static MultiplayerSession? ForCurrentGame => Current.Game?.GetComponent<MultiplayerSession>();

        internal bool Active => sessionId.Length > 0;
        internal string SessionId => sessionId;

        /// <summary>Host, after the vanilla start: fills the session for the colonists that actually landed.</summary>
        internal void Begin(Guid lobbyId, IEnumerable<(PlayerKey Key, string Name)> players, IReadOnlyDictionary<int, PlayerKey> owners,
            ICollection<int> colonists)
        {
            sessionId = lobbyId.ToString("N");
            var list = players.ToList();
            playerKeys = list.Select(p => p.Key.ToString()).ToList();
            playerNames = list.Select(p => p.Name).ToList();
            var landed = owners.Where(o => colonists.Contains(o.Key)).OrderBy(o => o.Key).ToList();
            ownedPawns = landed.Select(o => o.Key).ToList();
            pawnOwners = landed.Select(o => o.Value.ToString()).ToList();
        }

        internal IEnumerable<(int PawnId, string Owner)> Owners => ownedPawns.Zip(pawnOwners, (pawn, owner) => (pawn, owner));
        internal IEnumerable<(string Key, string Name)> Players => playerKeys.Zip(playerNames, (key, name) => (key, name));

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
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            sessionId ??= "";
            playerKeys ??= new List<string>(); playerNames ??= new List<string>();
            ownedPawns ??= new List<int>(); pawnOwners ??= new List<string>();
        }
    }
}
