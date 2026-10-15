using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>Minimal UPnP internet gateway (HTTP description + SOAP control) on loopback.</summary>
sealed class FakeGateway : IAsyncDisposable
{
    private const string Service = "urn:schemas-upnp-org:service:WANIPConnection:1";
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;

    private FakeGateway(List<string> calls, string externalIp, bool failAdd)
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        DescriptionUrl = new Uri("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/igd.xml");
        loop = Task.Run(() => ServeAsync(calls, externalIp, failAdd));
    }

    public Uri DescriptionUrl { get; }

    public static FakeGateway Start(List<string> calls, string externalIp, bool failAdd) =>
        new FakeGateway(calls, externalIp, failAdd);

    private async Task ServeAsync(List<string> calls, string externalIp, bool failAdd)
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(stop.Token); }
            catch (Exception) { break; }
            using (client)
            {
                NetworkStream stream = client.GetStream();
                (string head, string body) = await ReadRequestAsync(stream);
                string soapAction = head.Split("\r\n").FirstOrDefault(l => l.StartsWith("SOAPAction:",
                    StringComparison.OrdinalIgnoreCase))?.Substring(11).Trim() ?? "";
                string response;
                if (head.StartsWith("GET /igd.xml ", StringComparison.Ordinal)) response = Http(200, DescriptionXml);
                else if (!head.StartsWith("POST /ctl ", StringComparison.Ordinal)) response = Http(404, "");
                else
                {
                    lock (calls) calls.Add(soapAction + "\n" + body);
                    string action = soapAction.Trim('"').Split('#').Last();
                    if (action == "AddPortMapping" && failAdd)
                        response = Http(500, Envelope("<s:Fault><faultcode>s:Client</faultcode><faultstring>UPnPError</faultstring>" +
                            "<detail><UPnPError xmlns=\"urn:schemas-upnp-org:control-1-0\"><errorCode>718</errorCode>" +
                            "<errorDescription>ConflictInMappingEntry</errorDescription></UPnPError></detail></s:Fault>"));
                    else if (action == "GetExternalIPAddress")
                        response = Http(200, Envelope("<u:GetExternalIPAddressResponse xmlns:u=\"" + Service +
                            "\"><NewExternalIPAddress>" + externalIp + "</NewExternalIPAddress></u:GetExternalIPAddressResponse>"));
                    else response = Http(200, Envelope("<u:" + action + "Response xmlns:u=\"" + Service + "\"/>"));
                }
                byte[] bytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(bytes);
            }
        }
    }

    private static async Task<(string Head, string Body)> ReadRequestAsync(NetworkStream stream)
    {
        var buffer = new List<byte>();
        var one = new byte[1];
        while (!(buffer.Count >= 4 && buffer[^4] == '\r' && buffer[^3] == '\n' && buffer[^2] == '\r' && buffer[^1] == '\n'))
        {
            if (await stream.ReadAsync(one) == 0) break;
            buffer.Add(one[0]);
        }
        string head = Encoding.ASCII.GetString(buffer.ToArray());
        string? lengthLine = head.Split("\r\n").FirstOrDefault(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        int length = lengthLine == null ? 0 : int.Parse(lengthLine.Substring(15).Trim());
        var body = new byte[length];
        int read = 0;
        while (read < length)
        {
            int n = await stream.ReadAsync(body.AsMemory(read));
            if (n == 0) break;
            read += n;
        }
        return (head, Encoding.UTF8.GetString(body, 0, read));
    }

    private static string Http(int status, string body) =>
        "HTTP/1.1 " + status + (status == 200 ? " OK" : " Error") + "\r\nContent-Type: text/xml; charset=\"utf-8\"\r\n" +
        "Content-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\nConnection: close\r\n\r\n" + body;

    private static string Envelope(string content) =>
        "<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
        "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>" + content + "</s:Body></s:Envelope>";

    private const string DescriptionXml =
        "<?xml version=\"1.0\"?><root xmlns=\"urn:schemas-upnp-org:device-1-0\"><specVersion><major>1</major><minor>0</minor></specVersion>" +
        "<device><deviceType>urn:schemas-upnp-org:device:InternetGatewayDevice:1</deviceType><serviceList><service>" +
        "<serviceType>urn:schemas-upnp-org:service:Layer3Forwarding:1</serviceType><controlURL>/l3f</controlURL></service></serviceList>" +
        "<deviceList><device><deviceType>urn:schemas-upnp-org:device:WANDevice:1</deviceType><deviceList><device>" +
        "<deviceType>urn:schemas-upnp-org:device:WANConnectionDevice:1</deviceType><serviceList><service>" +
        "<serviceType>" + Service + "</serviceType><serviceId>urn:upnp-org:serviceId:WANIPConn1</serviceId>" +
        "<controlURL>/ctl</controlURL></service></serviceList></device></deviceList></device></deviceList></device></root>";

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        listener.Stop();
        await loop;
        stop.Dispose();
    }
}
