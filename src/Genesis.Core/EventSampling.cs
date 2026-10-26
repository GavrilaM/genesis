namespace Genesis.Core;

public static class EventSampling
{
    public static long Binomial(long n, double p, ulong seed, int tick, int system, int region, int species = 0, int slot = 0)
    {
        Validation.Require(n is >= 0 and <= 1000000000000 && double.IsFinite(p) && p >= 0 && p <= 1,
            "Binomial requires 0 <= n <= 1e12 and 0 <= p <= 1.");
        var random = new DeterministicRandom.Stream(seed, tick, system, region, species, slot);
        return Draw(n, p, ref random);
    }

    private static long Draw(long n, double p, ref DeterministicRandom.Stream random)
    {
        if (n == 0 || p == 0) return 0;
        if (p == 1) return n;
        if (p > .5) return n - Draw(n, 1 - p, ref random);
        if (n * p < 16)
        {
            // Exact geometric waiting times between successes; stable for tiny p.
            double logFailure = p < 1e-8 ? -p * (1 + p * (.5 + p / 3)) : Math.Log(1 - p);
            long remaining = n, successes = 0;
            while (remaining > 0)
            {
                double failures = Math.Floor(Math.Log(random.Next()) / logFailure);
                if (failures >= remaining) break;
                remaining -= (long)failures + 1; successes++;
            }
            return successes;
        }
        // Uniform order-statistic decomposition (Devroye, chapter X, section 4.5).
        // Median splitting bounds depth by log2(n); no normal/Poisson count approximation.
        long k = n / 2 + 1;
        double a = Gamma(k, ref random), b = Gamma(n - k + 1, ref random), pivot = a / (a + b);
        return pivot > p ? Draw(k - 1, p / pivot, ref random)
            : k + Draw(n - k, (p - pivot) / (1 - pivot), ref random);
    }

    private static double Gamma(double shape, ref DeterministicRandom.Stream random)
    {
        // Marsaglia & Tsang (2000), shape >= 1. Box-Muller supplies the normal.
        double d = shape - 1.0 / 3, c = 1 / Math.Sqrt(9 * d);
        while (true)
        {
            double x = Math.Sqrt(-2 * Math.Log(random.Next())) * Math.Cos(2 * Math.PI * random.Next());
            double v = 1 + c * x;
            if (v <= 0) continue;
            v = v * v * v;
            double u = random.Next(), x2 = x * x;
            if (u < 1 - .0331 * x2 * x2 || Math.Log(u) < .5 * x2 + d * (1 - v + Math.Log(v))) return d * v;
        }
    }
}
