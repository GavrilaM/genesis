using Genesis.Core;

namespace Genesis.Cli;

public static class CognitionArchive
{
    public static void Verify(string directory, WorldState initial, WorldState final)
    {
        Validation.Require(Persistence.Hash(initial.Cognition.Options) == Persistence.Hash(final.Cognition.Options), "Cognition configuration changed within run.");
        long count = initial.Cognition.EventCount; string hash = initial.Cognition.EventHash;
        int tick = initial.CurrentTick; long lastPerson = 0;
        var minds = initial.Cognition.Minds.ToDictionary(m => m.Person);
        var people = final.Identity.Living.Concat(final.Identity.Deceased).ToDictionary(p => p.Id);
        var migrations = File.ReadLines(Path.Combine(directory, "person-events.jsonl")).Select(Persistence.Decode<PersonEvent>)
            .Where(e => e.Reason == "explicit-intent").ToDictionary(e => e.Sequence);
        var referenced = new HashSet<long>();
        foreach (var line in File.ReadLines(Path.Combine(directory, "cognition-events.jsonl")))
        {
            var ev = Persistence.Decode<CognitionEvent>(line);
            Validation.Require(ev.Sequence == ++count && ev.Tick > initial.CurrentTick && ev.Tick >= tick && ev.Tick <= final.CurrentTick
                && (ev.Tick > tick || ev.Person.Value > lastPerson), "Broken cognition event sequence/order.");
            Validation.Require(people.TryGetValue(ev.Person, out var p) && ev.Tick > p.PromotedTick
                && (p.DeathTick is null || ev.Tick < p.DeathTick), "Cognition event has no surviving identity.");
            var o = ev.Observation;
            Validation.Require(o.Person == ev.Person && o.Tick == ev.Tick && ev.Policy == final.Cognition.Options.Policy
                && o.Source.Species == p.Cohort.Species && o.Source.Variant == p.Cohort.Variant
                && o.Source.Region.Value >= 0 && o.Source.Region.Value < final.Regions.Length, "Invalid cognition observation reference.");
            var exits = final.Scenario.Regions[o.Source.Region.Value].Neighbors.Where(e => e.Cost <= final.Scenario.Species[o.Source.Species.Value].MaxTravelCost)
                .Select(e => new LocalExit(e.Destination, e.Cost)).ToArray();
            Validation.Require(Persistence.Encode(exits) == Persistence.Encode(o.Exits), "Observation contains invalid exits.");
            minds.TryGetValue(ev.Person, out var prior);
            Validation.Require(Persistence.Encode(prior?.Memory ?? []) == Persistence.Encode(ev.PriorMemory), "Cognition memory provenance mismatch.");
            var advance = MindPolicy.Advance(o, prior, final.Cognition.Options);
            Validation.Require(Persistence.Hash(advance.Mind.Memory) == ev.MemoryAfterHash && advance.Reason == ev.Reason
                && advance.Intent == ev.Intent, "Cognition policy replay mismatch.");
            if (ev.Intent is { } intent)
            {
                Validation.Require(ev.Resolution is { Accepted: true } && ev.Resolution.Intent == intent, "Built-in decision was not resolved exactly once.");
                if (intent.Choice == MovementChoice.Move)
                {
                    Validation.Require(ev.MigrationEventSequence is { } sequence && migrations.TryGetValue(sequence, out var move)
                        && referenced.Add(sequence) && move.Person == ev.Person && move.Tick == ev.Tick
                        && move.Before == intent.Source && move.After == intent.Source with { Region = intent.Destination!.Value }, "Decision/migration link mismatch.");
                }
                else Validation.Require(ev.MigrationEventSequence is null, "Stay created a migration.");
            }
            else Validation.Require(ev.Resolution is null && ev.MigrationEventSequence is null, "ObserveOnly record has an override.");
            minds[ev.Person] = advance.Mind;
            tick = ev.Tick; lastPerson = ev.Person.Value; hash = Persistence.Hash(new { previous = hash, ev });
        }
        var finalLiving = final.Identity.Living.Select(p => p.Id).ToHashSet();
        var expectedMinds = minds.Values.Where(m => finalLiving.Contains(m.Person)).OrderBy(m => m.Person.Value).ToArray();
        Validation.Require(Persistence.Encode(expectedMinds) == Persistence.Encode(final.Cognition.Minds), "Cognition final memory mismatch.");
        Validation.Require(count == final.Cognition.EventCount && hash == final.Cognition.EventHash && referenced.Count == migrations.Count,
            "Cognition history count/hash or migration coverage mismatch.");
    }
}
