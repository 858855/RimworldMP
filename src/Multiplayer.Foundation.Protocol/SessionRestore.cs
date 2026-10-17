using System.Collections.Generic;
using System.Linq;

namespace Multiplayer.Foundation.Protocol
{
    /// <summary>
    /// SAVE-002 (ADR 0027), code 105: the host's game names the players of the Coop save it loaded. The lobby then admits
    /// only those player keys before the start and goes straight into the game transfer. A Factions save restores its
    /// slot table with <see cref="FactionRestore"/> (101) instead.
    /// </summary>
    public sealed class SessionRestore
    {
        public const int MaximumPlayers = 64;

        public SessionRestore(IReadOnlyList<PlayerKey> players)
        {
            if (players == null || players.Count == 0 || players.Count > MaximumPlayers) throw new ProtocolException("Restore 1-64 players.");
            if (players.Any(player => !player.IsValid)) throw new ProtocolException("Invalid player key.");
            if (players.Distinct().Count() != players.Count) throw new ProtocolException("Player keys must be distinct.");
            Players = players.ToArray();
        }

        /// <summary>Every player of the saved game, host included; ownership stays inside the save.</summary>
        public IReadOnlyList<PlayerKey> Players { get; }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter(); writer.WriteByte((byte)Players.Count);
            foreach (PlayerKey player in Players) player.Write(writer);
            return writer.ToArray();
        }

        public static SessionRestore FromPayload(byte[] bytes)
        {
            var reader = new PayloadReader(bytes); byte count = reader.ReadByte();
            if (count == 0 || count > MaximumPlayers) throw new ProtocolException("Restore 1-64 players.");
            var players = new PlayerKey[count];
            for (int i = 0; i < count; i++) players[i] = PlayerKey.Read(reader);
            reader.EnsureComplete();
            return new SessionRestore(players);
        }
    }
}
