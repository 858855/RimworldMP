using System;
using System.Collections.Generic;
using System.Linq;

namespace Multiplayer.Foundation.Protocol
{
    /// <summary>
    /// RimWorld def names as the lobby carries them. Same characters as <c>Verse.Def.AllowedDefNamesRegex</c> in 1.6
    /// (<c>^[a-zA-Z0-9\-_]*$</c>), but never empty.
    /// </summary>
    public static class DefNames
    {
        public const int MaximumChars = 128;

        public static bool IsValid(string? name) =>
            !string.IsNullOrEmpty(name) && name!.Length <= MaximumChars &&
            name.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_');

        public static void Write(PayloadWriter writer, string name)
        {
            if (!IsValid(name)) throw new ProtocolException("Invalid def name.");
            writer.WriteString(name, MaximumChars);
        }

        public static string Read(PayloadReader reader)
        {
            string name = reader.ReadString(MaximumChars);
            if (!IsValid(name)) throw new ProtocolException("Invalid def name.");
            return name;
        }
    }

    /// <summary>
    /// Factions mode (ADR 0037): the host's shared difficulty and the storytellers each player may choose for their
    /// own faction. Only the host sends it; guests cannot change the difficulty.
    /// </summary>
    public sealed class FactionOptions
    {
        public const int MaximumStorytellers = 32;

        public FactionOptions(string difficulty, IReadOnlyList<string> storytellers)
        {
            if (!DefNames.IsValid(difficulty)) throw new ProtocolException("Invalid difficulty def name.");
            if (storytellers == null || storytellers.Count == 0 || storytellers.Count > MaximumStorytellers)
                throw new ProtocolException("Offer 1-32 storytellers.");
            if (storytellers.Any(name => !DefNames.IsValid(name))) throw new ProtocolException("Invalid storyteller def name.");
            if (storytellers.Distinct(StringComparer.Ordinal).Count() != storytellers.Count)
                throw new ProtocolException("Storytellers must be distinct.");
            Difficulty = difficulty; Storytellers = storytellers.ToArray();
        }

        /// <summary>defName of the shared <c>RimWorld.DifficultyDef</c>; custom values travel with the host's save.</summary>
        public string Difficulty { get; }
        /// <summary>defNames of the selectable <c>RimWorld.StorytellerDef</c>s, in display order.</summary>
        public IReadOnlyList<string> Storytellers { get; }

        public bool Offers(string? storyteller) => storyteller != null && Storytellers.Contains(storyteller, StringComparer.Ordinal);

        public bool SameAs(FactionOptions? other) =>
            other != null && other.Difficulty == Difficulty && other.Storytellers.SequenceEqual(Storytellers, StringComparer.Ordinal);

        public void Write(PayloadWriter writer)
        {
            DefNames.Write(writer, Difficulty);
            writer.WriteByte((byte)Storytellers.Count);
            foreach (string name in Storytellers) DefNames.Write(writer, name);
        }

        public static FactionOptions Read(PayloadReader reader)
        {
            string difficulty = DefNames.Read(reader);
            byte count = reader.ReadByte();
            if (count == 0 || count > MaximumStorytellers) throw new ProtocolException("Offer 1-32 storytellers.");
            var storytellers = new string[count];
            for (int i = 0; i < count; i++) storytellers[i] = DefNames.Read(reader);
            return new FactionOptions(difficulty, storytellers);
        }

        public byte[] ToPayload() { var writer = new PayloadWriter(); Write(writer); return writer.ToArray(); }

        public static FactionOptions FromPayload(byte[] bytes)
        { var reader = new PayloadReader(bytes); FactionOptions options = Read(reader); reader.EnsureComplete(); return options; }
    }

    /// <summary>ADR 0038: which choice of a faction the host's game rejects.</summary>
    public enum FactionChoiceKind : byte
    {
        Site = 1,
        Ideology = 2
    }

    /// <summary>
    /// ADR 0038: what everyone sees of a faction's ideology. The data itself is opaque to the coordinator (its format belongs
    /// to the game side) and only goes to the host; the lobby carries a label and the SHA-256 of the data.
    /// </summary>
    public sealed class IdeologySummary
    {
        public IdeologySummary(string label, byte[] hash)
        {
            FactionPayloads.ValidateText(label, FactionPayloads.MaximumIdeologyLabelBytes, "ideology label");
            if (hash == null || hash.Length != 32) throw new ProtocolException("Ideology hash must have 32 bytes.");
            Label = label; Hash = (byte[])hash.Clone();
        }
        public string Label { get; }
        public byte[] Hash { get; }

