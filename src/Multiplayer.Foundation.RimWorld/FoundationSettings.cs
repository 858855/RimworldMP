using System;
using Multiplayer.Foundation.Protocol;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    public sealed class FoundationSettings : ModSettings
    {
        public string ServerAddress = "127.0.0.1";
        public string Port = "28491";
        public string PlayerName = "Player";
        public string HostBindAddress = "::";
        public bool UseUpnp = true;
        // Created once and kept: identifies this player when Steam is unavailable (ADR 0016).
        public string LocalPlayerKey = "";

        /// <summary>Creates the local key if missing. Returns true when the settings changed and must be written.</summary>
        internal bool EnsureLocalPlayerKey()
        {
            if (PlayerKey.TryParse(LocalPlayerKey, out PlayerKey existing) && existing.Kind == PlayerKeyKind.Local) return false;
            LocalPlayerKey = PlayerKey.Local(Guid.NewGuid()).ToString();
            return true;
        }

        internal PlayerKey LocalKey =>
            PlayerKey.TryParse(LocalPlayerKey, out PlayerKey key) ? key : throw new InvalidOperationException("Local player key missing.");

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ServerAddress, "serverAddress", "127.0.0.1");
            Scribe_Values.Look(ref Port, "port", "28491");
            Scribe_Values.Look(ref PlayerName, "playerName", "Player");
            // New key: the old "hostBindAddress" defaulted to 127.0.0.1 (this PC only) and must not carry over.
            Scribe_Values.Look(ref HostBindAddress, "hostBindAddressV2", "::");
            Scribe_Values.Look(ref UseUpnp, "useUpnp", true);
            Scribe_Values.Look(ref LocalPlayerKey, "localPlayerKey", "");
        }
    }
}
