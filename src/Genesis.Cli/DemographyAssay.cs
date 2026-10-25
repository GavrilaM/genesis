using Genesis.Core;

namespace Genesis.Cli;

public sealed record DemographyResult(ulong Seed, string AgeModel, string CountModel, int Months,
    long Initial, long Final, long Births, long Deaths, long BirthsBeforeMaturation, int FirstBirthTick);

// Offline, closed-colony diagnostic. No weather/food/migration/evolution, and no individual objects.
// It does not replace or select mechanics in the production simulation.
public static class DemographyAssay
{
    public static long Binomial(long n, double probability, Func<int, double> random)
    {
        Validation.Require(n is >= 0 and <= 100000 && double.IsFinite(probability) && probability >= 0 && probability <= 1,
            "Diagnostic binomial is limited to 100,000 trials and probabilities in [0,1].");
        long count = 0; for (int i = 0; i < n; i++) if (random(i) < probability) count++;
        return count;
    }
    public static DemographyResult Run(SpeciesDefinition sp, ulong seed, bool fixedDurations, bool binomial, int months = 600, long initial = 100)
    {
        int[] durations = [sp.MaturationMonths, sp.AdultMonths, sp.ElderMonths];
        var stages = durations.Select(n => new long[fixedDurations ? n : 1]).ToArray();
        stages[0][0] = initial;
        long births = 0, deaths = 0, earlyBirths = 0; int firstBirth = 0;
        for (int tick = 1; tick <= months; tick++)
        {
            int month = tick;
            long Sample(long n, double p, int system, int cell) => binomial
                ? Binomial(n, p, draw => DeterministicRandom.Unit(seed, month, system, cell, draw: draw))
                : DeterministicRandom.Round(n * p, DeterministicRandom.Unit(seed, month, system, cell));
            long adults = 0;
            var next = durations.Select(n => new long[fixedDurations ? n : 1]).ToArray();
            for (int stage = 0; stage < 3; stage++) for (int age = 0; age < stages[stage].Length; age++)
            {
                long n = stages[stage][age]; int cell = stage * 12001 + age;
                double hazard = sp.NaturalMortality * (stage == 0 ? 1.5 : 1) + (!fixedDurations && stage == 2 ? 1.0 / sp.ElderMonths : 0);
                long dead = Sample(n, Math.Min(1, hazard), 901, cell); deaths += dead; n -= dead;
                if (stage == 1) adults += n;
                if (fixedDurations)
                {
                    if (age + 1 < durations[stage]) next[stage][age + 1] += n;
                    else if (stage < 2) next[stage + 1][0] += n;
                    else deaths += n;
                }
                else
                {
                    long aged = stage == 2 ? 0 : Sample(n, 1.0 / durations[stage], 902, cell);
                    next[stage][0] += n - aged; if (stage < 2) next[stage + 1][0] += aged;
                }
            }
            long born = Sample(adults, sp.AnnualBirthRate / 12, 903, 0);
            next[0][0] += born; births += born;
            if (tick <= sp.MaturationMonths) earlyBirths += born;
            if (firstBirth == 0 && born > 0) firstBirth = tick;
            stages = next;
        }
        return new(seed, fixedDurations ? "fixed-duration" : "geometric", binomial ? "binomial" : "stochastic-rounding", months,
            initial, stages.Sum(a => a.Sum()), births, deaths, earlyBirths, firstBirth);
    }
    public static object Report(SpeciesDefinition species, int seeds)
    {
        Validation.Require(seeds > 1, "At least two demographic assay seeds required.");
        var rows = new List<DemographyResult>();
        foreach (bool fixedAge in new[] { false, true }) foreach (bool binomial in new[] { false, true })
            for (ulong seed = 1; seed <= (ulong)seeds; seed++) rows.Add(Run(species, seed, fixedAge, binomial));
        var rounded = new List<double>(); var sampled = new List<double>();
        for (ulong seed = 1; seed <= 8192; seed++)
        {
            rounded.Add(DeterministicRandom.Round(100 * .01, DeterministicRandom.Unit(seed, 1, 904, 0)));
            sampled.Add(Binomial(100, .01, draw => DeterministicRandom.Unit(seed, 1, 904, 0, draw: draw)));
        }
        object Moments(List<double> values)
        {
            double mean = values.Average(); return new { mean, variance = values.Sum(x => (x - mean) * (x - mean)) / (values.Count - 1) };
        }
        return new { engineVersion = Simulation.Version, species, seeds, months = 600, initial = 100,
            limitations = "Closed colony with abundant food, no climate/migration/traits. Fixed durations change all stages; diagnostic, not a calibrated alternative world model. Binomial uses exact per-trial draws only in this assay.",
            pulse = new { maturationMonths = species.MaturationMonths, fractionMaturedByQuarterMean = 1 - Math.Pow(1 - 1.0 / species.MaturationMonths, species.MaturationMonths / 4),
                fractionMaturedByMean = 1 - Math.Pow(1 - 1.0 / species.MaturationMonths, species.MaturationMonths),
                fractionStillJuvenileAtTwiceMean = Math.Pow(1 - 1.0 / species.MaturationMonths, species.MaturationMonths * 2) },
            mortalityDraws = new { n = 100, probability = .01, trials = 8192, analyticBinomialVariance = .99, rounding = Moments(rounded), binomial = Moments(sampled) },
            rows };
    }
}
