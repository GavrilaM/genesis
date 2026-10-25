using Genesis.Core;

namespace Genesis.Cli;

public sealed record CalibrationCase(string Name, Scenario Scenario);
public sealed record SpeciesObservation(SpeciesId Species, string Name, long Initial, long Final,
    long Births, long Deaths, long Inward, long Outward, long PersonMonths, long AdultPersonMonths,
    double Demand, double Consumed, double ColdExposurePersonMonths, double HeatExposurePersonMonths);
public sealed record PopulationFrame(int Tick, long[] SpeciesCounts);
public sealed record CalibrationResult(string Case, ulong Seed, int Months, string EngineVersion,
    string RulesetHash, SpeciesObservation[] Species, PopulationFrame[] Frames, string StateHash, string EventHash);

public static class Calibration
{
    public static CalibrationCase[] Cases(Scenario baseline)
    {
        Validation.Require(baseline.Species.Length == 4 && baseline.Regions.Length == 12 && baseline.InitialPopulations is null
            && baseline.Species.Select(s => s.Name).SequenceEqual(new[] { "human", "orc", "elf", "dwarf" }),
            "Calibration protocol requires the original uniform 12-region/four-species baseline.");
        var cases = new List<CalibrationCase> { new("baseline", baseline) };
        void Orc(string name, Func<SpeciesDefinition, SpeciesDefinition> change) => cases.Add(new(name,
            baseline with { Species = baseline.Species.Select(s => s.Id.Value == 1 ? change(s) : s).ToArray() }));
        Orc("orc-birth-low", s => s with { AnnualBirthRate = s.AnnualBirthRate * .8 });
        Orc("orc-birth-high", s => s with { AnnualBirthRate = s.AnnualBirthRate * 1.2 });
        Orc("orc-maturation-short", s => s with { MaturationMonths = s.MaturationMonths * 3 / 4 });
        Orc("orc-maturation-long", s => s with { MaturationMonths = s.MaturationMonths * 5 / 4 });
        Orc("orc-demand-low", s => s with { FoodDemand = s.FoodDemand * .8 });
        Orc("orc-demand-high", s => s with { FoodDemand = s.FoodDemand * 1.2 });
        Orc("orc-tolerance-narrow", s => s with { ColdTolerance = s.ColdTolerance + 5, HeatTolerance = s.HeatTolerance - 5 });
        Orc("orc-tolerance-wide", s => s with { ColdTolerance = s.ColdTolerance - 5, HeatTolerance = s.HeatTolerance + 5 });
        foreach (var factor in new[] { "birth", "maturation", "demand", "tolerance", "biology" })
        {
            var swapped = baseline.Species.Select(s =>
            {
                if (s.Id.Value > 1) return s;
                var other = baseline.Species[1 - s.Id.Value];
                return factor switch
                {
                    "birth" => s with { AnnualBirthRate = other.AnnualBirthRate },
                    "maturation" => s with { MaturationMonths = other.MaturationMonths },
                    "demand" => s with { FoodDemand = other.FoodDemand },
                    "tolerance" => s with { ColdTolerance = other.ColdTolerance, HeatTolerance = other.HeatTolerance },
                    _ => other with { Id = s.Id, Name = s.Name }
                };
            }).ToArray();
            cases.Add(new("swap-" + factor, baseline with { Species = swapped }));
        }
        var originalCohorts = Simulation.Create(baseline, 0).Cohorts;
        for (int rotation = 0; rotation < 4; rotation++)
        {
            // Preserve EACH species' total and age/trait composition, leaving eight regions empty.
            var starts = Enumerable.Range(0, 4).Select(s => new InitialPopulation(new(((s + rotation) % 4) * 3), new(s),
                baseline.InitialStageCounts.Select(n => n * 12).ToArray(), StageVariantCounts:
                Enumerable.Range(0, 3).Select(a => Enumerable.Range(0, 3).Select(v => originalCohorts
                    .Where(c => c.Id.Species.Value == s && c.Id.Stage == a && c.Id.Variant == v).Sum(c => c.Count)).ToArray()).ToArray()))
                .OrderBy(p => p.Region.Value).ToArray();
            cases.Add(new("geography-" + rotation, baseline with { InitialPopulations = starts }));
        }
        foreach (var c in cases) Validation.Scenario(c.Scenario);
        return cases.ToArray();
    }

