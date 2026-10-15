using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    public sealed class FoundationSettings : ModSettings
    {
        public string ServerAddress = "127.0.0.1";
        public string Port = "28491";
        public string PlayerName = "Player";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ServerAddress, "serverAddress", "127.0.0.1");
            Scribe_Values.Look(ref Port, "port", "28491");
            Scribe_Values.Look(ref PlayerName, "playerName", "Player");
        }
    }
}
