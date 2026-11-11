using System.Diagnostics;
using Genesis.Core;

namespace Genesis.Cli;

public static class CognitionProfile
{
    public static object Run(WorldState source, int iterations, string output, int warmMonths = 8)
    {
        Validation.Require(iterations is > 0 and <= 1000 && warmMonths is >= 0 and <= 8 && !File.Exists(output), "Invalid profile iteration count, warm-up or output exists.");
        Validation.State(source);
        string sourceHash = Persistence.Hash(source);
        // Warm bounded memory through real observation, never by inventing visits.
        var warm = source with { Cognition = source.Cognition with { Options = source.Cognition.Options with { Mode = CognitionMode.ObserveOnly } } };
        for (int t = 0; t < warmMonths; t++) warm = Simulation.Step(warm).State;
        var results = new List<object>(); string? bio = null, identity = null;
        foreach (var mode in new[] { CognitionMode.Off, CognitionMode.ObserveOnly })
        {
            var input = warm with { Cognition = warm.Cognition with { Options = warm.Cognition.Options with { Mode = mode } } };
            Validation.State(input); string before = Persistence.Hash(input);
            _ = Simulation.Step(input, profile: true); // JIT/warm-up outside measured interval.
            long allocationStart = GC.GetTotalAllocatedBytes(true); var watch = Stopwatch.StartNew();
            TickResult? last = null;
            double validation = 0, biology = 0, cognition = 0, migration = 0, commit = 0, evidence = 0;
            for (int i = 0; i < iterations; i++)
            {
                last = Simulation.Step(input, profile: true); var t = last.Timing!;
                validation += t.ValidationSeconds; biology += t.BiologySeconds; cognition += t.CognitionSeconds;
                migration += t.MigrationSeconds; commit += t.IdentityCommitSeconds; evidence += t.CognitionEvidenceSeconds;
            }
            watch.Stop(); long allocated = GC.GetTotalAllocatedBytes(true) - allocationStart;
            string afterBio = PopulationOwnership.BiologicalHash(last!);
            string afterIdentity = Persistence.Hash(last!.State.Identity);
            bio ??= afterBio; identity ??= afterIdentity;
            Validation.Require(bio == afterBio && identity == afterIdentity && before == Persistence.Hash(input), "Matched checkpoint profile changed its biological/identity input.");
            results.Add(new { mode, iterations, elapsedSeconds = watch.Elapsed.TotalSeconds, allocatedBytes = allocated,
                validationSeconds = validation, biologySeconds = biology, cognitionSeconds = cognition, migrationSeconds = migration,
                identityCommitSeconds = commit, cognitionEvidenceSeconds = evidence, observationCount = last.CognitionEvents.Length,
                mindBytes = System.Text.Encoding.UTF8.GetByteCount(Persistence.Encode(last.State.Cognition.Minds)) });
        }
        Validation.Require(Persistence.Hash(source) == sourceHash, "Profile changed source snapshot.");
        var report = new { status = "complete", sourceTick = source.CurrentTick, sourceDeceased = source.Identity.Deceased.Length,
            warmMonths, measuredTick = warm.CurrentTick + 1,
            living = warm.Identity.Living.Length, active = warm.Identity.Living.Count(p => p.Active), deceased = warm.Identity.Deceased.Length,
            protocol = "Configurable 0..8 simulated ObserveOnly warm months; repeatedly Step the same validated input. Full history validation enabled; no tombstones removed. No archive IO in this component profile.",
            sameBiologyAndIdentity = true, biologicalHash = bio, results };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, Persistence.Encode(report)); return report;
    }
}
