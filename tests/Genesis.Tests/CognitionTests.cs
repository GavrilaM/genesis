using Genesis.Core;
using Genesis.Cli;
using Xunit;

namespace Genesis.Tests;

public class CognitionTests
{
    private static WorldState World(Scenario? scenario = null, CognitionMode mode = CognitionMode.Decide, int count = 1, int active = 1, ulong seed = 1)
    {
        var w = Simulation.Create(scenario ?? CognitionDemo.Scenario(MechanicsTests.Baseline()), seed, new(Math.Max(256, count), Math.Max(64, active)), new(mode));
        return PopulationOwnership.Apply(w, PopulationOwnership.PlanPromotions(w, count, active)).State;
    }
    private static WorldState Advance(WorldState w, int months)
    { for (int i = 0; i < months; i++) w = Simulation.Step(w).State; return w; }
    private static DecisionObservation Observation(int tick = 2, double nutrition = .4, params LocalExit[] exits) =>
        new(tick, "post-demography", new(1), new(new(0), new(0), 1, 0), 20, 10, 0, 20,
            exits.Length == 0 ? [new(new(1), 1)] : exits, new(tick, new(0), nutrition));
    private static MindState Prior(params MemoryObservation[] observations) => new(new(1), observations, observations[^1].Tick, null);

    [Fact] public void RealUnknownExplorationChangesOnlyMovementAfterSharedFeedingAndDemography()
    {
        var active = World(); var observer = World(mode: CognitionMode.ObserveOnly);
        for (int t = 1; t <= 2; t++)
        {
            var a = Simulation.Step(active); var b = Simulation.Step(observer);
            Assert.Equal(Persistence.Encode(a.Food), Persistence.Encode(b.Food));
            Assert.Equal(0, a.Populations.Sum(f => f.Births + f.Deaths));
            var ev = Assert.Single(a.CognitionEvents);
            Assert.Equal(.5, ev.Observation.Feeding.Nutrition);
            Assert.Equal(t == 1 ? "insufficient-consecutive-local-evidence" : "explore-unfamiliar-exit", ev.Reason);
            Assert.Equal(20, a.State.Population);
            Assert.All(ev.PriorMemory, m => Assert.Equal(new RegionId(0), m.Region));
            active = a.State; observer = b.State;
            if (t == 2)
            {
                Assert.Equal(1, Assert.Single(a.Transfers).Count); Assert.Equal(1, a.State.Identity.Living[0].Cohort.Region.Value);
                Assert.Empty(b.Transfers); Assert.Equal(0, b.State.Identity.Living[0].Cohort.Region.Value);
                Assert.Equal(19, a.State.Cohorts.Sum(c => c.Count)); Assert.Equal(10, a.Food.Sum(f => f.Consumed));
                Assert.Equal(0, a.Food[1].Consumed); Assert.NotNull(ev.MigrationEventSequence);
                Assert.Equal("explicit-intent", Assert.Single(a.PersonEvents, e => e.Type == "migration").Reason);
            }
        }
        var next = Simulation.Step(active);
        Assert.Equal(1, next.CognitionEvents[0].Observation.Source.Region.Value);
        Assert.Equal(1, next.CognitionEvents[0].Observation.Feeding.Nutrition);
    }

    [Theory] [InlineData(16, 32, 64)] [InlineData(64, 80, 16)]
    public void ExplicitStaysExcludeExactlyTheActivePeopleFromGenericMigration(int active, int identified, int expectedMoves)
    {
        var s = MigrationForecastTests.CapacityCase() with { InitialPopulations = [new(new(0), new(0), [0, 80, 0])] };
        var w = World(s, count: identified, active: active);
        string input = Persistence.Hash(w);
        var result = Simulation.Step(w);
        Assert.Equal(expectedMoves, result.Transfers.Sum(t => t.Count));
        Assert.Equal(active, result.State.Identity.Living.Count(p => p.Cohort.Region.Value == 0));
        Assert.Equal(active, result.CognitionEvents.Length);
        Assert.All(result.CognitionEvents, e => Assert.Equal(MovementChoice.Stay, e.Intent!.Choice));
        Assert.Equal(input, Persistence.Hash(w));
    }

