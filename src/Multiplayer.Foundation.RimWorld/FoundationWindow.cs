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

        public override void DoWindowContents(Rect inRect) => owner.DrawWindow(inRect);
    }
}
