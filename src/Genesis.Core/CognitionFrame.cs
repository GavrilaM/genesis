namespace Genesis.Core;

internal sealed class CognitionFrame
{
    public CognitionState State { get; private set; }
    public IntentResolution[] Resolutions { get; private set; } = [];
    private readonly List<(DecisionObservation Observation, MemoryObservation[] Prior, MindAdvance Advance)> pending = [];
    private CognitionFrame(CognitionState state) { State = state; }

    public static CognitionFrame Prepare(WorldState input, IdentifiedPerson[] survivors,
        RegionState[] regions, long[] counts, PopulationFlow[] feeding, int tick)
    {
        var frame = new CognitionFrame(input.Cognition);
        if (input.Cognition.Options.Mode == CognitionMode.Off && input.Cognition.Minds.Length == 0) return frame;
        var old = input.Cognition.Minds.ToDictionary(m => m.Person);
        var minds = new List<MindState>(); var intents = new List<MovementIntent>();
        int ns = input.Scenario.Species.Length;
        foreach (var p in survivors)
        {
            old.TryGetValue(p.Id, out var prior);
            if (!p.Active || input.Cognition.Options.Mode == CognitionMode.Off)
            { if (prior is not null) minds.Add(prior); continue; }
            int r = p.Cohort.Region.Value; var region = regions[r]; long population = 0;
            for (int s = 0; s < ns; s++) for (int a = 0; a < 3; a++) for (int v = 0; v < 3; v++) population += counts[Simulation.Index(r, s, a, v, ns)];
            var observation = new DecisionObservation(tick, "post-demography", p.Id, p.Cohort, region.Temperature,
                region.Rainfall, region.Forage, population, input.Scenario.Regions[r].Neighbors
                    .Where(e => e.Cost <= input.Scenario.Species[p.Cohort.Species.Value].MaxTravelCost)
                    .Select(e => new LocalExit(e.Destination, e.Cost)).ToArray(),
                new(tick, region.Id, feeding[r * ns + p.Cohort.Species.Value].Nutrition));
            var advance = MindPolicy.Advance(observation, prior, input.Cognition.Options);
            minds.Add(advance.Mind); if (advance.Intent is not null) intents.Add(advance.Intent);
            frame.pending.Add((observation, prior is null ? [] : (MemoryObservation[])prior.Memory.Clone(), advance));
        }
        frame.State = input.Cognition with { Minds = minds.ToArray() };
        frame.Resolutions = intents.Count == 0 ? [] : IntentResolver.Resolve(input.Scenario, tick, survivors, intents, input.Cognition.Minds);
        return frame;
    }

    public CognitionEvent[] Complete(PersonEvent[] personEvents)
    {
        if (pending.Count == 0) return [];
        var resolutions = Resolutions.ToDictionary(r => r.Intent.Person);
        var moves = personEvents.Where(e => e.Type == "migration" && e.Reason == "explicit-intent").ToDictionary(e => e.Person);
        var events = new List<CognitionEvent>(); string hash = State.EventHash;
        foreach (var entry in pending)
        {
            resolutions.TryGetValue(entry.Observation.Person, out var resolution);
            moves.TryGetValue(entry.Observation.Person, out var move);
            var ev = new CognitionEvent(checked(State.EventCount + events.Count + 1), entry.Observation.Tick, entry.Observation.Person,
                State.Options.Policy, entry.Observation, entry.Prior, Persistence.Hash(entry.Advance.Mind.Memory), entry.Advance.Intent,
                entry.Advance.Reason, resolution, move?.Sequence);
            hash = Persistence.Hash(new { previous = hash, ev }); events.Add(ev);
        }
        State = State with { EventCount = checked(State.EventCount + events.Count), EventHash = hash };
        return events.ToArray();
    }
}
