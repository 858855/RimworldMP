namespace Multiplayer.Foundation.Protocol
{
    /// <summary>Server-side phase of a lobby's shared new-game setup (plan section 6.2).</summary>
    public enum SetupPhase : byte
    {
        Lobby = 0,
        Scenario = 1,
        Storyteller = 2,
        WorldParams = 3,
        WorldTransfer = 4,
        SiteVote = 5,
        Ideology = 6,
        Characters = 7,
        Generating = 8,
        GameTransfer = 9,
        Landing = 10,
        Playing = 11
    }
}
