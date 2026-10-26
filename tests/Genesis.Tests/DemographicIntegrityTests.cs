using Genesis.Core;
using Xunit;

namespace Genesis.Tests;

public class DemographicIntegrityTests
{
    public static Scenario Colony(int quantum = 1, bool binomial = true, long juveniles = 100, long adults = 0, int maturation = 24)
    {
        var b = MechanicsTests.Baseline();
        return b with
        {
            Regions = [b.Regions[0] with { Neighbors = [], Capacity = 1e12, BaseTemperature = 15 }],
            Species = [b.Species[0] with { MaturationMonths = maturation, NaturalMortality = 0, ColdTolerance = -100, HeatTolerance = 100 }],
            InitialPopulations = null, InitialStageCounts = [juveniles, adults, 0], InitialVariantWeights = [1, 0, 0], InitialStockFraction = 1,
            Rules = b.Rules with { ClimateMortality = 0, StarvationMortality = 0, MutationRate = 0 },
            Demography = new(quantum, binomial, true)
        };
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(1, 24)] [InlineData(12, 25)] [InlineData(12, 24)]
    public void NewbornPulseCannotMatureOrReproduceEarly(int quantum, int maturation)
    {
        var w = Simulation.Create(Colony(quantum, maturation: maturation), 7);
        int due = (maturation + quantum - 1) / quantum * quantum;
        for (int t = 1; t <= due; t++)
        {
            var step = Simulation.Step(w);
            Assert.Equal(0, step.Populations[0].Births);
            Assert.Equal(t == due ? 100 : 0, step.Populations[0].Matured);
            w = step.State;
        }
        Assert.Equal(100, w.Cohorts.Where(c => c.Id.Stage == 1).Sum(c => c.Count));
    }

    [Fact] public void MigrationPreservesEveryDeadlineAndCannotMoveArrivalsTwice()
    {
        var s = MigrationForecastTests.CapacityCase();
        s = s with { InitialPopulations = [new(new(0), new(0), [80, 0, 0])],
            Species = s.Species.Select(sp => sp with { MaturationMonths = 25 }).ToArray(), Demography = new(1, true, true) };
        var before = Simulation.Create(s, 1); var step = Simulation.Step(before);
        Assert.Equal(80, step.Transfers.Sum(t => t.Count));
        Assert.All(step.Transfers, t => Assert.Equal(0, t.Origin.Region.Value));
        for (int v = 0; v < 3; v++) for (int bin = 0; bin < 26; bin++)
            Assert.Equal(before.Cohorts.Where(c => c.Id.Stage == 0 && c.Id.Variant == v).Sum(c => c.JuvenileBins![bin]),
                step.State.Cohorts.Where(c => c.Id.Stage == 0 && c.Id.Variant == v).Sum(c => c.JuvenileBins![bin]));
        Assert.Equal(80, step.State.Cohorts.Where(c => c.Id.Region.Value == 1).Sum(c => c.Count));
    }

    [Fact] public void InitialAgeQueuesPartitionCountsAndCorruptionIsRejected()
    {
        foreach (int q in new[] { 1, 12 })
        {
            var s = Colony(q, juveniles: 101, maturation: 25) with { Demography = new(q, true, false) };
            var w = Simulation.Create(s, 2);
            Assert.Equal(101, w.Cohorts[0].JuvenileBins!.Sum());
            w.Cohorts[0].JuvenileBins![1]++;
            Assert.Throws<InvalidDataException>(() => Validation.State(w));
        }
        Assert.Throws<InvalidDataException>(() => Simulation.Create(Colony() with { Demography = new(7) }, 1));
    }

