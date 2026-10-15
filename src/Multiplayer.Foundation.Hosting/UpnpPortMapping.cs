using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Xml;

namespace Multiplayer.Foundation.Hosting;

public sealed class PortMappingException : Exception
{
    public PortMappingException(string message, Exception? inner = null, IPAddress? externalAddress = null)
        : base(message, inner) => ExternalAddress = externalAddress;

    /// <summary>The router's WAN address, if it could still be read although the mapping failed.</summary>
    public IPAddress? ExternalAddress { get; }
}

/// <summary>
/// Opens a TCP port on the home router via UPnP IGD (WANIPConnection/WANPPPConnection), so guests can reach a
/// listen host from the internet without manual port forwarding. Only IPv4 needs this; see <see cref="NetworkAddresses"/>.
/// </summary>
public sealed class UpnpPortMapping : IAsyncDisposable
{
    private const string Description = "RimWorld Multiplayer Foundation";
    private static readonly IPEndPoint SsdpTarget = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
    private static readonly string[] SearchTargets =
    {
        "urn:schemas-upnp-org:device:InternetGatewayDevice:1",
        "urn:schemas-upnp-org:device:InternetGatewayDevice:2",
        "urn:schemas-upnp-org:service:WANIPConnection:1",
        "urn:schemas-upnp-org:service:WANPPPConnection:1",
    };

    private readonly HttpClient http;
    private readonly Uri controlUrl;
    private readonly string serviceType;
    private int removed;

    private UpnpPortMapping(HttpClient http, Uri controlUrl, string serviceType, int port, IPAddress internalAddress)
    {
        this.http = http;
        this.controlUrl = controlUrl;
        this.serviceType = serviceType;
        Port = port;
        InternalAddress = internalAddress;
    }

    public int Port { get; }
    public IPAddress InternalAddress { get; }
    public IPAddress? ExternalAddress { get; private set; }
    /// <summary>False if the router itself sits behind carrier-grade NAT (DS-Lite, mobile, some cable ISPs).</summary>
    public bool ExternalAddressIsPublic => ExternalAddress != null && NetworkAddresses.IsPublic(ExternalAddress);

