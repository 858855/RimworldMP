using System;
using System.Globalization;

namespace Multiplayer.Foundation.Protocol
{
    public enum PlayerKeyKind : byte
    {
        Steam = 1,
        Local = 2
    }

    // Persistent player identity (plan §4, ADR 0016). Saved data such as pawn ownership uses this,
    // never the per-connection session ID. On Steam it is the SteamID verified by Steam itself.
    public readonly struct PlayerKey : IEquatable<PlayerKey>
    {
        private PlayerKey(PlayerKeyKind kind, ulong steamId, Guid localId)
        { Kind = kind; SteamId = steamId; LocalId = localId; }

        public PlayerKeyKind Kind { get; }
        public ulong SteamId { get; }
        public Guid LocalId { get; }
        public bool IsValid => Kind == PlayerKeyKind.Steam ? SteamId != 0 : Kind == PlayerKeyKind.Local && LocalId != Guid.Empty;

        public static PlayerKey Steam(ulong steamId)
        {
            if (steamId == 0) throw new ArgumentOutOfRangeException(nameof(steamId));
            return new PlayerKey(PlayerKeyKind.Steam, steamId, Guid.Empty);
        }

        public static PlayerKey Local(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(id));
            return new PlayerKey(PlayerKeyKind.Local, 0, id);
        }

        public bool Equals(PlayerKey other) => Kind == other.Kind && SteamId == other.SteamId && LocalId == other.LocalId;
        public override bool Equals(object? obj) => obj is PlayerKey other && Equals(other);
        public override int GetHashCode() => Kind == PlayerKeyKind.Steam ? SteamId.GetHashCode() : LocalId.GetHashCode();
        public static bool operator ==(PlayerKey left, PlayerKey right) => left.Equals(right);
        public static bool operator !=(PlayerKey left, PlayerKey right) => !left.Equals(right);

        /// <summary>Stable text form for saves and logs: "steam:76561198000000000" or "local:&lt;32 hex&gt;".</summary>
        public override string ToString() => Kind == PlayerKeyKind.Steam
            ? "steam:" + SteamId.ToString(CultureInfo.InvariantCulture)
            : Kind == PlayerKeyKind.Local ? "local:" + LocalId.ToString("N") : "invalid";

        public static bool TryParse(string? text, out PlayerKey key)
        {
            key = default;
            if (text == null) return false;
            if (text.StartsWith("steam:", StringComparison.Ordinal) &&
                ulong.TryParse(text.Substring(6), NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId) && steamId != 0)
            { key = Steam(steamId); return true; }
            if (text.StartsWith("local:", StringComparison.Ordinal) &&
                Guid.TryParseExact(text.Substring(6), "N", out Guid localId) && localId != Guid.Empty)
            { key = Local(localId); return true; }
            return false;
        }

        public void Write(PayloadWriter writer)
        {
            if (!IsValid) throw new ProtocolException("Invalid player key.");
            writer.WriteByte((byte)Kind);
            if (Kind == PlayerKeyKind.Steam) writer.WriteUInt64(SteamId);
            else writer.WriteGuid(LocalId);
        }

        public static PlayerKey Read(PayloadReader reader)
        {
            byte kind = reader.ReadByte();
            if (kind == (byte)PlayerKeyKind.Steam)
            {
                ulong steamId = reader.ReadUInt64();
                if (steamId == 0) throw new ProtocolException("Invalid player key.");
                return Steam(steamId);
            }
            if (kind == (byte)PlayerKeyKind.Local)
            {
                Guid localId = reader.ReadGuid();
                if (localId == Guid.Empty) throw new ProtocolException("Invalid player key.");
                return Local(localId);
            }
            throw new ProtocolException("Unknown player key kind.");
        }
    }
}
