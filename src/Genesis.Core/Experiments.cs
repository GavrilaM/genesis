namespace Genesis.Core;

public sealed record Observation(long Population, long Births, long Deaths, long Migration,
    double MeanScarcity, double FoodProduced, double ResistantFrequency);
public sealed record ExperimentRow(ulong Seed, string Treatment, Observation Control, Observation TreatmentResult);

public static class Experiments
{
    public static readonly string[] Treatments = ["drought", "cold-resistance", "food-loss", "productivity", "connectivity"];
    public static (Scenario Control, Scenario Treatment) Pair(Scenario baseline, string treatment)
    {
        var control = baseline;
        Scenario changed;
        switch (treatment)
        {
            case "drought": changed = baseline with { Rules = baseline.Rules with { DroughtStart = 24, DroughtEnd = 144, DroughtMultiplier = .15 } }; break;
            case "cold-resistance":
                // Isolate thermal fitness: remove warm refuges and food competition as confounders.
                // Supply remains finite and is consumed normally; 1e9 rations covers this 30-year assay.
                control = baseline with { InitialStockFraction = 1, InitialVariantWeights = [1, 0, 0],
                    Regions = baseline.Regions.Select(r => r with { BaseTemperature = -5, Capacity = 1e9 }).ToArray() };
                changed = control with { InitialVariantWeights = [0, 0, 1] }; break;
            case "food-loss":
                control = baseline with { Regions = baseline.Regions.Select(r => r with { Neighbors = [] }).ToArray() };
                changed = control with { InitialStockFraction = 0, Rules = control.Rules with { Productivity = 0 } }; break;
            case "productivity": changed = baseline with { Rules = baseline.Rules with { Productivity = baseline.Rules.Productivity * 1.5 } }; break;
            case "connectivity": changed = baseline with { Regions = baseline.Regions.Select(r => r with { Neighbors = [] }).ToArray() }; break;
            default: throw new ArgumentException("Unknown experiment treatment.");
        }
        return (control, changed);
    }
    public static Observation Observe(Scenario scenario, ulong seed, int months)
    {
        Validation.Require(months > 0, "Experiment requires positive months.");
        var w = Simulation.Create(scenario, seed);
        long births = 0, deaths = 0, migration = 0; double scarcity = 0, produced = 0;
        for (int i = 0; i < months; i++)
        {
            var step = Simulation.Step(w);
            births += step.Populations.Sum(f => f.Births); deaths += step.Populations.Sum(f => f.Deaths);
            migration += step.Populations.Sum(f => f.Outward); produced += step.Food.Sum(f => f.Regrowth);
            double demand = step.Populations.Sum(f => f.Demand);
            scarcity += demand == 0 ? 0 : 1 - step.Populations.Sum(f => f.Consumed) / demand;
            w = step.State;
        }
        return new(w.Population, births, deaths, migration, scarcity / months, produced,
            w.Population == 0 ? 0 : w.Cohorts.Where(c => c.Id.Variant == 2).Sum(c => c.Count) / (double)w.Population);
    }
    public static IEnumerable<ExperimentRow> Run(Scenario baseline, int seeds, int months)
    {
        Validation.Require(seeds > 0, "Experiment requires positive seed count.");
        foreach (var name in Treatments)
        {
            var pair = Pair(baseline, name);
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
                yield return new(seed, name, Observe(pair.Control, seed, months), Observe(pair.Treatment, seed, months));
        }
    }
    public static Scenario DoubleRegions(Scenario s)
    {
        int nr = s.Regions.Length;
        return s with { Name = s.Name + "-double-regions",
            InitialPopulations = s.InitialPopulations is null ? null : s.InitialPopulations.Concat(s.InitialPopulations.Select(p => p with { Region = new(p.Region.Value + nr) })).ToArray(),
            Regions = Enumerable.Range(0, nr * 2).Select(i =>
        {
            int offset = i >= nr ? nr : 0; var r = s.Regions[i % nr];
            return r with { Id = new(i), Name = r.Name + "-" + offset,
                Neighbors = r.Neighbors.Select(e => e with { Destination = new(e.Destination.Value + offset) })
                    .Append(new Edge(new((i + nr) % (nr * 2)), 1)).OrderBy(e => e.Destination.Value).ToArray() };
        }).ToArray() };
    }
}
