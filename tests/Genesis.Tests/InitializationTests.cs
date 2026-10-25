using Genesis.Core;
using Xunit;

namespace Genesis.Tests;

public class InitializationTests
{
    [Fact] public void LegacyScenarioFallbackPreservesAllStartingCohorts()
    {
        var s = MechanicsTests.Baseline(); Assert.Null(s.InitialPopulations);
        var implicitWorld = Simulation.Create(s, 1);
        var entries = s.Regions.SelectMany(r => s.Species.Select(sp => new InitialPopulation(r.Id, sp.Id, s.InitialStageCounts))).ToArray();
        var explicitWorld = Simulation.Create(s with { InitialPopulations = entries }, 1);
        Assert.Equal(38400, implicitWorld.Population);
        Assert.Equal(Persistence.Encode(implicitWorld.Cohorts), Persistence.Encode(explicitWorld.Cohorts));
    }
    [Fact] public void SparseEntriesOverrideDefaultsAndKeepUninhabitedCohorts()
    {
        var s = MechanicsTests.Baseline() with { InitialPopulations = [
            new(new(0), new(0), [10, 20, 30], [1, 0, 0]), new(new(5), new(2), [0, 17, 0], [0, 1, 0])] };
        var w = Simulation.Create(s, 1);
        Assert.Equal(77, w.Population); Assert.Equal(432, w.Cohorts.Length);
        Assert.Equal(60, w.Cohorts.Where(c => c.Id.Region.Value == 0).Sum(c => c.Count));
        Assert.Equal(17, w.Cohorts.Single(c => c.Id == new CohortId(new(5), new(2), 1, 1)).Count);
        Assert.All(w.Cohorts.Where(c => c.Id.Region.Value is not (0 or 5)), c => Assert.Equal(0, c.Count));
        s.InitialPopulations![0].StageCounts[0] = 999;
        Assert.Equal(77, w.Population); // creation takes a detached copy
    }
    [Fact] public void ExplicitEmptyListCreatesAnEmptyWorldDespiteNonzeroDefaults()
    {
        var w = Simulation.Create(MechanicsTests.Baseline() with { InitialPopulations = [] }, 1);
        for (int i = 0; i < 24; i++) w = Simulation.Step(w).State;
        Assert.Equal(0, w.Population);
    }
    [Fact] public void MigrationColonizesAnEmptyRegionWithoutCreatingPeople()
    {
        var s = MigrationForecastTests.CapacityCase() with { InitialPopulations = [new(new(0), new(0), [0, 80, 0])] };
        var step = Simulation.Step(Simulation.Create(s, 1));
        Assert.Equal(80, step.State.Population);
        Assert.True(step.Populations[1].End > 0); Assert.Equal(0, step.Populations[1].Births);
        Assert.Equal(step.Populations[0].Outward, step.Populations[1].Inward);
        Assert.All(step.Transfers, t => Assert.Equal(new RegionId(0), t.Origin.Region));
    }
    [Fact] public void BadReferencesDuplicatesOrderAndCountsAreRejected()
    {
        var s = MechanicsTests.Baseline(); var p = new InitialPopulation(new(0), new(0), [1, 1, 1]);
        InitialPopulation[][] invalid = [[p, p], [p with { Region = new(12) }], [p with { Species = new(4) }],
            [p with { StageCounts = [-1, 0, 0] }], [p with { StageCounts = [1, 1] }],
            [p with { VariantWeights = [1, 1, 1] }], [p with { Region = new(1) }, p]];
        foreach (var entries in invalid) Assert.Throws<InvalidDataException>(() => Simulation.Create(s with { InitialPopulations = entries }, 1));
    }
    [Fact] public void DoubledSparseScenarioDoublesPopulationAndPreservesCopies()
    {
        var s = MechanicsTests.Baseline() with { InitialPopulations = [new(new(2), new(1), [1, 2, 3])] };
        var w = Simulation.Create(Experiments.DoubleRegions(s), 1);
        Assert.Equal(12, w.Population); Assert.Equal(6, w.Cohorts.Where(c => c.Id.Region.Value == 14).Sum(c => c.Count));
    }
    [Fact] public void ExplicitTraitCountsAreExactAndRejectAmbiguousOrUnbalancedInputs()
    {
        var entry = new InitialPopulation(new(0), new(0), [10, 20, 30], StageVariantCounts: [[2, 3, 5], [10, 0, 10], [1, 28, 1]]);
        var s = MechanicsTests.Baseline() with { InitialPopulations = [entry] };
        var w = Simulation.Create(s, 1);
        Assert.Equal(28, w.Cohorts.Single(c => c.Id == new CohortId(new(0), new(0), 2, 1)).Count);
        Assert.Throws<InvalidDataException>(() => Simulation.Create(s with { InitialPopulations = [entry with { VariantWeights = [1, 0, 0] }] }, 1));
        Assert.Throws<InvalidDataException>(() => Simulation.Create(s with { InitialPopulations = [entry with { StageVariantCounts = [[2, 3, 4], [10, 0, 10], [1, 28, 1]] }] }, 1));
    }
}