    /// <summary>Discovers the router and maps external TCP <paramref name="port"/> to this machine.</summary>
    /// <param name="gatewayDescription">Skip SSDP discovery and use this device description URL (tests, manual setup).</param>
    public static async Task<UpnpPortMapping> CreateAsync(int port, TimeSpan timeout, Uri? gatewayDescription = null,
        CancellationToken cancellationToken = default)
    {
        if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        IReadOnlyList<Uri> locations;
        try
        {
            locations = gatewayDescription != null
                ? new[] { gatewayDescription }
                : await DiscoverAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException error)
        { throw new PortMappingException("UPnP discovery failed: " + error.Message, error); }
        if (locations.Count == 0)
            throw new PortMappingException("No UPnP router found. Enable UPnP on the router or forward the port manually.");
        Exception? lastError = null;
        IPAddress? knownExternal = null;
        foreach (Uri location in locations)
        {
            var http = new HttpClient { Timeout = timeout };
            try
            {
                (Uri control, string service) = await ReadDescriptionAsync(http, location, cancellationToken)
                    .ConfigureAwait(false);
                IPAddress local = await LocalAddressTowardsAsync(control.Host).ConfigureAwait(false);
                var mapping = new UpnpPortMapping(http, control, service, port, local);
                // Read the WAN address first: it is needed to tell guests where to connect even if the router
                // refuses the mapping and the port has to be forwarded by hand.
                try
                {
                    XmlDocument reply = await mapping.InvokeAsync("GetExternalIPAddress", cancellationToken)
                        .ConfigureAwait(false);
                    if (IPAddress.TryParse(FindText(reply.DocumentElement, "NewExternalIPAddress"), out IPAddress? external))
                        knownExternal = mapping.ExternalAddress = external;
                }
                catch (PortMappingException) { }
                await mapping.InvokeAsync("AddPortMapping", cancellationToken,
                    ("NewRemoteHost", ""), ("NewExternalPort", Number(port)), ("NewProtocol", "TCP"),
                    ("NewInternalPort", Number(port)), ("NewInternalClient", local.ToString()), ("NewEnabled", "1"),
                    ("NewPortMappingDescription", Description), ("NewLeaseDuration", "0")).ConfigureAwait(false);
                return mapping;
            }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested)
            {
                // Router firmware and runtimes fail in many ways (HTTP, XML, encodings, sockets); try the next device.
                http.Dispose();
                lastError = error;
            }
            catch
            {
                http.Dispose();
                throw;
            }
        }
        string message = lastError is PortMappingException ? lastError.Message
            : lastError is TaskCanceledException ? "UPnP router did not answer in time."
            : "UPnP router did not accept the port mapping: " + lastError?.Message;
        throw new PortMappingException(message, lastError, knownExternal);
    }

    public async Task RemoveAsync()
    {
        if (Interlocked.Exchange(ref removed, 1) != 0) return;
        try
        {
            await InvokeAsync("DeletePortMapping", CancellationToken.None,
                ("NewRemoteHost", ""), ("NewExternalPort", Number(Port)), ("NewProtocol", "TCP")).ConfigureAwait(false);
        }
        catch (Exception error) when (error is PortMappingException or HttpRequestException or XmlException
            or SocketException or TaskCanceledException) { }
        finally { http.Dispose(); }
    }

    public ValueTask DisposeAsync() => new ValueTask(RemoveAsync());

    internal static async Task<IReadOnlyList<Uri>> DiscoverAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var found = new List<Uri>();
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        // Multicast alone is not enough: e.g. a FRITZ!Box answered only a search sent directly to the gateway,
        // because the multicast left via another adapter or its reply was dropped.
        IPEndPoint[] gateways = NetworkAddresses.GetDefaultGateways().Select(g => new IPEndPoint(g, 1900)).ToArray();
        foreach (string target in SearchTargets)
        {
            byte[] request = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\n" +
                "MAN: \"ssdp:discover\"\r\nMX: 2\r\nST: " + target + "\r\n\r\n");
            await udp.SendAsync(request, request.Length, SsdpTarget).ConfigureAwait(false);
            foreach (IPEndPoint gateway in gateways)
            {
                try { await udp.SendAsync(request, request.Length, gateway).ConfigureAwait(false); }
                catch (SocketException) { }
            }
        }
        DateTime deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            Task<UdpReceiveResult> receive = udp.ReceiveAsync();
            if (await Task.WhenAny(receive, Task.Delay(remaining, cancellationToken)).ConfigureAwait(false) != receive)
            {
                _ = receive.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                break;
            }
            Uri? location = ParseSsdpLocation(Encoding.ASCII.GetString((await receive.ConfigureAwait(false)).Buffer));
            if (location != null && !found.Contains(location)) found.Add(location);
            // The first answers arrive within milliseconds; give other gateways a short moment, then stop.
            if (found.Count > 0 && deadline - DateTime.UtcNow > TimeSpan.FromMilliseconds(500))
                deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return found;
    }

    internal static Uri? ParseSsdpLocation(string response)
    {
        string[] lines = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length == 0 || !lines[0].StartsWith("HTTP/1.", StringComparison.OrdinalIgnoreCase) ||
            !lines[0].Contains(" 200")) return null;
        foreach (string line in lines.Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0 || !line.Substring(0, colon).Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.TryCreate(line.Substring(colon + 1).Trim(), UriKind.Absolute, out Uri? uri) &&
                uri.Scheme == Uri.UriSchemeHttp ? uri : null;
        }
        return null;
    }

    private static async Task<(Uri Control, string Service)> ReadDescriptionAsync(HttpClient http, Uri location,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(location, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new PortMappingException("Router description returned HTTP " + (int)response.StatusCode + ".");
        XmlDocument document = await ReadXmlAsync(response).ConfigureAwait(false);
        Uri baseUri = Uri.TryCreate(FindText(document.DocumentElement, "URLBase"), UriKind.Absolute, out Uri? urlBase)
            ? urlBase : location;
        foreach (string wanted in new[] { "WANIPConnection", "WANPPPConnection" })
        {
            foreach (XmlElement service in Elements(document.DocumentElement, "service"))
            {
                string type = FindText(service, "serviceType") ?? "";
                string? control = FindText(service, "controlURL");
                if (type.IndexOf(":" + wanted + ":", StringComparison.Ordinal) >= 0 && !string.IsNullOrEmpty(control))
                    return (new Uri(baseUri, control), type);
            }
        }
        throw new PortMappingException("UPnP device is not an internet gateway.");
    }

    private async Task<XmlDocument> InvokeAsync(string action, CancellationToken cancellationToken,
        params (string Name, string Value)[] arguments)
    {
        var body = new StringBuilder();
        body.Append("<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" ")
            .Append("s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>")
            .Append("<u:").Append(action).Append(" xmlns:u=\"").Append(SecurityElement.Escape(serviceType)).Append("\">");
        foreach ((string name, string value) in arguments)
            body.Append('<').Append(name).Append('>').Append(SecurityElement.Escape(value)).Append("</").Append(name).Append('>');
        body.Append("</u:").Append(action).Append("></s:Body></s:Envelope>");
        using var request = new HttpRequestMessage(HttpMethod.Post, controlUrl)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body.ToString())),
        };
        request.Content.Headers.TryAddWithoutValidation("Content-Type", "text/xml; charset=\"utf-8\"");
        request.Headers.ExpectContinue = false;
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"" + serviceType + "#" + action + "\"");
        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string detail = "HTTP " + (int)response.StatusCode;
            try
            {
                XmlDocument fault = await ReadXmlAsync(response).ConfigureAwait(false);
                detail = "UPnP error " + FindText(fault.DocumentElement, "errorCode") + " " +
                    FindText(fault.DocumentElement, "errorDescription");
            }
            catch (XmlException) { }
            throw new PortMappingException("Router rejected " + action + ": " + detail.Trim() + ".");
        }
        return await ReadXmlAsync(response).ConfigureAwait(false);
    }

    // Not ReadAsStringAsync: RimWorld's Mono cannot decode a quoted charset (FRITZ!Box sends charset="utf-8")
    // and throws ArgumentException. XmlDocument detects the encoding from the bytes and XML declaration itself.
    private static async Task<XmlDocument> ReadXmlAsync(HttpResponseMessage response)
    {
        byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var document = new XmlDocument { XmlResolver = null };
        using var stream = new MemoryStream(bytes);
        document.Load(stream);
        return document;
    }

    private static async Task<IPAddress> LocalAddressTowardsAsync(string gatewayHost)
    {
        IPAddress gateway = IPAddress.TryParse(gatewayHost, out IPAddress? literal) ? literal
            : (await Dns.GetHostAddressesAsync(gatewayHost).ConfigureAwait(false))
                .First(a => a.AddressFamily == AddressFamily.InterNetwork);
        // Connecting a UDP socket sends nothing; it only asks the OS which local address routes to the gateway.
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        probe.Connect(gateway, 1900);
        return ((IPEndPoint)probe.LocalEndPoint!).Address;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<XmlElement> Elements(XmlNode? root, string localName) =>
        root == null ? Enumerable.Empty<XmlElement>()
            : root.SelectNodes("//*")!.OfType<XmlElement>().Where(e => e.LocalName == localName);

    private static string? FindText(XmlNode? root, string localName) =>
        root == null ? null
            : (root.LocalName == localName ? root : root.SelectNodes(".//*")!.OfType<XmlElement>()
                .FirstOrDefault(e => e.LocalName == localName))?.InnerText.Trim();
}
