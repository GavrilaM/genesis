namespace Genesis.Core;

public static class Validation
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    private static void Range(double x, double min, double max, string name) =>
        Require(double.IsFinite(x) && x >= min && x <= max, $"Invalid {name}: expected [{min}, {max}].");
    public static void Scenario(Scenario s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.Rules is null || s.Regions is null || s.Species is null || s.InitialStageCounts is null || s.InitialVariantWeights is null)
            throw new InvalidDataException("Scenario contains null required fields.");
        Require(!string.IsNullOrWhiteSpace(s.Name), "Scenario name is required.");
        Require(s.SchemaVersion == 1, "Unsupported scenario schema version.");
        Require(s.Regions.Length is > 0 and <= 1000 && s.Species.Length is > 0 and <= 100,
            "Unsupported region/species count.");
        Require(JuvenileAges.Options(s).JuvenileBinMonths is 0 or 1 or 12, "Juvenile bin months must be 0 (geometric reference), 1 or 12.");
        Require(s.InitialStageCounts.Length == 3 && s.InitialStageCounts.All(n => n is >= 0 and <= 1000000000), "Invalid initial life stages.");
        Require(s.InitialVariantWeights.Length == 3 && s.InitialVariantWeights.All(v => double.IsFinite(v) && v >= 0)
            && Math.Abs(s.InitialVariantWeights.Sum() - 1) < 1e-9, "Trait weights must sum to one.");
        Range(s.InitialStockFraction, 0, 1, "initial stock fraction");
        if (s.InitialPopulations is not null)
        {
            int previous = -1;
            foreach (var entry in s.InitialPopulations)
            {
                if (entry is null || entry.StageCounts is null) throw new InvalidDataException("Null initial population entry.");
                Require(entry.Region.Value >= 0 && entry.Region.Value < s.Regions.Length && entry.Species.Value >= 0 && entry.Species.Value < s.Species.Length, "Unknown initial population region/species.");
                int key = entry.Region.Value * s.Species.Length + entry.Species.Value;
                Require(key > previous, "Initial populations must be unique and ordered by region then species."); previous = key;
                Require(entry.StageCounts.Length == 3 && entry.StageCounts.All(n => n is >= 0 and <= 1000000000), "Invalid initial population stage counts.");
                if (entry.StageVariantCounts is not null)
                {
                    Require(entry.VariantWeights is null && entry.StageVariantCounts.Length == 3, "Explicit stage/variant counts require three rows and cannot also specify variant weights.");
                    for (int a = 0; a < 3; a++)
                    {
                        var row = entry.StageVariantCounts[a];
                        Require(row is not null && row.Length == 3 && row.All(n => n is >= 0 and <= 1000000000), "Invalid explicit trait counts.");
                        Require(row!.Sum() == entry.StageCounts[a], "Explicit trait counts must sum to their stage count.");
                    }
                }
                if (entry.VariantWeights is not null)
                    Require(entry.VariantWeights.Length == 3 && entry.VariantWeights.All(v => double.IsFinite(v) && v >= 0 && v <= 1)
                        && Math.Abs(entry.VariantWeights.Sum() - 1) < 1e-9, "Invalid initial population variant weights.");
            }
        }
        for (int i = 0; i < s.Regions.Length; i++)
        {
            var r = s.Regions[i];
            if (r is null || r.Neighbors is null) throw new InvalidDataException("Region or edges cannot be null.");
            Require(!string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Terrain), "Region metadata required.");
            if (r.Neighbors.Any(e => e is null)) throw new InvalidDataException("Edge cannot be null.");
            Require(r.Id.Value == i, "Regions must have contiguous, canonical IDs starting at zero.");
            Range(r.Area, .01, 1e6, "area"); Range(r.Fertility, 0, 1, "fertility");
            Range(r.Capacity, 0, 1e12, "capacity"); Range(r.BaseTemperature, -80, 80, "temperature");
            Range(r.BaseRainfall, 0, 1000, "rainfall");
            Require(r.Neighbors.Select(e => e.Destination.Value).SequenceEqual(r.Neighbors.Select(e => e.Destination.Value).Distinct().Order()), "Edges must be sorted and unique.");
            foreach (var e in r.Neighbors)
            {
                Require(e.Destination.Value >= 0 && e.Destination.Value < s.Regions.Length && e.Destination != r.Id, "Invalid graph edge.");
                Range(e.Cost, .01, 1000, "travel cost");
            }
        }
        for (int i = 0; i < s.Species.Length; i++)
        {
            var d = s.Species[i];
            if (d is null) throw new InvalidDataException("Species cannot be null.");
            Require(!string.IsNullOrWhiteSpace(d.Name), "Species name required.");
            Require(d.Id.Value == i, "Species must have contiguous, canonical IDs starting at zero.");
            Range(d.FoodDemand, .01, 100, "food demand");
            Range(d.MaturationMonths, 1, 12000, "maturation"); Range(d.AdultMonths, 1, 12000, "adult duration");
            Range(d.ElderMonths, 1, 12000, "elder duration"); Range(d.AnnualBirthRate, 0, 1, "birth rate");
            Range(d.NaturalMortality, 0, 1, "mortality"); Range(d.MigrationRate, 0, 1, "migration rate");
            Range(d.MaxTravelCost, 0, 1000, "travel limit"); Range(d.ColdTolerance, -100, 100, "cold tolerance");
            Range(d.HeatTolerance, d.ColdTolerance, 120, "heat tolerance");
        }
        var p = s.Rules;
        int quantum = JuvenileAges.Options(s).JuvenileBinMonths;
        if (quantum > 0) Require(s.Species.Sum(d => (long)JuvenileAges.Length(d.MaturationMonths, quantum)) * s.Regions.Length * 3 <= 2000000,
            "Scenario exceeds the supported two million juvenile age cells.");
        Range(p.Productivity, 0, 100, "productivity"); Range(p.RainMultiplier, 0, 10, "rain multiplier");
        Range(p.DroughtMultiplier, 0, 1, "drought multiplier");
        Require(p.DroughtStart >= 0 && p.DroughtEnd >= p.DroughtStart, "Invalid drought interval.");
        Range(p.TemperatureOffset, -100, 100, "temperature offset");
        Range(p.StarvationMortality, 0, 1, "starvation mortality"); Range(p.ClimateMortality, 0, 1, "climate mortality");
        Range(p.MutationRate, 0, 1, "mutation rate"); Range(p.ResistanceDegrees, 0, 30, "resistance degrees");
        Range(p.ResistanceFoodCost, 0, 1, "resistance food cost");
        Range(p.FamineStart, 0, .99, "famine start"); Range(p.FamineEnd, p.FamineStart + .001, 1, "famine end");
        Range(p.MigrationThreshold, 0, 10, "migration threshold"); Range(p.TravelPenalty, 0, 10, "travel penalty");
    }
    public static void State(WorldState w)
    {
        ArgumentNullException.ThrowIfNull(w);
        if (w.Scenario is null || w.Regions is null || w.Cohorts is null || w.Memory is null || w.EventHash is null || w.Windows is null || w.Identity is null)
            throw new InvalidDataException("Snapshot contains null required fields.");
        Require(w.SchemaVersion == Simulation.SnapshotSchema && w.EngineVersion == Simulation.Version, "Incompatible snapshot schema or engine version.");
        Require(w.RngAlgorithm == DeterministicRandom.Algorithm, "Incompatible random stream version.");
        Scenario(w.Scenario);
        Require(w.RulesetHash == Persistence.Hash(w.Scenario), "Ruleset hash mismatch.");
        Require(w.CurrentTick >= 0 && w.EventCount >= 0 && w.EventHash.Length == 64 && w.EventHash.All(Uri.IsHexDigit), "Invalid clock/event cursor.");
        var nr = w.Scenario.Regions.Length; var ns = w.Scenario.Species.Length;
        int quantum = JuvenileAges.Options(w.Scenario).JuvenileBinMonths;
        Require(w.Regions.Length == nr && w.Cohorts.Length == nr * ns * 9 && w.Memory.Length == nr * ns && w.Windows.Length == nr * ns, "Incomplete canonical state.");
        IdentityValidation.State(w);
        var identified = new int[w.Cohorts.Length];
        foreach (var person in w.Identity.Living)
        {
            var c = person.Cohort;
            identified[Simulation.Index(c.Region.Value, c.Species.Value, c.Stage, c.Variant, ns)]++;
        }
        for (int r = 0; r < nr; r++)
        {
            var region = w.Regions[r];
            if (region is null) throw new InvalidDataException("Region state cannot be null.");
            Require(region.Id.Value == r, "Invalid region state order.");
            Range(region.Forage, 0, w.Scenario.Regions[r].Capacity, "stock");
            Range(region.Temperature, -200, 200, "current temperature"); Range(region.Rainfall, 0, 12000, "current rainfall");
            for (int s = 0; s < ns; s++)
            {
                var m = w.Memory[r * ns + s];
                if (m is null) throw new InvalidDataException("Group memory cannot be null.");
                Require(m.Region.Value == r && m.Species.Value == s && m.HungryMonths is >= 0 and <= 120, "Invalid group memory.");
                long population = 0;
                for (int a = 0; a < 3; a++) for (int v = 0; v < 3; v++)
                {
                    var c = w.Cohorts[Simulation.Index(r, s, a, v, ns)];
                    if (c is null) throw new InvalidDataException("Cohort cannot be null.");
                    Require(c.Id == new CohortId(new(r), new(s), a, v) && c.Count is >= 0 and <= 1000000000000, "Invalid cohort identity or count.");
                    if (a == 0 && quantum > 0)
                    {
                        Require(c.JuvenileBins is not null && c.JuvenileBins.Length == JuvenileAges.Length(w.Scenario.Species[s].MaturationMonths, quantum), "Invalid juvenile age queue length.");
                        long total = 0;
                        foreach (long n in c.JuvenileBins!) { Require(n is >= 0 and <= 1000000000000, "Invalid juvenile age count."); total += n; }
                        Require(total == c.Count, "Juvenile bins must partition, not duplicate, the cohort count.");
                        if (w.CurrentTick % quantum == 0) Require(c.JuvenileBins[w.CurrentTick / quantum % c.JuvenileBins.Length] == 0, "Expired juvenile deadline.");
                    }
                    else Require(c.JuvenileBins is null, "Unexpected age bins on a geometric or non-juvenile cohort.");
                    long combined = c.Count + identified[Simulation.Index(r, s, a, v, ns)];
                    Require(combined <= 1000000000000, "Combined cohort count exceeds supported limit.");
                    population += combined;
                }
                var window = w.Windows[r * ns + s];
                if (window is null) throw new InvalidDataException("Null population window.");
                Require(window.Region.Value == r && window.Species.Value == s && window.StartTick == w.CurrentTick / 12 * 12,
                    "Invalid population window identity/clock.");
                Require(window.Start is >= 0 and <= 1000000000000000 && window.Births is >= 0 and <= 1000000000000000
                    && window.Deaths is >= 0 and <= 1000000000000000 && window.Inward is >= 0 and <= 1000000000000000 && window.Outward is >= 0 and <= 1000000000000000
                    && population == window.Start + window.Births - window.Deaths + window.Inward - window.Outward, "Annual population accounting failed.");
                Range(window.Demand, 0, 1e18, "window demand"); Range(window.Consumed, 0, window.Demand + 1e-6, "window consumed");
                Require(window.Matured is >= 0 and <= 1000000000000000, "Invalid maturation accounting.");
            }
        }
    }
    public static void Transition(TickResult result)
    {
        foreach (var f in result.Populations)
        {
            Require(f.End == f.Start + f.Births - f.Deaths + f.Inward - f.Outward, "Population accounting failed.");
            Require(f.End >= 0 && f.Births >= 0 && f.Deaths >= 0 && f.Inward >= 0 && f.Outward >= 0, "Negative population flow.");
            Require(f.Matured >= 0 && f.Matured <= f.Start - f.Deaths, "Invalid monthly maturation count.");
            Require(f.Consumed >= 0 && f.Consumed <= f.Demand + 1e-7, "Double consumption.");
            Require(f.TraitCounts.Sum() == f.End, "Trait accounting failed.");
        }
        foreach (var f in result.Food)
            Require(f.End >= 0 && f.Regrowth >= 0 && Math.Abs(f.End - (f.Start + f.Regrowth - f.Consumed)) <= 1e-6 * Math.Max(1, f.Start + f.Regrowth), "Food accounting failed.");
        Require(result.Populations.Sum(f => f.Inward) == result.Populations.Sum(f => f.Outward), "Migration conservation failed.");
        foreach (var t in result.Transfers)
            Require(t.Count > 0 && result.State.Scenario.Regions[t.Origin.Region.Value].Neighbors.Any(e => e.Destination == t.Destination && e.Cost == t.Cost)
                && t.Cost <= result.State.Scenario.Species[t.Origin.Species.Value].MaxTravelCost, "Illegal migration.");
        foreach (var e in result.Events)
            Require(e.ActorRefs.All(s => s.Value >= 0 && s.Value < result.State.Scenario.Species.Length)
                && e.RegionRefs.All(r => r.Value >= 0 && r.Value < result.State.Regions.Length), "Unresolved event reference.");
    }
}
