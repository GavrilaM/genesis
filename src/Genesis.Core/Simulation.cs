namespace Genesis.Core;

public static class Simulation
{
    public const string Version = "0.1.0";
    public static int Index(int r, int s, int a, int v, int ns) => ((r * ns + s) * 3 + a) * 3 + v;

    public static WorldState Create(Scenario scenario, ulong seed)
    {
        // Own a detached copy: caller edits to configuration cannot alter a running world.
        var s = Persistence.Decode<Scenario>(Persistence.Encode(scenario));
        Validation.Scenario(s);
        var cohorts = new List<Cohort>(); var memory = new List<GroupMemory>();
        foreach (var r in s.Regions) foreach (var sp in s.Species)
        {
            memory.Add(new(r.Id, sp.Id, 0, false));
            for (int a = 0; a < 3; a++)
            {
                long remaining = s.InitialStageCounts[a];
                for (int v = 0; v < 3; v++)
                {
                    long n = v == 2 ? remaining : (long)Math.Floor(s.InitialStageCounts[a] * s.InitialVariantWeights[v]);
                    remaining -= n;
                    cohorts.Add(new(new(r.Id, sp.Id, a, v), n));
                }
            }
        }
        var w = new WorldState(1, Version, Persistence.Hash(s), seed, 0, DeterministicRandom.Algorithm, s,
            s.Regions.Select(r => new RegionState(r.Id, r.BaseTemperature, r.BaseRainfall, r.Capacity * s.InitialStockFraction)).ToArray(),
            cohorts.ToArray(), memory.ToArray(), 0, Persistence.EmptyEventHash);
        Validation.State(w);
        return w;
    }

    public static TickResult Step(WorldState w, bool validate = true, bool audit = false)
    {
        if (validate) Validation.State(w);
        int nr = w.Regions.Length, ns = w.Scenario.Species.Length, tick = checked(w.CurrentTick + 1);
        var rules = w.Scenario.Rules;
        var regions = new RegionState[nr]; var food = new FoodFlow[nr];
        var memory = new GroupMemory[nr * ns]; var flows = new PopulationFlow[nr * ns];
        var counts = new long[w.Cohorts.Length]; var events = new List<SimEvent>();
        List<CohortAudit>? auditRows = audit ? [] : null;
        double Rand(int system, int r, int s = 0, int draw = 0) => DeterministicRandom.Unit(w.Seed, tick, system, r, s, draw);
        long Round(double expected, int system, int r, int s, int draw) => DeterministicRandom.Round(expected, Rand(system, r, s, draw));
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
            double season = Math.Sin(2 * Math.PI * ((tick - 1) % 12) / 12);
            double temp = def.BaseTemperature + rules.TemperatureOffset + 8 * season + (Rand(1, r) - .5) * 4;
            double drought = tick > rules.DroughtStart && tick <= rules.DroughtEnd ? rules.DroughtMultiplier : 1;
            double rain = def.BaseRainfall * rules.RainMultiplier * drought * (.8 + .4 * Rand(1, r, draw: 1));
            double regrowth = Math.Min(def.Capacity - w.Regions[r].Forage,
                def.Area * def.Fertility * rules.Productivity * Math.Min(1.5, rain / 100) * Math.Clamp((temp + 10) / 25, 0, 1));
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
                long start = Total(r, s), deaths = 0, births = 0;
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
                    long dead = Round(w.Cohorts[i].Count * deathRate, 2, r, s, slot);
                    survivors[slot] = w.Cohorts[i].Count - dead; deaths += dead;
                    auditRows?.Add(new(w.Cohorts[i].Id, w.Cohorts[i].Count, Exposure(r, s, v), deathRate, dead, survivors[slot]));
                }
                // Age existing survivors once. New adults do not reproduce until next month.
                for (int a = 0; a < 3; a++) for (int v = 0; v < 3; v++)
                {
                    long alive = survivors[a * 3 + v];
                    long age = a == 2 ? 0 : Round(alive / (double)(a == 0 ? sp.MaturationMonths : sp.AdultMonths), 3, r, s, a * 3 + v);
                    counts[Index(r, s, a, v, ns)] += alive - age;
                    if (a < 2) counts[Index(r, s, a + 1, v, ns)] += age;
                }
                for (int v = 0; v < 3; v++)
                {
                    long children = Round(survivors[3 + v] * sp.AnnualBirthRate / 12 * nutrition * nutrition
                        * Math.Clamp(1 - Exposure(r, s, v) / 30, 0, 1), 4, r, s, v);
                    long mutations = Math.Min(children, Round(children * rules.MutationRate, 5, r, s, v));
                    counts[Index(r, s, 0, v, ns)] += children - mutations;
                    long left = Round(mutations * .5, 6, r, s, v);
                    counts[Index(r, s, 0, (v + 1) % 3, ns)] += left;
                    counts[Index(r, s, 0, (v + 2) % 3, ns)] += mutations - left;
                    births += children;
                }
                flows[g] = new(new(r), new(s), start, births, deaths, 0, 0, 0, demands[s], demands[s] * nutrition, nutrition, hungry, []);
                if (famine != previous.Famine)
                    Emit(famine ? "famine-start" : "famine-end", r, s, previous.Famine ? 1 : 0, famine ? 1 : 0,
                        [new("nutrition", nutrition), new("availableRations", available), new("regionalDemand", totalDemand), new("rainfallMm", rain), new("hungryMonths", hungry)],
                        "nutrition", famine ? "nutrition-below-start" : start == 0 ? "population-absent" : "nutrition-recovered", famine ? "warning" : "info");
            }
        }

        // Forecast is frozen for all origins; incoming migrants cannot migrate again this month.
        var forecastDemand = Enumerable.Range(0, nr).Select(r => Enumerable.Range(0, ns).Sum(s => Demand(r, s, counts))).ToArray();
        double Score(int r, int s, int v) => Math.Min(2, (regions[r].Forage + food[r].Regrowth) / Math.Max(1, forecastDemand[r])) - .04 * Exposure(r, s, v);
        var transfers = new List<Transfer>(); var delta = new long[counts.Length];
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
                transfers.Add(new(w.Cohorts[i].Id, best.Destination, movers, best.Cost, originScore, bestScore));
                int g = r * ns + s, dest = best.Destination.Value * ns + s;
                flows[g] = flows[g] with { Outward = flows[g].Outward + movers };
                flows[dest] = flows[dest] with { Inward = flows[dest].Inward + movers };
            }
        }
        var cohorts = new Cohort[counts.Length];
        for (int i = 0; i < counts.Length; i++) { counts[i] += delta[i]; cohorts[i] = w.Cohorts[i] with { Count = counts[i] }; }
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
        string eventHash = w.EventHash;
        foreach (var ev in events) eventHash = Persistence.Chain(eventHash, ev);
        var state = w with { CurrentTick = tick, Regions = regions, Cohorts = cohorts, Memory = memory, EventCount = w.EventCount + events.Count, EventHash = eventHash };
        var result = new TickResult(state, flows, food, transfers.ToArray(), events.ToArray(), auditRows?.ToArray() ?? []);
        if (validate) { Validation.State(state); Validation.Transition(result); }
        return result;
    }
}