        public static IdeologySummary Of(string label, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var sha = System.Security.Cryptography.SHA256.Create()) return new IdeologySummary(label, sha.ComputeHash(data));
        }
        public bool SameAs(IdeologySummary? other) => other != null && other.Label == Label && other.Hash.SequenceEqual(Hash);

        public void Write(PayloadWriter writer) { writer.WriteString(Label, FactionPayloads.MaximumIdeologyLabelBytes); writer.WriteFixed(Hash, 32); }
        public static IdeologySummary Read(PayloadReader reader)
        { string label = reader.ReadString(FactionPayloads.MaximumIdeologyLabelBytes); return new IdeologySummary(label, reader.ReadFixed(32)); }
    }

    /// <summary>ADR 0038: one faction of a saved Factions game, as the host's save records it.</summary>
    public readonly struct FactionRestoreEntry
    {
        public FactionRestoreEntry(PlayerKey player, byte slot, string storyteller)
        {
            if (!player.IsValid) throw new ProtocolException("Invalid player key.");
            if (slot >= 64) throw new ProtocolException("Invalid faction slot.");
            if (!DefNames.IsValid(storyteller)) throw new ProtocolException("Invalid storyteller def name.");
            Player = player; Slot = slot; Storyteller = storyteller;
        }
        public PlayerKey Player { get; }
        public byte Slot { get; }
        public string Storyteller { get; }
    }

    /// <summary>
    /// ADR 0038 (SAVE-002, Factions part): the host's game restores the faction table of a loaded save into its lobby.
    /// Player keys and slots are unique.
    /// </summary>
    public sealed class FactionRestore
    {
        public const int MaximumEntries = 64;

        public FactionRestore(IReadOnlyList<FactionRestoreEntry> entries)
        {
            if (entries == null || entries.Count == 0 || entries.Count > MaximumEntries) throw new ProtocolException("Restore 1-64 factions.");
            if (entries.Select(e => e.Player).Distinct().Count() != entries.Count) throw new ProtocolException("Player keys must be distinct.");
            if (entries.Select(e => e.Slot).Distinct().Count() != entries.Count) throw new ProtocolException("Faction slots must be distinct.");
            Entries = entries.ToArray();
        }
        public IReadOnlyList<FactionRestoreEntry> Entries { get; }

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter(); writer.WriteByte((byte)Entries.Count);
            foreach (FactionRestoreEntry entry in Entries)
            { entry.Player.Write(writer); writer.WriteByte(entry.Slot); DefNames.Write(writer, entry.Storyteller); }
            return writer.ToArray();
        }

        public static FactionRestore FromPayload(byte[] bytes)
        {
            var reader = new PayloadReader(bytes); byte count = reader.ReadByte();
            if (count == 0 || count > MaximumEntries) throw new ProtocolException("Restore 1-64 factions.");
            var entries = new FactionRestoreEntry[count];
            for (int i = 0; i < count; i++) entries[i] = new FactionRestoreEntry(PlayerKey.Read(reader), reader.ReadByte(), DefNames.Read(reader));
            reader.EnsureComplete();
            return new FactionRestore(entries);
        }
    }

    public static class FactionPayloads
    {
        public const int MaximumIdeologyLabelBytes = 96;
        public const int MaximumIdeologyBytes = 49152;
        public const int MaximumNoteBytes = 128;

        /// <summary>A player's storyteller for their own faction (ADR 0037).</summary>
        public static byte[] StorytellerChoice(string storyteller)
        { var writer = new PayloadWriter(); DefNames.Write(writer, storyteller); return writer.ToArray(); }

        public static string ReadStorytellerChoice(byte[] bytes)
        { var reader = new PayloadReader(bytes); string name = DefNames.Read(reader); reader.EnsureComplete(); return name; }

        /// <summary>ADR 0038 (98): the starting site of the own faction, or null to withdraw it.</summary>
        public static byte[] SiteChoice(SiteTile? tile)
        { var writer = new PayloadWriter(); SiteTile.WriteOptional(writer, tile); return writer.ToArray(); }

        public static SiteTile? ReadSiteChoice(byte[] bytes)
        { var reader = new PayloadReader(bytes); SiteTile? tile = SiteTile.ReadOptional(reader); reader.EnsureComplete(); return tile; }

        /// <summary>ADR 0038 (99, client to server): the own faction's ideology as a label and opaque data.</summary>
        public static byte[] IdeologyChoice(string label, byte[] data)
        {
            ValidateText(label, MaximumIdeologyLabelBytes, "ideology label"); ValidateIdeologyData(data);
            var writer = new PayloadWriter(); writer.WriteString(label, MaximumIdeologyLabelBytes); writer.WriteBytes(data, MaximumIdeologyBytes);
            return writer.ToArray();
        }

        public static (string Label, byte[] Data) ReadIdeologyChoice(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            string label = reader.ReadString(MaximumIdeologyLabelBytes); byte[] data = reader.ReadBytes(MaximumIdeologyBytes);
            reader.EnsureComplete();
            ValidateText(label, MaximumIdeologyLabelBytes, "ideology label"); ValidateIdeologyData(data);
            return (label, data);
        }

        /// <summary>ADR 0038 (99, server to the host only): an accepted ideology choice with the chooser's slot and key.</summary>
        public static byte[] IdeologyForHost(byte slot, PlayerKey player, string label, byte[] data)
        {
            if (slot >= 64) throw new ProtocolException("Invalid faction slot.");
            ValidateText(label, MaximumIdeologyLabelBytes, "ideology label"); ValidateIdeologyData(data);
            var writer = new PayloadWriter(); writer.WriteByte(slot); player.Write(writer);
            writer.WriteString(label, MaximumIdeologyLabelBytes); writer.WriteBytes(data, MaximumIdeologyBytes);
            return writer.ToArray();
        }

        public static (byte Slot, PlayerKey Player, string Label, byte[] Data) ReadIdeologyForHost(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            byte slot = reader.ReadByte(); PlayerKey player = PlayerKey.Read(reader);
            string label = reader.ReadString(MaximumIdeologyLabelBytes); byte[] data = reader.ReadBytes(MaximumIdeologyBytes);
            reader.EnsureComplete();
            if (slot >= 64) throw new ProtocolException("Invalid faction slot.");
            ValidateText(label, MaximumIdeologyLabelBytes, "ideology label"); ValidateIdeologyData(data);
            return (slot, player, label, data);
        }

        /// <summary>ADR 0038 (100, host only): reject a faction's site or ideology with a reason shown to its player.</summary>
        public static byte[] ClearChoice(byte slot, FactionChoiceKind kind, string reason)
        {
            ValidateClear(slot, kind, reason);
            var writer = new PayloadWriter(); writer.WriteByte(slot); writer.WriteByte((byte)kind); writer.WriteString(reason, MaximumNoteBytes);
            return writer.ToArray();
        }

        public static (byte Slot, FactionChoiceKind Kind, string Reason) ReadClearChoice(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            byte slot = reader.ReadByte(); var kind = (FactionChoiceKind)reader.ReadByte(); string reason = reader.ReadString(MaximumNoteBytes);
            reader.EnsureComplete(); ValidateClear(slot, kind, reason);
            return (slot, kind, reason);
        }

        /// <summary>Printable, not blank, at most this many UTF-8 bytes: labels, reasons and notes.</summary>
        public static bool IsValidText(string? text, int maximumBytes) =>
            !string.IsNullOrWhiteSpace(text) && !text!.Any(char.IsControl) && System.Text.Encoding.UTF8.GetByteCount(text) <= maximumBytes;

        internal static void ValidateText(string? text, int maximumBytes, string what)
        { if (!IsValidText(text, maximumBytes)) throw new ProtocolException("Invalid " + what + "."); }

        private static void ValidateIdeologyData(byte[]? data)
        {
            if (data == null || data.Length == 0 || data.Length > MaximumIdeologyBytes) throw new ProtocolException("Ideology data must have 1-49152 bytes.");
        }

        private static void ValidateClear(byte slot, FactionChoiceKind kind, string reason)
        {
            if (slot >= 64) throw new ProtocolException("Invalid faction slot.");
            if (kind != FactionChoiceKind.Site && kind != FactionChoiceKind.Ideology) throw new ProtocolException("Unknown faction choice.");
            ValidateText(reason, MaximumNoteBytes, "reason");
        }
    }
}
