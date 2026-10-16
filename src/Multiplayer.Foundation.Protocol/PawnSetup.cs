using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Multiplayer.Foundation.Protocol
{
    // SETUP-004 (plan §6.3, ADR 0015): the host's game owns the starting pawns. Guests see cards and send requests;
    // the host carries them out, so every random roll happens on the host.
    // 6–8 give guests what the vanilla page gives the host: all three names, xenotype (also custom) and development stage.
    public enum PawnRequestKind : byte { Claim = 1, Release = 2, Reroll = 3, Swap = 4, Rename = 5, SetName = 6, SetXenotype = 7, SetDevelopmentStage = 8 }

    public sealed class PawnRequest
    {
        public const int MaximumNameBytes = 64;
        /// <summary>Request text: a name, three names (<see cref="Names"/>) or a xenotype choice (<see cref="PawnXenotypeChoice"/>).</summary>
        public const int MaximumTextBytes = 4096;
        private const char Separator = '\u001f';

        /// <summary>SetName text: first, nick and last name.</summary>
        public static string Names(string first, string nick, string last) => first + Separator + nick + Separator + last;

        /// <summary>Splits a SetName text; false if it is not three parts.</summary>
        public static bool TryReadNames(string text, out string first, out string nick, out string last)
        {
            string[] parts = (text ?? "").Split(Separator);
            first = parts.Length == 3 ? parts[0] : ""; nick = parts.Length == 3 ? parts[1] : ""; last = parts.Length == 3 ? parts[2] : "";
            return parts.Length == 3;
        }

        public PawnRequest(PawnRequestKind kind, int pawnId, int otherPawnId = 0, string text = "", PlayerKey? requester = null)
        {
            if (!Enum.IsDefined(typeof(PawnRequestKind), kind)) throw new ProtocolException("Unknown pawn request.");
            Kind = kind; PawnId = pawnId; OtherPawnId = otherPawnId; Text = text ?? ""; Requester = requester;
        }
        public PawnRequestKind Kind { get; }
        public int PawnId { get; }
        /// <summary>Swap: the left-behind pawn to bring in. SetDevelopmentStage: the stage flags (RimWorld's DevelopmentalStage).</summary>
        public int OtherPawnId { get; }
        /// <summary>Rename: the new nickname. SetName: <see cref="Names"/>. SetXenotype: <see cref="PawnXenotypeChoice.ToText"/>.</summary>
        public string Text { get; }
        /// <summary>Set by the server from the sender's session; a client's own value is ignored.</summary>
        public PlayerKey? Requester { get; }
        public PawnRequest WithRequester(PlayerKey key) => new PawnRequest(Kind, PawnId, OtherPawnId, Text, key);

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter();
            writer.WriteByte((byte)Kind); writer.WriteUInt32((uint)PawnId); writer.WriteUInt32((uint)OtherPawnId);
            writer.WriteString(Text, MaximumTextBytes);
            writer.WriteBool(Requester.HasValue); Requester?.Write(writer);
            return writer.ToArray();
        }
        public static PawnRequest Read(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            var kind = (PawnRequestKind)reader.ReadByte(); int pawn = (int)reader.ReadUInt32(), other = (int)reader.ReadUInt32();
            string text = reader.ReadString(MaximumTextBytes);
            PlayerKey? requester = reader.ReadBool() ? PlayerKey.Read(reader) : (PlayerKey?)null;
            reader.EnsureComplete();
            return new PawnRequest(kind, pawn, other, text, requester);
        }
    }

    /// <summary>
    /// A xenotype as a guest picks it on its page: a XenotypeDef by name, or a custom xenotype made in the vanilla editor
    /// (name, inheritable, icon, genes by def name). The host resolves the names against its own defs.
    /// </summary>
    public sealed class PawnXenotypeChoice
    {
        public const int MaximumGenes = 256;
        private const char Separator = '\u001f';

        private PawnXenotypeChoice(string defName, string name, bool inheritable, string iconDef, IReadOnlyList<string> genes, bool custom)
        { DefName = defName; Name = name; Inheritable = inheritable; IconDef = iconDef; Genes = genes; Custom = custom; }

        public static PawnXenotypeChoice Standard(string defName) =>
            new PawnXenotypeChoice(defName ?? "", "", false, "", Array.Empty<string>(), false);

        public static PawnXenotypeChoice CustomXenotype(string name, bool inheritable, string iconDef, IReadOnlyList<string> genes)
        {
            if (genes == null || genes.Count > MaximumGenes) throw new ProtocolException("Too many genes.");
            return new PawnXenotypeChoice("", name ?? "", inheritable, iconDef ?? "", genes, true);
        }

        public bool Custom { get; }
        public string DefName { get; }
        public string Name { get; }
        public bool Inheritable { get; }
        public string IconDef { get; }
        public IReadOnlyList<string> Genes { get; }

        public string ToText() => Custom
            ? "c:" + Name + Separator + (Inheritable ? "1" : "0") + Separator + IconDef + Separator + string.Join(",", Genes)
            : "x:" + DefName;

        public static PawnXenotypeChoice Parse(string text)
        {
            text ??= "";
            if (text.StartsWith("x:") && text.Length > 2) return Standard(text.Substring(2));
            if (!text.StartsWith("c:")) throw new ProtocolException("Unknown xenotype choice.");
            string[] parts = text.Substring(2).Split(Separator);
            if (parts.Length != 4) throw new ProtocolException("Malformed custom xenotype.");
            string[] genes = parts[3].Length == 0 ? Array.Empty<string>() : parts[3].Split(',');
            return CustomXenotype(parts[0], parts[1] == "1", parts[2], genes);
        }
    }

    /// <summary>What a player sees of one starting pawn: display text only.</summary>
    public sealed class PawnCard
    {
        public const int MaximumEntries = 24;

        public PawnCard(int pawnId, bool selected, PlayerKey? owner, string name, string shortName, string summary,
            string childhood, string adulthood, IReadOnlyList<string> traits, IReadOnlyList<(string Skill, byte Level, byte Passion)> skills,
            IReadOnlyList<string> incapable, IReadOnlyList<string> health, PawnCardDetails? details = null)
        {
            PawnId = pawnId; Selected = selected; Owner = owner; Name = name ?? ""; ShortName = shortName ?? ""; Summary = summary ?? "";
            Childhood = childhood ?? ""; Adulthood = adulthood ?? "";
            Traits = Limit(traits); Skills = Limit(skills); Incapable = Limit(incapable); Health = Limit(health);
            Details = details;
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
        /// <summary>What the vanilla character page shows beyond the summary (names, ages, relations, …); null if not sent.</summary>
        public PawnCardDetails? Details { get; }

        internal static IReadOnlyList<T> Limit<T>(IReadOnlyList<T>? items)
        {
            if (items == null) return Array.Empty<T>();
            if (items.Count > MaximumEntries) throw new ProtocolException("Too many pawn card entries.");
            return items;
        }

        internal static byte ReadCount(PayloadReader reader)
        {
            byte count = reader.ReadByte();
            if (count > MaximumEntries) throw new ProtocolException("Too many pawn card entries.");
            return count;
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
            writer.WriteBool(Details != null); Details?.Write(writer);
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
            PawnCardDetails? details = reader.ReadBool() ? PawnCardDetails.Read(reader) : null;
            return new PawnCard(id, selected, owner, name, shortName, summary, childhood, adulthood, traits, skills, incapable, health, details);
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

    /// <summary>
    /// The rest of a vanilla character card for the guests' page (SETUP-004): name parts, gender and ages, xenotype,
    /// favourite colour, health by body part, relations with both opinions, starting possessions. Display only.
    /// </summary>
    public sealed class PawnCardDetails
    {
        public const int MaximumTipBytes = 1024;

        public PawnCardDetails(string first, string nick, string last, byte gender, ushort ageBiological, ushort ageChronological,
            string xenotype, string xenotypeIcon, uint favoriteColor, IReadOnlyList<(string Part, string Label)> health,
            IReadOnlyList<(string Relation, string Name, short Opinion, short TheirOpinion)> relations,
            IReadOnlyList<(string Label, int Count)> possessions, byte developmentStage = 0, string childhoodTip = "", string adulthoodTip = "",
            IReadOnlyList<string>? traitTips = null, IReadOnlyList<string>? abilities = null, IReadOnlyList<string>? possessionDefs = null)
        {
            First = first ?? ""; Nick = nick ?? ""; Last = last ?? ""; Gender = gender;
            AgeBiological = ageBiological; AgeChronological = ageChronological;
            Xenotype = xenotype ?? ""; XenotypeIcon = xenotypeIcon ?? ""; FavoriteColor = favoriteColor;
            Health = PawnCard.Limit(health); Relations = PawnCard.Limit(relations); Possessions = PawnCard.Limit(possessions);
            DevelopmentStage = developmentStage; ChildhoodTip = childhoodTip ?? ""; AdulthoodTip = adulthoodTip ?? "";
            TraitTips = PawnCard.Limit(traitTips); Abilities = PawnCard.Limit(abilities); PossessionDefs = PawnCard.Limit(possessionDefs);
        }
        public string First { get; }
        public string Nick { get; }
        public string Last { get; }
        /// <summary>RimWorld's Gender: 0 none, 1 male, 2 female.</summary>
        public byte Gender { get; }
        public ushort AgeBiological { get; }
        public ushort AgeChronological { get; }
        public string Xenotype { get; }
        /// <summary>"x:" + XenotypeDef or "c:" + XenotypeIconDef name, so the guest draws the same icon; empty if none.</summary>
        public string XenotypeIcon { get; }
        /// <summary>RGBA, 0 = none.</summary>
        public uint FavoriteColor { get; }
        public IReadOnlyList<(string Part, string Label)> Health { get; }
        /// <summary>Opinion: this pawn of the other; TheirOpinion: the other of this pawn.</summary>
        public IReadOnlyList<(string Relation, string Name, short Opinion, short TheirOpinion)> Relations { get; }
        public IReadOnlyList<(string Label, int Count)> Possessions { get; }
        /// <summary>The development stage the slot generates (RimWorld's DevelopmentalStage flags), 0 if unknown.</summary>
        public byte DevelopmentStage { get; }
        /// <summary>Vanilla's tooltip texts, as the host's game writes them for this pawn.</summary>
        public string ChildhoodTip { get; }
        public string AdulthoodTip { get; }
        /// <summary>One per entry of <see cref="PawnCard.Traits"/>, same order.</summary>
        public IReadOnlyList<string> TraitTips { get; }
        /// <summary>AbilityDef names, so the guest draws the icons from its own defs.</summary>
        public IReadOnlyList<string> Abilities { get; }
        /// <summary>ThingDef names, one per entry of <see cref="Possessions"/>, for vanilla's info button.</summary>
        public IReadOnlyList<string> PossessionDefs { get; }

        internal void Write(PayloadWriter writer)
        {
            writer.WriteString(First, 64); writer.WriteString(Nick, 64); writer.WriteString(Last, 64);
            writer.WriteByte(Gender); writer.WriteUInt16(AgeBiological); writer.WriteUInt16(AgeChronological);
            writer.WriteString(Xenotype, 64); writer.WriteString(XenotypeIcon, 96); writer.WriteUInt32(FavoriteColor);
            writer.WriteByte((byte)Health.Count);
            foreach (var (part, label) in Health) { writer.WriteString(part, 64); writer.WriteString(label, 128); }
            writer.WriteByte((byte)Relations.Count);
            foreach (var (relation, name, opinion, theirs) in Relations)
            { writer.WriteString(relation, 64); writer.WriteString(name, 96); writer.WriteUInt16((ushort)opinion); writer.WriteUInt16((ushort)theirs); }
            writer.WriteByte((byte)Possessions.Count);
            foreach (var (label, count) in Possessions) { writer.WriteString(label, 96); writer.WriteUInt32((uint)count); }
            writer.WriteByte(DevelopmentStage);
            writer.WriteString(ChildhoodTip, MaximumTipBytes); writer.WriteString(AdulthoodTip, MaximumTipBytes);
            WriteStrings(writer, TraitTips, MaximumTipBytes); WriteStrings(writer, Abilities, 64); WriteStrings(writer, PossessionDefs, 64);
        }

        private static void WriteStrings(PayloadWriter writer, IReadOnlyList<string> items, int bytes)
        { writer.WriteByte((byte)items.Count); foreach (string item in items) writer.WriteString(item, bytes); }

        private static List<string> ReadStrings(PayloadReader reader, int bytes)
        {
            var items = new List<string>();
            for (int i = PawnCard.ReadCount(reader); i > 0; i--) items.Add(reader.ReadString(bytes));
            return items;
        }

        internal static PawnCardDetails Read(PayloadReader reader)
        {
            string first = reader.ReadString(64), nick = reader.ReadString(64), last = reader.ReadString(64);
            byte gender = reader.ReadByte(); ushort bio = reader.ReadUInt16(), chrono = reader.ReadUInt16();
            string xenotype = reader.ReadString(64), icon = reader.ReadString(96); uint color = reader.ReadUInt32();
            var health = new List<(string, string)>();
            for (int i = PawnCard.ReadCount(reader); i > 0; i--) health.Add((reader.ReadString(64), reader.ReadString(128)));
            var relations = new List<(string, string, short, short)>();
            for (int i = PawnCard.ReadCount(reader); i > 0; i--)
                relations.Add((reader.ReadString(64), reader.ReadString(96), (short)reader.ReadUInt16(), (short)reader.ReadUInt16()));
            var possessions = new List<(string, int)>();
            for (int i = PawnCard.ReadCount(reader); i > 0; i--) possessions.Add((reader.ReadString(96), (int)reader.ReadUInt32()));
            byte stage = reader.ReadByte();
            string childhoodTip = reader.ReadString(MaximumTipBytes), adulthoodTip = reader.ReadString(MaximumTipBytes);
            List<string> traitTips = ReadStrings(reader, MaximumTipBytes), abilities = ReadStrings(reader, 64), possessionDefs = ReadStrings(reader, 64);
            return new PawnCardDetails(first, nick, last, gender, bio, chrono, xenotype, icon, color, health, relations, possessions,
                stage, childhoodTip, adulthoodTip, traitTips, abilities, possessionDefs);
        }
    }

    /// <summary>
    /// A starting pawn's portrait as the host's game draws it (PNG), so guests see the pawn as on the vanilla page.
    /// Sent once per pawn and again when its look changes; only the host, only in <c>Characters</c> (SETUP-004).
    /// </summary>
    public sealed class PawnPortrait
    {
        public const int MaximumImageBytes = 48 * 1024;
        /// <summary>Vanilla's view options: <see cref="Headgear"/> and/or <see cref="NoApparel"/>; 0 = apparel without headgear (its default).</summary>
        public const byte Headgear = 1, NoApparel = 2, Variants = 4;

        public PawnPortrait(int pawnId, byte[] png, byte variant = 0)
        {
            if (png == null || png.Length == 0 || png.Length > MaximumImageBytes) throw new ProtocolException("Invalid pawn portrait.");
            if (variant >= Variants) throw new ProtocolException("Unknown portrait variant.");
            PawnId = pawnId; Png = png; Variant = variant;
        }
        public int PawnId { get; }
        public byte[] Png { get; }
        public byte Variant { get; }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter();
            writer.WriteUInt32((uint)PawnId); writer.WriteByte(Variant); writer.WriteBytes(Png, MaximumImageBytes);
            return writer.ToArray();
        }
        public static PawnPortrait Read(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            int id = (int)reader.ReadUInt32(); byte variant = reader.ReadByte(); byte[] png = reader.ReadBytes(MaximumImageBytes);
            reader.EnsureComplete();
            return new PawnPortrait(id, png, variant);
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
