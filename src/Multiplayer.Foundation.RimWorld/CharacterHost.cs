using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-004, host side: the starting pawns stay in vanilla's GameInitData list. This class keeps who owns
    /// which pawn (by thing ID), builds the cards for the guests and carries out their requests with vanilla's
    /// StartingPawnUtility, so all random rolls happen in the host's game.
    /// </summary>
    internal sealed class CharacterHost
    {
        private readonly Dictionary<int, PlayerKey> owners = new Dictionary<int, PlayerKey>();
        private Dictionary<int, int> lastIndex = new Dictionary<int, int>();

        internal void Clear() { owners.Clear(); lastIndex.Clear(); }

        internal PlayerKey? OwnerOf(Pawn pawn) => owners.TryGetValue(pawn.thingIDNumber, out PlayerKey key) ? key : (PlayerKey?)null;

        /// <summary>SETUP-005: who owns which pawn at the moment the game starts.</summary>
        internal Dictionary<int, PlayerKey> SnapshotOwners() => new Dictionary<int, PlayerKey>(owners);

        /// <summary>Raises the starting group to one pawn per player (plan §6.3).</summary>
        internal void EnsurePawnCount(GameInitData init, int players)
        {
            int guard = 0;
            while (init.startingPawnCount < players && guard++ < PawnRoster.MaximumCards)
            {
                List<Pawn> pawns = init.startingAndOptionalPawns;
                if (pawns.Count <= init.startingPawnCount)
                {
                    int before = pawns.Count;
                    StartingPawnUtility.AddNewPawn(init.startingPawnCount);
                    if (pawns.Count == before) return; // vanilla refused; leave the count as it is
                }
                init.startingPawnCount++; // the first left-behind (or the new) pawn joins the starting group
            }
        }

        /// <summary>Current roster. Keeps ownership when the host re-rolls an owned pawn in place and frees pawns of players who left.</summary>
        internal PawnRoster Build(GameInitData init, ICollection<PlayerKey> present)
        {
            List<Pawn> pawns = init.startingAndOptionalPawns;
            var ids = new HashSet<int>(pawns.Select(p => p.thingIDNumber));
            foreach (var (id, key) in owners.ToList())
            {
                if (!present.Contains(key)) { owners.Remove(id); continue; }
                if (ids.Contains(id)) continue;
                owners.Remove(id);
                // Replaced in place (vanilla randomize, or our re-roll): the new pawn at that slot inherits the owner.
                if (lastIndex.TryGetValue(id, out int index) && index < pawns.Count && !owners.ContainsKey(pawns[index].thingIDNumber))
                    owners[pawns[index].thingIDNumber] = key;
            }
            lastIndex = pawns.Select((p, i) => (p.thingIDNumber, i)).ToDictionary(e => e.thingIDNumber, e => e.i);
            return new PawnRoster(pawns.Take(PawnRoster.MaximumCards)
                .Select((pawn, i) => Card(pawn, i < init.startingPawnCount, OwnerOf(pawn))).ToList());
        }

        /// <summary>Carries out one request. Returns null on success, otherwise why it was refused.</summary>
        internal string? Apply(PawnRequest request, GameInitData init)
        {
            if (request.Requester == null) return "Anfrage ohne Spieler.";
            PlayerKey who = request.Requester.Value;
            List<Pawn> pawns = init.startingAndOptionalPawns;
            int index = pawns.FindIndex(p => p.thingIDNumber == request.PawnId);
            if (index < 0) return "Diese Figur gibt es nicht mehr.";
            Pawn pawn = pawns[index];
            PlayerKey? owner = OwnerOf(pawn);
            bool selected = index < init.startingPawnCount;
            switch (request.Kind)
            {
                case PawnRequestKind.Claim:
                    if (!selected) return "Nur Figuren der Startgruppe können übernommen werden.";
                    if (owner != null) return owner == who ? null : "Diese Figur gehört schon jemandem.";
                    if (owners.ContainsValue(who)) return "Du hast schon eine Figur.";
                    owners[pawn.thingIDNumber] = who;
                    return null;
                case PawnRequestKind.Release:
                    if (owner != who) return "Das ist nicht deine Figur.";
                    owners.Remove(pawn.thingIDNumber);
                    return null;
                case PawnRequestKind.Reroll:
                    if (owner != who) return "Das ist nicht deine Figur.";
                    Pawn fresh = StartingPawnUtility.RandomizeInPlace(pawn);
                    owners.Remove(pawn.thingIDNumber);
                    if (fresh != null) owners[fresh.thingIDNumber] = who;
                    return null;
                case PawnRequestKind.Swap:
                    if (owner != who || !selected) return "Tauschen geht nur mit deiner eigenen Figur der Startgruppe.";
                    int other = pawns.FindIndex(p => p.thingIDNumber == request.OtherPawnId);
                    if (other < init.startingPawnCount) return "Getauscht wird mit einer zurückgelassenen Figur.";
                    if (OwnerOf(pawns[other]) != null) return "Diese Figur gehört schon jemandem.";
                    Pawn incoming = pawns[other];
                    // Swap the two positions, keeping vanilla's per-slot generation requests aligned.
                    pawns[index] = incoming; pawns[other] = pawn;
                    StartingPawnUtility.ReorderRequests(other, index);
                    StartingPawnUtility.ReorderRequests(index + 1, other);
                    owners.Remove(pawn.thingIDNumber);
                    owners[incoming.thingIDNumber] = who;
                    return null;
                case PawnRequestKind.Rename:
                    if (owner != who) return "Das ist nicht deine Figur.";
                    string nick = request.Text.Trim();
                    if (nick.Length == 0 || nick.Length > 24 || nick.Any(char.IsControl)) return "Name muss 1–24 druckbare Zeichen haben.";
                    if (pawn.Name is NameTriple triple) pawn.Name = new NameTriple(triple.First, nick, triple.Last);
                    else pawn.Name = new NameSingle(nick);
                    return null;
                default:
                    return "Unbekannte Anfrage.";
            }
        }

        internal static PawnCard Card(Pawn pawn, bool selected, PlayerKey? owner)
        {
            string Text(Func<string?> read) { try { return read() ?? ""; } catch (Exception) { return ""; } }
            var skills = new List<(string, byte, byte)>();
            try
            {
                foreach (SkillRecord record in pawn.skills?.skills ?? new List<SkillRecord>())
                    skills.Add((record.def.skillLabel.CapitalizeFirst(), record.TotallyDisabled ? (byte)255 : (byte)Math.Min(254, record.Level),
                        (byte)record.passion));
            }
            catch (Exception) { }
            var incapable = new List<string>();
            try
            {
                WorkTags disabled = pawn.CombinedDisabledWorkTags;
                foreach (WorkTags tag in Enum.GetValues(typeof(WorkTags)).Cast<WorkTags>())
                    if (tag != WorkTags.None && (disabled & tag) == tag && IsSingleFlag(tag)) incapable.Add(WorkTypeDefsUtility.LabelTranslated(tag).CapitalizeFirst());
            }
            catch (Exception) { }
            string summary = Text(() => GenderUtility.GetLabel(pawn.gender, false).CapitalizeFirst() + ", " + pawn.ageTracker.AgeBiologicalYears +
                (pawn.genes != null ? ", " + pawn.genes.XenotypeLabelCap : ""));
            return new PawnCard(pawn.thingIDNumber, selected, owner,
                Text(() => pawn.Name?.ToStringFull), Text(() => pawn.Name?.ToStringShort), summary,
                Text(() => pawn.story?.Childhood?.TitleCapFor(pawn.gender)), Text(() => pawn.story?.Adulthood?.TitleCapFor(pawn.gender)),
                Safe(() => pawn.story?.traits?.allTraits.Select(t => t.LabelCap).ToList()),
                skills.Take(PawnCard.MaximumEntries).ToList(),
                incapable.Take(PawnCard.MaximumEntries).ToList(),
                Safe(() => pawn.health?.hediffSet?.hediffs.Where(h => h.Visible).Select(h => h.LabelCap).ToList()),
                Details(pawn));
        }

        // What else the vanilla character card shows, for the guests' page. Every part is optional: a failure leaves it empty.
        private static PawnCardDetails Details(Pawn pawn)
        {
            string first = "", nick = "", last = "";
            if (pawn.Name is NameTriple triple) { first = triple.First; nick = triple.Nick; last = triple.Last; }
            else nick = pawn.Name?.ToStringShort ?? "";
            string xenotype = "", icon = "";
            try
            {
                if (pawn.genes != null)
                {
                    xenotype = pawn.genes.XenotypeLabelCap;
                    if (pawn.genes.CustomXenotype != null) icon = pawn.genes.CustomXenotype.iconDef != null ? "c:" + pawn.genes.CustomXenotype.iconDef.defName : "";
                    else if (pawn.genes.Xenotype != null) icon = "x:" + pawn.genes.Xenotype.defName;
                }
            }
            catch (Exception) { }
            uint color = 0;
            try
            {
                if (pawn.story?.favoriteColor != null)
                {
                    Color32 c = pawn.story.favoriteColor.color;
                    color = ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
                }
            }
            catch (Exception) { }
            var health = new List<(string, string)>();
            try
            {
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => h.Visible).Take(PawnCard.MaximumEntries))
                    health.Add((Clip(hediff.Part?.LabelCap ?? "WholeBody".Translate().CapitalizeFirst().ToString(), 64), Clip(hediff.LabelCap, 128)));
            }
            catch (Exception) { }
            var relations = new List<(string, string, short, short)>();
            try
            {
                foreach (Pawn other in pawn.relations.RelatedPawns.Take(PawnCard.MaximumEntries))
                {
                    PawnRelationDef? relation = pawn.GetMostImportantRelation(other);
                    if (relation == null) continue;
                    relations.Add((Clip(relation.GetGenderSpecificLabelCap(other), 64), Clip(other.Name?.ToStringFull ?? other.LabelShort, 96),
                        (short)pawn.relations.OpinionOf(other), (short)(other.relations?.OpinionOf(pawn) ?? 0)));
                }
            }
            catch (Exception) { }
            var possessions = new List<(string, int)>();
            try
            {
                if (Find.GameInitData?.startingPossessions != null && Find.GameInitData.startingPossessions.TryGetValue(pawn, out List<ThingDefCount> things))
                    foreach (ThingDefCount thing in things.Take(PawnCard.MaximumEntries)) possessions.Add((Clip(thing.ThingDef.LabelCap, 96), thing.Count));
            }
            catch (Exception) { }
            return new PawnCardDetails(Clip(first, 64), Clip(nick, 64), Clip(last, 64), (byte)pawn.gender,
                (ushort)Math.Min(ushort.MaxValue, pawn.ageTracker?.AgeBiologicalYears ?? 0), (ushort)Math.Min(ushort.MaxValue, pawn.ageTracker?.AgeChronologicalYears ?? 0),
                Clip(xenotype, 64), Clip(icon, 96), color, health, relations, possessions);
        }

        // Cuts text to what the wire allows (UTF-8 bytes).
        private static string Clip(string? text, int bytes)
        {
            text ??= "";
            while (text.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(text) > bytes) text = text.Substring(0, text.Length - 1);
            return text;
        }

        /// <summary>What decides a portrait's look; a new pawn or other genes, body or hair draws it again (guest page).</summary>
        internal static string PortraitKey(Pawn pawn)
        {
            try
            {
                return pawn.thingIDNumber + "|" + pawn.genes?.XenotypeLabel + "|" + pawn.story?.bodyType?.defName + "|" + pawn.story?.headType?.defName +
                    "|" + pawn.story?.hairDef?.defName + "|" + pawn.story?.SkinColor + "|" + pawn.apparel?.WornApparelCount;
            }
            catch (Exception) { return pawn.thingIDNumber.ToString(); }
        }

        /// <summary>The pawn as the vanilla character page shows it (no headgear), as PNG; null if it cannot be drawn.</summary>
        internal static byte[]? RenderPortrait(Pawn pawn)
        {
            RenderTexture? previous = RenderTexture.active;
            Texture2D? image = null;
            try
            {
                RenderTexture texture = PortraitsCache.Get(pawn, Page_ConfigureStartingPawns.PawnPortraitSize, Rot4.South, default, 1f, false, false, false, true);
                RenderTexture.active = texture;
                image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                image.Apply();
                byte[] png = image.EncodeToPNG();
                return png.Length > 0 && png.Length <= PawnPortrait.MaximumImageBytes ? png : null;
            }
            catch (Exception error) { Log.Warning("Multiplayer Foundation: portrait of " + pawn.LabelShort + " failed: " + error.Message); return null; }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.Destroy(image);
            }
        }

        private static IReadOnlyList<string> Safe(Func<List<string>?> read)
        {
            try { return (read() ?? new List<string>()).Take(PawnCard.MaximumEntries).ToList(); }
            catch (Exception) { return Array.Empty<string>(); }
        }

        private static bool IsSingleFlag(WorkTags tag) { int v = (int)tag; return v != 0 && (v & (v - 1)) == 0; }
    }
}
