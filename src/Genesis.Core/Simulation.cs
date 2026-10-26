namespace Genesis.Core;

public static class Simulation
{
    public const string Version = "0.3.0";
    public const int SnapshotSchema = 3;
    public static int Index(int r, int s, int a, int v, int ns) => ((r * ns + s) * 3 + a) * 3 + v;

    public static WorldState Create(Scenario scenario, ulong seed)
    {
        // Own a detached copy: caller edits to configuration cannot alter a running world.
        var s = Persistence.Decode<Scenario>(Persistence.Encode(scenario));
        Validation.Scenario(s);
        var demography = JuvenileAges.Options(s);
        var cohorts = new List<Cohort>(); var memory = new List<GroupMemory>();
        var windows = new List<PopulationWindow>();
        var configured = s.InitialPopulations?.ToDictionary(p => (p.Region, p.Species));
        foreach (var r in s.Regions) foreach (var sp in s.Species)
        {
            memory.Add(new(r.Id, sp.Id, 0, false));
            var entry = configured?.GetValueOrDefault((r.Id, sp.Id));
            var stages = configured is null ? s.InitialStageCounts : entry?.StageCounts ?? [0L, 0L, 0L];
            var weights = entry?.VariantWeights ?? s.InitialVariantWeights;
            windows.Add(new(r.Id, sp.Id, 0, stages.Sum(), 0, 0, 0, 0, 0, 0));
            for (int a = 0; a < 3; a++)
            {
                long remaining = stages[a];
                for (int v = 0; v < 3; v++)
                {
                    long n = entry?.StageVariantCounts is not null ? entry.StageVariantCounts[a][v]
                        : v == 2 ? remaining : (long)Math.Floor(stages[a] * weights[v]);
                    remaining -= n;
                    cohorts.Add(new(new(r.Id, sp.Id, a, v), n, a == 0 && demography.JuvenileBinMonths > 0
                        ? JuvenileAges.Initialize(n, sp.MaturationMonths, demography.JuvenileBinMonths, demography.InitialJuvenilesAreNewborn) : null));
                }
            }
        }
        var w = new WorldState(SnapshotSchema, Version, Persistence.Hash(s), seed, 0, DeterministicRandom.Algorithm, s,
            s.Regions.Select(r => new RegionState(r.Id, r.BaseTemperature, r.BaseRainfall, r.Capacity * s.InitialStockFraction)).ToArray(),
            cohorts.ToArray(), memory.ToArray(), 0, Persistence.EmptyEventHash, windows.ToArray());
        Validation.State(w);
        return w;
    }

