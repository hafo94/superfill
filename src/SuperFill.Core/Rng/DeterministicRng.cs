using System.Numerics;

namespace SuperFill.Core.Rng;

/// <summary>xoshiro256** seeded through SplitMix64. Never use System.Random in Core.</summary>
public sealed class DeterministicRng
{
    private ulong _s0, _s1, _s2, _s3;

    public DeterministicRng(ulong seed)
    {
        ulong x = seed;
        _s0 = SplitMix64.Next(ref x);
        _s1 = SplitMix64.Next(ref x);
        _s2 = SplitMix64.Next(ref x);
        _s3 = SplitMix64.Next(ref x);
    }

    public ulong NextUInt64()
    {
        ulong result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public double NextDouble(double min, double max) => min + (max - min) * NextDouble();

    /// <summary>Uniform integer in [0, exclusiveMax) without modulo bias.</summary>
    public int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        ulong bound = (ulong)exclusiveMax;
        ulong threshold = (0UL - bound) % bound;
        while (true)
        {
            ulong r = NextUInt64();
            if (r >= threshold) return (int)(r % bound);
        }
    }
}
