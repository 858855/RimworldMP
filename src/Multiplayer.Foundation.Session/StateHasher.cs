using System.Runtime.InteropServices;

namespace Multiplayer.Foundation.Session
{
    /// <summary>
    /// SYNC-001 (ADR 0024, minimum of DESYNC-001): order-dependent 64-bit hash of game values. Every player feeds the
    /// same values in the same order; any different value or order gives a different result (up to 64-bit collisions).
    /// Floats count by their exact bits.
    /// </summary>
    public sealed class StateHasher
    {
        private ulong state = 0x243F6A8885A308D3UL;

        public int Count { get; private set; }
        public ulong Value => SessionSeed.Mix(state ^ (ulong)Count);

        public void Add(ulong value)
        {
            unchecked { state = SessionSeed.Mix(state ^ (value + 0x9E3779B97F4A7C15UL + (state << 6) + (state >> 2))); }
            Count++;
        }
        public void Add(long value) => Add(unchecked((ulong)value));
        public void Add(int value) => Add(unchecked((ulong)(uint)value));
        public void Add(bool value) => Add(value ? 1UL : 0UL);
        public void Add(float value) => Add((ulong)new FloatBits { Float = value }.Bits);

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public uint Bits;
        }
    }
}
