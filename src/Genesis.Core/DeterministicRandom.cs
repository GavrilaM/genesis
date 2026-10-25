namespace Genesis.Core;

// Counter-based streams: no hidden cursor. IDs and draw slots are part of engine compatibility.
public static class DeterministicRandom
{
    public const string Algorithm = "splitmix64-counter-v1";
    private static ulong Mix(ulong x)
    {
        unchecked
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }
    public static double Unit(ulong seed, int tick, int system, int region, int species = 0, int draw = 0)
    {
        var h = Mix(seed);
        foreach (var key in new[] { tick, system, region, species, draw }) h = Mix(h ^ (uint)key);
        return (h >> 11) * (1.0 / 9007199254740992.0);
    }
    public static long Round(double expected, double unit)
    {
        if (!double.IsFinite(expected) || expected < 0 || expected > 1e12)
            throw new InvalidDataException("Expected count outside supported range [0, 1e12].");
        var whole = (long)Math.Floor(expected);
        return whole + (unit < expected - whole ? 1 : 0);
    }
}
