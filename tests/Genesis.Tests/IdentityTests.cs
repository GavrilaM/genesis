using Genesis.Core;
using Genesis.Cli;
using Xunit;

namespace Genesis.Tests;

public class IdentityTests
{
    private static WorldState Identify(WorldState w, int count, int active = 0) =>
        PopulationOwnership.Apply(w, PopulationOwnership.PlanPromotions(w, count, active)).State;
    private static WorldState Advance(WorldState w, int months)
    {
        for (int t = 0; t < months; t++) w = Simulation.Step(w).State;
        return w;
    }
    private static void Same(TickResult aggregate, TickResult identified) =>
        Assert.Equal(PopulationOwnership.BiologicalHash(aggregate), PopulationOwnership.BiologicalHash(identified));

    [Fact] public void PromotionPartitionsPeopleAndPreservesEveryBiologicalField()
    {
        var w = Simulation.Create(MechanicsTests.Baseline(), 42); string before = Persistence.Hash(w);
        var commands = PopulationOwnership.PlanPromotions(w, 256, 64);
        Assert.Equal(before, Persistence.Hash(w));
        var transaction = PopulationOwnership.Apply(w, commands); var promoted = transaction.State;
        Assert.Equal(before, Persistence.Hash(w));
        Assert.Equal(256, promoted.Identity.Living.Length); Assert.Equal(64, promoted.Identity.Living.Count(p => p.Active));
        Assert.Equal(w.Population, promoted.Population); Assert.Equal(w.Population - 256, promoted.Cohorts.Sum(c => c.Count));
        Assert.Equal(PopulationOwnership.BiologicalHash(w), PopulationOwnership.BiologicalHash(promoted));
        Assert.NotEqual(Persistence.Hash(w), Persistence.Hash(promoted));
        Assert.All(transaction.Events, e => Assert.Equal("promote", e.Type));
        foreach (var p in promoted.Identity.Living.Where(p => p.Cohort.Stage > 0))
        {
            Assert.Null(p.BirthTick); Assert.Null(p.MaturationTick); Assert.Equal(AgeProvenance.Unknown, p.AgeProvenance);
        }
    }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void PassiveDetailPreservesFullMonthlyProjectionsAcrossSeeds(int seed)
    {
        var plain = Simulation.Create(MechanicsTests.Baseline(), (ulong)seed);
        var detailed = Identify(plain, 256, seed % 2 == 0 ? 64 : 0);
        for (int t = 0; t < 120; t++)
        {
            var a = Simulation.Step(plain); var b = Simulation.Step(detailed, audit: t % 13 == 0);
            Same(a, b); plain = a.State; detailed = b.State;
            if (t == 57)
            {
                int available = 256 - detailed.Identity.Living.Length;
                if (available > 0) detailed = Identify(detailed, available);
            }
        }
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void AllIdentifiedColoniesStillHaveAnonymousBirthsAndSharedDeaths(bool binomial)
    {
        var s = DemographicIntegrityTests.Colony(binomial: binomial, juveniles: 0, adults: 120);
        s = s with { Species = [s.Species[0] with { AnnualBirthRate = 1, NaturalMortality = .01 }] };
        for (ulong seed = 1; seed <= 16; seed++)
        {
            var plain = Simulation.Create(s, seed, new(120, 0)); var detailed = Identify(plain, 120);
            long births = 0, deaths = 0;
            for (int t = 0; t < 36; t++)
            {
                var a = Simulation.Step(plain); var b = Simulation.Step(detailed); Same(a, b);
                births += b.Populations[0].Births; deaths += b.Populations[0].Deaths;
                plain = a.State; detailed = b.State;
            }
            Assert.True(births > 0 && deaths > 0); Assert.True(detailed.Cohorts.Sum(c => c.Count) > 0);
            Assert.Equal(121, detailed.Identity.NextPersonId);
            Assert.Equal(120, detailed.Identity.Living.Length + detailed.Identity.Deceased.Length);
        }
    }

    [Fact] public void FullBudgetDoesNotBlockBirthsOrDeleteAnonymousPeople()
    {
        var s = DemographicIntegrityTests.Colony(juveniles: 0, adults: 300);
        s = s with { Species = [s.Species[0] with { AnnualBirthRate = 1, AdultMonths = 12000 }] };
        var plain = Simulation.Create(s, 10); var detailed = Identify(plain, 256);
        var a = Simulation.Step(plain); var b = Simulation.Step(detailed); Same(a, b);
        Assert.Equal(256, b.State.Identity.Living.Length); Assert.True(b.Populations[0].Births > 0);
        Assert.Equal(44 + b.Populations[0].Births, b.State.Cohorts.Sum(c => c.Count));
        var command = new IdentityCommand(257, 1, "promote", new(new(0), new(0), 1, 0));
        Assert.Throws<InvalidDataException>(() => PopulationOwnership.Apply(b.State, [command]));
    }

    [Fact] public void CommandsAreAtomicSequencedAndCannotSpendTheSameAnonymousPersonTwice()
    {
        var w = Simulation.Create(DemographicIntegrityTests.Colony(juveniles: 0, adults: 1), 1);
        var command = new IdentityCommand(1, 0, "promote", new(new(0), new(0), 1, 0));
        string original = Persistence.Hash(w);
        Assert.Throws<InvalidDataException>(() => PopulationOwnership.Apply(w, [command, command with { Sequence = 2 }]));
        Assert.Equal(original, Persistence.Hash(w));
        var next = PopulationOwnership.Apply(w, [command]).State;
        Assert.Equal(2, next.Identity.NextPersonId);
        foreach (var invalid in new[] { command, command with { Sequence = 3 }, command with { Sequence = 2, Tick = 1 },
            command with { Sequence = 2, Type = "invent-person" }, command with { Sequence = 2, Source = new(new(99), new(0), 1, 0) } })
            Assert.Throws<InvalidDataException>(() => PopulationOwnership.Apply(next, [invalid]));
        Assert.Equal(original, Persistence.Hash(w));
    }

    [Fact] public void ActivationIsOnlyABudgetedReservationAndDeactivationRetainsOwnership()
    {
        var w = Identify(Simulation.Create(MechanicsTests.Baseline(), 1, new(3, 1)), 3, 1);
        string before = Persistence.Hash(w); string biological = PopulationOwnership.BiologicalHash(w);
        var enable = new IdentityCommand(4, 0, "set-active", Person: new(2), Active: true);
        Assert.Throws<InvalidDataException>(() => PopulationOwnership.Apply(w, [enable]));
        Assert.Equal(before, Persistence.Hash(w));
        var next = PopulationOwnership.Apply(w, [new(4, 0, "set-active", Person: new(1)), enable with { Sequence = 5 }]).State;
        Assert.Equal(3, next.Identity.Living.Length); Assert.Equal(new PersonId(2), Assert.Single(next.Identity.Living, p => p.Active).Id);
        Assert.Equal(biological, PopulationOwnership.BiologicalHash(next));
        Same(Simulation.Step(w), Simulation.Step(next));
    }

    [Theory] [InlineData(1)] [InlineData(25)]
    public void JuvenilesKeepDeadlinesAndCannotAgeTwiceOrReproduceOnMaturation(int maturation)
    {
        var s = DemographicIntegrityTests.Colony(juveniles: 30, maturation: maturation);
        s = s with { Species = [s.Species[0] with { AdultMonths = 1, AnnualBirthRate = 1 }] };
        var w = Identify(Simulation.Create(s, 1), 30);
        Assert.All(w.Identity.Living, p => { Assert.Equal((long)maturation, p.MaturationTick); Assert.Equal(AgeProvenance.InitialCondition, p.AgeProvenance); });
        for (int t = 1; t <= maturation; t++)
        {
            var step = Simulation.Step(w); w = step.State;
            Assert.Equal(0, step.Populations[0].Births);
            Assert.All(w.Identity.Living, p => Assert.Equal(t < maturation ? 0 : 1, p.Cohort.Stage));
        }
        var next = Simulation.Step(w);
        Assert.All(next.State.Identity.Living, p => Assert.Equal(2, p.Cohort.Stage));
        Assert.All(next.PersonEvents, e => Assert.Equal("elder-transition", e.Type));
    }

    [Fact] public void DeathOnMaturationTickProducesOneRetainedDeadIdentityAndFreesCapacity()
    {
        var s = DemographicIntegrityTests.Colony(juveniles: 1, adults: 100, maturation: 1);
        s = s with { Species = [s.Species[0] with { NaturalMortality = 2.0 / 3 }] };
        var w = Simulation.Create(s, 1, new(1, 1));
        w = PopulationOwnership.Apply(w, [new(1, 0, "promote", new(new(0), new(0), 0, 0), 1, Active: true)]).State;
        var result = Simulation.Step(w); Assert.Empty(result.State.Identity.Living);
        var dead = Assert.Single(result.State.Identity.Deceased); Assert.Equal(1, dead.DeathTick); Assert.False(dead.Active);
        Assert.Equal("death", Assert.Single(result.PersonEvents).Type); Assert.Equal(0, dead.Cohort.Stage);
        var replacement = Identify(result.State, 1); Assert.Equal(2, Assert.Single(replacement.Identity.Living).Id.Value);
        Assert.Equal(dead, PopulationOwnership.InspectPerson(replacement, new(1)));
        Assert.Throws<InvalidDataException>(() => PopulationOwnership.Apply(replacement, [new(3, 1, "set-active", Person: new(1), Active: true)]));
    }

    [Fact] public void ReadOnlyViewsCannotMaterializeMutateOrInventNeighborKnowledge()
    {
        var w = Identify(Simulation.Create(MechanicsTests.Baseline(), 3), 3);
        string before = Persistence.Hash(w); var person = w.Identity.Living[0];
        var observed = PopulationOwnership.ObserveLocal(w, person.Id);
        Assert.Equal(w.CurrentTick, observed.Tick); Assert.Equal(person.Cohort.Region, observed.Region);
        Assert.DoesNotContain("forage", Persistence.Encode(observed.Exits), StringComparison.OrdinalIgnoreCase);
        observed.Exits[0] = new(new(999), 999);
        var combined = PopulationOwnership.CombinedCohorts(w); combined[0].JuvenileBins![1] = 999;
        Assert.Null(PopulationOwnership.InspectPerson(w, new(999)));
        Assert.Throws<InvalidDataException>(() => PopulationOwnership.ObserveLocal(w, new(999)));
        _ = PopulationOwnership.BiologicalHash(w);
        Assert.Equal(before, Persistence.Hash(w));
    }

    [Fact] public void SaveResumePreservesIdentitiesAllocatorsHistoryAndCommands()
    {
        string path = Path.Combine(Path.GetTempPath(), "genesis-identity-" + Guid.NewGuid() + ".json");
        try
        {
            var w = Advance(Identify(Simulation.Create(MechanicsTests.Baseline(), 55), 64, 16), 17);
            Persistence.Save(path, w); var loaded = Persistence.Load(path);
            Assert.Equal(Persistence.Hash(w), Persistence.Hash(loaded));
            var commands = PopulationOwnership.PlanPromotions(w, 20);
            w = PopulationOwnership.Apply(w, commands).State; loaded = PopulationOwnership.Apply(loaded, commands).State;
            Assert.Throws<InvalidDataException>(() => PopulationOwnership.Apply(loaded, commands));
            Assert.Equal(Persistence.Hash(Advance(w, 100)), Persistence.Hash(Advance(loaded, 100)));
        }
        finally { File.Delete(path); }
    }

    [Fact] public void StateValidationRejectsDuplicatedOwnershipLostIdsAndCorruptDeadlines()
    {
        var w = Identify(Simulation.Create(DemographicIntegrityTests.Colony(), 1), 2);
        var cohorts = (Cohort[])w.Cohorts.Clone();
        cohorts[0] = cohorts[0] with { Count = cohorts[0].Count + 1, JuvenileBins = (long[])cohorts[0].JuvenileBins!.Clone() };
        cohorts[0].JuvenileBins![24]++;
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { Cohorts = cohorts }));
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { Identity = w.Identity with { Living = [w.Identity.Living[0], w.Identity.Living[0]] } }));
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { Identity = w.Identity with { NextPersonId = 4 } }));
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { Identity = w.Identity with { Living = [w.Identity.Living[0] with { MaturationTick = 23 }, w.Identity.Living[1]] } }));
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { Identity = w.Identity with { Living = [w.Identity.Living[0] with { AgeProvenance = AgeProvenance.Unknown }, w.Identity.Living[1]] } }));
        var later = Advance(w, 1);
        var duplicate = later with { Identity = later.Identity with { NextPersonId = 4, LastCommandSequence = 3, EventCount = 3,
            Deceased = [later.Identity.Living[0] with { DeathTick = 1, Active = false }] } };
        Assert.Contains("both living and deceased", Assert.Throws<InvalidDataException>(() => Validation.State(duplicate)).Message);
    }

    [Fact] public void MigrationCarriesIdentityAndDeadlineExactlyOnceEvenWhenEveryMoverIsIdentified()
    {
        var s = MigrationForecastTests.CapacityCase();
        s = s with { InitialPopulations = [new(new(0), new(0), [80, 0, 0])],
            Species = s.Species.Select(sp => sp with { MaturationMonths = 25 }).ToArray(), Demography = new(1, true, true) };
        var plain = Simulation.Create(s, 1); var detailed = Identify(plain, 80);
        string before = Persistence.Hash(detailed);
        var a = Simulation.Step(plain); var b = Simulation.Step(detailed); Same(a, b);
        Assert.Equal(before, Persistence.Hash(detailed));
        Assert.All(b.State.Identity.Living, p => { Assert.Equal(1, p.Cohort.Region.Value); Assert.Equal(25L, p.MaturationTick); });
        Assert.Equal(80, b.PersonEvents.Length);
        Assert.All(b.PersonEvents.GroupBy(e => e.Person), group => Assert.Single(group));
        Assert.All(b.State.Cohorts, c => Assert.Equal(0, c.Count));
    }

    [Fact] public void ScarcityAndExtinctionRemainIdenticalWithAllPeopleIdentified()
    {
        var s = DemographicIntegrityTests.Colony(juveniles: 20, adults: 20);
        s = s with { InitialStockFraction = 0, Rules = s.Rules with { Productivity = 0, StarvationMortality = 1 } };
        for (ulong seed = 1; seed <= 16; seed++)
        {
            var plain = Simulation.Create(s, seed); var detailed = Identify(plain, 40, 20);
            var a = Simulation.Step(plain); var b = Simulation.Step(detailed); Same(a, b);
            Assert.Equal(0, b.State.Population); Assert.Empty(b.State.Identity.Living);
            Assert.Equal(40, b.State.Identity.Deceased.Length); Assert.Equal(0, b.Food.Sum(f => f.Consumed));
            Same(Simulation.Step(a.State), Simulation.Step(b.State));
        }
    }

    [Fact] public void NewlyBornPromotedJuvenilesHaveSimulatedDatesAndSurviveRingWrapWithoutInventedHistory()
    {
        var s = DemographicIntegrityTests.Colony(juveniles: 0, adults: 120, maturation: 5);
        s = s with { Species = [s.Species[0] with { AnnualBirthRate = 1, AdultMonths = 12000 }] };
        var plain = Advance(Simulation.Create(s, 8), 8);
        long deadline = 13; var source = new CohortId(new(0), new(0), 0, 0);
        var detail = PopulationOwnership.Apply(plain, [new(1, 8, "promote", source, deadline)]).State;
        var p = Assert.Single(detail.Identity.Living); Assert.Equal(8L, p.BirthTick); Assert.Equal(AgeProvenance.SimulatedBirth, p.AgeProvenance);
        for (int t = 9; t <= 14; t++)
        {
            var a = Simulation.Step(plain); var b = Simulation.Step(detail); Same(a, b); plain = a.State; detail = b.State;
            Assert.Equal(t < deadline ? 0 : 1, detail.Identity.Living[0].Cohort.Stage);
            Assert.Equal(deadline, detail.Identity.Living[0].MaturationTick);
        }
    }

    [Fact] public void DisabledIdentityBudgetsAndUnsupportedAgeModelsFailWithoutLimitingPopulation()
    {
        var w = Simulation.Create(MechanicsTests.Baseline(), 1, new(0, 0));
        Assert.Equal(38400, w.Population); Assert.Throws<InvalidDataException>(() => PopulationOwnership.PlanPromotions(w, 1));
        Same(Simulation.Step(w), Simulation.Step(Simulation.Create(MechanicsTests.Baseline(), 1)));
        foreach (int q in new[] { 0, 12 })
        {
            var other = Simulation.Create(DemographicIntegrityTests.Colony(q), 1);
            Assert.Throws<InvalidDataException>(() => PopulationOwnership.Apply(other, [new(1, 0, "promote", new(new(0), new(0), 0, 0), 24)]));
        }
        Assert.Throws<InvalidDataException>(() => Simulation.Create(MechanicsTests.Baseline(), 1, new(1, 2)));
    }

    [Fact] public void IdentityArchiveReplaysCommandsAndDetectsTamperingAndRejectedTransactionsPublishNothing()
    {
        string root = Path.Combine(Path.GetTempPath(), "genesis-identity-archive-" + Guid.NewGuid());
        try
        {
            var w = Simulation.Create(MechanicsTests.Baseline(), 11);
            string setup = Path.Combine(root, "setup"), run = Path.Combine(root, "run"), invalid = Path.Combine(root, "invalid");
            var commands = PopulationOwnership.PlanPromotions(w, 64, 16);
            IdentityArchive.Apply(w, commands, setup); RunArchive.Verify(setup);
            var promoted = Persistence.Load(Path.Combine(setup, "latest.json"));
            Assert.Throws<InvalidDataException>(() => IdentityArchive.Apply(promoted, commands, invalid));
            Assert.False(Directory.Exists(invalid));
            RunArchive.Run(promoted, 24, run, 1, 12, null, null); RunArchive.Verify(run);
            string ledger = Path.Combine(run, "person-events.jsonl"), original = File.ReadAllText(ledger);
            Assert.NotEmpty(original); File.WriteAllText(ledger, "");
            Assert.Throws<InvalidDataException>(() => RunArchive.Verify(run));
            File.WriteAllText(ledger, original); RunArchive.Verify(run);
            File.WriteAllText(Path.Combine(setup, "identity-commands.json"), "[]");
            Assert.Throws<InvalidDataException>(() => RunArchive.Verify(setup));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact] public void ConditionalDeathAssignmentHasCorrectMarginalProbability()
    {
        var s = DemographicIntegrityTests.Colony(juveniles: 0, adults: 20);
        s = s with { Species = [s.Species[0] with { NaturalMortality = .25 }] };
        int dead = 0;
        for (ulong seed = 1; seed <= 2048; seed++)
        {
            var result = Simulation.Step(Identify(Simulation.Create(s, seed), 1));
            dead += result.State.Identity.Deceased.Length;
        }
        // Six standard errors around the analytic Bernoulli marginal, not a golden random count.
        double margin = 6 * Math.Sqrt(.25 * .75 / 2048);
        Assert.InRange(dead / 2048.0, .25 - margin, .25 + margin);
    }

    [Fact] public void SustainedIdentityBenchmarkResumesWithTheSameBoundaryCommands()
    {
        string root = Path.Combine(Path.GetTempPath(), "genesis-maintained-" + Guid.NewGuid());
        try
        {
            var initial = Simulation.Create(MechanicsTests.Baseline(), 24);
            var plan = new IdentityBenchmarkPlan(64, 16, 12);
            string full = Path.Combine(root, "full"), resumed = Path.Combine(root, "resumed");
            RunArchive.Run(initial, 36, full, 1, 12, null, null, PopulationOwnership.PlanPromotions(initial, 64, 16), plan);
            var checkpoint = Persistence.Load(Path.Combine(full, "snapshot-00000012.json"));
            int missing = 64 - checkpoint.Identity.Living.Length;
            var atBoundary = missing == 0 ? null : PopulationOwnership.PlanPromotions(checkpoint, missing, 16 - checkpoint.Identity.Living.Count(p => p.Active));
            RunArchive.Run(checkpoint, 24, resumed, 1, 12, null, null, atBoundary, plan);
            RunArchive.Verify(full); RunArchive.Verify(resumed);
            Assert.Equal(Persistence.Hash(Persistence.Load(Path.Combine(full, "latest.json"))),
                Persistence.Hash(Persistence.Load(Path.Combine(resumed, "latest.json"))));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
