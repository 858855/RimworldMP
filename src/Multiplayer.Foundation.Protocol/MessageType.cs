namespace Multiplayer.Foundation.Protocol
{
    public enum MessageType : byte
    {
        Hello = 1,
        HandshakeAccepted = 2,
        HandshakeRejected = 3,
        Ping = 4,
        Pong = 5,
        LobbyListRequest = 6,
        LobbyListResponse = 7,
        CreateLobby = 8,
        JoinLobby = 9,
        LeaveLobby = 10,
        LobbyStateUpdate = 11,
        PlayerJoined = 12,
        PlayerLeft = 13,
        PlayerReadyChanged = 14,
        Disconnect = 15,
        Error = 16,
        CloseLobby = 17,
        LobbyChatRequest = 18,
        LobbyChatMessage = 19,
        TransferBegin = 20,
        TransferChunk = 21,
        TransferAck = 22,
        TransferAbort = 23,
        SetupAdvance = 24,
        SetupAbort = 25,
        SiteFlag = 26,
        SiteDecided = 27,
        // MOD-001
        ModManifestSummary = 28,
        ModManifestRequest = 29,
        // SETUP-001
        SetupSettings = 30,
        // SETUP-002
        WorldReady = 31,
        WorldLoaded = 32,
        // SETUP-004
        PawnRoster = 33,
        PawnRequest = 34,
        // SETUP-005
        GameReady = 35,
        GameLoaded = 36,
        // SETUP-004, guest character page
        PawnPortrait = 37,
        // SYNC-001 lockstep (ADR 0024, wire version 4)
        LockstepCommand = 38,
        ScheduledCommand = 39,
        TickHorizon = 40,
        ClientTick = 41,
        StateHash = 42,
        DesyncDetected = 43
    }
}
