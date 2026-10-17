using System;
using Multiplayer.Foundation.Protocol;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SYNC-005 (plan §7.7, ADR 0026): who may give a pawn orders. Evaluated identically on every client inside command
    /// execution, from the saved ownership table (<see cref="MultiplayerSession"/>) and the issuer the coordinator stamped
    /// on the command; the same rule runs locally before a command is sent, for immediate feedback. Shared (unowned)
    /// pawns belong to everyone; commands the coordinator issues itself (no key) always pass.
    /// </summary>
    internal static class PawnControl
    {
        /// <summary>Null when the issuer may control the pawn, else the reason shown as "Keine Berechtigung: …".</summary>
        internal static string? Denial(Pawn pawn, PlayerKey? issuer, MultiplayerSession? session)
        {
            if (issuer == null || session == null || !session.Active) return null;
            if (session.Mode?.Mode == GameMode.Factions)
            {
                // Factions mode: ownership is by faction (ADR 0045/0046), not by pawn.
                int? faction = session.FactionIdFor(issuer.Value);
                if (pawn.Faction == null || faction == null || pawn.Faction.loadID != faction.Value)
                    return pawn.LabelShortCap + " gehört nicht deiner Fraktion.";
                return null;
            }
            PlayerKey? owner = session.OwnerKeyOf(pawn.thingIDNumber);
            if (owner == null || owner.Value == issuer.Value) return null;
            return pawn.LabelShortCap + " gehört " + (session.PlayerNameOf(owner.Value) ?? owner.Value.ToString()) + ".";
        }
    }
}
