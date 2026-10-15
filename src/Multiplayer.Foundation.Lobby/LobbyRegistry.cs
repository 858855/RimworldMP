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
                    .Select(r => new LobbySummary(r.Id, r.Name, members[r.Host].Name, (byte)r.Members.Count, r.Capacity, r.Phase)).ToArray();
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
                if (room.Host == actor) { ResetSetup(room); room.Host = room.Members[0]; }
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
                if (room.Phase == SetupPhase.Generating) throw new LobbyRuleException(6, "The next setup step is not available yet.");
                if (target != room.Phase + 1) throw new LobbyRuleException(6, "Unexpected setup step.");
                switch (room.Phase)
                {
                    case SetupPhase.Lobby:
                        if (room.Members.Count < 2) throw new LobbyRuleException(6, "At least 2 players are needed.");
                        RequireEveryoneReady(room, "Not every player is ready.");
                        break;
                    case SetupPhase.SiteSelection:
                        throw new LobbyRuleException(6, "Choose the starting site to continue.");
                    case SetupPhase.Characters:
                        // ADR 0015: everyone must be done; the host cannot force-start.
                        RequireEveryoneReady(room, "Not every player is done choosing characters.");
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
        private void RequireEveryoneReady(Room room, string message)
        { if (room.Members.Any(id => !members[id].Ready)) throw new LobbyRuleException(6, message); }
        // Ready and flags belong to one phase; a lobby "ready" must never count as "done" later.
        private void EnterPhase(Room room, SetupPhase phase)
        {
            room.Phase = phase; room.Revision++;
            foreach (Guid id in room.Members) { members[id].Ready = false; members[id].Flag = null; }
        }
        private void ResetSetup(Room room)
        {
            room.Phase = SetupPhase.Lobby; room.Site = null;
            foreach (Guid id in room.Members) { members[id].Ready = false; members[id].Flag = null; }
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
                members[id].Flag)).ToArray();
            return new LobbyChange(new LobbyView(room.Id, room.Name, room.Host, room.Revision, room.Capacity, players,
                room.Phase, room.Site), room.Members.ToArray(), actor);
        }
        private sealed class Member
        {
            public Member(Guid id, string name, PlayerKey key) { Id = id; Name = name; Key = key; }
            public Guid Id { get; }
            public string Name { get; }
            public PlayerKey Key { get; }
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
            public List<Guid> Members { get; } = new List<Guid>();
        }
    }
}
