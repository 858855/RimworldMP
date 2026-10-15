using System;
using System.Collections.Generic;
using System.Linq;

namespace Multiplayer.Foundation.Protocol
{
    public sealed class PlayerView
    {
        public PlayerView(Guid id, string name, bool ready, PlayerKey key, SiteTile? flag = null,
            ModStatus modStatus = ModStatus.Unchecked, WorldStatus worldStatus = WorldStatus.None)
        { Id = id; Name = name; Ready = ready; Key = key; Flag = flag; ModStatus = modStatus; WorldStatus = worldStatus; }
        /// <summary>Session ID of this connection; changes on every reconnect.</summary>
        public Guid Id { get; }
        /// <summary>Persistent identity; use this for anything saved.</summary>
        public PlayerKey Key { get; }
        public string Name { get; }
        /// <summary>Lobby: ready to start. Characters: done choosing. Cleared on every setup phase change.</summary>
        public bool Ready { get; }
        /// <summary>Site suggestion during SiteSelection; carries no voting weight (ADR 0015).</summary>
        public SiteTile? Flag { get; }
        /// <summary>This member's mods compared with the current host's (MOD-001).</summary>
        public ModStatus ModStatus { get; }
        /// <summary>SETUP-002: host's world loaded with a matching fingerprint.</summary>
        public WorldStatus WorldStatus { get; }
    }

    public sealed class LobbyView
    {
        public LobbyView(Guid id, string name, Guid hostId, ulong revision, byte capacity, IReadOnlyList<PlayerView> players,
            SetupPhase phase = SetupPhase.Lobby, SiteTile? site = null)
        { Id = id; Name = name; HostId = hostId; Revision = revision; Capacity = capacity; Players = players; Phase = phase; Site = site; }
        public Guid Id { get; }
        public string Name { get; }
        public Guid HostId { get; }
        public ulong Revision { get; }
        public byte Capacity { get; }
        public IReadOnlyList<PlayerView> Players { get; }
        public SetupPhase Phase { get; }
        /// <summary>Starting site chosen by the host; set once SiteSelection ends.</summary>
        public SiteTile? Site { get; }
    }

    public sealed class LobbySummary
    {
        public LobbySummary(Guid id, string name, string hostName, byte playerCount, byte capacity, SetupPhase phase = SetupPhase.Lobby,
            ModManifestSummary? hostMods = null)
        { Id = id; Name = name; HostName = hostName; PlayerCount = playerCount; Capacity = capacity; Phase = phase; HostMods = hostMods; }
        /// <summary>Host's mod checksum, mod count and game version, so joiners see a mismatch before joining.</summary>
        public ModManifestSummary? HostMods { get; }
        public Guid Id { get; }
        public string Name { get; }
        public string HostName { get; }
        public byte PlayerCount { get; }
        public byte Capacity { get; }
        /// <summary>Anything but Lobby means setup is running and joining is refused.</summary>
        public SetupPhase Phase { get; }
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

        public static byte[] Hello(string playerName, PlayerKey key)
        { var writer = new PayloadWriter(); writer.WriteString(playerName, 160); key.Write(writer); return writer.ToArray(); }
        public static (string Name, PlayerKey Key) ReadHello(byte[] bytes)
        { var reader = new PayloadReader(bytes); string name = reader.ReadString(160); PlayerKey key = PlayerKey.Read(reader); reader.EnsureComplete(); return (name, key); }

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
            writer.WriteByte((byte)state.Phase); SiteTile.WriteOptional(writer, state.Site);
            if (state.Players.Count > 64) throw new ProtocolException("Too many players.");
            writer.WriteByte((byte)state.Players.Count);
            foreach (PlayerView player in state.Players)
            {
                writer.WriteGuid(player.Id); writer.WriteString(player.Name, 160); writer.WriteBool(player.Ready);
                player.Key.Write(writer); SiteTile.WriteOptional(writer, player.Flag); writer.WriteByte((byte)player.ModStatus);
                writer.WriteByte((byte)player.WorldStatus);
            }
            return writer.ToArray();
        }
        public static LobbyView ReadState(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            Guid id = reader.ReadGuid(); string name = reader.ReadString(160); Guid host = reader.ReadGuid();
            ulong revision = reader.ReadUInt64(); byte capacity = reader.ReadByte();
            SetupPhase phase = SetupPayloads.ReadPhase(reader); SiteTile? site = SiteTile.ReadOptional(reader);
            byte count = reader.ReadByte();
            if (count > 64 || count > capacity) throw new ProtocolException("Invalid player count.");
            var players = new List<PlayerView>();
            for (int i = 0; i < count; i++)
            {
                Guid playerId = reader.ReadGuid(); string playerName = reader.ReadString(160); bool ready = reader.ReadBool();
                PlayerKey key = PlayerKey.Read(reader); SiteTile? flag = SiteTile.ReadOptional(reader);
                var modStatus = (ModStatus)reader.ReadByte();
                if (!Enum.IsDefined(typeof(ModStatus), modStatus)) throw new ProtocolException("Invalid mod status.");
                var worldStatus = (WorldStatus)reader.ReadByte();
                if (!Enum.IsDefined(typeof(WorldStatus), worldStatus)) throw new ProtocolException("Invalid world status.");
                players.Add(new PlayerView(playerId, playerName, ready, key, flag, modStatus, worldStatus));
            }
            reader.EnsureComplete(); return new LobbyView(id, name, host, revision, capacity, players, phase, site);
        }
        public static byte[] List(IReadOnlyList<LobbySummary> lobbies)
        {
            if (lobbies.Count > 128) throw new ProtocolException("Too many lobbies.");
            var writer = new PayloadWriter(); writer.WriteByte((byte)lobbies.Count);
            foreach (LobbySummary lobby in lobbies)
            {
                writer.WriteGuid(lobby.Id); writer.WriteString(lobby.Name, 160); writer.WriteString(lobby.HostName, 160);
                writer.WriteByte(lobby.PlayerCount); writer.WriteByte(lobby.Capacity); writer.WriteByte((byte)lobby.Phase);
                writer.WriteBool(lobby.HostMods != null); lobby.HostMods?.Write(writer);
            }
            return writer.ToArray();
        }
        public static IReadOnlyList<LobbySummary> ReadList(byte[] bytes)
        {
            var reader = new PayloadReader(bytes); byte count = reader.ReadByte();
            if (count > 128) throw new ProtocolException("Too many lobbies.");
            var lobbies = new List<LobbySummary>();
            for (int i = 0; i < count; i++)
            {
                Guid id = reader.ReadGuid(); string name = reader.ReadString(160), host = reader.ReadString(160);
                byte players = reader.ReadByte(), capacity = reader.ReadByte(); SetupPhase phase = SetupPayloads.ReadPhase(reader);
                ModManifestSummary? mods = reader.ReadBool() ? ModManifestSummary.Read(reader) : null;
                lobbies.Add(new LobbySummary(id, name, host, players, capacity, phase, mods));
            }
            reader.EnsureComplete(); return lobbies;
        }
    }
}
