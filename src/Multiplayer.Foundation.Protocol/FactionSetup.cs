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

    public static class FactionPayloads
    {
        /// <summary>A player's storyteller for their own faction (ADR 0037).</summary>
        public static byte[] StorytellerChoice(string storyteller)
        { var writer = new PayloadWriter(); DefNames.Write(writer, storyteller); return writer.ToArray(); }

        public static string ReadStorytellerChoice(byte[] bytes)
        { var reader = new PayloadReader(bytes); string name = DefNames.Read(reader); reader.EnsureComplete(); return name; }
    }
}
