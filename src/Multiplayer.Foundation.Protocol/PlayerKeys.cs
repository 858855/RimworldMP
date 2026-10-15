using System;
using System.Globalization;

namespace Multiplayer.Foundation.Protocol
{
    /// <summary>Persistent player identity sent in Hello: <c>steam:&lt;SteamID64&gt;</c> or <c>local:&lt;32 hex&gt;</c>.</summary>
    public static class PlayerKeys
    {
        public const int MaximumBytes = 64;
        private const string SteamPrefix = "steam:";
        private const string LocalPrefix = "local:";

        public static string Steam(ulong steamId)
        {
            if (steamId == 0) throw new ArgumentOutOfRangeException(nameof(steamId));
            return SteamPrefix + steamId.ToString(CultureInfo.InvariantCulture);
        }

        public static string Local(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(id));
            return LocalPrefix + id.ToString("N");
        }

        public static bool IsValid(string? key)
        {
            if (key == null) return false;
            if (key.StartsWith(SteamPrefix, StringComparison.Ordinal))
            {
                string digits = key.Substring(SteamPrefix.Length);
                return digits.Length > 0 && digits.Length <= 20 && AllChars(digits, false) &&
                    ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) &&
                    value != 0 && digits[0] != '0';
            }
            if (key.StartsWith(LocalPrefix, StringComparison.Ordinal))
            {
                string hex = key.Substring(LocalPrefix.Length);
                return hex.Length == 32 && AllChars(hex, true) && hex != new string('0', 32);
            }
            return false;
        }

        private static bool AllChars(string value, bool hex)
        {
            foreach (char c in value)
                if (!(c >= '0' && c <= '9') && !(hex && c >= 'a' && c <= 'f')) return false;
            return true;
        }
    }
}
