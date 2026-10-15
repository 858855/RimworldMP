using System;
using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-003 (ADR 0015): site suggestions on the world map. Every player may set one flag; only the host
    /// chooses. Flags have no voting weight and are not saved.
    /// </summary>
    internal static class SiteFlags
    {
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.35f, 0.35f), new Color(0.35f, 0.75f, 0.95f), new Color(0.45f, 0.9f, 0.45f),
            new Color(0.95f, 0.8f, 0.3f), new Color(0.8f, 0.5f, 0.95f), new Color(0.95f, 0.6f, 0.3f)
        };

        /// <summary>Flags to draw this frame: set from the lobby state (and by the self-test).</summary>
        internal static IReadOnlyList<(string Name, PlanetTile Tile, Color Color)> Shown = Array.Empty<(string, PlanetTile, Color)>();
        /// <summary>Frames in which at least one flag was drawn; read by the self-test.</summary>
        internal static int FramesDrawn;

        internal static Color ColorFor(int playerIndex) => Palette[Math.Abs(playerIndex) % Palette.Length];
        internal static PlanetTile ToPlanetTile(SiteTile tile) => new PlanetTile(tile.TileId, tile.LayerId);
        internal static SiteTile ToSiteTile(PlanetTile tile) => new SiteTile(tile.tileId, tile.Layer.LayerID);

        internal static string Describe(PlanetTile tile)
        {
            if (!tile.Valid || Find.World == null) return "–";
            return Find.WorldGrid[tile].PrimaryBiome?.LabelCap.ToString() + " (Feld " + tile.tileId + ")";
        }

        internal static void JumpTo(PlanetTile tile)
        {
            if (!tile.Valid || Find.World == null) return;
            Find.WorldSelector.SelectedTile = tile;
            Find.WorldCameraDriver.JumpTo(tile);
        }

        /// <summary>Draws a small flag with the player's name above each flagged tile that faces the camera.</summary>
        internal static void Draw()
        {
            if (Shown.Count == 0 || Find.World == null) return;
            TextAnchor anchor = Text.Anchor; GameFont font = Text.Font;
            Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.MiddleCenter;
            bool drewAny = false;
            try
            {
                foreach (var (name, tile, color) in Shown)
                {
                    if (!tile.Valid) continue;
                    Vector3 position = Find.WorldGrid.GetTileCenter(tile);
                    if (WorldRendererUtility.HiddenBehindTerrainNow(position)) continue;
                    Vector2 ui = GenWorldUI.WorldToUIPosition(position);
                    Widgets.DrawBoxSolid(new Rect(ui.x - 1f, ui.y - 20f, 2f, 20f), Color.white);
                    Widgets.DrawBoxSolid(new Rect(ui.x + 1f, ui.y - 20f, 13f, 9f), color);
                    float width = 10f + name.Length * 7f;
                    var label = new Rect(ui.x - width / 2f, ui.y - 38f, width, 16f);
                    Widgets.DrawBoxSolid(label, new Color(0f, 0f, 0f, 0.65f));
                    GUI.color = color;
                    Widgets.Label(label, name);
                    GUI.color = Color.white;
                    drewAny = true;
                }
            }
            finally { Text.Anchor = anchor; Text.Font = font; if (drewAny) FramesDrawn++; }
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), nameof(Page_SelectStartingSite.ExtraOnGUI))]
    internal static class SiteFlagsDrawPatch
    {
        private static void Postfix() => SiteFlags.Draw();
    }

    /// <summary>
    /// A guest's starting-site page only suggests: "Next" (and Enter) sets the guest's flag instead of continuing,
    /// so a guest never walks on into the single-player setup. Untouched outside a guest's shared setup.
    /// </summary>
    [HarmonyPatch(typeof(Page_SelectStartingSite), "DoNext")]
    internal static class GuestSiteNextPatch
    {
        private static bool Prefix()
        {
            if (FoundationMod.Instance?.GuestSiteMapActive != true) return true;
            FoundationMod.Instance.PlaceFlagAtSelection();
            return false;
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), nameof(Page_SelectStartingSite.OnAcceptKeyPressed))]
    internal static class GuestSiteAcceptPatch
    {
        private static bool Prefix()
        {
            if (FoundationMod.Instance?.GuestSiteMapActive != true) return true;
            FoundationMod.Instance.PlaceFlagAtSelection();
            return false;
        }
    }

    /// <summary>"Back" on a guest's map only closes the map; the guest stays in the shared setup.</summary>
    [HarmonyPatch(typeof(Page), "DoBack")]
    internal static class GuestSiteBackPatch
    {
        private static bool Prefix(Page __instance)
        {
            if (!(__instance is Page_SelectStartingSite) || FoundationMod.Instance?.GuestSiteMapActive != true) return true;
            FoundationMod.Instance.CloseGuestSiteMap(true);
            return false;
        }
    }
}
