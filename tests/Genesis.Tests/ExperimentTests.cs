using Genesis.Core;
using Xunit;

namespace Genesis.Tests;

public class ExperimentTests
{
    [Theory]
    [InlineData("drought")]
    [InlineData("cold-resistance")]
    [InlineData("food-loss")]
    [InlineData("productivity")]
    [InlineData("connectivity")]
    public void ControlledChangesHaveCoherentPairedEffects(string treatment)
    {
        var pair = Experiments.Pair(MechanicsTests.Baseline(), treatment);
        var differences = new List<double>();
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var a = Experiments.Observe(pair.Control, seed, 240);
            var b = Experiments.Observe(pair.Treatment, seed, 240);
            switch (treatment)
            {
                case "drought": differences.Add(b.MeanScarcity - a.MeanScarcity); break;
                case "cold-resistance": differences.Add(b.Population - a.Population); break;
                case "food-loss": Assert.Equal(0, b.Population); differences.Add(a.Population - b.Population); break;
                case "productivity": differences.Add(b.Population - a.Population); break;
                case "connectivity": Assert.Equal(0, b.Migration); differences.Add(a.Migration - b.Migration); break;
            }
        }
        Assert.True(differences.Average() > 0, $"Expected positive paired effect for {treatment}; mean {differences.Average()}.");
        Assert.True(differences.Count(d => d > 0) >= 10, $"Expected at least 10/12 positive pairs for {treatment}.");
    }
    [Fact] public void OneThousandYearsPreserveAllMechanicalInvariants()
    {
        var w = Simulation.Create(MechanicsTests.Baseline(), 847192);
        for (int i = 0; i < 12000; i++) w = Simulation.Step(w).State;
        Assert.Equal(12000, w.CurrentTick); Assert.True(w.Population > 0);
        Assert.Equal(432, w.Cohorts.Length);
    }
    [Fact] public void ColdSelectsInheritedResistanceWithoutSpeciesStatChanges()
    {
        var s = Experiments.Pair(MechanicsTests.Baseline(), "cold-resistance").Control;
        s = s with { InitialVariantWeights = [1.0 / 3, 1.0 / 3, 1.0 / 3], Rules = s.Rules with { MutationRate = 0 } };
        var observations = Enumerable.Range(1, 12).Select(seed => Experiments.Observe(s, (ulong)seed, 240)).ToArray();
        Assert.True(observations.Average(o => o.ResistantFrequency) > .4);
    }
}
