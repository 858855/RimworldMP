using System;
using System.Collections.Generic;
using System.Linq;

namespace Multiplayer.Foundation.Protocol
{
    public sealed class PlayerView
    {
        public PlayerView(Guid id, string name, bool ready, PlayerKey key, SiteTile? flag = null,
            ModStatus modStatus = ModStatus.Unchecked, WorldStatus worldStatus = WorldStatus.None, byte? factionSlot = null,
            string? storyteller = null)
        {
            Id = id; Name = name; Ready = ready; Key = key; Flag = flag; ModStatus = modStatus; WorldStatus = worldStatus;
            FactionSlot = factionSlot; Storyteller = storyteller;
        }
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
        /// <summary>Factions mode (ADR 0037): this player's faction, bound to the player key; null in Coop.</summary>
        public byte? FactionSlot { get; }
        /// <summary>Factions mode (ADR 0037): defName of the storyteller chosen for the own faction, or null.</summary>
        public string? Storyteller { get; }
    }

    public sealed class LobbyView
    {
        public LobbyView(Guid id, string name, Guid hostId, ulong revision, byte capacity, IReadOnlyList<PlayerView> players,
            SetupPhase phase = SetupPhase.Lobby, SiteTile? site = null, LobbyMode? mode = null, FactionOptions? factionOptions = null)
        {
            Id = id; Name = name; HostId = hostId; Revision = revision; Capacity = capacity; Players = players; Phase = phase; Site = site;
            Mode = mode ?? LobbyMode.CoopHost; FactionOptions = factionOptions;
        }
        public Guid Id { get; }
        public string Name { get; }
        public Guid HostId { get; }
        public ulong Revision { get; }
        public byte Capacity { get; }
        public IReadOnlyList<PlayerView> Players { get; }
        public SetupPhase Phase { get; }
        /// <summary>Starting site chosen by the host; set once SiteSelection ends.</summary>
        public SiteTile? Site { get; }
        /// <summary>Fixed when the lobby was created (ADR 0031).</summary>
        public LobbyMode Mode { get; }
        /// <summary>Factions mode (ADR 0037): shared difficulty and selectable storytellers, once the host sent them.</summary>
        public FactionOptions? FactionOptions { get; }
    }

    public sealed class LobbySummary
    {
        public LobbySummary(Guid id, string name, string hostName, byte playerCount, byte capacity, SetupPhase phase = SetupPhase.Lobby,
            ModManifestSummary? hostMods = null, LobbyMode? mode = null, bool passwordRequired = false)
        {
            Id = id; Name = name; HostName = hostName; PlayerCount = playerCount; Capacity = capacity; Phase = phase; HostMods = hostMods;
            Mode = mode ?? LobbyMode.CoopHost; PasswordRequired = passwordRequired;
        }
        /// <summary>Joining needs the lobby password (ADR 0035); the password itself is never listed.</summary>
        public bool PasswordRequired { get; }
        /// <summary>Game mode, so players see Coop or Factions before joining.</summary>
        public LobbyMode Mode { get; }
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

        /// <param name="proof">Optional key proof for IP connections (ADR 0035); appended so older Hellos stay valid.</param>
        public static byte[] Hello(string playerName, PlayerKey key, HelloProof? proof = null)
        {
            var writer = new PayloadWriter(); writer.WriteString(playerName, 160); key.Write(writer);
            proof?.Write(writer);
            return writer.ToArray();
        }
        public static (string Name, PlayerKey Key, HelloProof? Proof) ReadHello(byte[] bytes)
        {
            var reader = new PayloadReader(bytes); string name = reader.ReadString(160); PlayerKey key = PlayerKey.Read(reader);
            HelloProof? proof = reader.Remaining > 0 ? HelloProof.Read(reader) : null;
            reader.EnsureComplete(); return (name, key, proof);
        }

