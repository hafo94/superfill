namespace SuperFill.Core.Rng;

/// <summary>Derives child seeds from where a choice lives (layer, lattice index, …), not from generation order.</summary>
public static class SeedDeriver
{
    public static ulong Derive(ulong seed, params long[] indices)
    {
        ulong h = seed;
        foreach (long i in indices)
        {
            ulong x = h ^ unchecked((ulong)i) * 0x9E3779B97F4A7C15UL;
            h = SplitMix64.Next(ref x);
        }
        return h;
    }
}
