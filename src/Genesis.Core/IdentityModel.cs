namespace Genesis.Core;

public readonly record struct PersonId(long Value);
public sealed record IdentityOptions(int MaxIdentified = 256, int MaxActiveMinds = 64);
public enum AgeProvenance { Unknown, InitialCondition, SimulatedBirth }
public sealed record IdentifiedPerson(PersonId Id, CohortId Cohort, int PromotedTick,
    long? MaturationTick, long? BirthTick, AgeProvenance AgeProvenance,
    bool Active, int? DeathTick = null);
// Active reserves future cognition capacity; Phase 1A executes no minds.
public sealed record IdentityState(IdentityOptions Options, long NextPersonId,
    long LastCommandSequence, IdentifiedPerson[] Living, IdentifiedPerson[] Deceased,
    long EventCount, string EventHash)
{
    public static IdentityState Empty(IdentityOptions? options = null) =>
        new(options ?? new(), 1, 0, [], [], 0, Persistence.EmptyEventHash);
}
public sealed record IdentityCommand(long Sequence, int Tick, string Type,
    CohortId? Source = null, long? JuvenileDeadline = null, PersonId? Person = null, bool Active = false);
public sealed record PersonEvent(long Sequence, int Tick, PersonId Person, string Type,
    CohortId Before, CohortId After, string Reason, long? CommandSequence = null);
public sealed record IdentityTransaction(WorldState State, PersonEvent[] Events);

// Pure value snapshots. No API below returns mutable arrays owned by the world.
public sealed record LocalExit(RegionId Destination, double Cost);
public sealed record LocalObservation(int Tick, PersonId Person, RegionId Region,
    double Temperature, double Rainfall, double Forage, long Population, LocalExit[] Exits);
