using System;
using System.Globalization;
using System.Linq;
using System.Net;

namespace Multiplayer.Foundation.Network
{
    /// <summary>Parses user input such as <c>host</c>, <c>host:port</c>, <c>[2001:db8::1]:port</c> or a bare IPv6 address.</summary>
    public static class HostEndpoint
    {
        public static bool TryParse(string? input, int defaultPort, out string host, out int port)
        {
            host = string.Empty;
            port = defaultPort;
            string text = (input ?? string.Empty).Trim();
            if (text.Length == 0 || text.Any(char.IsWhiteSpace) || text.Any(char.IsControl)) return false;
            if (text.StartsWith("[", StringComparison.Ordinal))
            {
                int close = text.IndexOf(']');
                if (close < 2) return false;
                host = text.Substring(1, close - 1);
                if (!IPAddress.TryParse(host, out IPAddress? literal) ||
                    literal.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return false;
                string rest = text.Substring(close + 1);
                if (rest.Length == 0) return ValidPort(port);
                return rest[0] == ':' && TryParsePort(rest.Substring(1), out port);
            }
            int colon = text.IndexOf(':');
            if (colon >= 0 && colon == text.LastIndexOf(':'))
            {
                host = text.Substring(0, colon);
                return host.Length > 0 && TryParsePort(text.Substring(colon + 1), out port);
            }
            // Several colons without brackets: only valid as a bare IPv6 address.
            if (colon >= 0 && !IPAddress.TryParse(text, out _)) return false;
            host = text;
            return ValidPort(port);
        }

        private static bool TryParsePort(string text, out int port) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && ValidPort(port);

        private static bool ValidPort(int port) => port >= 1 && port <= 65535;
    }
}
