namespace Genesis.Core;

// The only representation bridge. Combined arrays are derived, never persisted
// alongside their owners. The simulation commits anonymous remainders + people.
public static class PopulationOwnership
{
    public static Cohort[] CombinedCohorts(WorldState w)
    {
        var combined = Copy(w.Cohorts);
        foreach (var p in w.Identity.Living) Adjust(combined, p, w.Scenario.Species.Length, 1);
        return combined;
    }

    internal static Cohort[] Copy(Cohort[] source) => source.Select(c => c with
        { JuvenileBins = c.JuvenileBins is null ? null : (long[])c.JuvenileBins.Clone() }).ToArray();

    internal static void Adjust(Cohort[] cohorts, IdentifiedPerson p, int speciesCount, int delta)
    {
        int i = Simulation.Index(p.Cohort.Region.Value, p.Cohort.Species.Value, p.Cohort.Stage, p.Cohort.Variant, speciesCount);
        var c = cohorts[i];
        cohorts[i] = c with { Count = checked(c.Count + delta) };
        if (p.Cohort.Stage == 0) c.JuvenileBins![(int)(p.MaturationTick!.Value % c.JuvenileBins.Length)] += delta;
    }

    public static string BiologicalHash(WorldState w)
    {
        Validation.State(w);
        return Persistence.Hash(new { w.Seed, w.CurrentTick, w.RngAlgorithm, w.Scenario,
            w.Regions, cohorts = CombinedCohorts(w), w.Memory, w.Windows, w.EventCount, w.EventHash });
    }

    // Includes exact monthly evidence, not just final population. Identity state,
    // budgets and identity history are intentionally outside this projection.
    public static string BiologicalHash(TickResult result) => Persistence.Hash(new
    {
        state = BiologicalHash(result.State), result.Populations, result.Food,
        result.Transfers, result.Events, result.Summaries
    });

    public static IdentifiedPerson? InspectPerson(WorldState w, PersonId id) =>
        w.Identity.Living.FirstOrDefault(p => p.Id == id) ?? w.Identity.Deceased.FirstOrDefault(p => p.Id == id);

    // Explicit command planning for CLI/benchmarks; calling this is read-only.
    // Round-robin cohorts, earliest available deadline within each juvenile cohort.
    public static IdentityCommand[] PlanPromotions(WorldState w, int count, int active = 0)
    {
        Validation.State(w);
        Validation.Require(count > 0 && count <= w.Identity.Options.MaxIdentified - w.Identity.Living.Length
            && active >= 0 && active <= count && active <= w.Identity.Options.MaxActiveMinds - w.Identity.Living.Count(p => p.Active), "Invalid requested identity counts.");
        Validation.Require(JuvenileAges.Options(w.Scenario).JuvenileBinMonths == 1, "Identities require monthly age bins.");
        var cohorts = Copy(w.Cohorts); var commands = new List<IdentityCommand>(); int cursor = 0;
        while (commands.Count < count)
        {
            int searched = 0;
            while (cohorts[cursor].Count == 0 && searched++ < cohorts.Length) cursor = (cursor + 1) % cohorts.Length;
            Validation.Require(cohorts[cursor].Count > 0, "Insufficient anonymous population for requested promotions.");
            var c = cohorts[cursor]; long? due = null;
            if (c.JuvenileBins is { } bins)
            {
                for (int offset = 1; offset < bins.Length; offset++)
                {
                    long candidate = (long)w.CurrentTick + offset;
                    int bin = (int)(candidate % bins.Length);
                    if (bins[bin] == 0) continue;
                    due = candidate; bins[bin]--; break;
                }
            }
            cohorts[cursor] = c with { Count = c.Count - 1 };
            commands.Add(new(checked(w.Identity.LastCommandSequence + commands.Count + 1), w.CurrentTick, "promote", c.Id, due, Active: commands.Count < active));
            cursor = (cursor + 1) % cohorts.Length;
        }
        return commands.ToArray();
    }

    // Phase 1B receives this capability, not WorldState. Exits expose topology and
    // travel cost, never neighbor stock/weather/forecasts. Unknown is not unsafe:
    // a future policy may choose to explore an exit without knowing its resources.
    public static LocalObservation ObserveLocal(WorldState w, PersonId id)
    {
        var p = w.Identity.Living.FirstOrDefault(p => p.Id == id)
            ?? throw new InvalidDataException("No living person with that ID.");
        int r = p.Cohort.Region.Value;
        var region = w.Regions[r];
        return new(w.CurrentTick, id, region.Id, region.Temperature, region.Rainfall, region.Forage,
            w.Cohorts.Where(c => c.Id.Region == region.Id).Sum(c => c.Count) + w.Identity.Living.Count(x => x.Cohort.Region == region.Id),
            w.Scenario.Regions[r].Neighbors.Where(e => e.Cost <= w.Scenario.Species[p.Cohort.Species.Value].MaxTravelCost)
                .Select(e => new LocalExit(e.Destination, e.Cost)).ToArray());
    }

