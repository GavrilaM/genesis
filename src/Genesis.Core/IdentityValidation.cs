namespace Genesis.Core;

internal static class IdentityValidation
{
    public static void State(WorldState w)
    {
        var state = w.Identity;
        if (state.Options is null || state.Living is null || state.Deceased is null || state.EventHash is null)
            throw new InvalidDataException("Identity state has null required fields.");
        Validation.Require(state.Options.MaxIdentified is >= 0 and <= 10000 && state.Options.MaxActiveMinds >= 0
            && state.Options.MaxActiveMinds <= state.Options.MaxIdentified, "Invalid identity representation budgets.");
        Validation.Require(state.NextPersonId > 0 && state.LastCommandSequence >= 0 && state.EventCount >= state.LastCommandSequence
            && state.EventHash.Length == 64 && state.EventHash.All(Uri.IsHexDigit), "Invalid identity sequence/hash.");
        Validation.Require(state.Living.Length <= state.Options.MaxIdentified, "Living identity budget exceeded.");
        int active = 0;
        void People(IdentifiedPerson[] people, bool alive)
        {
            long previous = 0;
            foreach (var p in people)
            {
                Validation.Require(p is not null, "Null person.");
                Validation.Require(p!.Id.Value > previous && p.Id.Value < state.NextPersonId, "Person IDs must be unique, ordered and allocated.");
                previous = p.Id.Value;
                PopulationOwnership.ValidateCohort(p.Cohort, w.Scenario);
                Validation.Require(p.PromotedTick >= 0 && p.PromotedTick <= w.CurrentTick, "Invalid promotion tick.");
                Validation.Require(alive ? p.DeathTick is null : p.DeathTick > p.PromotedTick && p.DeathTick <= w.CurrentTick && !p.Active,
                    "Invalid living/deceased ownership or death tick.");
                if (p.Active) active++;
                int lifeTick = p.DeathTick ?? w.CurrentTick;
                int maturation = w.Scenario.Species[p.Cohort.Species.Value].MaturationMonths;
                if (p.AgeProvenance == AgeProvenance.Unknown)
                    Validation.Require(p.Cohort.Stage > 0 && p.MaturationTick is null && p.BirthTick is null, "Unknown ages cannot have invented dates.");
                else
                {
                    Validation.Require(p.AgeProvenance is AgeProvenance.InitialCondition or AgeProvenance.SimulatedBirth,
                        "Unknown age provenance.");
                    Validation.Require(p.MaturationTick is not null && p.BirthTick is not null && p.BirthTick <= p.PromotedTick
                        && p.MaturationTick == p.BirthTick + maturation && p.MaturationTick > p.PromotedTick,
                        "Invalid preserved juvenile dates.");
                    Validation.Require(p.AgeProvenance == AgeProvenance.InitialCondition ? p.BirthTick <= 0 && p.BirthTick > -maturation : p.BirthTick > 0,
                        "Age provenance does not match dates.");
                    // Survival precedes maturation: a juvenile can die on its due tick.
                    Validation.Require(p.Cohort.Stage == 0 ? (alive ? p.MaturationTick > lifeTick : p.MaturationTick >= lifeTick) && p.MaturationTick <= (long)lifeTick + maturation
                        : p.MaturationTick <= lifeTick, "Person crossed or lost a maturation deadline.");
                }
            }
        }
        People(state.Living, true); People(state.Deceased, false);
        // Both lists are sorted: detect cross-list duplicates without allocating
        // a history-sized hash set twice per tick. All records are still validated.
        int deadIndex = 0;
        foreach (var p in state.Living)
        {
            while (deadIndex < state.Deceased.Length && state.Deceased[deadIndex].Id.Value < p.Id.Value) deadIndex++;
            Validation.Require(deadIndex == state.Deceased.Length || state.Deceased[deadIndex].Id != p.Id, "Identity is both living and deceased.");
        }
        long allocated = (long)state.Living.Length + state.Deceased.Length;
        Validation.Require(allocated == state.NextPersonId - 1 && state.LastCommandSequence >= allocated, "Allocated identity was lost or reused.");
        Validation.Require(active <= state.Options.MaxActiveMinds, "Active reservation budget exceeded.");
        if (allocated > 0) Validation.Require(JuvenileAges.Options(w.Scenario).JuvenileBinMonths == 1, "Identities require monthly age bins.");
    }
}
