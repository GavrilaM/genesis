namespace Genesis.Core;

public enum CognitionMode { Off, ObserveOnly, Decide }
public sealed record CognitionOptions(CognitionMode Mode = CognitionMode.Off,
    string Policy = "survival-v1", int MemoryCapacity = 8, int MemoryMaxAge = 12, double AdequateNutrition = .8);
public sealed record MemoryObservation(int Tick, RegionId Region, double Nutrition);
public sealed record MindState(PersonId Person, MemoryObservation[] Memory, int LastObservedTick, int? LastDecisionTick);
public sealed record CognitionState(CognitionOptions Options, MindState[] Minds, long EventCount, string EventHash)
{
    public static CognitionState Empty(CognitionOptions? options = null) => new(options ?? new(), [], 0, Persistence.EmptyEventHash);
}
public sealed record FeedingReceipt(int Tick, RegionId Region, double Nutrition);
public sealed record DecisionObservation(int Tick, string Phase, PersonId Person, CohortId Source,
    double Temperature, double Rainfall, double Forage, long Population, LocalExit[] Exits, FeedingReceipt Feeding);
public readonly record struct DecisionId(PersonId Person, int Tick);
public enum MovementChoice { Stay, Move }
public sealed record MovementIntent(PersonId Person, DecisionId Decision, int Tick, CohortId Source,
    MovementChoice Choice, RegionId? Destination = null);
public sealed record IntentResolution(MovementIntent Intent, bool Accepted, string Reason);
public sealed record MindAdvance(MindState Mind, MovementIntent? Intent, string Reason);
public sealed record CognitionEvent(long Sequence, int Tick, PersonId Person, string Policy,
    DecisionObservation Observation, MemoryObservation[] PriorMemory, string MemoryAfterHash,
    MovementIntent? Intent, string Reason, IntentResolution? Resolution, long? MigrationEventSequence);
// Diagnostics never enter canonical state or influence a decision.
public sealed record TickTiming(double ValidationSeconds, double BiologySeconds, double CognitionSeconds,
    double MigrationSeconds, double IdentityCommitSeconds, double CognitionEvidenceSeconds);

public static class MindPolicy
{
    public static MindAdvance Advance(DecisionObservation observation, MindState? previous, CognitionOptions options)
    {
        CognitionValidation.Options(options);
        Validation.Require(options.Mode != CognitionMode.Off, "Off mode does not advance minds.");
        Validation.Require(observation.Phase == "post-demography" && observation.Tick > 0
            && observation.Feeding.Tick == observation.Tick && observation.Feeding.Region == observation.Source.Region
            && double.IsFinite(observation.Feeding.Nutrition) && observation.Feeding.Nutrition is >= 0 and <= 1,
            "Invalid decision observation/receipt.");
        if (previous is not null)
        {
            Validation.Require(previous.Person == observation.Person && previous.LastObservedTick < observation.Tick,
                "Observation is repeated or belongs to another person.");
            CognitionValidation.Memory(previous, options, observation.Tick - 1);
        }
        var old = previous?.Memory ?? [];
        var memory = old.Append(new MemoryObservation(observation.Tick, observation.Source.Region, observation.Feeding.Nutrition))
            .TakeLast(options.MemoryCapacity).ToArray();
        MovementIntent Intent(MovementChoice choice, RegionId? destination = null) => new(observation.Person,
            new(observation.Person, observation.Tick), observation.Tick, observation.Source, choice, destination);
        MovementIntent? intent = null; string reason;
        if (options.Mode == CognitionMode.ObserveOnly) reason = "observe-only";
        else if (observation.Feeding.Nutrition >= options.AdequateNutrition)
        { intent = Intent(MovementChoice.Stay); reason = "adequate-current-feeding"; }
        else if (!old.Any(m => m.Tick == observation.Tick - 1 && m.Region == observation.Source.Region && m.Nutrition < options.AdequateNutrition))
        { intent = Intent(MovementChoice.Stay); reason = "insufficient-consecutive-local-evidence"; }
        else
        {
            // Only bounded retained knowledge is usable, including deterministic
            // eviction by today's observation. There is no world/forecast callback.
            var known = memory.Where(m => observation.Tick - m.Tick <= options.MemoryMaxAge)
                .GroupBy(m => m.Region).ToDictionary(g => g.Key, g => g.Last());
            var adequate = observation.Exits.Where(e => known.TryGetValue(e.Destination, out var m) && m.Nutrition >= options.AdequateNutrition)
                .OrderByDescending(e => known[e.Destination].Nutrition).ThenByDescending(e => known[e.Destination].Tick)
                .ThenBy(e => e.Cost).ThenBy(e => e.Destination.Value).FirstOrDefault();
            var unknown = observation.Exits.Where(e => !known.ContainsKey(e.Destination))
                .OrderBy(e => e.Cost).ThenBy(e => e.Destination.Value).FirstOrDefault();
            var target = adequate ?? unknown;
            intent = target is null ? Intent(MovementChoice.Stay) : Intent(MovementChoice.Move, target.Destination);
            reason = adequate is not null ? "remembered-adequate-exit" : unknown is not null ? "explore-unfamiliar-exit" : "no-promising-or-unfamiliar-exit";
        }
        return new(new(observation.Person, memory, observation.Tick, intent is null ? previous?.LastDecisionTick : observation.Tick), intent, reason);
    }
}

public static class IntentResolver
{
    // Returns rejections without reserving anyone. No delayed action queue exists.
    public static IntentResolution[] Resolve(Scenario scenario, int tick, IReadOnlyList<IdentifiedPerson> survivors,
        IReadOnlyList<MovementIntent> intents, IReadOnlyList<MindState>? previousMinds = null)
    {
        var people = survivors.ToDictionary(p => p.Id);
        var duplicate = intents.GroupBy(i => i.Person).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var prior = (previousMinds ?? []).ToDictionary(m => m.Person);
        return intents.OrderBy(i => i.Person.Value).ThenBy(i => i.Tick).ThenBy(i => i.Choice).ThenBy(i => i.Destination?.Value ?? -1)
            .Select(i =>
            {
                string? rejected = duplicate.Contains(i.Person) ? "duplicate-actor-intents"
                    : i.Tick != tick || i.Decision != new DecisionId(i.Person, tick) ? "stale-or-future-decision"
                    : !people.TryGetValue(i.Person, out var p) || p.DeathTick is not null ? "actor-not-alive"
                    : !p.Active ? "actor-inactive"
                    : p.Cohort != i.Source ? "source-changed"
                    : prior.TryGetValue(i.Person, out var m) && m.LastDecisionTick >= tick ? "already-resolved"
                    : i.Choice == MovementChoice.Stay ? i.Destination is null ? null : "stay-has-destination"
                    : i.Choice != MovementChoice.Move || i.Destination is null ? "invalid-choice"
                    : !scenario.Regions[p.Cohort.Region.Value].Neighbors.Any(e => e.Destination == i.Destination
                        && e.Cost <= scenario.Species[p.Cohort.Species.Value].MaxTravelCost) ? "illegal-edge" : null;
                return new IntentResolution(i, rejected is null, rejected ?? "accepted");
            }).ToArray();
    }
}