    public static TickResult Step(WorldState w, bool validate = true, bool audit = false)
    {
        if (validate) Validation.State(w);
        int nr = w.Regions.Length, ns = w.Scenario.Species.Length, tick = checked(w.CurrentTick + 1);
        var rules = w.Scenario.Rules;
        var demography = JuvenileAges.Options(w.Scenario); int quantum = demography.JuvenileBinMonths;
        var ageBins = w.Cohorts.Select(c => c.JuvenileBins is null ? null : (long[])c.JuvenileBins.Clone()).ToArray();
        var regions = new RegionState[nr]; var food = new FoodFlow[nr];
        var memory = new GroupMemory[nr * ns]; var flows = new PopulationFlow[nr * ns];
        var counts = new long[w.Cohorts.Length]; var events = new List<SimEvent>();
        List<CohortAudit>? auditRows = audit ? [] : null;
        double Rand(int system, int r, int s = 0, int draw = 0) => DeterministicRandom.Unit(w.Seed, tick, system, r, s, draw);
        long Round(double expected, int system, int r, int s, int draw) => DeterministicRandom.Round(expected, Rand(system, r, s, draw));
        long Sample(long n, double p, int system, int r, int s, int draw) => demography.BinomialEvents
            ? EventSampling.Binomial(n, p, w.Seed, tick, system, r, s, draw) : Round(n * p, system, r, s, draw);
        long Total(int r, int s, long[]? source = null)
        {
            long sum = 0;
            for (int a = 0; a < 3; a++) for (int v = 0; v < 3; v++)
            {
                int i = Index(r, s, a, v, ns); sum += source is null ? w.Cohorts[i].Count : source[i];
            }
            return sum;
        }
        double Demand(int r, int s, long[]? source = null)
        {
            double sum = 0;
            for (int a = 0; a < 3; a++) for (int v = 0; v < 3; v++)
            {
                int i = Index(r, s, a, v, ns);
                sum += (source is null ? w.Cohorts[i].Count : source[i]) * w.Scenario.Species[s].FoodDemand * (1 + v * rules.ResistanceFoodCost);
            }
            return sum;
        }
        double Exposure(int r, int s, int v) => Math.Max(0, w.Scenario.Species[s].ColdTolerance - v * rules.ResistanceDegrees - regions[r].Temperature)
            + Math.Max(0, regions[r].Temperature - w.Scenario.Species[s].HeatTolerance);
        void Emit(string type, int r, int s, double before, double after, Evidence[] evidence, string system, string reason, string severity)
            => events.Add(new(new(w.EventCount + events.Count + 1), tick, type, [new(s)], r < 0 ? [] : [new(r)], before, after, evidence, system, reason, severity));

        // Environment and proportional finite food allocation use start-of-month populations.
        for (int r = 0; r < nr; r++)
        {
            var def = w.Scenario.Regions[r];
            double temp = EnvironmentModel.MeanTemperature(def, rules, tick) + (Rand(1, r) - .5) * 4;
            double rain = EnvironmentModel.MeanRainfall(def, rules, tick) * (.8 + .4 * Rand(1, r, draw: 1));
            double regrowth = Math.Min(def.Capacity - w.Regions[r].Forage,
                EnvironmentModel.Production(def, rules, temp, rain));
            double available = w.Regions[r].Forage + regrowth;
            var demands = Enumerable.Range(0, ns).Select(s => Demand(r, s)).ToArray();
            double totalDemand = demands.Sum();
            double nutrition = totalDemand == 0 ? 1 : Math.Min(1, available / totalDemand);
            double consumed = Math.Min(available, totalDemand);
            regions[r] = new(def.Id, temp, rain, Math.Max(0, available - consumed));
            food[r] = new(def.Id, w.Regions[r].Forage, regrowth, consumed, regions[r].Forage, temp, rain);

            for (int s = 0; s < ns; s++)
            {
                int g = r * ns + s; var sp = w.Scenario.Species[s]; var previous = w.Memory[g];
                long start = Total(r, s), deaths = 0, births = 0, matured = 0;
                int hungry = start == 0 ? 0 : nutrition < .8 ? Math.Min(120, previous.HungryMonths + 1) : Math.Max(0, previous.HungryMonths - 1);
                bool famine = start > 0 && (previous.Famine ? nutrition < rules.FamineEnd : nutrition < rules.FamineStart);
                memory[g] = new(new(r), new(s), hungry, famine);
                var survivors = new long[9];
                for (int a = 0; a < 3; a++) for (int v = 0; v < 3; v++)
                {
                    int i = Index(r, s, a, v, ns), slot = a * 3 + v;
                    double deathRate = Math.Clamp(sp.NaturalMortality * (a == 0 ? 1.5 : 1)
                        + (a == 2 ? 1.0 / sp.ElderMonths : 0)
                        + rules.StarvationMortality * (1 - nutrition) * (1 + Math.Min(hungry, 12) / 12.0)
                        + rules.ClimateMortality * Exposure(r, s, v), 0, 1);
                    long dead;
                    if (ageBins[i] is { } bins)
                    {
                        dead = 0;
                        if (demography.BinomialEvents)
                            for (int b = 0; b < bins.Length; b++)
                            {
                                if (bins[b] == 0) continue;
                                long loss = Sample(bins[b], deathRate, 12, r, s, v * 12002 + b);
                                bins[b] -= loss; dead += loss;
                            }
                        else
                        {
                            dead = Round(w.Cohorts[i].Count * deathRate, 2, r, s, slot);
                            var losses = JuvenileAges.Allocate(bins, w.Cohorts[i].Count, dead, Rand(13, r, s, v));
                            for (int b = 0; b < bins.Length; b++) bins[b] -= losses[b];
                        }
                    }
                    else dead = Sample(w.Cohorts[i].Count, deathRate, 2, r, s, slot);
                    survivors[slot] = w.Cohorts[i].Count - dead; deaths += dead;
                    auditRows?.Add(new(w.Cohorts[i].Id, w.Cohorts[i].Count, Exposure(r, s, v), deathRate, dead, survivors[slot]));
                }
                // Age existing survivors once. New adults do not reproduce until next month.
                for (int a = 0; a < 3; a++) for (int v = 0; v < 3; v++)
                {
                    long alive = survivors[a * 3 + v];
                    long age;
                    if (a == 0 && ageBins[Index(r, s, a, v, ns)] is { } bins)
                    {
                        int due = tick / quantum % bins.Length;
                        age = tick % quantum == 0 ? bins[due] : 0;
                        if (tick % quantum == 0) bins[due] = 0;
                    }
                    else age = a == 2 ? 0 : Round(alive / (double)(a == 0 ? sp.MaturationMonths : sp.AdultMonths), 3, r, s, a * 3 + v);
                    if (a == 0) matured += age;
                    counts[Index(r, s, a, v, ns)] += alive - age;
                    if (a < 2) counts[Index(r, s, a + 1, v, ns)] += age;
                }
                for (int v = 0; v < 3; v++)
                {
                    long children = Sample(survivors[3 + v], sp.AnnualBirthRate / 12 * nutrition * nutrition
                        * Math.Clamp(1 - Exposure(r, s, v) / 30, 0, 1), 4, r, s, v);
                    long mutations = Math.Min(children, Round(children * rules.MutationRate, 5, r, s, v));
                    counts[Index(r, s, 0, v, ns)] += children - mutations;
                    long left = Round(mutations * .5, 6, r, s, v);
                    counts[Index(r, s, 0, (v + 1) % 3, ns)] += left;
                    counts[Index(r, s, 0, (v + 2) % 3, ns)] += mutations - left;
                    births += children;
                }
                if (quantum > 0) for (int v = 0; v < 3; v++)
                {
                    int i = Index(r, s, 0, v, ns); var bins = ageBins[i]!;
                    long born = counts[i] - bins.Sum();
                    bins[JuvenileAges.DueSlot(tick, sp.MaturationMonths, quantum, bins.Length)] += born;
                }
                flows[g] = new(new(r), new(s), start, births, deaths, 0, 0, 0, demands[s], demands[s] * nutrition, nutrition, hungry, [], matured);
                if (famine != previous.Famine)
                    Emit(famine ? "famine-start" : "famine-end", r, s, previous.Famine ? 1 : 0, famine ? 1 : 0,
                        [new("nutrition", nutrition), new("availableRations", available), new("regionalDemand", totalDemand), new("rainfallMm", rain), new("hungryMonths", hungry)],
                        "nutrition", famine ? "nutrition-below-start" : start == 0 ? "population-absent" : "nutrition-recovered", famine ? "warning" : "info");
            }
        }

        // Forecast is frozen for all origins; incoming migrants cannot migrate again this month.
        var forecastDemand = Enumerable.Range(0, nr).Select(r => Enumerable.Range(0, ns).Sum(s => Demand(r, s, counts))).ToArray();
        var forecasts = Enumerable.Range(0, nr).Select(r => EnvironmentModel.Forecast(w.Scenario.Regions[r], rules, regions[r], checked(tick + 1))).ToArray();
        double Score(int r, int s, int v)
        {
            var sp = w.Scenario.Species[s]; double temperature = forecasts[r].Temperature;
            double exposure = Math.Max(0, sp.ColdTolerance - v * rules.ResistanceDegrees - temperature)
                + Math.Max(0, temperature - sp.HeatTolerance);
            return Math.Min(2, forecasts[r].Available / Math.Max(1, forecastDemand[r])) - .04 * exposure;
        }
        var transfers = new List<Transfer>(); var delta = new long[counts.Length];
        var ageDelta = ageBins.Select(b => b is null ? null : new long[b.Length]).ToArray();
        for (int r = 0; r < nr; r++) for (int s = 0; s < ns; s++) for (int v = 0; v < 3; v++)
        {
            var sp = w.Scenario.Species[s]; double originScore = Score(r, s, v), bestScore = originScore + rules.MigrationThreshold;
            Edge? best = null;
            foreach (var edge in w.Scenario.Regions[r].Neighbors)
            {
                if (edge.Cost > sp.MaxTravelCost) continue;
                double prospective = Score(edge.Destination.Value, s, v) - edge.Cost * rules.TravelPenalty;
                if (prospective > bestScore) { best = edge; bestScore = prospective; }
            }
            if (best is null) continue;
            double fraction = sp.MigrationRate * Math.Min(1, bestScore - originScore);
            for (int a = 0; a < 3; a++)
            {
                int i = Index(r, s, a, v, ns), to = Index(best.Destination.Value, s, a, v, ns);
                long movers = Math.Min(counts[i], Round(counts[i] * fraction, 7, r, s, a * 3 + v));
                if (movers == 0) continue;
                delta[i] -= movers; delta[to] += movers;
                if (ageBins[i] is { } bins)
                {
                    var moved = JuvenileAges.Allocate(bins, counts[i], movers, Rand(14, r, s, v));
                    for (int b = 0; b < bins.Length; b++) { ageDelta[i]![b] -= moved[b]; ageDelta[to]![b] += moved[b]; }
                }
                transfers.Add(new(w.Cohorts[i].Id, best.Destination, movers, best.Cost, originScore, bestScore));
                int g = r * ns + s, dest = best.Destination.Value * ns + s;
                flows[g] = flows[g] with { Outward = flows[g].Outward + movers };
                flows[dest] = flows[dest] with { Inward = flows[dest].Inward + movers };
            }
        }
        var cohorts = new Cohort[counts.Length];
        for (int i = 0; i < counts.Length; i++)
        {
            counts[i] += delta[i];
            if (ageBins[i] is { } bins) for (int b = 0; b < bins.Length; b++) bins[b] += ageDelta[i]![b];
            cohorts[i] = w.Cohorts[i] with { Count = counts[i], JuvenileBins = ageBins[i] };
        }
        for (int r = 0; r < nr; r++) for (int s = 0; s < ns; s++)
        {
            int g = r * ns + s; var f = flows[g];
            long end = Total(r, s, counts);
            flows[g] = f with { End = end, TraitCounts = Enumerable.Range(0, 3).Select(v => Enumerable.Range(0, 3).Sum(a => counts[Index(r, s, a, v, ns)])).ToArray() };
            if (f.Start >= 20 && end <= f.Start / 2)
                Emit("population-collapse", r, s, f.Start, end, [new("births", f.Births), new("deaths", f.Deaths), new("outward", f.Outward), new("inward", f.Inward), new("nutrition", f.Nutrition)], "population", "monthly-loss-at-least-half", "critical");
        }
        for (int s = 0; s < ns; s++)
        {
            long before = flows.Where(f => f.Species.Value == s).Sum(f => f.Start), after = flows.Where(f => f.Species.Value == s).Sum(f => f.End);
            if (before > 0 && after == 0) Emit("species-extinction", -1, s, before, 0,
                [new("deaths", flows.Where(f => f.Species.Value == s).Sum(f => f.Deaths))], "population", "no-surviving-cohorts", "critical");
        }
        // Annual accounting is canonical state so a mid-year snapshot cannot lose evidence.
        var windows = new PopulationWindow[flows.Length]; var summaries = new List<PopulationSummary>();
        for (int g = 0; g < flows.Length; g++)
        {
            var f = flows[g]; var old = w.Windows[g];
            var sum = old with { Births = old.Births + f.Births, Deaths = old.Deaths + f.Deaths,
                Inward = old.Inward + f.Inward, Outward = old.Outward + f.Outward,
                Demand = old.Demand + f.Demand, Consumed = old.Consumed + f.Consumed, Matured = old.Matured + f.Matured };
            windows[g] = sum;
            if (tick % 12 != 0) continue;
            summaries.Add(new(sum, tick, f.End));
            Evidence[] evidence = [new("fromTick", sum.StartTick), new("births", sum.Births), new("deaths", sum.Deaths),
                new("inward", sum.Inward), new("outward", sum.Outward), new("demandRations", sum.Demand),
                new("consumedRations", sum.Consumed), new("nutrition", sum.Demand == 0 ? 1 : sum.Consumed / sum.Demand), new("matured", sum.Matured)];
            if (Math.Abs(f.End - sum.Start) >= Math.Max(50, sum.Start * .2))
                Emit("population-change", f.Region.Value, f.Species.Value, sum.Start, f.End, evidence,
                    "annual-accounting", "annual-change-at-least-20-percent-and-50", "info");
            // Throughput, not a count of unique migrants; a person may move more than once.
            if (sum.Outward >= Math.Max(50, sum.Start * .5))
                Emit("migration-wave", f.Region.Value, f.Species.Value, sum.Start, f.End, evidence,
                    "annual-accounting", "annual-outflow-at-least-half-start-and-50", "info");
            windows[g] = new(f.Region, f.Species, tick, f.End, 0, 0, 0, 0, 0, 0);
        }
        string eventHash = w.EventHash;
        foreach (var ev in events) eventHash = Persistence.Chain(eventHash, ev);
        var state = w with { CurrentTick = tick, Regions = regions, Cohorts = cohorts, Memory = memory, EventCount = w.EventCount + events.Count, EventHash = eventHash, Windows = windows };
        var result = new TickResult(state, flows, food, transfers.ToArray(), events.ToArray(), auditRows?.ToArray() ?? [], audit ? forecasts : [], summaries.ToArray());
        if (validate) { Validation.State(state); Validation.Transition(result); }
        return result;
    }
}
