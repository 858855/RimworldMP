using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Session
{
    /// <summary>
    /// SYNC-001 (ADR 0024): game ticks per real second at each session speed. Vanilla 1.6 runs 1×, 3× and 6× of 60 ticks
    /// per second (TickManager.TickRateMultiplier; checked by the in-game self-test).
    /// </summary>
    public static class SessionSpeeds
    {
        public const int NormalTicksPerSecond = 60;

        public static int Multiplier(SessionSpeed speed) => speed switch
        {
            SessionSpeed.Normal => 1,
            SessionSpeed.Fast => 3,
            SessionSpeed.Superfast => 6,
            _ => 0
        };

        public static int TicksPerSecond(SessionSpeed speed) => Multiplier(speed) * NormalTicksPerSecond;
    }
}
