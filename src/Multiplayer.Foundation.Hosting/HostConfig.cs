using System;
using System.IO;
using System.Net;

namespace Multiplayer.Foundation.Server;

public class HostConfig
{
    /// <summary>"::" = all interfaces, IPv4 and IPv6. "127.0.0.1" = this machine only.</summary>
    public string BindAddress { get; set; } = "::";
    public int Port { get; set; } = 28491;
    public int MaxClients { get; set; } = 32;
    public byte MaxPlayersPerLobby { get; set; } = 8;
    public int HeartbeatIntervalSeconds { get; set; } = 5;
    public int ClientTimeoutSeconds { get; set; } = 15;
    public string LogLevel { get; set; } = "Information";

    public IPAddress Validate(bool allowEphemeralPort)
    {
        if (!IPAddress.TryParse(BindAddress, out IPAddress? address))
            throw new InvalidDataException("BindAddress must be an IP address, e.g. :: for all interfaces.");
        if (Port < (allowEphemeralPort ? 0 : 1) || Port > 65535)
            throw new InvalidDataException("Port must be 1-65535.");
        if (MaxClients < 1 || MaxClients > 256)
            throw new InvalidDataException("MaxClients must be 1-256.");
        if (MaxPlayersPerLobby < 2 || MaxPlayersPerLobby > 64)
            throw new InvalidDataException("MaxPlayersPerLobby must be 2-64.");
        if (HeartbeatIntervalSeconds < 1 || HeartbeatIntervalSeconds > 300)
            throw new InvalidDataException("HeartbeatIntervalSeconds must be 1-300.");
        if (ClientTimeoutSeconds <= HeartbeatIntervalSeconds || ClientTimeoutSeconds > 900)
            throw new InvalidDataException("ClientTimeoutSeconds must exceed heartbeat interval and be <=900.");
        if (LogLevel is not ("Debug" or "Information" or "Warning" or "Error"))
            throw new InvalidDataException("Unsupported LogLevel.");
        return address;
    }
}
