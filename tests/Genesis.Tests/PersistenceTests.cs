using Genesis.Core;
using Genesis.Cli;
using Xunit;

namespace Genesis.Tests;

public class PersistenceTests
{
    [Fact] public void SnapshotCorruptionAndFailedReplacementPreserveExistingSave()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var w = Simulation.Create(MechanicsTests.Baseline(), 1); Persistence.Save(path, w);
            string good = File.ReadAllText(path);
            Assert.Throws<InvalidDataException>(() => Persistence.Save(path, w with { SchemaVersion = 99 }));
            Assert.Equal(good, File.ReadAllText(path));
            File.WriteAllText(path, good.Replace("\"seed\":1", "\"seed\":2", StringComparison.Ordinal));
            Assert.Throws<InvalidDataException>(() => Persistence.Load(path));
        }
        finally { File.Delete(path); }
    }
    [Fact] public void RunArchiveVerifiesAndRejectsTamperedLedger()
    {
        string path = Path.Combine(Path.GetTempPath(), "genesis-test-" + Guid.NewGuid());
        try
        {
            var w = Simulation.Create(MechanicsTests.Baseline(), 1);
            RunArchive.Run(w, 36, path, 12, 12, 1, 0); RunArchive.Verify(path);
            Assert.True(File.Exists(Path.Combine(path, "trace.json")));
            var summary = Path.Combine(path, "summaries.jsonl"); var original = File.ReadAllText(summary);
            File.AppendAllText(summary, "{}\n");
            Assert.Throws<InvalidDataException>(() => RunArchive.Verify(path));
            File.WriteAllText(summary, original); RunArchive.Verify(path);
            var ledger = Path.Combine(path, "events.jsonl");
            Assert.NotEmpty(File.ReadAllLines(ledger));
            File.AppendAllText(ledger, File.ReadLines(ledger).First() + Environment.NewLine);
            Assert.Throws<InvalidDataException>(() => RunArchive.Verify(path));
            Assert.Throws<InvalidDataException>(() => RunArchive.Run(w, 12, path, 12, 12, null, null));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    [Fact] public void FamineHysteresisDoesNotRepeatStartEveryMonth()
    {
        var s = MechanicsTests.Baseline(); s = s with { InitialStockFraction = 0, Rules = s.Rules with { Productivity = 0 } };
        var w = Simulation.Create(s, 1); var starts = new HashSet<(RegionId, SpeciesId)>();
        for (int i = 0; i < 24; i++)
        {
            var step = Simulation.Step(w);
            foreach (var ev in step.Events.Where(e => e.Type == "famine-start")) Assert.True(starts.Add((ev.RegionRefs[0], ev.ActorRefs[0])));
            w = step.State;
        }
        Assert.Equal(48, starts.Count);
    }
}