    [Theory] [InlineData(1)] [InlineData(12)]
    public void EveryBirthKeepsItsDeadlineAcrossRingWrapAndMidMonthResume(int quantum)
    {
        var w = Simulation.Create(Colony(quantum, juveniles: 0, adults: 1000, maturation: 25), 37);
        var expected = new long[200]; WorldState? resumed = null;
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            for (int tick = 1; tick <= 120; tick++)
            {
                string before = Persistence.Hash(w);
                var step = Simulation.Step(w, audit: true);
                Assert.Equal(before, Persistence.Hash(w));
                if (resumed is not null)
                {
                    var continuation = Simulation.Step(resumed);
                    Assert.Equal(Persistence.Hash(step.State), Persistence.Hash(continuation.State));
                    Assert.Equal(Persistence.Encode(step.Summaries), Persistence.Encode(continuation.Summaries));
                    resumed = continuation.State;
                }
                Assert.Equal(expected[tick], step.Populations[0].Matured);
                expected[(tick + 25 + quantum - 1) / quantum * quantum] += step.Populations[0].Births;
                w = step.State;
                if (tick == 7) { Persistence.Save(path, w); resumed = Persistence.Load(path); }
            }
        }
        finally { File.Delete(path); }
    }

    [Fact] public void PartialMigrationAllocationIsBoundedAndExact()
    {
        long[] bins = [0, 1, 2, 0, 7, 1000000000000L - 10];
        foreach (long amount in new[] { 0L, 1L, 17L, 500000000001L, 1000000000000L })
            foreach (double u in new[] { 0, .001, .5, .999, Math.BitDecrement(1.0) })
            {
                var moved = JuvenileAges.Allocate(bins, bins.Sum(), amount, u);
                Assert.Equal(amount, moved.Sum());
                for (int b = 0; b < bins.Length; b++) Assert.InRange(moved[b], 0, bins[b]);
            }
        long[] edge = [999999999998, 1, 1];
        Assert.Equal(edge, JuvenileAges.Allocate(edge, edge.Sum(), edge.Sum(), Math.BitDecrement(1.0)));
    }

    [Theory]
    [InlineData(1, .1)] [InlineData(20, .2)] [InlineData(100, .01)]
    [InlineData(100, .5)] [InlineData(1000, .02)] [InlineData(1000000, .2)]
    [InlineData(1000000000000L, .5)] [InlineData(1000000000000L, 1e-12)]
    public void BinomialMomentsMatchAnalyticDistribution(long n, double p)
    {
        const int trials = 16384; double mean = 0, m2 = 0;
        for (int i = 1; i <= trials; i++)
        {
            long x = EventSampling.Binomial(n, p, (ulong)i, 1, 1, 0);
            Assert.InRange(x, 0, n);
            double delta = x - mean; mean += delta / i; m2 += delta * (x - mean);
        }
        double variance = n * p * (1 - p);
        Assert.InRange(mean, n * p - 6 * Math.Sqrt(variance / trials), n * p + 6 * Math.Sqrt(variance / trials));
        Assert.InRange(m2 / (trials - 1), variance * .93, variance * 1.07);
    }

    [Fact] public void BinomialTailsAndPartitionedCountsAgreeWithExactProbabilities()
    {
        const int trials = 32768; int[] histogram = new int[21]; double sum = 0, sum2 = 0;
        for (int i = 1; i <= trials; i++)
        {
            histogram[EventSampling.Binomial(20, .2, (ulong)i, 1, 2, 0)]++;
            long partitioned = 0;
            for (int b = 0; b < 10; b++) partitioned += EventSampling.Binomial(10, .1, (ulong)i, 1, 2, 0, slot: b);
            sum += partitioned; sum2 += partitioned * partitioned;
        }
        double pmf = Math.Pow(.8, 20);
        for (int k = 0; k <= 20; k++)
        {
            double expected = trials * pmf;
            Assert.InRange(histogram[k], Math.Max(0, expected - 6 * Math.Sqrt(expected * (1 - pmf)) - 2), expected + 6 * Math.Sqrt(expected * (1 - pmf)) + 2);
            pmf *= (20 - k) / (double)(k + 1) * .25;
        }
        double mean = sum / trials, variance = (sum2 - trials * mean * mean) / (trials - 1);
        Assert.InRange(mean, 9.85, 10.15); Assert.InRange(variance, 8.37, 9.63);
    }

    [Fact] public void SamplerEndpointsComplementsAndStreamIsolationHold()
    {
        Assert.Equal(0, EventSampling.Binomial(0, .3, 1, 1, 1, 0));
        Assert.Equal(0, EventSampling.Binomial(100, 0, 1, 1, 1, 0));
        Assert.Equal(100, EventSampling.Binomial(100, 1, 1, 1, 1, 0));
        Assert.Throws<InvalidDataException>(() => EventSampling.Binomial(-1, .5, 1, 1, 1, 0));
        Assert.Throws<InvalidDataException>(() => EventSampling.Binomial(1, double.NaN, 1, 1, 1, 0));
        for (ulong seed = 1; seed <= 100; seed++)
        {
            long a = EventSampling.Binomial(1000, .25, seed, 1, 4, 0);
            _ = EventSampling.Binomial(1000000000000, .5, seed, 1, 4, 0, slot: 1);
            Assert.Equal(a, EventSampling.Binomial(1000, .25, seed, 1, 4, 0));
            Assert.Equal(1000 - a, EventSampling.Binomial(1000, .75, seed, 1, 4, 0));
        }
    }

    [Fact] public void ProductionBirthsAndDeathsRetainBinomialVariance()
    {
        var birth = Colony(juveniles: 0, adults: 120);
        birth = birth with { Species = [birth.Species[0] with { AnnualBirthRate = 1 }] };
        var death = Colony();
        death = death with { Species = [death.Species[0] with { NaturalMortality = .01 / 1.5, AnnualBirthRate = 0 }] };
        var births = new List<double>(); var deaths = new List<double>();
        for (ulong seed = 1; seed <= 2048; seed++)
        {
            births.Add(Simulation.Step(Simulation.Create(birth, seed)).Populations[0].Births);
            deaths.Add(Simulation.Step(Simulation.Create(death, seed)).Populations[0].Deaths);
        }
        static double Variance(List<double> samples)
        {
            double mean = samples.Average(); return samples.Sum(x => (x - mean) * (x - mean)) / (samples.Count - 1);
        }
        Assert.InRange(births.Average(), 9.5, 10.5); Assert.InRange(Variance(births), 7.8, 10.55);
        Assert.InRange(deaths.Average(), .85, 1.15); Assert.InRange(Variance(deaths), .84, 1.14);
    }

    [Fact] public void DeathOnlyExtinctionMatchesIndependentSurvivalProbability()
    {
        var s = Colony(juveniles: 20, maturation: 120);
        s = s with { Species = [s.Species[0] with { NaturalMortality = .1 / 1.5, AnnualBirthRate = 0 }] };
        const int seeds = 1024; int extinct = 0;
        for (ulong seed = 1; seed <= seeds; seed++)
        {
            var w = Simulation.Create(s, seed);
            for (int t = 0; t < 24; t++) w = Simulation.Step(w).State;
            if (w.Population == 0) extinct++;
        }
        double expected = Math.Pow(1 - Math.Pow(.9, 24), 20);
        double margin = 5 * Math.Sqrt(expected * (1 - expected) / seeds);
        Assert.InRange(extinct / (double)seeds, expected - margin, expected + margin);
    }

    [Fact] public void StudySerializesSingletonExtinctionTimeAsUndefinedStandardDeviation()
    {
        string path = Path.Combine(Path.GetTempPath(), "genesis-demography-" + Guid.NewGuid());
        try
        {
            Genesis.Cli.DemographicStudy.Run(MechanicsTests.Baseline(), "death-only", 9, 2, path, "monthly-binomial");
            using var summary = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "summary.json")));
            var model = summary.RootElement.GetProperty("models")[0];
            Assert.Equal(1, model.GetProperty("extinct")[0].GetInt32());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, model.GetProperty("extinctionTickAmongExtinct")[0].GetProperty("sd").ValueKind);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
