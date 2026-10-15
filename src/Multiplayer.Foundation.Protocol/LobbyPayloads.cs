using System;
using System.Collections.Generic;
using System.Linq;

namespace Multiplayer.Foundation.Protocol
{
    public sealed class PlayerView
    {
        public PlayerView(Guid id, string name, bool ready) { Id = id; Name = name; Ready = ready; }
        public Guid Id { get; }
        public string Name { get; }
        public bool Ready { get; }
    }

    public sealed class LobbyView
    {
        public LobbyView(Guid id, string name, Guid hostId, ulong revision, byte capacity, IReadOnlyList<PlayerView> players)
        { Id = id; Name = name; HostId = hostId; Revision = revision; Capacity = capacity; Players = players; }
        public Guid Id { get; }
        public string Name { get; }
        public Guid HostId { get; }
        public ulong Revision { get; }
        public byte Capacity { get; }
        public IReadOnlyList<PlayerView> Players { get; }
    }

    public sealed class LobbySummary
    {
        public LobbySummary(Guid id, string name, string hostName, byte playerCount, byte capacity)
        { Id = id; Name = name; HostName = hostName; PlayerCount = playerCount; Capacity = capacity; }
        public Guid Id { get; }
        public string Name { get; }
        public string HostName { get; }
        public byte PlayerCount { get; }
        public byte Capacity { get; }
    }

    public sealed class LobbyChatView
    {
        public LobbyChatView(Guid lobbyId, Guid senderId, string senderName, string text)
        { LobbyId = lobbyId; SenderId = senderId; SenderName = senderName; Text = text; }
        public Guid LobbyId { get; }
        public Guid SenderId { get; }
        public string SenderName { get; }
        public string Text { get; }
    }

    public static class LobbyPayloads
    {
        public const int MaximumChatBytes = 512;

        public static byte[] ChatRequest(string text)
        { ValidateChat(text); var writer = new PayloadWriter(); writer.WriteString(text, MaximumChatBytes); return writer.ToArray(); }
        public static string ReadChatRequest(byte[] bytes)
        { var reader = new PayloadReader(bytes); string text = reader.ReadString(MaximumChatBytes); reader.EnsureComplete(); ValidateChat(text); return text; }
        public static byte[] ChatMessage(Guid lobbyId, Guid senderId, string senderName, string text)
        {
            ValidateChat(text);
            var writer = new PayloadWriter(); writer.WriteGuid(lobbyId); writer.WriteGuid(senderId);
            writer.WriteString(senderName, 160); writer.WriteString(text, MaximumChatBytes);
            return writer.ToArray();
        }
        public static LobbyChatView ReadChatMessage(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            Guid lobbyId = reader.ReadGuid(); Guid senderId = reader.ReadGuid();
            string senderName = reader.ReadString(160); string text = reader.ReadString(MaximumChatBytes);
            reader.EnsureComplete(); ValidateChat(text);
            return new LobbyChatView(lobbyId, senderId, senderName, text);
        }
        private static void ValidateChat(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Any(char.IsControl))
                throw new ProtocolException("Chat message must contain printable text.");
        }

