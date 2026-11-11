namespace Genesis.Core;

// Assign the already sampled biological totals, never resample biology. All
// candidates are frozen at the relevant phase, preventing double aging/migration.
internal sealed class IdentityEvolution
{
    private readonly WorldState input;
    private readonly IdentifiedPerson[] people;
    private readonly Dictionary<(int Cohort, int Bin), int[]> biological;
    private Dictionary<(int Cohort, int Bin), int[]> migration = [];
    private readonly List<PersonEvent> events = [];
    private readonly int tick;

    public IdentityEvolution(WorldState input)
    {
        this.input = input; tick = checked(input.CurrentTick + 1);
        people = (IdentifiedPerson[])input.Identity.Living.Clone();
        biological = Groups();
    }

    public IdentifiedPerson[] Survivors => people.Where(p => p.DeathTick is null).ToArray();
    private Dictionary<(int, int), int[]> Groups(HashSet<PersonId>? excluded = null) => Enumerable.Range(0, people.Length)
        .Where(i => people[i].DeathTick is null && (excluded is null || !excluded.Contains(people[i].Id))).GroupBy(i =>
        {
            var p = people[i]; var c = p.Cohort;
            int index = Simulation.Index(c.Region.Value, c.Species.Value, c.Stage, c.Variant, input.Scenario.Species.Length);
            int bin = c.Stage == 0 ? (int)(p.MaturationTick!.Value % input.Cohorts[index].JuvenileBins!.Length) : -1;
            return (index, bin);
        }).ToDictionary(g => g.Key, g => g.ToArray());

    private void Select(Dictionary<(int, int), int[]> groups, int cohort, int bin,
        long total, long selected, int system, Func<IdentifiedPerson, IdentifiedPerson> change, string type)
    {
        if (selected == 0 || !groups.TryGetValue((cohort, bin), out var candidates)) return;
        var c = input.Cohorts[cohort].Id;
        int slot = bin < 0 ? c.Stage * 3 + c.Variant : c.Variant * 12002 + bin;
        var rng = new DeterministicRandom.Stream(input.Seed, tick, system, c.Region.Value, c.Species.Value, slot);
        foreach (int index in candidates)
        {
            var before = people[index];
            if (before.DeathTick is not null) continue;
            Validation.Require(total > 0 && selected <= total, "Conditional identity allocation exhausted its population.");
            bool chosen = selected == total || rng.Next() < selected / (double)total;
            total--;
            if (!chosen) continue;
            selected--;
            var after = change(before); people[index] = after;
            events.Add(new(input.Identity.EventCount + events.Count + 1, tick, before.Id, type,
                before.Cohort, after.Cohort, "shared-biological-outcome"));
        }
    }

    public void Deaths(int cohort, int bin, long total, long deaths) => Select(biological, cohort, bin, total, deaths, 101,
        p => p with { DeathTick = tick, Active = false }, "death");

    public void Age(int cohort, long total, long aged)
    {
        var c = input.Cohorts[cohort].Id;
        if (c.Stage == 0)
        {
            int bin = tick % input.Cohorts[cohort].JuvenileBins!.Length;
            Select(biological, cohort, bin, aged, aged, 102, p => p with { Cohort = p.Cohort with { Stage = 1 } }, "maturation");
        }
        else if (c.Stage == 1) Select(biological, cohort, -1, total, aged, 102,
            p => p with { Cohort = p.Cohort with { Stage = 2 } }, "elder-transition");
    }

    public void FreezeMigration(HashSet<PersonId>? excluded = null) => migration = Groups(excluded);
    public void MoveExplicit(MovementIntent intent)
    {
        int index = Array.FindIndex(people, p => p.Id == intent.Person && p.DeathTick is null);
        Validation.Require(index >= 0, "Explicit mover lost its living owner.");
        var before = people[index];
        Validation.Require(before.Cohort == intent.Source && before.Active && intent.Destination is not null, "Reserved mover changed before commit.");
        var after = before with { Cohort = before.Cohort with { Region = intent.Destination.Value } };
        people[index] = after;
        events.Add(new(input.Identity.EventCount + events.Count + 1, tick, intent.Person, "migration", before.Cohort, after.Cohort, "explicit-intent"));
    }
    public void Move(int cohort, int bin, long total, long movers, RegionId destination) =>
        Select(migration, cohort, bin, total, movers, 103,
            p => p with { Cohort = p.Cohort with { Region = destination } }, "migration");

    public (IdentityState Identity, PersonEvent[] Events) Commit(Cohort[] combined)
    {
        var living = people.Where(p => p.DeathTick is null).ToArray();
        foreach (var p in living) PopulationOwnership.Adjust(combined, p, input.Scenario.Species.Length, -1);
        var newlyDead = people.Where(p => p.DeathTick is not null).ToArray();
        var deceased = input.Identity.Deceased;
        if (newlyDead.Length > 0)
        {
            // Old and new tombstones are already ordered by ID. Reuse immutable
            // history on no-death ticks; otherwise merge without sorting it again.
            var merged = new IdentifiedPerson[deceased.Length + newlyDead.Length];
            int oldIndex = 0, newIndex = 0;
            for (int i = 0; i < merged.Length; i++)
                merged[i] = newIndex == newlyDead.Length || oldIndex < deceased.Length && deceased[oldIndex].Id.Value < newlyDead[newIndex].Id.Value
                    ? deceased[oldIndex++] : newlyDead[newIndex++];
            deceased = merged;
        }
        var identity = PopulationOwnership.WithEvents(input.Identity with { Living = living, Deceased = deceased }, events);
        return (identity, events.ToArray());
    }
}
