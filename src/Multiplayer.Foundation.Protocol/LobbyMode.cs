using System;

namespace Multiplayer.Foundation.Protocol
{
    public enum GameMode : byte
    {
        /// <summary>One shared colony (plan §2–§7).</summary>
        Coop = 1,
        /// <summary>Every player has an own faction and colony.</summary>
        Factions = 2
    }

    public enum CoopDecisions : byte
    {
        None = 0,
        /// <summary>The host decides in-game events.</summary>
        Host = 1,
        /// <summary>Players vote on in-game events; the host breaks ties. Setup stays with the host.</summary>
        Democracy = 2
    }

    public enum FactionRelation : byte
    {
        None = 0,
        Allied = 1,
        Neutral = 2,
        Hostile = 3
    }

    /// <summary>
    /// Game mode chosen once when the lobby is created (ADR 0031). It never changes afterwards; a loaded game
    /// brings its own. Coop carries who decides in-game events, Factions the relation between the player factions.
    /// </summary>
    public readonly struct LobbyMode : IEquatable<LobbyMode>
    {
        private LobbyMode(GameMode mode, CoopDecisions decisions, FactionRelation relation)
        { Mode = mode; Decisions = decisions; Relation = relation; }

        public GameMode Mode { get; }
        public CoopDecisions Decisions { get; }
        public FactionRelation Relation { get; }

        public static LobbyMode CoopHost => new LobbyMode(GameMode.Coop, CoopDecisions.Host, FactionRelation.None);
        public static LobbyMode CoopDemocracy => new LobbyMode(GameMode.Coop, CoopDecisions.Democracy, FactionRelation.None);
        public static LobbyMode Factions(FactionRelation relation)
        {
            if (relation == FactionRelation.None || !Enum.IsDefined(typeof(FactionRelation), relation))
                throw new ArgumentOutOfRangeException(nameof(relation));
            return new LobbyMode(GameMode.Factions, CoopDecisions.None, relation);
        }

        public bool Equals(LobbyMode other) => Mode == other.Mode && Decisions == other.Decisions && Relation == other.Relation;
        public override bool Equals(object? obj) => obj is LobbyMode other && Equals(other);
        public override int GetHashCode() => ((int)Mode << 16) | ((int)Decisions << 8) | (int)Relation;
        public static bool operator ==(LobbyMode left, LobbyMode right) => left.Equals(right);
        public static bool operator !=(LobbyMode left, LobbyMode right) => !left.Equals(right);
        public override string ToString() => Mode == GameMode.Coop ? "Coop/" + Decisions : "Factions/" + Relation;

        public void Write(PayloadWriter writer)
        {
            Validate(Mode, Decisions, Relation);
            writer.WriteByte((byte)Mode); writer.WriteByte((byte)Decisions); writer.WriteByte((byte)Relation);
        }

        public static LobbyMode Read(PayloadReader reader)
        {
            var mode = (GameMode)reader.ReadByte(); var decisions = (CoopDecisions)reader.ReadByte(); var relation = (FactionRelation)reader.ReadByte();
            Validate(mode, decisions, relation);
            return new LobbyMode(mode, decisions, relation);
        }

        // Coop needs a decision rule and no relation; Factions needs a relation and no decision rule.
        private static void Validate(GameMode mode, CoopDecisions decisions, FactionRelation relation)
        {
            if (mode == GameMode.Coop && (decisions == CoopDecisions.Host || decisions == CoopDecisions.Democracy) && relation == FactionRelation.None) return;
            if (mode == GameMode.Factions && decisions == CoopDecisions.None &&
                (relation == FactionRelation.Allied || relation == FactionRelation.Neutral || relation == FactionRelation.Hostile)) return;
            throw new ProtocolException("Invalid game mode.");
        }
    }
}
