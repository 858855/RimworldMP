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

        public LobbyRegistry(byte maximumCapacity)
        { if (maximumCapacity < 2 || maximumCapacity > 64) throw new ArgumentOutOfRangeException(nameof(maximumCapacity)); this.maximumCapacity = maximumCapacity; }

        public void AddMember(Guid id, string name, string? playerKey = null)
        {
            ValidateName(name);
            if (playerKey != null && !PlayerKeys.IsValid(playerKey)) throw new LobbyRuleException(1, "Invalid player key.");
            lock (gate)
            { if (id == Guid.Empty || members.ContainsKey(id)) throw new LobbyRuleException(6, "Invalid session."); members.Add(id, new Member(id, name, playerKey)); }
        }

        /// <summary>Persistent key of a connected member; null for members added without one.</summary>
        public string? PlayerKeyOf(Guid id)
        { lock (gate) return GetMember(id).PlayerKey; }

        public IReadOnlyList<LobbySummary> List()
        {
            lock (gate)
                return rooms.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id)
                    .Select(r => new LobbySummary(r.Id, r.Name, members[r.Host].Name, (byte)r.Members.Count, r.Capacity,
                        members[r.Host].Mods, r.Phase)).ToArray();
        }

        /// <summary>Stores a member's mod manifest summary; returns the lobby change when the member is in a lobby.</summary>
        public LobbyChange? SetManifest(Guid actor, ModManifestSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            lock (gate)
            {
                Member member = GetMember(actor);
                member.Mods = summary;
                if (member.Room == null) return null;
                Room room = GetRoom(member.Room.Value);
                ClearReadyWithoutMatch(room);
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>The host's session ID for a member's lobby request (e.g. the full mod list for the diff report).</summary>
        public Guid HostOfCurrentLobby(Guid actor)
        { lock (gate) return GetCurrentRoom(GetMember(actor)).Host; }

        public (Guid LobbyId, IReadOnlyList<Guid> Recipients) ChatRecipients(Guid actor)
        {
            lock (gate)
            {
                Room room = GetCurrentRoom(GetMember(actor));
                return (room.Id, room.Members.ToArray());
            }
        }

        /// <summary>Receivers of a chunked transfer: one member of the actor's lobby, or all others when target is empty.</summary>
        public IReadOnlyList<Guid> TransferRecipients(Guid actor, Guid target)
        {
            lock (gate)
            {
                Room room = GetCurrentRoom(GetMember(actor));
                if (target == actor) throw new LobbyRuleException(6, "Cannot send a transfer to yourself.");
                if (target != Guid.Empty)
                {
                    if (!room.Members.Contains(target)) throw new LobbyRuleException(4, "Receiver is not in this lobby.");
                    return new[] { target };
                }
                Guid[] others = room.Members.Where(id => id != actor).ToArray();
                if (others.Length == 0) throw new LobbyRuleException(6, "No other lobby members to receive the transfer.");
                return others;
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
                if (room.Phase != SetupPhase.Lobby) throw new LobbyRuleException(9, "Spieleinrichtung läuft – Beitritt gesperrt.");
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
                if (room.Phase != SetupPhase.Lobby) throw new LobbyRuleException(6, "Während der Spieleinrichtung nicht möglich.");
                if (ready)
                {
                    ModStatus status = StatusOf(room, member);
                    if (status == ModStatus.Checking) throw new LobbyRuleException(8, "Mod-Prüfung läuft noch.");
                    if (status == ModStatus.Mismatch) throw new LobbyRuleException(8, "Mods stimmen nicht mit dem Host überein.");
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
                member.Room = null; member.Ready = false; room.Members.Remove(actor); room.Revision++;
                if (room.Members.Count == 0) { rooms.Remove(room.Id); return new LobbyChange(null, Array.Empty<Guid>(), actor); }
                if (room.Host == actor)
                {
                    // Only the old host holds the generated setup state, so setup aborts before the host role moves.
                    if (room.Phase != SetupPhase.Lobby) ResetToLobby(room);
                    room.Host = room.Members[0]; ClearReadyWithoutMatch(room);
                }
                return Change(room, actor);
            }
        }

        /// <summary>START-001: host starts the shared setup when at least two players are ready and all mods match.</summary>
        public LobbyChange StartSetup(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                var blocked = StartBlock(Change(room, actor).State!, actor);
                if (blocked != null) throw new LobbyRuleException(blocked.Value.Code, blocked.Value.Reason);
                room.Phase = SetupPhase.Scenario; room.Revision++;
                return Change(room, actor);
            }
        }

        public LobbyChange AbortSetup(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Host != actor) throw new LobbyRuleException(3, "Nur der Host kann die Einrichtung abbrechen.");
                if (room.Phase == SetupPhase.Lobby) throw new LobbyRuleException(6, "Keine Spieleinrichtung aktiv.");
                ResetToLobby(room); room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>First reason why the host may not start yet, or null. Same rules the client shows next to the button.</summary>
        public static string? StartBlockReason(LobbyView lobby, Guid actor) => StartBlock(lobby, actor)?.Reason;

        // Error codes: 3 = not host, 6 = invalid state, 8 = mods.
        private static (byte Code, string Reason)? StartBlock(LobbyView lobby, Guid actor)
        {
            if (lobby.HostId != actor) return (3, "Nur der Host kann das Spiel starten.");
            if (lobby.Phase != SetupPhase.Lobby) return (6, "Spieleinrichtung läuft bereits.");
            if (lobby.Players.Count < 2) return (6, "Mindestens 2 Spieler nötig.");
            if (lobby.Players.Any(p => p.ModStatus == ModStatus.Checking)) return (8, "Mod-Prüfung läuft …");
            string[] mismatched = lobby.Players.Where(p => p.ModStatus != ModStatus.Match).Select(p => p.Name).ToArray();
            if (mismatched.Length > 0) return (8, "Mods stimmen nicht überein: " + string.Join(", ", mismatched));
            int ready = lobby.Players.Count(p => p.Ready);
            if (ready < lobby.Players.Count) return (6, "Warte auf Bereitschaft: " + ready + "/" + lobby.Players.Count + " bereit");
            return null;
        }

        private void ResetToLobby(Room room)
        {
            room.Phase = SetupPhase.Lobby;
            foreach (Guid id in room.Members) members[id].Ready = false;
        }

        public LobbyChange Close(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Host != actor) throw new LobbyRuleException(3, "Only host may close lobby.");
                Guid[] recipients = room.Members.ToArray();
                foreach (Guid id in recipients) { members[id].Room = null; members[id].Ready = false; }
                rooms.Remove(room.Id);
                return new LobbyChange(null, recipients, actor);
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
        private static void RequireFree(Member member)
        { if (member.Room != null) throw new LobbyRuleException(6, "Already in a lobby."); }
        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Any(char.IsControl))
                throw new LobbyRuleException(1, "Name must have 1-40 printable characters.");
        }
        // MOD-001: compare each member's manifest hash with the current host's.
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
            {
                ModStatus status = StatusOf(room, members[id]);
                if (status == ModStatus.Checking || status == ModStatus.Mismatch) members[id].Ready = false;
            }
        }

        private LobbyChange Change(Room room, Guid actor)
        {
            var players = room.Members.Select(id => new PlayerView(id, members[id].Name, members[id].Ready,
                StatusOf(room, members[id]))).ToArray();
            return new LobbyChange(new LobbyView(room.Id, room.Name, room.Host, room.Revision, room.Capacity, players, room.Phase),
                room.Members.ToArray(), actor);
        }
        private sealed class Member
        {
            public Member(Guid id, string name, string? playerKey) { Id = id; Name = name; PlayerKey = playerKey; }
            public Guid Id { get; }
            public string Name { get; }
            public string? PlayerKey { get; }
            public ModManifestSummary? Mods { get; set; }
            public Guid? Room { get; set; }
            public bool Ready { get; set; }
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
            public List<Guid> Members { get; } = new List<Guid>();
        }
    }
}
