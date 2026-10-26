using Genesis.Core;
using Genesis.Cli;
using Xunit;

namespace Genesis.Tests;

public class ReportingTests
{
    [Fact] public void AnnualSummariesEqualAllTwelveMonthlyFlows()
    {
        var w = Simulation.Create(MechanicsTests.Baseline(), 1); var steps = new List<TickResult>();
        for (int tick = 1; tick <= 24; tick++)
        {
            var step = Simulation.Step(w); steps.Add(step); w = step.State;
            if (tick % 12 != 0) { Assert.Empty(step.Summaries); continue; }
            Assert.Equal(48, step.Summaries.Length);
            foreach (var summary in step.Summaries)
            {
                var a = summary.Window;
                var flows = steps.TakeLast(12).SelectMany(t => t.Populations).Where(f => f.Region == a.Region && f.Species == a.Species).ToArray();
                Assert.Equal(flows[0].Start, a.Start); Assert.Equal(flows.Sum(f => f.Births), a.Births);
                Assert.Equal(flows.Sum(f => f.Deaths), a.Deaths); Assert.Equal(flows.Sum(f => f.Outward), a.Outward);
                Assert.Equal(flows.Sum(f => f.Inward), a.Inward); Assert.Equal(flows.Sum(f => f.Consumed), a.Consumed, 6);
                Assert.Equal(flows.Sum(f => f.Matured), a.Matured);
                Assert.Equal(a.Start + a.Births - a.Deaths + a.Inward - a.Outward, summary.End);
            }
        }
        var waves = steps.SelectMany(s => s.Events).Where(e => e.Type == "migration-wave").ToArray();
        Assert.All(waves, e => { Assert.Equal(0, e.Tick % 12); Assert.True(e.Contributors.Single(c => c.Name == "outward").Value >= Math.Max(50, e.Before * .5)); });
        Assert.Equal(waves.Length, waves.Select(e => (e.Tick, e.RegionRefs[0], e.ActorRefs[0])).Distinct().Count());
    }
    [Fact] public void KnownMassMigrationEmitsOneMeasuredAnnualWave()
    {
        var s = MigrationForecastTests.CapacityCase() with { InitialPopulations = [new(new(0), new(0), [0, 80, 0])] };
        var w = Simulation.Create(s, 1); var events = new List<SimEvent>();
        for (int tick = 0; tick < 12; tick++) { var step = Simulation.Step(w); events.AddRange(step.Events); w = step.State; }
        var wave = Assert.Single(events, e => e.Type == "migration-wave");
        Assert.Equal(12, wave.Tick); Assert.Equal(80, wave.Before); Assert.Equal(0, wave.After);
        Assert.Equal(80, wave.Contributors.Single(c => c.Name == "outward").Value);
        Assert.Contains(events, e => e.Type == "population-change" && e.RegionRefs[0].Value == 1 && e.Before == 0 && e.After == 80);
    }
    [Fact] public void MidYearResumePreservesAnnualEvidenceAndEventHashes()
    {
        var s = MechanicsTests.Baseline() with { InitialPopulations = [new(new(0), new(0), [100, 500, 30])] };
        var a = Simulation.Create(s, 27); for (int t = 0; t < 7; t++) a = Simulation.Step(a).State;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            Persistence.Save(path, a); var b = Persistence.Load(path);
            for (int t = 0; t < 1200; t++)
            {
                var x = Simulation.Step(a); var y = Simulation.Step(b);
                Assert.Equal(Persistence.Encode(x.Summaries), Persistence.Encode(y.Summaries)); a = x.State; b = y.State;
            }
            Assert.Equal(Persistence.Hash(a), Persistence.Hash(b)); Assert.Equal(a.EventHash, b.EventHash);
        }
        finally { File.Delete(path); }
    }
    [Theory] [InlineData(1, "0.1.0")] [InlineData(2, "0.2.0")] [InlineData(3, "0.3.0")]
    public void LegacySnapshotGetsCompatibilityErrorBeforeChecksumError(int schema, string engine)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, Persistence.Encode(new { checksum = "legacy", state = new { schemaVersion = schema, engineVersion = engine } }));
            var e = Assert.Throws<InvalidDataException>(() => Persistence.Load(path)); Assert.Contains("original engine", e.Message);
        }
        finally { File.Delete(path); }
    }
    [Fact] public void GeographyControlsPreserveEachSpeciesAndLeaveEightRegionsEmpty()
    {
        var b = MechanicsTests.Baseline(); var cases = Calibration.Cases(b);
        var original = Simulation.Create(b, 1);
        foreach (var c in cases.Where(c => c.Name.StartsWith("geography-", StringComparison.Ordinal)))
        {
            var w = Simulation.Create(c.Scenario, 1); Assert.Equal(38400, w.Population);
            Assert.Equal(8, w.Cohorts.GroupBy(c => c.Id.Region).Count(g => g.Sum(c => c.Count) == 0));
            Assert.All(w.Cohorts.GroupBy(c => c.Id.Species), g => Assert.Equal(9600, g.Sum(c => c.Count)));
            for (int sp = 0; sp < 4; sp++) for (int a = 0; a < 3; a++) for (int v = 0; v < 3; v++)
                Assert.Equal(original.Cohorts.Where(c => c.Id.Species.Value == sp && c.Id.Stage == a && c.Id.Variant == v).Sum(c => c.Count),
                    w.Cohorts.Where(c => c.Id.Species.Value == sp && c.Id.Stage == a && c.Id.Variant == v).Sum(c => c.Count));
        }
    }
    [Fact] public void DemographyAssaysConservePeopleAndDistinguishEarlyMaturity()
    {
        var sp = MechanicsTests.Baseline().Species[0];
        foreach (bool binomial in new[] { false, true })
        {
            var fixedAge = DemographyAssay.Run(sp, 1, true, binomial);
            var geometric = DemographyAssay.Run(sp, 1, false, binomial);
            Assert.Equal(0, fixedAge.BirthsBeforeMaturation); Assert.True(geometric.BirthsBeforeMaturation > 0);
            Assert.Equal(fixedAge.Initial + fixedAge.Births - fixedAge.Deaths, fixedAge.Final);
            Assert.Equal(geometric.Initial + geometric.Births - geometric.Deaths, geometric.Final);
        }
    }
}