    [Fact] public void ExplicitStaysCanEmptyAnOtherwiseFullyMovingGenericPool()
    {
        var s = MigrationForecastTests.CapacityCase() with { InitialPopulations = [new(new(0), new(0), [0, 64, 0])] };
        var active = World(s, count: 64, active: 64);
        var control = World(s, mode: CognitionMode.ObserveOnly, count: 64, active: 64);
        var held = Simulation.Step(active); var migrated = Simulation.Step(control);
        Assert.Empty(held.Transfers); Assert.Equal(64, migrated.Transfers.Sum(t => t.Count));
        Assert.All(held.State.Identity.Living, p => Assert.Equal(0, p.Cohort.Region.Value));
        Assert.All(held.State.Cohorts, c => Assert.Equal(0, c.Count));
        Assert.Equal(Persistence.Encode(held.Food), Persistence.Encode(migrated.Food));
    }

    [Fact] public void DeathRemovesPreviouslyObservedMindButRetainsItsIdentity()
    {
        var b = CognitionDemo.Scenario(MechanicsTests.Baseline());
        var s = b with { Rules = b.Rules with { StarvationMortality = .7 } };
        int learnedDeaths = 0;
        for (ulong seed = 1; seed <= 16; seed++)
        {
            var w = World(s, count: 20, active: 20, seed: seed);
            for (int t = 0; t < 6; t++)
            {
                var hadMemory = w.Cognition.Minds.Select(m => m.Person).ToHashSet();
                var result = Simulation.Step(w);
                foreach (var death in result.PersonEvents.Where(e => e.Type == "death" && hadMemory.Contains(e.Person)))
                {
                    learnedDeaths++;
                    Assert.DoesNotContain(result.State.Cognition.Minds, m => m.Person == death.Person);
                    Assert.DoesNotContain(result.CognitionEvents, e => e.Person == death.Person);
                    Assert.Contains(result.State.Identity.Deceased, p => p.Id == death.Person);
                }
                w = result.State;
            }
        }
        Assert.True(learnedDeaths > 0, "Fixture must exercise death after an actual observation.");
    }

    [Theory] [InlineData(1)] [InlineData(25)]
    public void AllNamedJuvenileOverridesPreserveDeadlineAndDoNotAgeOrMigrateTwice(int maturation)
    {
        var b = CognitionDemo.Scenario(MechanicsTests.Baseline());
        var s = b with { InitialPopulations = [new(new(0), new(0), [20, 0, 0])],
            Species = [b.Species[0] with { MaturationMonths = maturation, AdultMonths = 1 }], Demography = new(1, true, true) };
        var w = World(s, count: 20, active: 20);
        var first = Simulation.Step(w); Assert.Empty(first.Transfers);
        Assert.All(first.State.Identity.Living, p => Assert.Equal(maturation == 1 ? 1 : 0, p.Cohort.Stage));
        var second = Simulation.Step(first.State);
        Assert.Equal(20, second.Transfers.Sum(t => t.Count)); Assert.Equal(20, second.State.Population);
        Assert.All(second.State.Identity.Living, p => { Assert.Equal(1, p.Cohort.Region.Value); Assert.Equal((long)maturation, p.MaturationTick); Assert.Equal(maturation == 1 ? 2 : 0, p.Cohort.Stage); });
        Assert.Equal(20, second.PersonEvents.Count(e => e.Type == "migration"));
        Assert.All(second.State.Cohorts, c => Assert.Equal(0, c.Count));
    }

    [Fact] public void SimultaneousOpposingMovesAndChainsUseFrozenOrigins()
    {
        foreach (bool chain in new[] { false, true })
        {
            var b = CognitionDemo.Scenario(MechanicsTests.Baseline());
            var regions = new[] { b.Regions[0] with { Capacity = 0 }, b.Regions[1] with { Capacity = 0, Neighbors = [new(new(chain ? 2 : 0), 1)] } }.ToList();
            if (chain) regions.Add(b.Regions[1] with { Id = new(2), Capacity = 0, Neighbors = [] });
            var s = b with { Regions = regions.ToArray(), InitialPopulations = [new(new(0), new(0), [0, 20, 0]), new(new(1), new(0), [0, 20, 0])] };
            var w = Simulation.Create(s, 1, cognitionOptions: new(CognitionMode.Decide));
            w = PopulationOwnership.Apply(w, [new(1, 0, "promote", new(new(0), new(0), 1, 0), Active: true),
                new(2, 0, "promote", new(new(1), new(0), 1, 0), Active: true)]).State;
            var result = Simulation.Step(Simulation.Step(w).State);
            Assert.Equal(2, result.Transfers.Sum(t => t.Count)); Assert.Equal(40, result.State.Population);
            Assert.Equal(1, result.State.Identity.Living[0].Cohort.Region.Value);
            Assert.Equal(chain ? 2 : 0, result.State.Identity.Living[1].Cohort.Region.Value);
            Assert.All(result.PersonEvents.Where(e => e.Type == "migration").GroupBy(e => e.Person), group => Assert.Single(group));
        }
    }

