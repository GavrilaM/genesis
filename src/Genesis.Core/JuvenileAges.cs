namespace Genesis.Core;

// Counts inside a juvenile cohort, never additional people. Slots are absolute
// maturation deadlines modulo the queue length, shared across regions of a species.
public static class JuvenileAges
{
    public static DemographyOptions Options(Scenario s) => s.Demography ?? new();
    public static int Length(int maturation, int quantum) => (maturation + quantum - 1) / quantum + 1;
    public static int DueSlot(int birthTick, int maturation, int quantum, int length)
        => (int)(((long)birthTick + maturation + quantum - 1) / quantum % length);

    public static long[] Initialize(long count, int maturation, int quantum, bool newborn)
    {
        var bins = new long[Length(maturation, quantum)];
        if (newborn) bins[DueSlot(0, maturation, quantum, bins.Length)] = count;
        else
        {
            // A uniform initial residence-age distribution, with integer quotas.
            // Round deadlines upward, never below the actual remaining months.
            long previous = 0;
            for (int due = 1; due < bins.Length; due++)
            {
                long cumulative = count * Math.Min(maturation, due * quantum) / maturation;
                bins[due] = cumulative - previous; previous = cumulative;
            }
        }
        return bins;
    }

    // Systematic proportional allocation: conserve the rounded migration total,
    // preserve deadlines and avoid an order-dependent preference for young/old bins.
    // This is an allocation policy, not independent demographic event sampling.
    public static long[] Allocate(long[] bins, long total, long amount, double offset)
    {
        var selected = new long[bins.Length];
        if (amount == 0) return selected;
        // Integer arithmetic keeps even 1e12-person cohorts exact near quota boundaries.
        long offsetUnits = (long)(offset * total);
        long cumulative = 0, previous = 0;
        for (int i = 0; i < bins.Length; i++)
        {
            cumulative += bins[i];
            long allocated = (long)(((Int128)cumulative * amount + offsetUnits) / total);
            selected[i] = allocated - previous; previous = allocated;
        }
        return selected;
    }
}
