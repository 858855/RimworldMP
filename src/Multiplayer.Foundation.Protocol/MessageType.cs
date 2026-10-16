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
        // SYNC-001 / DESYNC-001 lockstep coordinator (ADR 0030). Numbered apart from the setup codes on purpose.
        Command = 64,
        ScheduledCommand = 65,
        TickHorizon = 66,
        ClientTick = 67,
        StateHash = 68,
        DesyncDetected = 69,
        SessionControl = 70,
        // Claude lane (codes 71-79): SAVE-001 / JOIN-001 (ADR 0032)
        JoinSnapshot = 71,
        PlayerSynced = 72,
        // DESYNC-003 (ADR 0033)
        DesyncResolved = 73,
        ResyncGaveUp = 74,
        SnapshotLoaded = 75,
        // VOTE-001 (ADR 0034); last codes of the Claude lane
        VoteOpen = 76,
        VoteCast = 77,
        VoteClose = 78,
        VoteState = 79,
        // Claude lane, second range 96-111 (user, 2026-10-16). FACTION-004 (ADR 0037): Factions mode storytellers.
        FactionOptions = 96,
        StorytellerChoice = 97
    }
}
