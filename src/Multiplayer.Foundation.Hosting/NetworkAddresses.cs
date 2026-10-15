using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Multiplayer.Foundation.Hosting;

/// <summary>Addresses a host can hand out to guests.</summary>
public static class NetworkAddresses
{
    /// <summary>True for addresses routable on the internet (not private, CGNAT, loopback, link-local or documentation).</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        byte[] b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 ||
                (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||
                (b[0] == 169 && b[1] == 254) ||
                (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                (b[0] == 192 && b[1] == 168) ||
                (b[0] == 192 && b[1] == 0 && b[2] == 2) ||
                (b[0] == 198 && b[1] == 51 && b[2] == 100) ||
                (b[0] == 203 && b[1] == 0 && b[2] == 113));
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (b[0] & 0xE0) == 0x20 && !(b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8);
        return false;
    }

    /// <summary>LAN IPv4 addresses (for players on the same network) and global IPv6 addresses (reachable from the
    /// internet when the router's IPv6 firewall allows the port). Empty if the platform cannot enumerate interfaces.</summary>
    public static IReadOnlyList<IPAddress> GetShareableAddresses()
    {
        var result = new List<(IPAddress Address, int Rank, long Lifetime)>();
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                IPInterfaceProperties properties = adapter.GetIPProperties();
                bool routed = properties.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                foreach (UnicastIPAddressInformation info in properties.UnicastAddresses)
                {
                    IPAddress address = info.Address;
                    bool v4 = address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address) &&
                        !(address.GetAddressBytes()[0] == 169 && address.GetAddressBytes()[1] == 254);
                    bool global = address.AddressFamily == AddressFamily.InterNetworkV6 && IsPublic(address);
                    if (!v4 && !global) continue;
                    // Routed LAN adapter first, then VPN overlays (Tailscale etc.), global IPv6, other adapters (VMs).
                    int rank = global ? 2 : routed ? 0 : IsSharedAddressSpace(address) ? 1 : 3;
                    long lifetime = long.MaxValue;
                    try { lifetime = info.AddressPreferredLifetime; } catch (NotImplementedException) { }
                    catch (PlatformNotSupportedException) { }
                    if (!result.Any(r => r.Address.Equals(address))) result.Add((address, rank, lifetime));
                }
            }
        }
        catch (NetworkInformationException) { }
        catch (NotImplementedException) { }
        catch (PlatformNotSupportedException) { }
        // Adapters without a gateway (VMware/VirtualBox host-only, Hyper-V) are only listed if there is no routed LAN.
        if (result.Any(r => r.Rank == 0)) result.RemoveAll(r => r.Rank == 3);
        // Among IPv6 prefer the longest-lived (stable rather than temporary privacy) address.
        return result.OrderBy(r => r.Rank).ThenByDescending(r => r.Lifetime).Select(r => r.Address).ToList();
    }

    /// <summary>100.64.0.0/10: carrier-grade NAT, also used by VPN overlays such as Tailscale.</summary>
    public static bool IsSharedAddressSpace(IPAddress address)
    {
        byte[] b = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork && b[0] == 100 && b[1] >= 64 && b[1] <= 127;
    }

    /// <summary>IPv4 default gateways of active adapters (usually the home router).</summary>
    public static IReadOnlyList<IPAddress> GetDefaultGateways()
    {
        var result = new List<IPAddress>();
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                foreach (GatewayIPAddressInformation gateway in adapter.GetIPProperties().GatewayAddresses)
                {
                    IPAddress address = gateway.Address;
                    if (address.AddressFamily == AddressFamily.InterNetwork && !address.Equals(IPAddress.Any) &&
                        !result.Contains(address)) result.Add(address);
                }
            }
        }
        catch (NetworkInformationException) { }
        catch (NotImplementedException) { }
        catch (PlatformNotSupportedException) { }
        return result;
    }

    /// <summary>Formats an address and port the way <c>HostEndpoint.TryParse</c> reads it back.</summary>
    public static string Format(IPAddress address, int port) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + address + "]:" + port : address + ":" + port;
}
