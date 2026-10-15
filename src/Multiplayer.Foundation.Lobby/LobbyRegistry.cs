using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Lobby
{
    public sealed class LobbyRuleException : Exception
    {
        public LobbyRuleException(byte code, string message) : base(message) { Code = code; }
        public byte Code { get; }
    }

    public sealed class LobbyChange
    {
        public LobbyChange(LobbyView? state, IReadOnlyList<Guid> recipients, Guid actor)
        { State = state; Recipients = recipients; Actor = actor; }
        public LobbyView? State { get; }
        public IReadOnlyList<Guid> Recipients { get; }
        public Guid Actor { get; }
    }

    public sealed class LobbyRegistry
    {
        private readonly object gate = new object();
        private readonly Dictionary<Guid, Member> members = new Dictionary<Guid, Member>();
        private readonly Dictionary<Guid, Room> rooms = new Dictionary<Guid, Room>();
        private readonly byte maximumCapacity;
        private readonly byte minimumStartPlayers;

        /// <param name="minimumStartPlayers">Players needed to start the shared setup: 2 normally, 1 only as a debug option.</param>
        public LobbyRegistry(byte maximumCapacity, byte minimumStartPlayers = 2)
        {
            if (maximumCapacity < 2 || maximumCapacity > 64) throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
            if (minimumStartPlayers < 1 || minimumStartPlayers > 2) throw new ArgumentOutOfRangeException(nameof(minimumStartPlayers));
            this.maximumCapacity = maximumCapacity; this.minimumStartPlayers = minimumStartPlayers;
        }

        public void AddMember(Guid id, string name, PlayerKey key)
        {
            ValidateName(name);
            if (!key.IsValid) throw new LobbyRuleException(1, "Invalid player key.");
            lock (gate)
            {
                if (id == Guid.Empty || members.ContainsKey(id)) throw new LobbyRuleException(6, "Invalid session.");
                // One connection per player: ownership and permissions hang on the key (ADR 0016).
                if (members.Values.Any(member => member.Key == key)) throw new LobbyRuleException(6, "This player is already connected.");
                members.Add(id, new Member(id, name, key));
            }
        }

        public IReadOnlyList<LobbySummary> List()
        {
            lock (gate)
                return rooms.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id)
                    .Select(r => new LobbySummary(r.Id, r.Name, members[r.Host].Name, (byte)r.Members.Count, r.Capacity, r.Phase,
                        members[r.Host].Mods)).ToArray();
        }

        /// <summary>MOD-001: stores a member's mod summary; returns the lobby change when the member is in a lobby.</summary>
        public LobbyChange? SetManifest(Guid actor, ModManifestSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            lock (gate)
            {
                Member member = GetMember(actor);
                member.Mods = summary;
                if (member.Room == null) return null;
                Room room = GetRoom(member.Room.Value);
                if (room.Phase == SetupPhase.Lobby) ClearReadyWithoutMatch(room);
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>Session ID of the host of the actor's lobby, e.g. for the full mod list behind the diff report.</summary>
        public Guid HostOfCurrentLobby(Guid actor)
        { lock (gate) return GetCurrentRoom(GetMember(actor)).Host; }

        /// <summary>First reason why the host may not start yet, or null. The same rules the server enforces.</summary>
        public static string? StartBlockReason(LobbyView lobby, Guid actor, bool allowSoloStart = false)
        {
            if (lobby.HostId != actor) return "Nur der Host kann das Spiel starten.";
            if (lobby.Phase != SetupPhase.Lobby) return "Spieleinrichtung läuft bereits.";
            if (lobby.Players.Count < (allowSoloStart ? 1 : 2)) return "Mindestens 2 Spieler nötig.";
            if (lobby.Players.Any(p => p.ModStatus == ModStatus.Checking)) return "Mod-Prüfung läuft …";
            string[] mismatched = lobby.Players.Where(p => p.ModStatus == ModStatus.Mismatch).Select(p => p.Name).ToArray();
            if (mismatched.Length > 0) return "Mods stimmen nicht überein: " + string.Join(", ", mismatched);
            int ready = lobby.Players.Count(p => p.Ready);
            if (ready < lobby.Players.Count) return "Warte auf Bereitschaft: " + ready + "/" + lobby.Players.Count + " bereit";
            return null;
        }

        public (Guid LobbyId, IReadOnlyList<Guid> Recipients) ChatRecipients(Guid actor)
        {
            lock (gate)
            {
                Room room = GetCurrentRoom(GetMember(actor));
                return (room.Id, room.Members.ToArray());
            }
        }

        public LobbyChange Create(Guid actor, string name, byte capacity)
        {
            ValidateName(name);
            lock (gate)
            {
                Member member = GetMember(actor); RequireFree(member);
                if (capacity < 2 || capacity > maximumCapacity) throw new LobbyRuleException(6, "Invalid lobby capacity.");
                if (rooms.Count >= 128) throw new LobbyRuleException(7, "Lobby limit reached.");
                var room = new Room(Guid.NewGuid(), name, actor, capacity); room.Members.Add(actor);
                rooms.Add(room.Id, room); member.Room = room.Id;
                return Change(room, actor);
            }
        }

        public LobbyChange Join(Guid actor, Guid lobbyId)
        {
            lock (gate)
            {
                Member member = GetMember(actor); RequireFree(member);
                Room room = GetRoom(lobbyId);
                if (room.Phase != SetupPhase.Lobby) throw new LobbyRuleException(6, "Setup in progress.");
                if (room.Members.Count >= room.Capacity) throw new LobbyRuleException(5, "Lobby is full.");
                room.Members.Add(actor); member.Room = room.Id; room.Revision++;
                return Change(room, actor);
            }
        }

        public LobbyChange Ready(Guid actor, bool ready)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                // Ready means "ready to start" in the lobby and "done" in character selection; nowhere else.
                if (room.Phase != SetupPhase.Lobby && room.Phase != SetupPhase.Characters)
                    throw new LobbyRuleException(6, "Ready is not used in this setup step.");
                // SETUP-004: "done" needs a pawn of your own in the starting group (ADR 0015).
                if (ready && room.Phase == SetupPhase.Characters && (room.Roster == null || !room.Roster.OwnsSelectedPawn(member.Key)))
                    throw new LobbyRuleException(6, "Take a pawn before you are done.");
                if (ready && room.Phase == SetupPhase.Lobby)
                {
                    ModStatus status = StatusOf(room, member);
                    if (status == ModStatus.Checking) throw new LobbyRuleException(8, "Mod check is still running.");
                    if (status == ModStatus.Mismatch) throw new LobbyRuleException(8, "Mods do not match the host.");
                }
                member.Ready = ready; room.Revision++;
                return Change(room, actor);
            }
        }

        public LobbyChange Leave(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                member.Room = null; member.Ready = false; member.Flag = null; room.Members.Remove(actor); room.Revision++;
                if (room.Members.Count == 0) { rooms.Remove(room.Id); return new LobbyChange(null, Array.Empty<Guid>(), actor); }
                // Only the host holds the generated setup state, so it ends before the host role moves (plan §6.2).
                if (room.Host == actor) { ResetSetup(room); room.Host = room.Members[0]; ClearReadyWithoutMatch(room); }
                return Change(room, actor);
            }
        }

        public LobbyChange Close(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Host != actor) throw new LobbyRuleException(3, "Only host may close lobby.");
                Guid[] recipients = room.Members.ToArray();
                foreach (Guid id in recipients) { members[id].Room = null; members[id].Ready = false; members[id].Flag = null; }
                rooms.Remove(room.Id);
                return new LobbyChange(null, recipients, actor);
            }
        }

        /// <summary>Host moves setup one phase on. The target must be the next phase, so a repeated click skips nothing.</summary>
        public LobbyChange AdvanceSetup(Guid actor, SetupPhase target)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase == SetupPhase.Playing) throw new LobbyRuleException(6, "Setup is complete; time runs with lockstep (SYNC-001).");
                if (target != room.Phase + 1) throw new LobbyRuleException(6, "Unexpected setup step.");
                switch (room.Phase)
                {
                    case SetupPhase.Lobby:
                        if (room.Members.Count < minimumStartPlayers) throw new LobbyRuleException(6, "At least 2 players are needed.");
                        // MOD-001: start again checks that every member's mods match the host's.
                        if (room.Members.Any(id => StatusOf(room, members[id]) is ModStatus.Checking or ModStatus.Mismatch))
                            throw new LobbyRuleException(8, "Mods do not match for every player.");
                        RequireEveryoneReady(room, "Not every player is ready.");
                        break;
                    case SetupPhase.WorldTransfer:
                        // SETUP-002: every guest must have loaded the host's world with the same fingerprint.
                        if (room.HostWorld == null) throw new LobbyRuleException(6, "The world has not been sent yet.");
                        if (room.Members.Any(id => WorldStatusOf(room, members[id]) == WorldStatus.Mismatch))
                            throw new LobbyRuleException(8, "The world differs for at least one player.");
                        if (room.Members.Any(id => WorldStatusOf(room, members[id]) != WorldStatus.Match))
                            throw new LobbyRuleException(6, "Not every player has loaded the world yet.");
                        break;
                    case SetupPhase.SiteSelection:
                        throw new LobbyRuleException(6, "Choose the starting site to continue.");
                    case SetupPhase.Characters:
                        // ADR 0015: everyone must be done; the host cannot force-start.
                        RequireEveryoneReady(room, "Not every player is done choosing characters.");
                        break;
                    case SetupPhase.Generating:
                        // SETUP-005: every guest must have loaded the host's starting game with the same fingerprint.
                        if (room.HostGame == null) throw new LobbyRuleException(6, "The game has not been sent yet.");
                        if (room.Members.Any(id => WorldStatusOf(room, members[id]) == WorldStatus.Mismatch))
                            throw new LobbyRuleException(8, "The game differs for at least one player.");
                        if (room.Members.Any(id => WorldStatusOf(room, members[id]) != WorldStatus.Match))
                            throw new LobbyRuleException(6, "Not every player has loaded the game yet.");
                        break;
                }
                EnterPhase(room, target);
                return Change(room, actor);
            }
        }

        public LobbyChange AbortSetup(Guid actor)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase == SetupPhase.Lobby) throw new LobbyRuleException(6, "No setup is running.");
                ResetSetup(room); room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>Any member places, moves or (with null) removes their site suggestion.</summary>
        public LobbyChange SetSiteFlag(Guid actor, SiteTile? tile)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Phase != SetupPhase.SiteSelection) throw new LobbyRuleException(6, "Flags are only placed during site selection.");
                member.Flag = tile; room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>SETUP-002: the host announces the fingerprint of the world it is about to send.</summary>
        public LobbyChange SetHostWorld(Guid actor, byte[] fingerprint)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase != SetupPhase.WorldTransfer) throw new LobbyRuleException(6, "The world is sent during world transfer.");
                room.HostWorld = fingerprint;
                // A new world (host went back and generated again) invalidates every earlier report.
                foreach (Guid id in room.Members) members[id].World = null;
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>SETUP-002: a guest reports the fingerprint of the world it loaded.</summary>
        public LobbyChange ReportWorld(Guid actor, byte[] fingerprint)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Phase != SetupPhase.WorldTransfer) throw new LobbyRuleException(6, "The world is reported during world transfer.");
                if (room.Host == actor) throw new LobbyRuleException(6, "The host announces its world with WorldReady.");
                member.World = fingerprint; room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>
        /// SETUP-005: the host announces the fingerprint of the starting game after loading its own save. Guests load the
        /// copy at the same time and may report first; their reports count once this arrives.
        /// </summary>
        public LobbyChange SetHostGame(Guid actor, byte[] fingerprint)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase != SetupPhase.Generating) throw new LobbyRuleException(6, "The game is sent while it is generated.");
                room.HostGame = fingerprint;
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>SETUP-005: a guest reports the fingerprint of the game it loaded.</summary>
        public LobbyChange ReportGame(Guid actor, byte[] fingerprint)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Phase != SetupPhase.Generating) throw new LobbyRuleException(6, "The game is reported while it is generated.");
                if (room.Host == actor) throw new LobbyRuleException(6, "The host announces its game with GameReady.");
                member.Game = fingerprint; room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>
        /// SETUP-004: the host publishes the starting pawns. A member whose own pawns changed loses "done" (ADR 0015).
        /// Returns the change and the members to forward the roster to.
        /// </summary>
        public (LobbyChange Change, IReadOnlyList<Guid> Viewers) SetRoster(Guid actor, PawnRoster roster)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase != SetupPhase.Characters) throw new LobbyRuleException(6, "Pawns are chosen during character selection.");
                room.Roster = roster;
                foreach (Guid id in room.Members)
                {
                    Member member = members[id];
                    byte[] hash = roster.OwnerHash(member.Key);
                    if (member.PawnHash != null && !member.PawnHash.SequenceEqual(hash)) member.Ready = false;
                    if (!roster.OwnsSelectedPawn(member.Key)) member.Ready = false;
                    member.PawnHash = hash;
                }
                room.Revision++;
                return (Change(room, actor), room.Members.Where(id => id != actor).ToArray());
            }
        }

        /// <summary>SETUP-004: a guest's pawn request goes to the host, stamped with the guest's player key.</summary>
        public (Guid Host, PlayerKey Requester) PawnRequestRoute(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Phase != SetupPhase.Characters) throw new LobbyRuleException(6, "Pawns are chosen during character selection.");
                if (room.Host == actor) throw new LobbyRuleException(6, "The host changes pawns in its own game.");
                return (room.Host, member.Key);
            }
        }

        /// <summary>SETUP-001: the host's page mirror goes to every other member while setup runs.</summary>
        public IReadOnlyList<Guid> SetupSettingsRecipients(Guid actor)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase == SetupPhase.Lobby) throw new LobbyRuleException(6, "No setup is running.");
                return room.Members.Where(id => id != actor).ToArray();
            }
        }

        /// <summary>Only the host chooses the site, flagged or not (ADR 0015).</summary>
        public LobbyChange DecideSite(Guid actor, SiteTile tile)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase != SetupPhase.SiteSelection) throw new LobbyRuleException(6, "The site is chosen during site selection.");
                room.Site = tile;
                EnterPhase(room, SetupPhase.Ideology);
                return Change(room, actor);
            }
        }

        public LobbyChange? RemoveMember(Guid id)
        {
            lock (gate)
            {
                if (!members.TryGetValue(id, out Member? member)) return null;
                LobbyChange? change = null;
                if (member.Room != null) change = Leave(id);
                members.Remove(id); return change;
            }
        }

        private Member GetMember(Guid id)
        { if (!members.TryGetValue(id, out Member? member)) throw new LobbyRuleException(3, "Unknown session."); return member; }
        private Room GetRoom(Guid id)
        { if (!rooms.TryGetValue(id, out Room? room)) throw new LobbyRuleException(4, "Lobby not found."); return room; }
        private Room GetCurrentRoom(Member member)
        { if (member.Room == null) throw new LobbyRuleException(6, "Not in a lobby."); return GetRoom(member.Room.Value); }
        private Room GetHostedRoom(Guid actor)
        {
            Room room = GetCurrentRoom(GetMember(actor));
            if (room.Host != actor) throw new LobbyRuleException(3, "Only the host can do this.");
            return room;
        }
        // MOD-001: compare each member's mod checksum with the current host's.
        private ModStatus StatusOf(Room room, Member member)
        {
            ModManifestSummary? host = members[room.Host].Mods;
            if (host == null) return member.Mods == null || member.Id == room.Host ? ModStatus.Unchecked : ModStatus.Checking;
            if (member.Mods == null) return ModStatus.Checking;
            return member.Mods.SameAs(host) ? ModStatus.Match : ModStatus.Mismatch;
        }

        private void ClearReadyWithoutMatch(Room room)
        {
            foreach (Guid id in room.Members)
                if (StatusOf(room, members[id]) is ModStatus.Checking or ModStatus.Mismatch) members[id].Ready = false;
        }

        private void RequireEveryoneReady(Room room, string message)
        { if (room.Members.Any(id => !members[id].Ready)) throw new LobbyRuleException(6, message); }
        // Ready and flags belong to one phase; a lobby "ready" must never count as "done" later.
        private void EnterPhase(Room room, SetupPhase phase)
        {
            room.Phase = phase; room.Revision++; room.Roster = null;
            foreach (Guid id in room.Members) { members[id].Ready = false; members[id].Flag = null; members[id].PawnHash = null; }
        }
        private void ResetSetup(Room room)
        {
            room.Phase = SetupPhase.Lobby; room.Site = null; room.HostWorld = null; room.Roster = null; room.HostGame = null;
            foreach (Guid id in room.Members)
            {
                members[id].Ready = false; members[id].Flag = null; members[id].World = null; members[id].PawnHash = null;
                members[id].Game = null;
            }
        }
        // One status for the copy that matters now: the world in WorldTransfer, the starting game from Generating on.
        private WorldStatus WorldStatusOf(Room room, Member member)
        {
            byte[]? hostCopy, ownCopy;
            if (room.Phase == SetupPhase.WorldTransfer) { hostCopy = room.HostWorld; ownCopy = member.World; }
            else if (room.Phase >= SetupPhase.Generating) { hostCopy = room.HostGame; ownCopy = member.Game; }
            else return WorldStatus.None;
            if (hostCopy == null) return WorldStatus.Waiting;
            if (member.Id == room.Host) return WorldStatus.Match;
            if (ownCopy == null) return WorldStatus.Waiting;
            return ownCopy.SequenceEqual(hostCopy) ? WorldStatus.Match : WorldStatus.Mismatch;
        }
        private static void RequireFree(Member member)
        { if (member.Room != null) throw new LobbyRuleException(6, "Already in a lobby."); }
        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Any(char.IsControl))
                throw new LobbyRuleException(1, "Name must have 1-40 printable characters.");
        }
        private LobbyChange Change(Room room, Guid actor)
        {
            var players = room.Members.Select(id => new PlayerView(id, members[id].Name, members[id].Ready, members[id].Key,
                members[id].Flag, StatusOf(room, members[id]), WorldStatusOf(room, members[id]))).ToArray();
            return new LobbyChange(new LobbyView(room.Id, room.Name, room.Host, room.Revision, room.Capacity, players,
                room.Phase, room.Site), room.Members.ToArray(), actor);
        }
        private sealed class Member
        {
            public Member(Guid id, string name, PlayerKey key) { Id = id; Name = name; Key = key; }
            public Guid Id { get; }
            public string Name { get; }
            public PlayerKey Key { get; }
            public ModManifestSummary? Mods { get; set; }
            public byte[]? World { get; set; }
            public byte[]? PawnHash { get; set; }
            public byte[]? Game { get; set; }
            public Guid? Room { get; set; }
            public bool Ready { get; set; }
            public SiteTile? Flag { get; set; }
        }
        private sealed class Room
        {
            public Room(Guid id, string name, Guid host, byte capacity) { Id = id; Name = name; Host = host; Capacity = capacity; }
            public Guid Id { get; }
            public string Name { get; }
            public Guid Host { get; set; }
            public byte Capacity { get; }
            public ulong Revision { get; set; } = 1;
            public SetupPhase Phase { get; set; } = SetupPhase.Lobby;
            public SiteTile? Site { get; set; }
            public byte[]? HostWorld { get; set; }
            public byte[]? HostGame { get; set; }
            public PawnRoster? Roster { get; set; }
            public List<Guid> Members { get; } = new List<Guid>();
        }
    }
}
