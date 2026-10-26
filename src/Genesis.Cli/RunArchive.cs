using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Genesis.Core;

namespace Genesis.Cli;

// Benchmark-only external commands, not an automatic materialization mechanic.
public sealed record IdentityBenchmarkPlan(int Target, int Active, int EveryMonths);

public static class RunArchive
{
    public static object Run(WorldState start, int months, string output, int sampleEvery, int snapshotEvery, int? traceTick, int? traceRegion,
        IdentityCommand[]? initialCommands = null, IdentityBenchmarkPlan? replenishment = null)
    {
        Validation.Require(months > 0 && sampleEvery > 0 && snapshotEvery > 0, "Durations and intervals must be positive.");
        _ = checked(start.CurrentTick + months);
        Validation.Require(traceTick is null || (traceTick > start.CurrentTick && traceTick <= start.CurrentTick + months), "Trace tick must fall within this run.");
        Validation.Require(traceRegion is null || (traceRegion >= 0 && traceRegion < start.Regions.Length), "Unknown trace region.");
        Validation.Require(!Directory.Exists(output) && !File.Exists(output), "Output path already exists; use a new run directory.");
        // Validate the entire representation transaction before publishing a run.
        var setup = initialCommands is null ? null : PopulationOwnership.Apply(start, initialCommands);
        if (replenishment is not null)
            Validation.Require(replenishment.Target > 0 && replenishment.Target <= start.Identity.Options.MaxIdentified
                && replenishment.Active >= 0 && replenishment.Active <= replenishment.Target
                && replenishment.Active <= start.Identity.Options.MaxActiveMinds && replenishment.EveryMonths > 0,
                "Invalid benchmark replenishment plan.");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "status.json"), Persistence.Encode(new { status = "incomplete", startTick = start.CurrentTick }));
        Persistence.Save(Path.Combine(output, "initial.json"), start);
        var watch = Stopwatch.StartNew();
        var w = setup?.State ?? start; long births = 0, deaths = 0, migrations = 0; int eventCount = 0;
        int initialIdentified = w.Identity.Living.Length, initialActive = w.Identity.Living.Count(p => p.Active);
        long identifiedPersonMonths = 0, activeReservationMonths = 0;
        var executedCommands = new List<IdentityCommand>(initialCommands ?? []);
        File.WriteAllText(Path.Combine(output, "identity-commands.json"), Persistence.Encode(initialCommands ?? []));
        double produced = 0, consumed = 0;
        using (var events = new StreamWriter(Path.Combine(output, "events.jsonl")))
        using (var personEvents = new StreamWriter(Path.Combine(output, "person-events.jsonl")))
        using (var population = new StreamWriter(Path.Combine(output, "population.csv")))
        using (var food = new StreamWriter(Path.Combine(output, "food.csv")))
        using (var summaries = new StreamWriter(Path.Combine(output, "summaries.jsonl")))
        {
            foreach (var ev in setup?.Events ?? []) personEvents.WriteLine(Persistence.Encode(ev));
            population.WriteLine("tick,region,species,start,births,deaths,inward,outward,end,demand,consumed,nutrition,hungry_months,trait0,trait1,trait2,matured");
            food.WriteLine("tick,region,start,regrowth,consumed,end,temperature_c,rainfall_mm");
            for (int i = 0; i < months; i++)
            {
                if (replenishment is not null && i > 0 && i % replenishment.EveryMonths == 0)
                {
                    int missing = (int)Math.Min(w.Cohorts.Sum(c => c.Count), Math.Max(0, replenishment.Target - w.Identity.Living.Length));
                    if (missing > 0)
                    {
                        var commands = PopulationOwnership.PlanPromotions(w, missing,
                            Math.Min(missing, Math.Max(0, replenishment.Active - w.Identity.Living.Count(p => p.Active))));
                        var transaction = PopulationOwnership.Apply(w, commands); w = transaction.State;
                        executedCommands.AddRange(commands);
                        foreach (var ev in transaction.Events) personEvents.WriteLine(Persistence.Encode(ev));
                    }
                }
                identifiedPersonMonths += w.Identity.Living.Length;
                activeReservationMonths += w.Identity.Living.Count(p => p.Active);
                var step = Simulation.Step(w, audit: traceTick == w.CurrentTick + 1); w = step.State;
                foreach (var ev in step.Events) { events.WriteLine(Persistence.Encode(ev)); eventCount++; }
                foreach (var ev in step.PersonEvents) personEvents.WriteLine(Persistence.Encode(ev));
                foreach (var summary in step.Summaries) summaries.WriteLine(Persistence.Encode(summary));
                births += step.Populations.Sum(f => f.Births); deaths += step.Populations.Sum(f => f.Deaths);
                migrations += step.Populations.Sum(f => f.Outward);
                produced += step.Food.Sum(f => f.Regrowth); consumed += step.Food.Sum(f => f.Consumed);
                if (w.CurrentTick % sampleEvery == 0 || i == months - 1)
                {
                    foreach (var f in step.Populations) population.WriteLine(Csv(w.CurrentTick, f.Region.Value, f.Species.Value, f.Start, f.Births, f.Deaths, f.Inward, f.Outward, f.End, f.Demand, f.Consumed, f.Nutrition, f.HungryMonths, f.TraitCounts[0], f.TraitCounts[1], f.TraitCounts[2], f.Matured));
                    foreach (var f in step.Food) food.WriteLine(Csv(w.CurrentTick, f.Region.Value, f.Start, f.Regrowth, f.Consumed, f.End, f.Temperature, f.Rainfall));
                }
                if (traceTick == w.CurrentTick)
                    File.WriteAllText(Path.Combine(output, "trace.json"), Persistence.Encode(new
                    {
                        tick = w.CurrentTick, region = traceRegion,
                        populations = step.Populations.Where(f => traceRegion is null || f.Region.Value == traceRegion),
                        food = step.Food.Where(f => traceRegion is null || f.Region.Value == traceRegion),
                        survival = step.Audit.Where(f => traceRegion is null || f.Cohort.Region.Value == traceRegion),
                        forecasts = step.Forecasts.Where(f => traceRegion is null || f.Region.Value == traceRegion),
                        transfers = step.Transfers.Where(f => traceRegion is null || f.Origin.Region.Value == traceRegion || f.Destination.Value == traceRegion)
                    }));
                if (w.CurrentTick % snapshotEvery == 0) Persistence.Save(Path.Combine(output, $"snapshot-{w.CurrentTick:D8}.json"), w);
            }
        }
        Persistence.Save(Path.Combine(output, "latest.json"), w);
        File.WriteAllText(Path.Combine(output, "identity-commands.json"), Persistence.Encode(executedCommands));
        watch.Stop();
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var initialCohorts = PopulationOwnership.CombinedCohorts(start);
        var finalCohorts = PopulationOwnership.CombinedCohorts(w);
        var report = new
        {
            status = "complete", engineVersion = Simulation.Version, w.RulesetHash, w.Seed,
            startTick = start.CurrentTick, endTick = w.CurrentTick, months, regions = w.Regions.Length, cohorts = w.Cohorts.Length,
            initialPopulation = start.Population, finalPopulation = w.Population, births, deaths, migrations,
            foodProduced = produced, foodConsumed = consumed,
            species = w.Scenario.Species.Select(s => new { s.Id, s.Name, initialPopulation = initialCohorts.Where(c => c.Id.Species == s.Id).Sum(c => c.Count), finalPopulation = finalCohorts.Where(c => c.Id.Species == s.Id).Sum(c => c.Count) }).ToArray(),
            extinctSpecies = w.Scenario.Species.Count(s => finalCohorts.Where(c => c.Id.Species == s.Id).Sum(c => c.Count) == 0),
            stateHash = Persistence.Hash(w), w.EventHash, eventCount, totalEventCount = w.EventCount,
            biologicalProjectionHash = PopulationOwnership.BiologicalHash(w),
            identity = new { initialIdentified, initialActive, living = w.Identity.Living.Length,
                active = w.Identity.Living.Count(p => p.Active), deceased = w.Identity.Deceased.Length,
                anonymous = w.Cohorts.Sum(c => c.Count), w.Identity.Options, w.Identity.EventCount, w.Identity.EventHash,
                identifiedPersonMonths, activeReservationMonths, meanLiving = identifiedPersonMonths / (double)months,
                representationCommands = executedCommands.Count, replenishment },
            sampleEveryMonths = sampleEvery, sampleSemantics = "monthly flows at sampled ticks; not interval totals",
            elapsedSeconds = watch.Elapsed.TotalSeconds, ticksPerSecond = months / watch.Elapsed.TotalSeconds,
            peakWorkingSetBytes = process.PeakWorkingSet64, snapshotBytes = new FileInfo(Path.Combine(output, "latest.json")).Length,
            eventBytes = new FileInfo(Path.Combine(output, "events.jsonl")).Length,
            summarySha256 = FileHash(Path.Combine(output, "summaries.jsonl")),
            populationSha256 = FileHash(Path.Combine(output, "population.csv")), foodSha256 = FileHash(Path.Combine(output, "food.csv")),
            personEventBytes = new FileInfo(Path.Combine(output, "person-events.jsonl")).Length,
            identityCommandSha256 = FileHash(Path.Combine(output, "identity-commands.json")),
            runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessors = Environment.ProcessorCount,
            validation = "full every tick; file IO included; process peak memory includes startup"
        };
        File.WriteAllText(Path.Combine(output, "report.json"), Persistence.Encode(report));
        File.WriteAllText(Path.Combine(output, "status.json"), Persistence.Encode(new { status = "complete", endTick = w.CurrentTick }));
        return report;
    }
    public static string Csv(params object[] fields) => string.Join(',', fields.Select(f => Convert.ToString(f, CultureInfo.InvariantCulture)));
    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static void Verify(string directory)
    {
        using var status = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "status.json")));
        Validation.Require(status.RootElement.GetProperty("status").GetString() == "complete", "Run archive is incomplete.");
        if (status.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == "identity")
        { IdentityArchive.Verify(directory); return; }
        var initial = Persistence.Load(Path.Combine(directory, "initial.json"));
        var final = Persistence.Load(Path.Combine(directory, "latest.json"));
        string hash = initial.EventHash; long count = initial.EventCount; int tick = initial.CurrentTick;
        foreach (string line in File.ReadLines(Path.Combine(directory, "events.jsonl")))
        {
            var ev = Persistence.Decode<SimEvent>(line);
            Validation.Require(ev.Id.Value == ++count && ev.Tick > initial.CurrentTick && ev.Tick >= tick && ev.Tick <= final.CurrentTick, "Broken event sequence.");
            Validation.Require(ev.ActorRefs.All(s => s.Value >= 0 && s.Value < final.Scenario.Species.Length) && ev.RegionRefs.All(r => r.Value >= 0 && r.Value < final.Regions.Length), "Broken event references.");
            tick = ev.Tick; hash = Persistence.Chain(hash, ev);
        }
        Validation.Require(hash == final.EventHash && count == final.EventCount, "Event ledger hash/count mismatch.");
        IdentityArchive.VerifyLedger(directory, initial, final);
        Validation.Require(initial.RulesetHash == final.RulesetHash && initial.Seed == final.Seed && final.CurrentTick >= initial.CurrentTick, "Run inputs changed.");
        using var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "report.json")));
        Validation.Require(report.RootElement.GetProperty("summarySha256").GetString() == FileHash(Path.Combine(directory, "summaries.jsonl")),
            "Historical summary checksum mismatch.");
        Validation.Require(report.RootElement.GetProperty("stateHash").GetString() == Persistence.Hash(final)
            && report.RootElement.GetProperty("biologicalProjectionHash").GetString() == PopulationOwnership.BiologicalHash(final), "Report/snapshot mismatch.");
        foreach (var name in new[] { "population", "food" })
            Validation.Require(report.RootElement.GetProperty(name + "Sha256").GetString() == FileHash(Path.Combine(directory, name + ".csv")), "Metric checksum mismatch.");
        Validation.Require(report.RootElement.GetProperty("identityCommandSha256").GetString() == FileHash(Path.Combine(directory, "identity-commands.json")), "Identity command checksum mismatch.");
    }
}