    public static CalibrationResult Observe(CalibrationCase configuration, ulong seed, int months)
    {
        Validation.Require(months > 0, "Positive calibration duration required.");
        var w = Simulation.Create(configuration.Scenario, seed); int ns = w.Scenario.Species.Length;
        long[] Totals(WorldState state) => Enumerable.Range(0, ns).Select(s => state.Cohorts.Where(c => c.Id.Species.Value == s).Sum(c => c.Count)).ToArray();
        var initial = Totals(w); var births = new long[ns]; var deaths = new long[ns];
        var inward = new long[ns]; var outward = new long[ns]; var people = new long[ns]; var adults = new long[ns];
        var demand = new double[ns]; var consumed = new double[ns]; var cold = new double[ns]; var heat = new double[ns];
        var frames = new List<PopulationFrame> { new(0, initial) };
        for (int month = 0; month < months; month++)
        {
            var step = Simulation.Step(w);
            foreach (var f in step.Populations)
            {
                int s = f.Species.Value; births[s] += f.Births; deaths[s] += f.Deaths;
                inward[s] += f.Inward; outward[s] += f.Outward; demand[s] += f.Demand; consumed[s] += f.Consumed;
            }
            foreach (var c in w.Cohorts)
            {
                int s = c.Id.Species.Value; var sp = w.Scenario.Species[s];
                double temp = step.State.Regions[c.Id.Region.Value].Temperature;
                people[s] += c.Count; if (c.Id.Stage == 1) adults[s] += c.Count;
                cold[s] += c.Count * Math.Max(0, sp.ColdTolerance - c.Id.Variant * w.Scenario.Rules.ResistanceDegrees - temp);
                heat[s] += c.Count * Math.Max(0, temp - sp.HeatTolerance);
            }
            w = step.State;
            if (w.CurrentTick % 1200 == 0 || w.CurrentTick == months) frames.Add(new(w.CurrentTick, Totals(w)));
        }
        var final = Totals(w);
        return new(configuration.Name, seed, months, Simulation.Version, w.RulesetHash,
            Enumerable.Range(0, ns).Select(s => new SpeciesObservation(new(s), w.Scenario.Species[s].Name, initial[s], final[s],
                births[s], deaths[s], inward[s], outward[s], people[s], adults[s], demand[s], consumed[s], cold[s], heat[s])).ToArray(),
            frames.ToArray(), Persistence.Hash(w), w.EventHash);
    }

    public static void Run(Scenario baseline, int seeds, int months, string output, string? selected)
    {
        Validation.Require(seeds > 0 && months > 0, "Positive seed count and duration required.");
        var cases = Cases(baseline);
        if (selected is not null)
        {
            var names = selected.Split(',');
            Validation.Require(names.Distinct().Count() == names.Length && names.All(n => cases.Any(c => c.Name == n)), "Unknown or duplicate calibration case.");
            cases = cases.Where(c => names.Contains(c.Name)).ToArray();
        }
        Validation.Require(!Directory.Exists(output) && !File.Exists(output), "Calibration output exists; use a new directory.");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "protocol.json"), Persistence.Encode(new { status = "incomplete", engine = Simulation.Version, seeds, months, cases = cases.Select(c => c.Name).ToArray() }));
        foreach (var c in cases) File.WriteAllText(Path.Combine(output, c.Name + ".json"), Persistence.Encode(c.Scenario));
        using var rows = new StreamWriter(Path.Combine(output, "observations.jsonl"));
        foreach (var c in cases)
        {
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                rows.WriteLine(Persistence.Encode(Observe(c, seed, months))); rows.Flush();
            }
            Console.Error.WriteLine($"Calibration {c.Name}: {seeds} seeds x {months} months complete.");
        }
        File.WriteAllText(Path.Combine(output, "protocol.json"), Persistence.Encode(new { status = "complete", engine = Simulation.Version, seeds, months, cases = cases.Select(c => c.Name).ToArray() }));
    }
}
