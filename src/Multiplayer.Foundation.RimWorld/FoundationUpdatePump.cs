using UnityEngine;

namespace Multiplayer.Foundation.RimWorld
{
    public sealed class FoundationUpdatePump : MonoBehaviour
    {
        internal FoundationMod? Owner;
        private void Update() => Owner?.TickNetwork();
    }
}