        public const int MaximumPasswordChars = 64;
        /// <summary>JoinLobby: the lobby ID, then the password if the lobby has one (ADR 0035).</summary>
        public static byte[] Join(Guid lobbyId, string? password = null)
        {
            var writer = new PayloadWriter(); writer.WriteGuid(lobbyId);
            if (password != null) { ValidatePassword(password); writer.WriteString(password); }
            return writer.ToArray();
        }
        public static (Guid LobbyId, string? Password) ReadJoin(byte[] bytes)
        {
            var reader = new PayloadReader(bytes); Guid id = reader.ReadGuid();
            string? password = reader.Remaining > 0 ? reader.ReadString() : null;
            reader.EnsureComplete();
            if (password != null) ValidatePassword(password);
            return (id, password);
        }
        private static void ValidatePassword(string password)
        {
            if (password.Length == 0 || password.Length > MaximumPasswordChars || password.Any(char.IsControl))
                throw new ProtocolException("Password must have 1-64 printable characters.");
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
        /// <param name="mode">Chosen once here and never changed (ADR 0031); default Coop with host decisions.</param>
        /// <param name="password">Optional; joiners must then send it (ADR 0035).</param>
        public static byte[] Create(string name, byte capacity, LobbyMode? mode = null, string? password = null)
        {
            var writer = new PayloadWriter(); writer.WriteString(name, 160); writer.WriteByte(capacity);
            (mode ?? LobbyMode.CoopHost).Write(writer);
            writer.WriteBool(password != null);
            if (password != null) { ValidatePassword(password); writer.WriteString(password); }
            return writer.ToArray();
        }
        public static (string Name, byte Capacity, LobbyMode Mode, string? Password) ReadCreate(byte[] bytes)
        {
            var reader = new PayloadReader(bytes); string name = reader.ReadString(160); byte capacity = reader.ReadByte();
            LobbyMode mode = LobbyMode.Read(reader);
            string? password = reader.ReadBool() ? reader.ReadString() : null;
            reader.EnsureComplete();
            if (password != null) ValidatePassword(password);
            return (name, capacity, mode, password);
        }
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
            state.Mode.Write(writer);
            // ADR 0037: Factions only, appended so a Coop state stays as before.
            if (state.Mode.Mode == GameMode.Factions)
            {
                writer.WriteBool(state.FactionOptions != null); state.FactionOptions?.Write(writer);
                foreach (PlayerView player in state.Players)
                {
                    if (player.FactionSlot >= 64) throw new ProtocolException("Invalid faction slot.");
                    writer.WriteBool(player.FactionSlot.HasValue);
                    if (player.FactionSlot.HasValue) writer.WriteByte(player.FactionSlot.Value);
                    writer.WriteBool(player.Storyteller != null);
                    if (player.Storyteller != null) DefNames.Write(writer, player.Storyteller);
                }
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
            LobbyMode mode = LobbyMode.Read(reader);
            FactionOptions? options = null;
            if (mode.Mode == GameMode.Factions && reader.Remaining > 0)
            {
                options = reader.ReadBool() ? FactionOptions.Read(reader) : null;
                for (int i = 0; i < players.Count; i++)
                {
                    byte? slot = reader.ReadBool() ? reader.ReadByte() : (byte?)null;
                    if (slot >= 64) throw new ProtocolException("Invalid faction slot.");
                    string? storyteller = reader.ReadBool() ? DefNames.Read(reader) : null;
                    PlayerView p = players[i];
                    players[i] = new PlayerView(p.Id, p.Name, p.Ready, p.Key, p.Flag, p.ModStatus, p.WorldStatus, slot, storyteller);
                }
                byte[] slots = players.Where(p => p.FactionSlot.HasValue).Select(p => p.FactionSlot!.Value).ToArray();
                if (slots.Distinct().Count() != slots.Length) throw new ProtocolException("Faction slots must be distinct.");
            }
            reader.EnsureComplete(); return new LobbyView(id, name, host, revision, capacity, players, phase, site, mode, options);
        }
        public static byte[] List(IReadOnlyList<LobbySummary> lobbies)
        {
            if (lobbies.Count > 128) throw new ProtocolException("Too many lobbies.");
            var writer = new PayloadWriter(); writer.WriteByte((byte)lobbies.Count);
            foreach (LobbySummary lobby in lobbies)
            {
                writer.WriteGuid(lobby.Id); writer.WriteString(lobby.Name, 160); writer.WriteString(lobby.HostName, 160);
                writer.WriteByte(lobby.PlayerCount); writer.WriteByte(lobby.Capacity); writer.WriteByte((byte)lobby.Phase);
                writer.WriteBool(lobby.HostMods != null); lobby.HostMods?.Write(writer); lobby.Mode.Write(writer);
                writer.WriteBool(lobby.PasswordRequired);
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
                LobbyMode mode = LobbyMode.Read(reader); bool passwordRequired = reader.ReadBool();
                lobbies.Add(new LobbySummary(id, name, host, players, capacity, phase, mods, mode, passwordRequired));
            }
            reader.EnsureComplete(); return lobbies;
        }
    }
}