    [Fact] public void DeadPersonCannotObserveDecideOrAvoidDeathOnMaturationTick()
    {
        var s = DemographicIntegrityTests.Colony(juveniles: 20, maturation: 1);
        s = s with { Species = [s.Species[0] with { NaturalMortality = 1 }] };
        var result = Simulation.Step(World(s));
        Assert.Empty(result.CognitionEvents); Assert.Empty(result.State.Cognition.Minds);
        Assert.Empty(result.State.Identity.Living); Assert.Equal(1, Assert.Single(result.State.Identity.Deceased).DeathTick);
        Assert.Equal(0, result.State.Population);
    }

    [Fact] public void BoundedMemoryIsDatedAndReadOnlyPolicyDoesNotMutateItsInput()
    {
        MindState? mind = null;
        for (int t = 1; t <= 20; t++)
        {
            string original = Persistence.Encode(mind);
            var advanced = MindPolicy.Advance(Observation(t, 1), mind, new(CognitionMode.ObserveOnly));
            Assert.Equal(original, Persistence.Encode(mind)); Assert.Null(advanced.Intent); mind = advanced.Mind;
            Assert.Equal(Math.Min(t, 8), mind.Memory.Length);
        }
        Assert.Equal(13, mind!.Memory[0].Tick); Assert.Equal(20, mind.LastObservedTick);
        Assert.Throws<InvalidDataException>(() => MindPolicy.Advance(Observation(20), mind, new(CognitionMode.Decide)));
    }

    [Fact] public void DatedKnownAdequateExitBeatsUnknownButExpiredOrEvictedKnowledgeDoesNot()
    {
        var obs = Observation(20, .2, new(new(1), 5), new(new(2), 1));
        var prior = Prior(new(10, new(1), .95), new(19, new(0), .2));
        var known = MindPolicy.Advance(obs, prior, new(CognitionMode.Decide));
        Assert.Equal(new RegionId(1), known.Intent!.Destination); Assert.Equal("remembered-adequate-exit", known.Reason);
        var expired = MindPolicy.Advance(obs, Prior(new(3, new(1), 1), new(19, new(0), .2)), new(CognitionMode.Decide));
        Assert.Equal(new RegionId(2), expired.Intent!.Destination); Assert.Equal("explore-unfamiliar-exit", expired.Reason);
        var allBad = MindPolicy.Advance(Observation(20, .2, new LocalExit(new(1), 1)), Prior(new(18, new(1), .1), new(19, new(0), .2)), new(CognitionMode.Decide));
        Assert.Equal(MovementChoice.Stay, allBad.Intent!.Choice); Assert.Equal("no-promising-or-unfamiliar-exit", allBad.Reason);
        var evicted = MindPolicy.Advance(obs, prior, new(CognitionMode.Decide, MemoryCapacity: 2));
        Assert.Equal(new RegionId(2), evicted.Intent!.Destination);
    }

    [Fact] public void CostIdTiesAndExitEnumerationDoNotChangeDecision()
    {
        var exits = new LocalExit[] { new(new(3), 2), new(new(2), 1), new(new(1), 1) };
        var prior = Prior(new MemoryObservation(1, new(0), .2));
        var first = MindPolicy.Advance(Observation(2, .2, exits), prior, new(CognitionMode.Decide));
        var second = MindPolicy.Advance(Observation(2, .2, exits.Reverse().ToArray()), prior, new(CognitionMode.Decide));
        Assert.Equal(first.Intent, second.Intent); Assert.Equal(new RegionId(1), first.Intent!.Destination);
    }

    [Fact] public void AFullMealDoesNotLookLikeStarvationWhenRegionalForageWasConsumed()
    {
        var observation = Observation(2, 1); Assert.Equal(0, observation.Forage);
        var result = MindPolicy.Advance(observation, Prior(new MemoryObservation(1, new(0), .2)), new(CognitionMode.Decide));
        Assert.Equal(MovementChoice.Stay, result.Intent!.Choice);
        Assert.Equal("adequate-current-feeding", result.Reason);
    }

