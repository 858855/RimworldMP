using UnityEngine;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    public sealed class FoundationWindow : Window
    {
        private readonly FoundationMod owner;

        public FoundationWindow(FoundationMod owner)
        {
            this.owner = owner;
            doCloseX = true;
            draggable = true;
            onlyOneOfTypeAllowed = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(900f, UI.screenWidth - 24f), Mathf.Min(690f, UI.screenHeight - 24f));

        public override void DoWindowContents(Rect inRect)
        {
            // In the main menu RimWorld drops the current game once no window is open (self-test, 2026-10-15). A guest
            // who still needs the received world keeps this window, so the world survives until the site is chosen.
            bool keep = owner.KeepWindowOpen;
            doCloseX = !keep;
            closeOnCancel = !keep;
            owner.DrawWindow(inRect);
        }
    }
}
