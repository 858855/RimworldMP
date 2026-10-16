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
    /// SETUP-004, guest: the vanilla "Create characters" page rebuilt from the host's cards and portraits, with the host
    /// page's controls. The pawns live only in the host's game (ADR 0022), so every change (names, randomize, xenotype,
    /// development stage, custom xenotypes from vanilla's editor) goes to the host as a request; the page shows what comes
    /// back. Layout, icons and words follow vanilla (its translation keys where they exist).
    /// </summary>
    internal sealed class GuestCharacterPage
    {
        private const float TitleHeight = 45f, ButtonHeight = 38f, ListWidth = 140f, EntryHeight = 60f, TeamHeight = 110f, Gap = 10f;
        private const string NameControl = "mpGuestName";
        private static readonly Color HeaderColor = new Color(0.9f, 0.85f, 0.35f), DimColor = new Color(0.65f, 0.65f, 0.65f),
            BoxColor = new Color(0.12f, 0.12f, 0.12f), GoodColor = new Color(0.3f, 0.85f, 0.3f), BadColor = new Color(0.9f, 0.3f, 0.3f);
        private readonly Dictionary<(int Pawn, byte Variant), Texture2D> portraits = new Dictionary<(int, byte), Texture2D>();
        private Texture2D? barFill;
        private Vector2 listScroll;
        private bool showHeadgear, showApparel = true;
        // The names being edited on the own pawn; sent when the boxes lose focus or on Enter, like vanilla keeps them.
        private int? namesPawnId;
        private string editFirst = "", editNick = "", editLast = "", sentFirst = "", sentNick = "", sentLast = "";
        private bool wasEditing;
        private float sentNamesAt = -100f;

        internal int PortraitCount => portraits.Count;

        internal void SetPortrait(PawnPortrait portrait)
        {
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(portrait.Png)) { UnityEngine.Object.Destroy(texture); return; }
            var key = (portrait.PawnId, portrait.Variant);
            if (portraits.TryGetValue(key, out Texture2D old)) UnityEngine.Object.Destroy(old);
            portraits[key] = texture;
        }

        internal void Clear()
        {
            foreach (Texture2D texture in portraits.Values) UnityEngine.Object.Destroy(texture);
            portraits.Clear(); namesPawnId = null; editFirst = editNick = editLast = ""; listScroll = Vector2.zero;
        }

        // The portrait for the current view options; the default view until the others arrived.
        private Texture2D? Portrait(int pawnId)
        {
            byte variant = (byte)((showHeadgear ? PawnPortrait.Headgear : 0) | (showApparel ? 0 : PawnPortrait.NoApparel));
            return portraits.TryGetValue((pawnId, variant), out Texture2D wanted) ? wanted
                : portraits.TryGetValue((pawnId, (byte)0), out Texture2D fallback) ? fallback : null;
        }

        /// <summary>Draws the page. <paramref name="chosenPawnId"/> is the pawn shown on the card.</summary>
        internal void Draw(Rect inRect, PawnRoster roster, IReadOnlyList<PlayerView> players, PlayerKey? myKey, bool myDone, string doneLine,
            ref int? chosenPawnId, Action<PawnRequest> request, Action<bool> setDone, Action openLobby)
        {
            PawnCard? myPawn = roster.Cards.FirstOrDefault(c => c.Selected && myKey != null && c.Owner == myKey);
            int? shownId = chosenPawnId;
            if (shownId == null || roster.Cards.All(c => c.PawnId != shownId))
                chosenPawnId = (myPawn ?? roster.Cards.FirstOrDefault())?.PawnId;
            // Title with who is done; vanilla's view options on the right.
            Text.Font = GameFont.Medium;
            string title = L("CreateCharacters", "Figuren erstellen");
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), title);
            float titleWidth = Text.CalcSize(title).x;
            Text.Font = GameFont.Small;
            GUI.color = DimColor;
            Widgets.Label(new Rect(inRect.x + titleWidth + 20f, inRect.y + 8f, inRect.width - titleWidth - 230f, 24f), doneLine);
            GUI.color = Color.white;
            Widgets.CheckboxLabeled(new Rect(inRect.xMax - 190f, inRect.y, 190f, 22f), L("ShowHeadgear", "Kopfbedeckung zeigen"), ref showHeadgear);
            Widgets.CheckboxLabeled(new Rect(inRect.xMax - 190f, inRect.y + 22f, 190f, 22f), L("ShowApparel", "Kleidung zeigen"), ref showApparel);
            float top = inRect.y + TitleHeight, bottom = inRect.yMax - ButtonHeight - Gap;
            chosenPawnId = DrawList(new Rect(inRect.x, top, ListWidth, bottom - top), roster, players, myKey, chosenPawnId);
            shownId = chosenPawnId;
            PawnCard? chosen = roster.Cards.FirstOrDefault(c => c.PawnId == shownId);
            int slot = roster.Cards.ToList().FindIndex(c => c.PawnId == shownId);
            float mainX = inRect.x + ListWidth + Gap, mainWidth = inRect.xMax - mainX;
            Rect card = new Rect(mainX, top, mainWidth, bottom - top - TeamHeight - Gap);
            Widgets.DrawMenuSection(card);
            if (chosen != null) DrawCard(card.ContractedBy(17f), chosen, slot, players, myKey, myPawn, request);
            DrawTeamSkills(new Rect(mainX, card.yMax + Gap, mainWidth, TeamHeight), roster);
            // Bottom row where vanilla has Back, Xenotype editor and Start.
            float buttonY = inRect.yMax - ButtonHeight;
            if (Widgets.ButtonText(new Rect(inRect.x, buttonY, 150f, ButtonHeight), "Chat / Lobby")) openLobby();
            bool mine = chosen != null && myKey != null && chosen.Owner == myKey;
            if (mine && ModsConfig.BiotechActive &&
                Widgets.ButtonText(new Rect(inRect.center.x - 100f, buttonY, 200f, ButtonHeight), L("XenotypeEditor", "Xenotyp-Editor")))
                OpenXenotypeEditor(chosen!, slot, request);
            if (myPawn == null && !myDone)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = DimColor;
                Widgets.Label(new Rect(inRect.x + 160f, buttonY, inRect.width - 330f, ButtonHeight), "Erst eine Figur der Startgruppe übernehmen.");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 150f, buttonY, 150f, ButtonHeight), myDone ? "Nicht fertig" : "Fertig", true, true, myDone || myPawn != null))
                setDone(!myDone);
        }

        // Left column: the starting group and the pawns left behind, each with its portrait like vanilla.
        private int? DrawList(Rect rect, PawnRoster roster, IReadOnlyList<PlayerView> players, PlayerKey? myKey, int? chosenPawnId)
        {
            int count = roster.Cards.Count;
            float height = 2f * 30f + count * (EntryHeight + 4f);
            Rect view = new Rect(0f, 0f, rect.width - (height > rect.height ? 16f : 0f), height);
            Widgets.BeginScrollView(rect, ref listScroll, view);
            float y = 0f;
            foreach (bool selected in new[] { true, false })
            {
                Widgets.Label(new Rect(0f, y, view.width, 26f), selected ? L("StartingPawnsSelected", "Startgruppe") : L("StartingPawnsLeftBehind", "Zurückgelassen"));
                y += 30f;
                foreach (PawnCard card in roster.Cards.Where(c => c.Selected == selected))
                {
                    Rect entry = new Rect(selected ? 0f : 6f, y, view.width - (selected ? 0f : 6f), EntryHeight);
                    Widgets.DrawOptionBackground(entry, card.PawnId == chosenPawnId);
                    Texture2D? portrait = Portrait(card.PawnId);
                    if (portrait != null)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.6f);
                        GUI.DrawTexture(new Rect(entry.xMax - 50f, entry.y, 50f, EntryHeight), portrait, ScaleMode.ScaleAndCrop);
                        GUI.color = Color.white;
                    }
                    Widgets.Label(new Rect(entry.x + 6f, entry.y + 2f, entry.width - 12f, 22f), card.ShortName);
                    GUI.color = DimColor;
                    Widgets.Label(new Rect(entry.x + 6f, entry.y + 20f, entry.width - 12f, 22f), card.Adulthood.Length > 0 ? card.Adulthood : card.Childhood);
                    Text.Font = GameFont.Tiny;
                    int ownerIndex = card.Owner == null ? -1 : players.ToList().FindIndex(p => p.Key == card.Owner);
                    GUI.color = ownerIndex < 0 ? DimColor : SiteFlags.ColorFor(ownerIndex);
                    string owner = ownerIndex < 0 ? "frei" : players[ownerIndex].Name + (card.Owner == myKey ? " (du)" : "");
                    Widgets.Label(new Rect(entry.x + 6f, entry.y + 40f, entry.width - 12f, 18f), owner);
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    if (Widgets.ButtonInvisible(entry)) chosenPawnId = card.PawnId;
                    y += EntryHeight + 4f;
                }
            }
            Widgets.EndScrollView();
            return chosenPawnId;
        }

        private void DrawCard(Rect rect, PawnCard card, int slot, IReadOnlyList<PlayerView> players, PlayerKey? myKey, PawnCard? myPawn,
            Action<PawnRequest> request)
        {
            PawnCardDetails? d = card.Details;
            bool mine = myKey != null && card.Owner == myKey;
            float x = rect.x, y = rect.y;
            DrawNames(new Rect(x, y, 432f, 30f), card, d, mine, request);
            // The portrait right of the names, as vanilla shows it (kept clear of the action box on the right).
            Vector2 size = Page_ConfigureStartingPawns.PawnPortraitSize * 0.8f;
            Texture2D? portrait = Portrait(card.PawnId);
            if (portrait != null) GUI.DrawTexture(new Rect(rect.x + 476f, rect.y - 8f, size.x, size.y), portrait, ScaleMode.ScaleToFit);
            DrawActions(new Rect(rect.xMax - 200f, rect.y, 200f, 130f), card, slot, players, mine, myPawn, request);
            // Gender, age and xenotype; then faction and favourite colour.
            y += 38f;
            if (d != null)
            {
                Texture2D? genderIcon = d.Gender == 0 ? null : GenderUtility.GetIcon((Gender)d.Gender);
                if (genderIcon != null) GUI.DrawTexture(new Rect(x, y + 3f, 22f, 22f), genderIcon);
                string age = L("Age", "Alter") + " " + d.AgeBiological + " (" + d.AgeChronological + ")";
                float ageWidth = Text.CalcSize(age).x + 10f;
                Widgets.Label(new Rect(x + 28f, y + 2f, ageWidth, 26f), age);
                if (d.Xenotype.Length > 0)
                {
                    Rect xeno = new Rect(x + 34f + ageWidth, y, Text.CalcSize(d.Xenotype).x + 40f, 28f);
                    Widgets.DrawBoxSolid(xeno, BoxColor);
                    Texture2D? icon = XenotypeIcon(d.XenotypeIcon);
                    if (icon != null) GUI.DrawTexture(new Rect(xeno.x + 4f, xeno.y + 3f, 22f, 22f), icon);
                    Widgets.Label(new Rect(xeno.x + 30f, xeno.y + 2f, xeno.width - 30f, 26f), d.Xenotype);
                    XenotypeDef? xenotypeDef = d.XenotypeIcon.StartsWith("x:") ? DefDatabase<XenotypeDef>.GetNamedSilentFail(d.XenotypeIcon.Substring(2)) : null;
                    if (xenotypeDef != null) TooltipHandler.TipRegion(xeno, xenotypeDef.LabelCap + "\n\n" + xenotypeDef.description);
                }
            }
            else Widgets.Label(new Rect(x, y + 2f, 400f, 26f), card.Summary);
            y += 32f;
            Faction? player = Find.World != null ? Find.FactionManager?.OfPlayer : null;
            if (player != null)
            {
                Rect faction = new Rect(x, y, Text.CalcSize(player.Name).x + 40f, 28f);
                Widgets.DrawBoxSolid(faction, BoxColor);
                GUI.color = player.Color;
                GUI.DrawTexture(new Rect(faction.x + 4f, faction.y + 3f, 22f, 22f), player.def.FactionIcon);
                GUI.color = Color.white;
                Widgets.Label(new Rect(faction.x + 30f, faction.y + 2f, faction.width - 30f, 26f), player.Name);
                if (d != null && d.FavoriteColor != 0)
                    Widgets.DrawBoxSolid(new Rect(faction.xMax + 6f, y + 2f, 24f, 24f), FromRgba(d.FavoriteColor));
            }
            y += 42f;
            float columnsTop = Math.Max(y, rect.y + 136f);
            DrawBackgroundColumn(new Rect(rect.x, columnsTop, 240f, rect.yMax - columnsTop), card);
            DrawSkills(new Rect(rect.x + 255f, columnsTop, 230f, rect.yMax - columnsTop), card);
            DrawRightColumn(new Rect(rect.x + 500f, columnsTop, rect.width - 500f, rect.yMax - columnsTop), card);
        }

        // First, nick and last name. On the player's own pawn they are text boxes like vanilla's; a change goes to the host
        // when the boxes lose focus or on Enter, and the boxes show what the host has as soon as it arrives.
        private void DrawNames(Rect rect, PawnCard card, PawnCardDetails? d, bool mine, Action<PawnRequest> request)
        {
            string first = d?.First ?? "", nick = d?.Nick ?? card.ShortName, last = d?.Last ?? "";
            Rect[] boxes = { new Rect(rect.x, rect.y, 140f, 30f), new Rect(rect.x + 146f, rect.y, 140f, 30f), new Rect(rect.x + 292f, rect.y, 140f, 30f) };
            bool editing = GUI.GetNameOfFocusedControl().StartsWith(NameControl);
            // Another pawn shown now: what was typed for the one before still counts, as on the vanilla page.
            if (namesPawnId != null && namesPawnId != card.PawnId) { CommitNames(namesPawnId.Value, request); namesPawnId = null; }
            if (!mine || d == null)
            {
                NameBox(boxes[0], first); NameBox(boxes[1], nick); NameBox(boxes[2], last);
                wasEditing = false;
                return;
            }
            // Until the host's answer shows the sent names (or 3 s passed), keep what was typed instead of the older card.
            bool waiting = Time.realtimeSinceStartup - sentNamesAt < 3f && (editFirst.Trim() != first || editNick.Trim() != nick || editLast.Trim() != last);
            if (namesPawnId == null || (!editing && !waiting))
            {
                namesPawnId = card.PawnId;
                editFirst = sentFirst = first; editNick = sentNick = nick; editLast = sentLast = last;
            }
            bool enter = editing && Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName(NameControl + "First"); editFirst = Clip(Widgets.TextField(boxes[0], editFirst));
            GUI.SetNextControlName(NameControl + "Nick"); editNick = Clip(Widgets.TextField(boxes[1], editNick));
            GUI.SetNextControlName(NameControl + "Last"); editLast = Clip(Widgets.TextField(boxes[2], editLast));
            if (enter || (wasEditing && !editing))
            {
                CommitNames(card.PawnId, request);
                if (enter) { GUI.FocusControl(null); Event.current.Use(); }
            }
            wasEditing = editing;
        }

        // Sends the typed names if they differ from what was last sent; first and last name must not be empty (vanilla).
        private void CommitNames(int pawnId, Action<PawnRequest> request)
        {
            string first = editFirst.Trim(), nick = editNick.Trim(), last = editLast.Trim();
            if ((first == sentFirst && nick == sentNick && last == sentLast) || first.Length == 0 || last.Length == 0) return;
            request(new PawnRequest(PawnRequestKind.SetName, pawnId, 0, PawnRequest.Names(first, nick, last)));
            sentFirst = first; sentNick = nick; sentLast = last;
            sentNamesAt = Time.realtimeSinceStartup;
        }

        // Where vanilla has Randomize and the two generation buttons under it: who owns the pawn and what this player may do.
        private void DrawActions(Rect rect, PawnCard card, int slot, IReadOnlyList<PlayerView> players, bool mine, PawnCard? myPawn,
            Action<PawnRequest> request)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.16f, 0.16f, 0.16f));
            Rect inner = rect.ContractedBy(6f);
            string owner = card.Owner == null ? "frei" : players.FirstOrDefault(p => p.Key == card.Owner)?.Name ?? "?";
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), mine ? "Deine Figur" : card.Selected ? "Besitzer: " + owner : "Zurückgelassen");
            Text.Anchor = TextAnchor.UpperLeft;
            float y = inner.y + 26f;
            bool Button(string label)
            {
                bool clicked = Widgets.ButtonText(new Rect(inner.x, y, inner.width, 28f), label);
                y += 32f;
                return clicked;
            }
            if (mine)
            {
                if (Button(L("Randomize", "Neu würfeln"))) request(new PawnRequest(PawnRequestKind.Reroll, card.PawnId));
                if (ModsConfig.BiotechActive)
                {
                    float half = (inner.width - 4f) / 2f;
                    var stage = (DevelopmentalStage)(card.Details?.DevelopmentStage ?? 0);
                    if (Widgets.ButtonText(new Rect(inner.x, y, half, 28f), StageLabel(stage)))
                        Find.WindowStack.Add(new FloatMenu(new[] { DevelopmentalStage.Adult, DevelopmentalStage.Child, DevelopmentalStage.Baby }
                            .Select(s => new FloatMenuOption(StageLabel(s), () =>
                                request(new PawnRequest(PawnRequestKind.SetDevelopmentStage, card.PawnId, (int)s)))).ToList()));
                    if (Widgets.ButtonText(new Rect(inner.x + half + 4f, y, half, 28f), (card.Details?.Xenotype ?? "").Truncate(half - 8f)))
                        Find.WindowStack.Add(new FloatMenu(XenotypeOptions(card, slot, request)));
                    y += 32f;
                }
                if (Button("Freigeben")) request(new PawnRequest(PawnRequestKind.Release, card.PawnId));
            }
            else if (card.Selected && card.Owner == null && myPawn == null)
            {
                if (Button("Übernehmen")) request(new PawnRequest(PawnRequestKind.Claim, card.PawnId));
            }
            else if (!card.Selected && card.Owner == null && myPawn != null)
            {
                if (Button("Mit meiner Figur tauschen")) request(new PawnRequest(PawnRequestKind.Swap, myPawn.PawnId, card.PawnId));
            }
        }

        // Vanilla's xenotype menu: every xenotype, this player's custom ones, and the editor.
        private List<FloatMenuOption> XenotypeOptions(PawnCard card, int slot, Action<PawnRequest> request)
        {
            var options = new List<FloatMenuOption>();
            foreach (XenotypeDef def in DefDatabase<XenotypeDef>.AllDefs.OrderByDescending(x => x.displayPriority).ThenBy(x => x.label))
                options.Add(new FloatMenuOption(def.LabelCap, () =>
                    request(new PawnRequest(PawnRequestKind.SetXenotype, card.PawnId, 0, PawnXenotypeChoice.Standard(def.defName).ToText()))));
            foreach (CustomXenotype custom in CharacterCardUtility.CustomXenotypesForReading)
            {
                CustomXenotype chosen = custom;
                options.Add(new FloatMenuOption(chosen.name.CapitalizeFirst(), () => request(new PawnRequest(PawnRequestKind.SetXenotype, card.PawnId, 0,
                    Choice(chosen).ToText()))));
            }
            options.Add(new FloatMenuOption(L("XenotypeEditor", "Xenotyp-Editor") + "...", () => OpenXenotypeEditor(card, slot, request)));
            return options;
        }

        // Vanilla's editor, on this player's side. It leaves the result in this game's (empty) generation request for the
        // slot; the callback sends it to the host, which generates the pawn with it.
        private static void OpenXenotypeEditor(PawnCard card, int slot, Action<PawnRequest> request)
        {
            int index = Math.Max(0, slot);
            Find.WindowStack.Add(new Dialog_CreateXenotype(index, () =>
            {
                CustomXenotype? made = StartingPawnUtility.GetGenerationRequest(index).ForcedCustomXenotype;
                if (made != null) request(new PawnRequest(PawnRequestKind.SetXenotype, card.PawnId, 0, Choice(made).ToText()));
            }));
        }

        internal static PawnXenotypeChoice Choice(CustomXenotype custom) =>
            PawnXenotypeChoice.CustomXenotype(custom.name ?? "", custom.inheritable, custom.iconDef?.defName ?? "",
                (custom.genes ?? new List<GeneDef>()).Select(g => g.defName).ToList());

        // Childhood, adulthood, traits, what the pawn cannot do, and its abilities.
        private static void DrawBackgroundColumn(Rect rect, PawnCard card)
        {
            PawnCardDetails? d = card.Details;
            float y = rect.y;
            foreach (var (label, value, tip) in new[] { (L("Childhood", "Kindheit"), card.Childhood, d?.ChildhoodTip ?? ""),
                (L("Adulthood", "Erwachsen"), card.Adulthood, d?.AdulthoodTip ?? "") })
            {
                if (value.Length == 0) continue;
                Widgets.Label(new Rect(rect.x, y + 2f, 90f, 26f), label);
                Rect box = new Rect(rect.x + 95f, y, Math.Min(rect.width - 95f, Text.CalcSize(value).x + 16f), 28f);
                Widgets.DrawBoxSolid(box, BoxColor);
                Widgets.DrawHighlightIfMouseover(box);
                Widgets.Label(new Rect(box.x + 8f, box.y + 2f, box.width - 8f, 26f), value);
                if (tip.Length > 0) TooltipHandler.TipRegion(box, tip);
                y += 34f;
            }
            y += 14f;
            y = Header(rect, y, L("Traits", "Merkmale"));
            float x = rect.x;
            for (int i = 0; i < card.Traits.Count; i++)
            {
                string trait = card.Traits[i];
                float width = Math.Min(rect.width, Text.CalcSize(trait).x + 16f);
                if (x + width > rect.xMax) { x = rect.x; y += 32f; }
                Rect box = new Rect(x, y, width, 28f);
                Widgets.DrawBoxSolid(box, BoxColor);
                Widgets.DrawHighlightIfMouseover(box);
                Widgets.Label(new Rect(box.x + 8f, box.y + 2f, box.width - 8f, 26f), trait);
                if (d != null && i < d.TraitTips.Count && d.TraitTips[i].Length > 0) TooltipHandler.TipRegion(box, d.TraitTips[i]);
                x += width + 6f;
            }
            y += 46f;
            y = Header(rect, y, L("IncapableOf", "Unfähig zu"));
            GUI.color = DimColor;
            if (card.Incapable.Count == 0) { Widgets.Label(new Rect(rect.x, y, rect.width, 24f), L("None", "Nichts")); y += 22f; }
            foreach (string item in card.Incapable)
            {
                if (y > rect.yMax - 24f) break;
                Widgets.Label(new Rect(rect.x, y, rect.width, 24f), item);
                y += 22f;
            }
            GUI.color = Color.white;
            List<AbilityDef> abilities = (d?.Abilities ?? Array.Empty<string>()).Select(name => DefDatabase<AbilityDef>.GetNamedSilentFail(name))
                .Where(def => def != null).Select(def => def!).ToList();
            if (abilities.Count == 0 || y > rect.yMax - 60f) return;
            y = Header(rect, y + 12f, L("Abilities", "Fähigkeiten"));
            x = rect.x;
            foreach (AbilityDef ability in abilities)
            {
                if (x + 30f > rect.xMax) { x = rect.x; y += 32f; }
                Rect icon = new Rect(x, y, 28f, 28f);
                if (ability.uiIcon != null) GUI.DrawTexture(icon, ability.uiIcon);
                Widgets.DrawHighlightIfMouseover(icon);
                TooltipHandler.TipRegion(icon, ability.LabelCap + "\n\n" + ability.description);
                x += 32f;
            }
        }

        // Skills with passion flames and level bars, named and explained in this player's language.
        private void DrawSkills(Rect rect, PawnCard card)
        {
            barFill ??= SolidColorMaterials.NewSolidColorTexture(new Color(1f, 1f, 1f, 0.12f));
            List<SkillDef> order = DefDatabase<SkillDef>.AllDefsListForReading;
            float y = rect.y;
            for (int i = 0; i < card.Skills.Count; i++)
            {
                if (y > rect.yMax - 26f) break;
                var (hostLabel, level, passion) = card.Skills[i];
                SkillDef? def = i < order.Count ? order[i] : null;
                bool disabled = level == 255;
                Rect row = new Rect(rect.x, y, rect.width, 27f);
                GUI.color = disabled ? DimColor : Color.white;
                Widgets.Label(new Rect(rect.x, y + 2f, 120f, 24f), def?.skillLabel.CapitalizeFirst() ?? hostLabel);
                Texture2D? flame = passion == 2 ? SkillUI.PassionMajorIcon : passion == 1 ? SkillUI.PassionMinorIcon : null;
                if (flame != null && !disabled) GUI.DrawTexture(new Rect(rect.x + 122f, y + 2f, 22f, 22f), flame);
                Rect bar = new Rect(rect.x + 148f, y + 1f, rect.width - 148f, 24f);
                if (!disabled) Widgets.FillableBar(bar, Mathf.Clamp01(level / 20f), barFill, BaseContent.ClearTex, false);
                Widgets.Label(new Rect(bar.x + 6f, y + 2f, 40f, 24f), disabled ? "–" : level.ToString());
                GUI.color = Color.white;
                Widgets.DrawHighlightIfMouseover(row);
                if (def != null)
                    TooltipHandler.TipRegion(row, def.LabelCap + "\n\n" + def.description + (disabled ? "" :
                        passion == 2 ? "\n\n" + L("PassionMajor", "Begeistert") : passion == 1 ? "\n\n" + L("PassionMinor", "Interessiert") : ""));
                y += 27f;
            }
        }

        // Health by body part, relations with both opinions, starting possessions with vanilla's info button.
        private static void DrawRightColumn(Rect rect, PawnCard card)
        {
            PawnCardDetails? d = card.Details;
            float y = Header(rect, rect.y, L("Health", "Gesundheit"));
            if (d != null)
                foreach (var (part, label) in d.Health)
                {
                    if (y > rect.yMax - 24f) return;
                    GUI.color = HeaderColor;
                    Widgets.Label(new Rect(rect.x, y, rect.width * 0.4f, 24f), part);
                    GUI.color = DimColor;
                    Widgets.Label(new Rect(rect.x + rect.width * 0.4f, y, rect.width * 0.6f, 24f), label);
                    y += 24f;
                }
            else
                foreach (string label in card.Health) { GUI.color = DimColor; Widgets.Label(new Rect(rect.x, y, rect.width, 24f), label); y += 24f; }
            GUI.color = Color.white;
            if (d == null) return;
            y = Header(rect, y + 16f, L("Relations", "Beziehungen"));
            foreach (var (relation, name, opinion, theirs) in d.Relations)
            {
                if (y > rect.yMax - 24f) return;
                GUI.color = DimColor;
                string head = relation + ": ";
                float headWidth = Text.CalcSize(head).x;
                Widgets.Label(new Rect(rect.x, y, headWidth, 24f), head);
                GUI.color = Color.white;
                Widgets.Label(new Rect(rect.x + headWidth, y, rect.width - headWidth - 70f, 24f), name.Truncate(rect.width - headWidth - 70f));
                GUI.color = opinion >= 0 ? GoodColor : BadColor;
                Text.Anchor = TextAnchor.UpperRight;
                Widgets.Label(new Rect(rect.xMax - 70f, y, 36f, 24f), ((int)opinion).ToStringWithSign());
                GUI.color = (theirs >= 0 ? GoodColor : BadColor) * new Color(1f, 1f, 1f, 0.6f);
                Widgets.Label(new Rect(rect.xMax - 34f, y, 34f, 24f), "(" + ((int)theirs).ToStringWithSign() + ")");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                y += 24f;
            }
            if (d.Possessions.Count == 0) return;
            y = Header(rect, y + 16f, L("Possessions", "Besitz"));
            for (int i = 0; i < d.Possessions.Count; i++)
            {
                if (y > rect.yMax - 24f) return;
                var (label, count) = d.Possessions[i];
                Widgets.Label(new Rect(rect.x, y, rect.width - 30f, 24f), label + " x" + count);
                ThingDef? thing = i < d.PossessionDefs.Count ? DefDatabase<ThingDef>.GetNamedSilentFail(d.PossessionDefs[i]) : null;
                if (thing != null) Widgets.InfoCardButton(rect.xMax - 24f, y, thing);
                y += 24f;
            }
        }

        // Vanilla's team panel: the best level in the starting group for the skills it lists.
        private static void DrawTeamSkills(Rect rect, PawnRoster roster)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(12f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 34f), L("TeamSkills", "Team-Fertigkeiten"));
            Text.Font = GameFont.Small;
            List<SkillDef> order = DefDatabase<SkillDef>.AllDefsListForReading;
            SkillDef[] shown = { SkillDefOf.Shooting, SkillDefOf.Melee, SkillDefOf.Construction, SkillDefOf.Mining, SkillDefOf.Cooking,
                SkillDefOf.Plants, SkillDefOf.Medicine, SkillDefOf.Intellectual };
            List<PawnCard> group = roster.Cards.Where(c => c.Selected).ToList();
            float columnWidth = inner.width / 4f;
            for (int i = 0; i < shown.Length; i++)
            {
                int index = order.IndexOf(shown[i]);
                var levels = group.Where(c => index >= 0 && index < c.Skills.Count && c.Skills[index].Level != 255).Select(c => c.Skills[index]).ToList();
                var best = levels.OrderByDescending(s => s.Level).FirstOrDefault();
                float x = inner.x + (i / 2) * columnWidth, y = inner.y + 38f + (i % 2) * 28f;
                Widgets.Label(new Rect(x, y + 2f, columnWidth - 70f, 24f), shown[i].skillLabel.CapitalizeFirst());
                Texture2D? flame = best.Passion == 2 ? SkillUI.PassionMajorIcon : best.Passion == 1 ? SkillUI.PassionMinorIcon : null;
                if (flame != null && levels.Count > 0) GUI.DrawTexture(new Rect(x + columnWidth - 70f, y + 2f, 22f, 22f), flame);
                Rect box = new Rect(x + columnWidth - 44f, y, 32f, 26f);
                Widgets.DrawBoxSolid(box, BoxColor);
                Widgets.Label(new Rect(box.x + 6f, box.y + 2f, box.width - 6f, 24f), levels.Count > 0 ? best.Level.ToString() : "–");
            }
        }

        private static void NameBox(Rect rect, string text)
        {
            Widgets.DrawBoxSolid(rect, BoxColor);
            Widgets.DrawBox(rect);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 6f, rect.y, rect.width - 6f, rect.height), text);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static float Header(Rect rect, float y, string text)
        {
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(rect.x, y, rect.width, 26f), text);
            GUI.color = Color.white;
            return y + 28f;
        }

        private static string StageLabel(DevelopmentalStage stage) =>
            (stage == DevelopmentalStage.Child ? L("Child", "Kind") : stage == DevelopmentalStage.Baby ? L("Baby", "Baby") : L("Adult", "Erwachsen"))
            .CapitalizeFirst();

        private static Texture2D? XenotypeIcon(string key)
        {
            if (key.StartsWith("x:")) return DefDatabase<XenotypeDef>.GetNamedSilentFail(key.Substring(2))?.Icon;
            if (key.StartsWith("c:")) return DefDatabase<XenotypeIconDef>.GetNamedSilentFail(key.Substring(2))?.Icon;
            return null;
        }

        private static string Clip(string text) => text.Length > 24 ? text.Substring(0, 24) : text;

        private static Color FromRgba(uint rgba) =>
            new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

        // Vanilla's own word where the key exists, otherwise the mod's German.
        private static string L(string key, string fallback) => key.CanTranslate() ? key.Translate().ToString() : fallback;
    }
}
