using System.Diagnostics;
using Genesis.Core;

namespace Genesis.Cli;

public sealed record DemographicObservation(string Case, string Model, ulong Seed, int Months,
    string RulesHash, string StateHash, string EventHash, double Seconds, long Initial,
    long[] Final, long[] Births, long[] Deaths, long[] Matured, int[] ExtinctionTick,
    int FirstBirthTick, long EarlyBirths, long[] MaturationByCalendarMonth);

public static class DemographicStudy
{
    public static readonly (string Name, int Quantum, bool Binomial)[] Models =
    [ ("geometric-rounding", 0, false), ("geometric-binomial", 0, true),
      ("monthly-rounding", 1, false), ("monthly-binomial", 1, true),
      ("annual-rounding", 12, false), ("annual-binomial", 12, true) ];

    public static Scenario Colony(Scenario baseline, string kind)
    {
        var sp = baseline.Species[0] with { ColdTolerance = -100, HeatTolerance = 100, MigrationRate = 0 };
        if (kind == "extinction") sp = sp with { NaturalMortality = .003 };
        if (kind == "bottleneck") sp = sp with { NaturalMortality = .006 };
        if (kind == "death-only") sp = sp with { NaturalMortality = .1 / 1.5, MaturationMonths = 120, AnnualBirthRate = 0 };
        return baseline with
        {
            Regions = [baseline.Regions[0] with { Neighbors = [], Capacity = 1e12, BaseTemperature = 15 }],
            Species = [sp], InitialPopulations = null,
            InitialStageCounts = kind == "extinction" ? [0, 20, 0] : kind is "death-only" or "bottleneck" ? [20, 0, 0] : [100, 0, 0],
            InitialVariantWeights = [1, 0, 0], InitialStockFraction = 1,
            Rules = baseline.Rules with { StarvationMortality = 0, ClimateMortality = 0, MutationRate = 0 }
        };
    }

    public static DemographicObservation Observe(Scenario scenario, string kind, string model, ulong seed, int months)
    {
        var w = Simulation.Create(scenario, seed); long initial = w.Population;
        int ns = scenario.Species.Length;
        var births = new long[ns]; var deaths = new long[ns]; var matured = new long[ns];
        var final = new long[ns]; var extinct = new int[ns]; var seasonal = new long[12];
        int firstBirth = 0; long earlyBirths = 0;
        var watch = Stopwatch.StartNew();
        for (int t = 1; t <= months; t++)
        {
            var step = Simulation.Step(w); w = step.State; Array.Clear(final);
            foreach (var flow in step.Populations)
            {
                int s = flow.Species.Value;
                births[s] += flow.Births; deaths[s] += flow.Deaths; matured[s] += flow.Matured; final[s] += flow.End;
                seasonal[(t - 1) % 12] += flow.Matured;
                if (flow.Births > 0 && firstBirth == 0) firstBirth = t;
                if (t <= scenario.Species[s].MaturationMonths) earlyBirths += flow.Births;
            }
            foreach (var ev in step.Events.Where(e => e.Type == "species-extinction")) extinct[ev.ActorRefs[0].Value] = t;
        }
        watch.Stop();
        Validation.Require(w.Population == initial + births.Sum() - deaths.Sum(), "Study accounting failed.");
        return new(kind, model, seed, months, w.RulesetHash, Persistence.Hash(w), w.EventHash, watch.Elapsed.TotalSeconds,
            initial, final, births, deaths, matured, extinct, firstBirth, earlyBirths, seasonal);
    }

    public static void Run(Scenario baseline, string suite, int seeds, int years, string output, string? selected = null)
    {
        Validation.Require(seeds > 1 && years > 0, "Study needs multiple seeds and positive years.");
        Validation.Require(suite is "worlds" or "pulse" or "extinction" or "bottleneck" or "death-only", "Unknown demographic suite.");
        Validation.Require(!Directory.Exists(output) && !File.Exists(output), "Study output exists.");
        var source = suite == "worlds" ? baseline : Colony(baseline, suite);
        // Geometric maturation invalidates the analytic death-only comparison.
        var models = suite == "death-only" ? Models.Where(m => m.Quantum == 1).ToArray() : Models;
        if (selected is not null)
        {
            var names = selected.Split(',');
            Validation.Require(names.Distinct().Count() == names.Length && names.All(n => models.Any(m => m.Name == n)), "Unknown or duplicate demographic model.");
            models = models.Where(m => names.Contains(m.Name)).ToArray();
        }
        var cases = models.Select(m => new { m.Name, Scenario = source with { Demography = new(m.Quantum, m.Binomial, suite != "worlds") } }).ToArray();
        int months = suite == "death-only" ? 24 : checked(years * 12);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "protocol.json"), Persistence.Encode(new
        {
            engineVersion = Simulation.Version, snapshotSchema = Simulation.SnapshotSchema, suite, seeds, months,
            seedRange = "1..seeds; rotated model order by seed", cases,
            notes = "Adult/elder duration and mutation/migration rounding unchanged. Uniform initial juvenile ages in world; newborn initial juveniles in colonies. Extinction=zero total species population, no rescue. Timing includes invariant validation but excludes final serialization."
        }));
        var rows = new List<DemographicObservation>();
        using var writer = new StreamWriter(Path.Combine(output, "observations.jsonl"));
        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            for (int offset = 0; offset < cases.Length; offset++)
            {
                var c = cases[(offset + (int)seed - 1) % cases.Length];
                var row = Observe(c.Scenario, suite, c.Name, seed, months);
                writer.WriteLine(Persistence.Encode(row)); writer.Flush(); rows.Add(row);
            }
            if (suite == "worlds" || seed % 32 == 0) Console.WriteLine($"{suite}: seed {seed}/{seeds} complete");
        }
        static object Moments(IEnumerable<double> values)
        {
            var data = values.ToArray(); double mean = data.Average();
            return new { mean, sd = data.Length > 1 ? (double?)Math.Sqrt(data.Sum(x => (x - mean) * (x - mean)) / (data.Length - 1)) : null, min = data.Min(), max = data.Max() };
        }
        var summary = rows.GroupBy(r => r.Model).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new
        {
            model = g.Key, runs = g.Count(), seconds = Moments(g.Select(r => r.Seconds)),
            final = Enumerable.Range(0, source.Species.Length).Select(s => Moments(g.Select(r => (double)r.Final[s]))),
            extinct = Enumerable.Range(0, source.Species.Length).Select(s => g.Count(r => r.Final[s] == 0)),
            extinctionTickAmongExtinct = Enumerable.Range(0, source.Species.Length).Select(s => g.Any(r => r.ExtinctionTick[s] > 0) ? Moments(g.Where(r => r.ExtinctionTick[s] > 0).Select(r => (double)r.ExtinctionTick[s])) : null),
            firstBirthAmongReproducing = g.Any(r => r.FirstBirthTick > 0) ? Moments(g.Where(r => r.FirstBirthTick > 0).Select(r => (double)r.FirstBirthTick)) : null,
            earlyBirths = Moments(g.Select(r => (double)r.EarlyBirths)),
            maturationByCalendarMonth = Enumerable.Range(0, 12).Select(m => g.Sum(r => r.MaturationByCalendarMonth[m]))
        });
        File.WriteAllText(Path.Combine(output, "summary.json"), Persistence.Encode(new { suite, seeds, months, models = summary }));
    }
}
