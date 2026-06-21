namespace Sim.Core;

/// <summary>
/// Deterministic xorshift64 RNG. Produces identical sequences on any .NET runtime and Unity,
/// unlike <see cref="System.Random"/> whose algorithm changed across .NET versions.
/// Seeded via splitmix64 to guarantee a non-zero xorshift64 state for every input seed including 0.
/// </summary>
public sealed class SimRandom
{
    private ulong _state;

    /// <summary>Creates a <see cref="SimRandom"/> from a 32-bit seed.</summary>
    public SimRandom(uint seed)
    {
        // splitmix64 maps any 32-bit seed (including 0) to a 64-bit non-zero state.
        ulong z = seed + 0x9e3779b97f4a7c15UL;
        z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
        z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
        _state = z ^ (z >> 31);
        // Zero is the xorshift64 fixed point; splitmix64 will not produce it for any input,
        // but guard anyway in case the compiler constant-folds incorrectly on some target.
        if (_state == 0) _state = 0x9e3779b97f4a7c15UL;
    }

    /// <summary>Advances state and returns the next 64-bit pseudo-random value.</summary>
    public ulong NextUInt64()
    {
        ulong x = _state;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        return _state = x;
    }

    /// <summary>Returns a uniform <see cref="double"/> in [0, 1) with 53-bit precision.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
}
