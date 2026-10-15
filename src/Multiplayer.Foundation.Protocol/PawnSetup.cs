using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Multiplayer.Foundation.Protocol
{
    // SETUP-004 (plan §6.3, ADR 0015): the host's game owns the starting pawns. Guests see cards and send requests;
    // the host carries them out, so every random roll happens on the host.
    public enum PawnRequestKind : byte { Claim = 1, Release = 2, Reroll = 3, Swap = 4, Rename = 5 }

    public sealed class PawnRequest
    {
        public const int MaximumNameBytes = 64;

        public PawnRequest(PawnRequestKind kind, int pawnId, int otherPawnId = 0, string text = "", PlayerKey? requester = null)
        {
            if (!Enum.IsDefined(typeof(PawnRequestKind), kind)) throw new ProtocolException("Unknown pawn request.");
            Kind = kind; PawnId = pawnId; OtherPawnId = otherPawnId; Text = text ?? ""; Requester = requester;
        }
        public PawnRequestKind Kind { get; }
        public int PawnId { get; }
        /// <summary>Swap: the left-behind pawn to bring in.</summary>
        public int OtherPawnId { get; }
        /// <summary>Rename: the new nickname.</summary>
        public string Text { get; }
        /// <summary>Set by the server from the sender's session; a client's own value is ignored.</summary>
        public PlayerKey? Requester { get; }
        public PawnRequest WithRequester(PlayerKey key) => new PawnRequest(Kind, PawnId, OtherPawnId, Text, key);

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter();
            writer.WriteByte((byte)Kind); writer.WriteUInt32((uint)PawnId); writer.WriteUInt32((uint)OtherPawnId);
            writer.WriteString(Text, MaximumNameBytes);
            writer.WriteBool(Requester.HasValue); Requester?.Write(writer);
            return writer.ToArray();
        }
        public static PawnRequest Read(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            var kind = (PawnRequestKind)reader.ReadByte(); int pawn = (int)reader.ReadUInt32(), other = (int)reader.ReadUInt32();
            string text = reader.ReadString(MaximumNameBytes);
            PlayerKey? requester = reader.ReadBool() ? PlayerKey.Read(reader) : (PlayerKey?)null;
            reader.EnsureComplete();
            return new PawnRequest(kind, pawn, other, text, requester);
        }
    }

    /// <summary>What a player sees of one starting pawn: display text only.</summary>
    public sealed class PawnCard
    {
        public const int MaximumEntries = 24;

        public PawnCard(int pawnId, bool selected, PlayerKey? owner, string name, string shortName, string summary,
            string childhood, string adulthood, IReadOnlyList<string> traits, IReadOnlyList<(string Skill, byte Level, byte Passion)> skills,
            IReadOnlyList<string> incapable, IReadOnlyList<string> health)
        {
            PawnId = pawnId; Selected = selected; Owner = owner; Name = name ?? ""; ShortName = shortName ?? ""; Summary = summary ?? "";
            Childhood = childhood ?? ""; Adulthood = adulthood ?? "";
            Traits = Limit(traits); Skills = Limit(skills); Incapable = Limit(incapable); Health = Limit(health);
        }
        public int PawnId { get; }
        /// <summary>In the starting group (true) or left behind (false).</summary>
        public bool Selected { get; }
        public PlayerKey? Owner { get; }
        public string Name { get; }
        public string ShortName { get; }
        /// <summary>Age, gender, xenotype in one line.</summary>
        public string Summary { get; }
        public string Childhood { get; }
        public string Adulthood { get; }
        public IReadOnlyList<string> Traits { get; }
        /// <summary>Passion: 0 none, 1 minor, 2 major; Level 255 = incapable of this skill.</summary>
        public IReadOnlyList<(string Skill, byte Level, byte Passion)> Skills { get; }
        public IReadOnlyList<string> Incapable { get; }
        public IReadOnlyList<string> Health { get; }

        private static IReadOnlyList<T> Limit<T>(IReadOnlyList<T>? items)
        {
            if (items == null) return Array.Empty<T>();
            if (items.Count > MaximumEntries) throw new ProtocolException("Too many pawn card entries.");
            return items;
        }

        internal void Write(PayloadWriter writer)
        {
            writer.WriteUInt32((uint)PawnId); writer.WriteBool(Selected);
            writer.WriteBool(Owner.HasValue); Owner?.Write(writer);
            foreach (string text in new[] { Name, ShortName, Summary, Childhood, Adulthood }) writer.WriteString(text, 160);
            WriteList(writer, Traits);
            writer.WriteByte((byte)Skills.Count);
            foreach (var (skill, level, passion) in Skills) { writer.WriteString(skill, 64); writer.WriteByte(level); writer.WriteByte(passion); }
            WriteList(writer, Incapable); WriteList(writer, Health);
        }
        internal static PawnCard Read(PayloadReader reader)
        {
            int id = (int)reader.ReadUInt32(); bool selected = reader.ReadBool();
            PlayerKey? owner = reader.ReadBool() ? PlayerKey.Read(reader) : (PlayerKey?)null;
            string name = reader.ReadString(160), shortName = reader.ReadString(160), summary = reader.ReadString(160),
                childhood = reader.ReadString(160), adulthood = reader.ReadString(160);
            var traits = ReadList(reader);
            byte skillCount = reader.ReadByte();
            if (skillCount > MaximumEntries) throw new ProtocolException("Too many pawn card entries.");
            var skills = new List<(string, byte, byte)>(skillCount);
            for (int i = 0; i < skillCount; i++) skills.Add((reader.ReadString(64), reader.ReadByte(), reader.ReadByte()));
            var incapable = ReadList(reader); var health = ReadList(reader);
            return new PawnCard(id, selected, owner, name, shortName, summary, childhood, adulthood, traits, skills, incapable, health);
        }
        private static void WriteList(PayloadWriter writer, IReadOnlyList<string> items)
        { writer.WriteByte((byte)items.Count); foreach (string item in items) writer.WriteString(item, 128); }
        private static List<string> ReadList(PayloadReader reader)
        {
            byte count = reader.ReadByte();
            if (count > MaximumEntries) throw new ProtocolException("Too many pawn card entries.");
            var items = new List<string>(count);
            for (int i = 0; i < count; i++) items.Add(reader.ReadString(128));
            return items;
        }
    }

    /// <summary>All starting pawns as the host's game holds them, in order (starting group first).</summary>
    public sealed class PawnRoster
    {
        public const int MaximumCards = 24;

        public PawnRoster(IReadOnlyList<PawnCard> cards)
        {
            if (cards == null || cards.Count > MaximumCards) throw new ProtocolException("Too many pawns.");
            Cards = cards;
        }
        public IReadOnlyList<PawnCard> Cards { get; }

        public bool OwnsSelectedPawn(PlayerKey key) => Cards.Any(c => c.Selected && c.Owner == key);

        /// <summary>
        /// SHA-256 over this player's pawns. Any change (re-roll, swap, rename, assignment) changes it, which resets
        /// the player's "done" (ADR 0015).
        /// </summary>
        public byte[] OwnerHash(PlayerKey key)
        {
            var writer = new PayloadWriter();
            foreach (PawnCard card in Cards.Where(c => c.Owner == key)) card.Write(writer);
            using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(writer.ToArray());
        }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter(); writer.WriteByte((byte)Cards.Count);
            foreach (PawnCard card in Cards) card.Write(writer);
            byte[] payload = writer.ToArray();
            if (payload.Length > FrameCodec.MaximumBodyLength - 16) throw new ProtocolException("Pawn roster too large for one frame.");
            return payload;
        }
        public static PawnRoster Read(byte[] bytes)
        {
            var reader = new PayloadReader(bytes); byte count = reader.ReadByte();
            if (count > MaximumCards) throw new ProtocolException("Too many pawns.");
            var cards = new List<PawnCard>(count);
            for (int i = 0; i < count; i++) cards.Add(PawnCard.Read(reader));
            reader.EnsureComplete();
            return new PawnRoster(cards);
        }
    }
}
