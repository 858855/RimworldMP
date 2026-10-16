using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Server;
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
        // Debug: host may start the shared setup alone. Only read when hosting starts.
        public bool AllowSoloStart;
        // Created once and kept: identifies this player when Steam is unavailable (ADR 0016).
        public string LocalPlayerKey = "";
        // Private identity never goes over the network or into a shared game save.
        public string LocalIdentity = "";
        public List<string> PinnedPlayerKeys = new List<string>();
        public List<string> PinnedFingerprints = new List<string>();

        /// <summary>Creates the local key if missing. Returns true when the settings changed and must be written.</summary>
        internal bool EnsureLocalPlayerKey()
        {
            if (PlayerKey.TryParse(LocalPlayerKey, out PlayerKey existing) && existing.Kind == PlayerKeyKind.Local) return false;
            LocalPlayerKey = PlayerKey.Local(Guid.NewGuid()).ToString();
            return true;
        }

        internal PlayerKey LocalKey =>
            PlayerKey.TryParse(LocalPlayerKey, out PlayerKey key) ? key : throw new InvalidOperationException("Local player key missing.");

        internal PlayerIdentity GetOrCreateIdentity(out bool created)
        {
            created = LocalIdentity.Length == 0;
            if (created)
                LocalIdentity = Convert.ToBase64String(PlayerIdentity.Create().ToBytes());
            try { return PlayerIdentity.FromBytes(Convert.FromBase64String(LocalIdentity)); }
            catch (Exception error)
            { throw new InvalidOperationException("Gespeicherte IP-Identität ist beschädigt; sie wird nicht ersetzt.", error); }
        }

        internal PlayerKeyPinStore LoadPins()
        {
            if (PinnedPlayerKeys.Count != PinnedFingerprints.Count)
                throw new InvalidOperationException("Gespeicherte Schlüsselbindungen sind beschädigt.");
            var pins = new PlayerKeyPinStore();
            pins.Import(PinnedPlayerKeys.Zip(PinnedFingerprints, (key, fingerprint) => (key, fingerprint)));
            return pins;
        }

        internal bool UpdatePins(PlayerKeyPinStore pins)
        {
            var exported = pins.Export();
            if (PinnedPlayerKeys.SequenceEqual(exported.Select(entry => entry.PlayerKey)) &&
                PinnedFingerprints.SequenceEqual(exported.Select(entry => entry.Fingerprint))) return false;
            PinnedPlayerKeys = exported.Select(entry => entry.PlayerKey).ToList();
            PinnedFingerprints = exported.Select(entry => entry.Fingerprint).ToList();
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ServerAddress, "serverAddress", "127.0.0.1");
            Scribe_Values.Look(ref Port, "port", "28491");
            Scribe_Values.Look(ref PlayerName, "playerName", "Player");
            // New key: the old "hostBindAddress" defaulted to 127.0.0.1 (this PC only) and must not carry over.
            Scribe_Values.Look(ref HostBindAddress, "hostBindAddressV2", "::");
            Scribe_Values.Look(ref UseUpnp, "useUpnp", true);
            Scribe_Values.Look(ref AllowSoloStart, "allowSoloStart", false);
            Scribe_Values.Look(ref LocalPlayerKey, "localPlayerKey", "");
            Scribe_Values.Look(ref LocalIdentity, "localIdentity", "");
            Scribe_Collections.Look(ref PinnedPlayerKeys, "pinnedPlayerKeys", LookMode.Value);
            Scribe_Collections.Look(ref PinnedFingerprints, "pinnedFingerprints", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                LocalIdentity ??= "";
                PinnedPlayerKeys ??= new List<string>();
                PinnedFingerprints ??= new List<string>();
            }
        }
    }
}
