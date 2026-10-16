using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-004: a guest's character selection, the size of vanilla's setup pages (see GuestCharacterPage); the host
    /// uses the vanilla page plus its panel.
    /// </summary>
    public sealed class CharacterSelectionWindow : Window
    {
        private readonly FoundationMod owner;

        public CharacterSelectionWindow(FoundationMod owner)
        {
            this.owner = owner;
            doCloseX = true;
            draggable = true;
            onlyOneOfTypeAllowed = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize =>
            new Vector2(Mathf.Min(Page.StandardSize.x, UI.screenWidth - 24f), Mathf.Min(Page.StandardSize.y, UI.screenHeight - 24f));

        public override void DoWindowContents(Rect inRect) => owner.DrawCharacterSelection(inRect);
    }
}
