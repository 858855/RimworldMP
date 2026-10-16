using UnityEngine;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>SETUP-001: small panel above the host's vanilla setup pages with phase, chat and abort.</summary>
    public sealed class SetupOverlayWindow : Window
    {
        private readonly FoundationMod owner;

        public SetupOverlayWindow(FoundationMod owner)
        {
            this.owner = owner;
            layer = WindowLayer.Super;
            doCloseX = false;
            closeOnCancel = false;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            focusWhenOpened = false;
            draggable = true;
            onlyOneOfTypeAllowed = true;
        }

        // Tall enough for the flag list during site selection (SETUP-003) and the lockstep lines in the game (SYNC-001).
        public override Vector2 InitialSize => new Vector2(340f, 330f);

        public override void PostOpen()
        {
            base.PostOpen();
            windowRect = new Rect(UI.screenWidth - InitialSize.x - 12f, 12f, InitialSize.x, InitialSize.y);
        }

        public override void DoWindowContents(Rect inRect) => owner.DrawSetupOverlay(inRect);
    }
}