    public static IdentityTransaction Apply(WorldState input, IReadOnlyList<IdentityCommand> commands)
    {
        Validation.State(input);
        ArgumentNullException.ThrowIfNull(commands);
        Validation.Require(commands.Count is > 0 and <= 10000, "An identity transaction requires 1..10000 commands.");
        var cohorts = Copy(input.Cohorts);
        var living = input.Identity.Living.ToList();
        var events = new List<PersonEvent>();
        long sequence = input.Identity.LastCommandSequence, nextId = input.Identity.NextPersonId;
        foreach (var command in commands)
        {
            Validation.Require(command is not null, "Null identity command.");
            Validation.Require(command!.Sequence == checked(sequence + 1) && command.Tick == input.CurrentTick,
                "Identity commands must be consecutive, unrepeated and at the current tick boundary.");
            IdentifiedPerson person;
            if (command.Type == "promote")
            {
                Validation.Require(command.Source is not null && command.Person is null, "Promotion requires only a source bucket.");
                Validation.Require(JuvenileAges.Options(input.Scenario).JuvenileBinMonths == 1, "Identities require monthly juvenile deadlines.");
                Validation.Require(living.Count < input.Identity.Options.MaxIdentified, "Living identity budget is full; population is unchanged.");
                var source = command.Source!.Value;
                ValidateCohort(source, input.Scenario);
                int i = Simulation.Index(source.Region.Value, source.Species.Value, source.Stage, source.Variant, input.Scenario.Species.Length);
                Validation.Require(cohorts[i].Count > 0, "No anonymous person available in source cohort.");
                long? birth = null; var provenance = AgeProvenance.Unknown;
                if (source.Stage == 0)
                {
                    int maturation = input.Scenario.Species[source.Species.Value].MaturationMonths;
                    Validation.Require(command.JuvenileDeadline > input.CurrentTick && command.JuvenileDeadline <= (long)input.CurrentTick + maturation,
                        "Promotion must specify an existing juvenile deadline.");
                    int bin = (int)(command.JuvenileDeadline!.Value % cohorts[i].JuvenileBins!.Length);
                    Validation.Require(cohorts[i].JuvenileBins![bin] > 0, "No anonymous juvenile at that deadline.");
                    birth = command.JuvenileDeadline.Value - maturation;
                    provenance = birth <= 0 ? AgeProvenance.InitialCondition : AgeProvenance.SimulatedBirth;
                }
                else Validation.Require(command.JuvenileDeadline is null, "Unknown adult/elder ages cannot be supplied or invented.");
                person = new(new(nextId), source, input.CurrentTick, command.JuvenileDeadline, birth, provenance, command.Active);
                nextId = checked(nextId + 1);
                Adjust(cohorts, person, input.Scenario.Species.Length, -1);
                living.Add(person);
            }
            else if (command.Type == "set-active")
            {
                Validation.Require(command.Person is not null && command.Source is null && command.JuvenileDeadline is null,
                    "set-active requires only a living person ID.");
                int index = living.FindIndex(p => p.Id == command.Person);
                Validation.Require(index >= 0, "Cannot activate/deactivate an absent or deceased person.");
                person = living[index] with { Active = command.Active }; living[index] = person;
            }
            else throw new InvalidDataException("Unknown identity command; expected promote or set-active.");
            Validation.Require(living.Count(p => p.Active) <= input.Identity.Options.MaxActiveMinds, "Active mind reservation budget is full.");
            sequence = command.Sequence;
            events.Add(new(input.Identity.EventCount + events.Count + 1, input.CurrentTick, person.Id,
                command.Type, person.Cohort, person.Cohort, command.Active ? "active-reserved" : "passive", sequence));
        }
        var identity = WithEvents(input.Identity with { Living = living.ToArray(), NextPersonId = nextId, LastCommandSequence = sequence }, events);
        var result = input with { Cohorts = cohorts, Identity = identity };
        Validation.State(result);
        Validation.Require(BiologicalHash(input) == BiologicalHash(result), "Representation transaction changed biology.");
        return new(result, events.ToArray());
    }

    internal static IdentityState WithEvents(IdentityState identity, IEnumerable<PersonEvent> events)
    {
        string hash = identity.EventHash; long count = identity.EventCount;
        foreach (var ev in events)
        {
            Validation.Require(ev.Sequence == checked(++count), "Identity event sequence is not contiguous.");
            hash = Persistence.Hash(new { previous = hash, ev });
        }
        return identity with { EventCount = count, EventHash = hash };
    }

    internal static void ValidateCohort(CohortId c, Scenario s) => Validation.Require(
        c.Region.Value >= 0 && c.Region.Value < s.Regions.Length && c.Species.Value >= 0 && c.Species.Value < s.Species.Length
        && c.Stage is >= 0 and <= 2 && c.Variant is >= 0 and <= 2, "Invalid person cohort.");
}
