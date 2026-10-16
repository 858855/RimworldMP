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
    /// <summary>Debug only: the host may start the shared setup alone (one player). Off by default.</summary>
    public bool AllowSoloStart { get; set; }
    /// <summary>ADR 0035: TCP players must prove their player key with a signed Hello. Steam connections are exempt.</summary>
    public bool RequireKeyProof { get; set; }
    /// <summary>How far a key proof's timestamp may differ from this host's clock.</summary>
    public int KeyProofMaxSkewSeconds { get; set; } = 600;
    /// <summary>ADR 0036: a joining or resyncing game must confirm its snapshot within this time.</summary>
    public int SnapshotLoadTimeoutSeconds { get; set; } = 180;
    /// <summary>
    /// ADR 0037, tests only: lets a Factions lobby start its setup. Internal on purpose, so neither the mod nor a config
    /// file can turn it on; the Factions start stays refused until its game side and tests are done.
    /// </summary>
    internal bool EnableFactionsSetup { get; set; }

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
        if (SnapshotLoadTimeoutSeconds < 5 || SnapshotLoadTimeoutSeconds > 3600)
            throw new InvalidDataException("SnapshotLoadTimeoutSeconds must be 5-3600.");
        if (KeyProofMaxSkewSeconds < 30 || KeyProofMaxSkewSeconds > 3600)
            throw new InvalidDataException("KeyProofMaxSkewSeconds must be 30-3600.");
        if (LogLevel is not ("Debug" or "Information" or "Warning" or "Error"))
            throw new InvalidDataException("Unsupported LogLevel.");
        return address;
    }
}