        public static byte[] String(string value, int maxBytes = 160)
        { var writer = new PayloadWriter(); writer.WriteString(value, maxBytes); return writer.ToArray(); }
        public static string ReadString(byte[] bytes, int maxBytes = 160)
        { var reader = new PayloadReader(bytes); string value = reader.ReadString(maxBytes); reader.EnsureComplete(); return value; }
        public static byte[] GuidValue(Guid value)
        { var writer = new PayloadWriter(); writer.WriteGuid(value); return writer.ToArray(); }
        public static Guid ReadGuid(byte[] bytes)
        { var reader = new PayloadReader(bytes); Guid value = reader.ReadGuid(); reader.EnsureComplete(); return value; }
        public static byte[] Bool(bool value)
        { var writer = new PayloadWriter(); writer.WriteBool(value); return writer.ToArray(); }
        public static bool ReadBool(byte[] bytes)
        { var reader = new PayloadReader(bytes); bool value = reader.ReadBool(); reader.EnsureComplete(); return value; }
        public static byte[] Ping(ulong value)
        { var writer = new PayloadWriter(); writer.WriteUInt64(value); return writer.ToArray(); }
        public static ulong ReadPing(byte[] bytes)
        { var reader = new PayloadReader(bytes); ulong value = reader.ReadUInt64(); reader.EnsureComplete(); return value; }
        public static byte[] Accepted(Guid sessionId, ushort heartbeatSeconds)
        { var writer = new PayloadWriter(); writer.WriteGuid(sessionId); writer.WriteUInt16(heartbeatSeconds); return writer.ToArray(); }
        public static byte[] Error(byte code, string reason)
        { var writer = new PayloadWriter(); writer.WriteByte(code); writer.WriteString(reason); return writer.ToArray(); }
        public static byte[] Create(string name, byte capacity)
        { var writer = new PayloadWriter(); writer.WriteString(name, 160); writer.WriteByte(capacity); return writer.ToArray(); }
        public static (string Name, byte Capacity) ReadCreate(byte[] bytes)
        { var reader = new PayloadReader(bytes); string name = reader.ReadString(160); byte capacity = reader.ReadByte(); reader.EnsureComplete(); return (name, capacity); }
        public static byte[] State(LobbyView state)
        {
            var writer = new PayloadWriter();
            writer.WriteGuid(state.Id); writer.WriteString(state.Name, 160); writer.WriteGuid(state.HostId);
            writer.WriteUInt64(state.Revision); writer.WriteByte(state.Capacity);
            if (state.Players.Count > 64) throw new ProtocolException("Too many players.");
            writer.WriteByte((byte)state.Players.Count);
            foreach (PlayerView player in state.Players)
            { writer.WriteGuid(player.Id); writer.WriteString(player.Name, 160); writer.WriteBool(player.Ready); }
            return writer.ToArray();
        }
        public static LobbyView ReadState(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            Guid id = reader.ReadGuid(); string name = reader.ReadString(160); Guid host = reader.ReadGuid();
            ulong revision = reader.ReadUInt64(); byte capacity = reader.ReadByte(); byte count = reader.ReadByte();
            if (count > 64 || count > capacity) throw new ProtocolException("Invalid player count.");
            var players = new List<PlayerView>();
            for (int i = 0; i < count; i++) players.Add(new PlayerView(reader.ReadGuid(), reader.ReadString(160), reader.ReadBool()));
            reader.EnsureComplete(); return new LobbyView(id, name, host, revision, capacity, players);
        }
        public static byte[] List(IReadOnlyList<LobbySummary> lobbies)
        {
            if (lobbies.Count > 128) throw new ProtocolException("Too many lobbies.");
            var writer = new PayloadWriter(); writer.WriteByte((byte)lobbies.Count);
            foreach (LobbySummary lobby in lobbies)
            { writer.WriteGuid(lobby.Id); writer.WriteString(lobby.Name, 160); writer.WriteString(lobby.HostName, 160); writer.WriteByte(lobby.PlayerCount); writer.WriteByte(lobby.Capacity); }
            return writer.ToArray();
        }
        public static IReadOnlyList<LobbySummary> ReadList(byte[] bytes)
        {
            var reader = new PayloadReader(bytes); byte count = reader.ReadByte();
            if (count > 128) throw new ProtocolException("Too many lobbies.");
            var lobbies = new List<LobbySummary>();
            for (int i = 0; i < count; i++)
            { lobbies.Add(new LobbySummary(reader.ReadGuid(), reader.ReadString(160), reader.ReadString(160), reader.ReadByte(), reader.ReadByte())); }
            reader.EnsureComplete(); return lobbies;
        }
    }
}
