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

        public void AddMember(Guid id, string name)
        {
            ValidateName(name);
            lock (gate)
            { if (id == Guid.Empty || members.ContainsKey(id)) throw new LobbyRuleException(6, "Invalid session."); members.Add(id, new Member(id, name)); }
        }

        public IReadOnlyList<LobbySummary> List()
        {
            lock (gate)
                return rooms.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id)
                    .Select(r => new LobbySummary(r.Id, r.Name, members[r.Host].Name, (byte)r.Members.Count, r.Capacity)).ToArray();
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
                if (room.Host == actor) room.Host = room.Members[0];
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
        private LobbyChange Change(Room room, Guid actor)
        {
            var players = room.Members.Select(id => new PlayerView(id, members[id].Name, members[id].Ready)).ToArray();
            return new LobbyChange(new LobbyView(room.Id, room.Name, room.Host, room.Revision, room.Capacity, players), room.Members.ToArray(), actor);
        }
        private sealed class Member
        {
            public Member(Guid id, string name) { Id = id; Name = name; }
            public Guid Id { get; }
            public string Name { get; }
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
            public List<Guid> Members { get; } = new List<Guid>();
        }
    }
}