    [Fact] public void HiddenDestinationResourcesNeverEnterPolicyInputs()
    {
        var b = CognitionDemo.Scenario(MechanicsTests.Baseline());
        var rich = World(b); var hostile = World(b with { Regions = [b.Regions[0], b.Regions[1] with { Capacity = 0, BaseTemperature = -80 }] });
        var a = Simulation.Step(Simulation.Step(rich).State).CognitionEvents[0];
        var c = Simulation.Step(Simulation.Step(hostile).State).CognitionEvents[0];
        Assert.Equal(Persistence.Encode(a.Observation), Persistence.Encode(c.Observation));
        Assert.Equal(a.Intent, c.Intent); Assert.Equal("explore-unfamiliar-exit", c.Reason);
        Assert.DoesNotContain("forage", Persistence.Encode(c.Observation.Exits), StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void DormancyDoesNotInventExperiencesOrSatisfyConsecutiveMonthRule()
    {
        var w = Simulation.Step(World()).State;
        w = PopulationOwnership.Apply(w, [new(2, 1, "set-active", Person: new(1))]).State;
        w = Advance(w, 3); Assert.Equal(1, Assert.Single(w.Cognition.Minds).LastObservedTick);
        w = PopulationOwnership.Apply(w, [new(3, 4, "set-active", Person: new(1), Active: true)]).State;
        var resumed = Simulation.Step(w);
        Assert.Equal("insufficient-consecutive-local-evidence", Assert.Single(resumed.CognitionEvents).Reason);
        Assert.Equal("explore-unfamiliar-exit", Assert.Single(Simulation.Step(resumed.State).CognitionEvents).Reason);
    }

    [Fact] public void InvalidAndDuplicateIntentsNeverReserveLivingOrDeadPeople()
    {
        var w = World(); var p = w.Identity.Living[0];
        var good = new MovementIntent(p.Id, new(p.Id, 2), 2, p.Cohort, MovementChoice.Move, new(1));
        var bad = new[] { good with { Tick = 1 }, good with { Tick = 3 }, good with { Decision = new(p.Id, 1) },
            good with { Source = p.Cohort with { Stage = 2 } }, good with { Destination = new(999) },
            good with { Choice = MovementChoice.Stay }, good with { Choice = (MovementChoice)99 }, good with { Person = new(99), Decision = new(new(99), 2) } };
        foreach (var intent in bad) Assert.False(Assert.Single(IntentResolver.Resolve(w.Scenario, 2, [p], [intent])).Accepted);
        Assert.False(Assert.Single(IntentResolver.Resolve(w.Scenario, 2, [p with { DeathTick = 2 }], [good])).Accepted);
        Assert.False(Assert.Single(IntentResolver.Resolve(w.Scenario, 2, [p with { Active = false }], [good])).Accepted);
        Assert.False(Assert.Single(IntentResolver.Resolve(w.Scenario, 2, [p], [good], [new(p.Id, [new(2, new(0), .2)], 2, 2)])).Accepted);
        var stay = good with { Choice = MovementChoice.Stay, Destination = null };
        var duplicate = IntentResolver.Resolve(w.Scenario, 2, [p], [good, stay]);
        Assert.All(duplicate, r => Assert.False(r.Accepted));
        Assert.Equal(Persistence.Encode(duplicate), Persistence.Encode(IntentResolver.Resolve(w.Scenario, 2, [p], [stay, good])));
        Assert.True(Assert.Single(IntentResolver.Resolve(w.Scenario, 2, [p], [good])).Accepted);
    }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(5)] [InlineData(12)] [InlineData(24)]
    public void ActiveResumePreservesFullStateAndAllEventStreams(int split)
    {
        string path = Path.Combine(Path.GetTempPath(), "genesis-cognition-" + Guid.NewGuid() + ".json");
        try
        {
            var initial = World(MechanicsTests.Baseline(), count: 64, active: 16, seed: 55);
            var checkpoint = Advance(initial, split); Persistence.Save(path, checkpoint); var resumed = Persistence.Load(path);
            Assert.Equal(Persistence.Hash(checkpoint), Persistence.Hash(resumed));
            for (int t = split; t < 40; t++)
            {
                var full = Simulation.Step(checkpoint); var continued = Simulation.Step(resumed);
                Assert.Equal(Persistence.Hash(full.State), Persistence.Hash(continued.State));
                Assert.Equal(Persistence.Encode(full.Events), Persistence.Encode(continued.Events));
                Assert.Equal(Persistence.Encode(full.PersonEvents), Persistence.Encode(continued.PersonEvents));
                Assert.Equal(Persistence.Encode(full.CognitionEvents), Persistence.Encode(continued.CognitionEvents));
                checkpoint = full.State; resumed = continued.State;
            }
        }
        finally { File.Delete(path); }
    }

    [Fact] public void ProfilingAndRepeatedInspectionDoNotChangeTruth()
    {
        var w = Simulation.Step(World()).State; string before = Persistence.Hash(w);
        _ = PopulationOwnership.InspectPerson(w, new(1)); _ = PopulationOwnership.ObserveLocal(w, new(1));
        _ = PopulationOwnership.BiologicalHash(w);
        var mind = PopulationOwnership.InspectMind(w, new(1)); mind!.Memory[0] = new(1, new(999), 0);
        Assert.Null(PopulationOwnership.InspectMind(w, new(999)));
        var plain = Simulation.Step(w); var profiled = Simulation.Step(w, profile: true, audit: true);
        Assert.Equal(before, Persistence.Hash(w)); Assert.Equal(Persistence.Hash(plain.State), Persistence.Hash(profiled.State));
        Assert.Equal(Persistence.Encode(plain.CognitionEvents), Persistence.Encode(profiled.CognitionEvents)); Assert.NotNull(profiled.Timing);
    }

    [Fact] public void BadMindOwnershipDatesAndHistoryAreRejected()
    {
        var w = Simulation.Step(World()).State; var m = Assert.Single(w.Cognition.Minds);
        foreach (var invalid in new[] { m with { Person = new(999) }, m with { Memory = [new(2, new(0), .5)], LastObservedTick = 2 },
            m with { LastObservedTick = 0 }, m with { Memory = [new(1, new(999), .2)] }, m with { Memory = [new(0, new(0), .5)] } })
            Assert.Throws<InvalidDataException>(() => Validation.State(w with { Cognition = w.Cognition with { Minds = [invalid] } }));
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { Cognition = w.Cognition with { Minds = [m, m] } }));
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { Cognition = w.Cognition with { EventCount = 0 } }));
    }

    [Fact] public void LateAccountingFailureLeavesInputMindsPeopleFoodAndCursorsUntouched()
    {
        var s = DemographicIntegrityTests.Colony(juveniles: 0, adults: 0, maturation: 1);
        s = s with { Species = [s.Species[0] with { AnnualBirthRate = 0, MigrationRate = 0 }] };
        var w = Simulation.Create(s, 1, cognitionOptions: new(CognitionMode.Decide)); var cohorts = (Cohort[])w.Cohorts.Clone();
        cohorts[0] = cohorts[0] with { Count = 1000000000000, JuvenileBins = [0, 1000000000000] };
        cohorts[3] = cohorts[3] with { Count = 1000000000000 };
        w = w with { Cohorts = cohorts, Windows = [w.Windows[0] with { Start = 2000000000000 }] };
        w = PopulationOwnership.Apply(w, [new(1, 0, "promote", cohorts[0].Id, 1, Active: true)]).State;
        string original = Persistence.Hash(w);
        Assert.Throws<InvalidDataException>(() => Simulation.Step(w)); Assert.Equal(original, Persistence.Hash(w));
    }

    [Fact] public void CognitionArchiveReplaysPolicyAndRejectsCorruptHistory()
    {
        string root = Path.Combine(Path.GetTempPath(), "genesis-cognition-archive-" + Guid.NewGuid());
        try
        {
            CognitionDemo.Run(MechanicsTests.Baseline(), root);
            string active = Path.Combine(root, "Decide"); RunArchive.Verify(active);
            CognitionContinuation.Run(active, 1, Path.Combine(root, "continued"));
            string ledger = Path.Combine(active, "cognition-events.jsonl"); var good = File.ReadAllText(ledger);
            File.WriteAllText(ledger, good.Replace("explore-unfamiliar-exit", "omniscient-destination", StringComparison.Ordinal));
            Assert.Throws<InvalidDataException>(() => RunArchive.Verify(active));
            File.WriteAllText(ledger, good); RunArchive.Verify(active);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
