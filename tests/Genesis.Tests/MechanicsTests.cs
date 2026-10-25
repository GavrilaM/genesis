using Genesis.Core;
using Xunit;

namespace Genesis.Tests;

public class MechanicsTests
{
    public static Scenario Baseline() => Persistence.ReadScenario(Path.Combine(AppContext.BaseDirectory, "scenarios", "baseline.json"));
    private static WorldState Advance(WorldState w, int months)
    {
        for (int i = 0; i < months; i++) w = Simulation.Step(w).State;
        return w;
    }
    [Fact] public void BaselineHasTwelveConnectedRegionsAndFourSpecies()
    {
        var s = Baseline(); Assert.Equal(12, s.Regions.Length); Assert.Equal(4, s.Species.Length);
        var seen = new HashSet<int> { 0 }; var queue = new Queue<int>(); queue.Enqueue(0);
        while (queue.TryDequeue(out var r)) foreach (var e in s.Regions[r].Neighbors) if (seen.Add(e.Destination.Value)) queue.Enqueue(e.Destination.Value);
        Assert.Equal(12, seen.Count);
        Assert.Equal(432, Simulation.Create(s, 1).Cohorts.Length);
    }
    [Fact] public void ConservationAndReferencesHoldAcrossSeeds()
    {
        for (ulong seed = 0; seed < 5; seed++)
        {
            var w = Simulation.Create(Baseline(), seed);
            for (int i = 0; i < 240; i++)
            {
                var before = Persistence.Hash(w); var result = Simulation.Step(w);
                Assert.Equal(before, Persistence.Hash(w));
                Assert.Equal(w.Population + result.Populations.Sum(f => f.Births - f.Deaths), result.State.Population);
                foreach (var f in result.Food)
                    Assert.Equal(f.Consumed, result.Populations.Where(p => p.Region == f.Region).Sum(p => p.Consumed), 6);
                w = result.State;
            }
        }
    }
    [Fact] public void SameSeedAndSnapshotContinuationAreExactForOneHundredYears()
    {
        var start = Advance(Simulation.Create(Baseline(), 847192), 120);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            Persistence.Save(path, start); var loaded = Persistence.Load(path);
            Assert.Equal(Persistence.Hash(start), Persistence.Hash(loaded));
            var a = Advance(start, 1200); var b = Advance(loaded, 1200);
            var c = Advance(Simulation.Create(Baseline(), 847192), 1320);
            Assert.Equal(Persistence.Hash(a), Persistence.Hash(b)); Assert.Equal(Persistence.Hash(a), Persistence.Hash(c));
            Assert.Equal(a.EventHash, b.EventHash); Assert.True(a.EventCount > 0);
        }
        finally { File.Delete(path); }
    }
    [Fact] public void EmptyWorldCannotReproduce()
    {
        var s = Baseline() with { InitialStageCounts = [0, 0, 0] };
        Assert.Equal(0, Advance(Simulation.Create(s, 4), 120).Population);
    }
    [Fact] public void JuvenilesCannotReproduceDuringTheirMaturationStep()
    {
        var s = Baseline(); s = s with { InitialStageCounts = [100, 0, 0], Species = s.Species.Select(x => x with { MaturationMonths = 1 }).ToArray() };
        Assert.All(Simulation.Step(Simulation.Create(s, 4)).Populations, f => Assert.Equal(0, f.Births));
    }
    [Fact] public void MovementRequiresAnAffordableEdge()
    {
        var s = Baseline(); s = s with { Species = s.Species.Select(x => x with { MaxTravelCost = 0 }).ToArray() };
        var w = Simulation.Create(s, 1);
        for (int i = 0; i < 36; i++) { var result = Simulation.Step(w); Assert.Empty(result.Transfers); w = result.State; }
    }
    [Fact] public void InvalidConfigurationAndIncompatibleSnapshotsAreRejected()
    {
        var s = Baseline();
        Assert.Throws<InvalidDataException>(() => Simulation.Create(s with { Rules = s.Rules with { Productivity = -1 } }, 0));
        var w = Simulation.Create(s, 1);
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { EngineVersion = "future" }));
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { RulesetHash = "changed" }));
        Assert.Throws<InvalidDataException>(() => Validation.State(w with { RngAlgorithm = "other" }));
    }
    [Fact] public void TraitInheritancePreservesVariantWithoutMutation()
    {
        var s = Baseline(); s = s with { InitialVariantWeights = [0, 1, 0], Rules = s.Rules with { MutationRate = 0 } };
        var end = Advance(Simulation.Create(s, 2), 120);
        Assert.True(end.Population > 0);
        Assert.All(end.Cohorts.Where(c => c.Id.Variant != 1), c => Assert.Equal(0, c.Count));
    }
    [Fact] public void AuditIsBoundedAndDoesNotChangeCanonicalResults()
    {
        var w = Simulation.Create(Baseline(), 3);
        var plain = Simulation.Step(w); var traced = Simulation.Step(w, audit: true);
        Assert.Empty(plain.Audit); Assert.Equal(w.Cohorts.Length, traced.Audit.Length);
        Assert.Equal(Persistence.Hash(plain.State), Persistence.Hash(traced.State));
        Assert.All(traced.Audit, a => Assert.Equal(a.Start, a.Deaths + a.Survivors));
    }
    [Fact] public void EqualCohortsUnderColdHaveDifferentialThermalMortality()
    {
        var s = Baseline();
        s = s with { InitialStockFraction = 1, InitialStageCounts = [300, 300, 300],
            Regions = s.Regions.Select(r => r with { Capacity = 1e9, BaseTemperature = -15 }).ToArray() };
        var step = Simulation.Step(Simulation.Create(s, 1), audit: true);
        for (int r = 0; r < 12; r++) for (int sp = 0; sp < 4; sp++) for (int age = 0; age < 3; age++)
        {
            var low = step.Audit[Simulation.Index(r, sp, age, 0, 4)];
            var high = step.Audit[Simulation.Index(r, sp, age, 2, 4)];
            Assert.True(high.DeathProbability < low.DeathProbability);
        }
    }
    [Fact] public void InvalidNullAndUnknownScenarioFieldsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Simulation.Create(Baseline() with { Regions = null! }, 0));
        Assert.Throws<System.Text.Json.JsonException>(() => Persistence.Decode<Scenario>("{\"unexpected\":true}"));
    }
}
