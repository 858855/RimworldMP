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
        // Persistent identity when Steam is unavailable; generated once (ID-001).
        public string LocalPlayerKey = "";

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
