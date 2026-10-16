using System;
using System.Collections.Generic;
using System.Text;
using Multiplayer.Foundation.Protocol;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Game-side checks for ADR 0038; the coordinator only checks duplicate tile identities.</summary>
    internal static class FactionSiteChoice
    {
        // A small but explicit exclusion radius. ApproxDistanceInTiles is a checked 1.6 WorldGrid API.
        internal const float MinimumDistanceInTiles = 5f;

        internal static string? InvalidReason(SiteTile? candidate, IEnumerable<SiteTile> otherSites)
        {
            if (!candidate.HasValue) return "Startort fehlt.";
            if (Find.World == null) return "Die gemeinsame Welt ist noch nicht geladen.";
            try
            {
                PlanetTile tile = SiteFlags.ToPlanetTile(candidate.Value);
                if (!tile.Valid) return "Startort liegt außerhalb der Welt.";
                var reason = new StringBuilder();
                if (!TileFinder.IsValidTileForNewSettlement(tile, reason, false))
                    return "Hier kann keine Kolonie starten: " + reason;
                foreach (SiteTile other in otherSites)
                {
                    PlanetTile neighbor = SiteFlags.ToPlanetTile(other);
                    if (!neighbor.Valid) return "Ein anderer Fraktionsort ist ungültig.";
                    if (tile.Layer.LayerID != neighbor.Layer.LayerID)
                        return "Fraktionsorte müssen auf derselben Welt-Ebene liegen.";
                    if (Find.WorldGrid.ApproxDistanceInTiles(tile, neighbor) < MinimumDistanceInTiles)
                        return "Zu nah an einem anderen Fraktionsort (mindestens 5 Felder).";
                }
                return null;
            }
            catch (Exception error) when (error is ArgumentException || error is IndexOutOfRangeException || error is NullReferenceException)
            { return "Startort kann in dieser Welt nicht geprüft werden."; }
        }

    }
}
