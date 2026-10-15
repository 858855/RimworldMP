using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;
using UnityEngine;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>MOD-001: shows how the local mod list differs from the host's.</summary>
    public sealed class ModDiffWindow : Window
    {
        private const int OrderLines = 12;
        private readonly List<(string Text, bool Heading)> lines = new List<(string, bool)>();
        private Vector2 scroll;

        public ModDiffWindow(ModManifestDiff diff, string hostName)
        {
            doCloseX = true;
            doCloseButton = true;
            draggable = true;
            absorbInputAroundWindow = true;
            Build(diff, hostName);
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(780f, UI.screenWidth - 24f), Mathf.Min(660f, UI.screenHeight - 24f));

        private void Build(ModManifestDiff diff, string hostName)
        {
            lines.Add(("Mod-Unterschiede zu " + hostName, true));
            if (diff.GameVersionDiffers)
            {
                lines.Add(("Spielversion (nur Hinweis, zählt nicht zur Prüfsumme)", true));
                lines.Add(("  Du: " + diff.LocalGameVersion + "   Host: " + diff.HostGameVersion, false));
            }
            if (diff.IsMatch)
            {
                lines.Add(("Keine Mod-Unterschiede gefunden. Möglicherweise hat sich die Mod-Liste seit der Prüfung geändert.", false));
                return;
            }
            Section("Fehlt bei dir", diff.MissingLocally.Select(m => m.Label + Version(m)));
            Section("Zusätzlich bei dir", diff.ExtraLocally.Select(m => m.Label + Version(m)));
            Section("Andere Version oder anderer Inhalt", diff.Changed.Select(p =>
                p.Local.Label + ": du " + Shown(p.Local.Version) + ", Host " + Shown(p.Host.Version) +
                (p.Local.Version == p.Host.Version ? " (Dateien unterscheiden sich)" : "")));
            if (diff.FirstOrderDifference >= 0)
            {
                int start = diff.FirstOrderDifference;
                lines.Add(("Falsche Reihenfolge ab Position " + (start + 1) + " der gemeinsamen Mods", true));
                lines.Add(("  Host: " + string.Join(", ", diff.HostSharedOrder.Skip(start).Take(OrderLines)) + More(diff.HostSharedOrder.Count - start), false));
                lines.Add(("  Du:   " + string.Join(", ", diff.LocalSharedOrder.Skip(start).Take(OrderLines)) + More(diff.LocalSharedOrder.Count - start), false));
            }
            lines.Add(("So behebst du es", true));
            lines.Add(("Mods und Reihenfolge im Mod-Menü an die Liste des Hosts anpassen, RimWorld neu starten und der Lobby erneut beitreten. " +
                "Fehlende Mods findest du im Steam Workshop unter ihrem Namen.", false));
        }

        private void Section(string title, IEnumerable<string> entries)
        {
            string[] items = entries.ToArray();
            if (items.Length == 0) return;
            lines.Add((title + " (" + items.Length + ")", true));
            foreach (string item in items) lines.Add(("  • " + item, false));
        }

        private static string Version(ModEntry mod) => mod.Version.Length > 0 ? " " + mod.Version : "";
        private static string Shown(string version) => version.Length > 0 ? version : "ohne Version";
        private static string More(int remaining) => remaining > OrderLines ? ", …" : "";

        public override void DoWindowContents(Rect inRect)
        {
            Rect outer = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 50f);
            float width = outer.width - 20f;
            float height = lines.Sum(l => Text.CalcHeight(l.Text, width) + (l.Heading ? 10f : 2f));
            Widgets.BeginScrollView(outer, ref scroll, new Rect(0f, 0f, width, Mathf.Max(height, outer.height)));
            float y = 0f;
            foreach (var (text, heading) in lines)
            {
                if (heading) y += 8f;
                float lineHeight = Text.CalcHeight(text, width);
                if (heading) GUI.color = new Color(1f, 0.85f, 0.45f);
                Widgets.Label(new Rect(0f, y, width, lineHeight), text);
                GUI.color = Color.white;
                y += lineHeight + 2f;
            }
            Widgets.EndScrollView();
        }
    }
}
