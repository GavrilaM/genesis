using Genesis.Core;

namespace Genesis.Cli;

public static class IdentityArchive
{
    public static object Apply(WorldState input, IdentityCommand[] commands, string output)
    {
        Validation.Require(!Directory.Exists(output) && !File.Exists(output), "Output path exists; choose a new identity archive.");
        var result = PopulationOwnership.Apply(input, commands);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "status.json"), Persistence.Encode(new { status = "incomplete", kind = "identity" }));
        Persistence.Save(Path.Combine(output, "initial.json"), input);
        File.WriteAllText(Path.Combine(output, "identity-commands.json"), Persistence.Encode(commands));
        File.WriteAllLines(Path.Combine(output, "person-events.jsonl"), result.Events.Select(Persistence.Encode));
        Persistence.Save(Path.Combine(output, "latest.json"), result.State);
        var report = new { status = "complete", kind = "identity", tick = input.CurrentTick,
            commands = commands.Length, population = result.State.Population, identified = result.State.Identity.Living.Length,
            active = result.State.Identity.Living.Count(p => p.Active), stateHash = Persistence.Hash(result.State),
            biologicalProjectionHash = PopulationOwnership.BiologicalHash(result.State) };
        File.WriteAllText(Path.Combine(output, "report.json"), Persistence.Encode(report));
        File.WriteAllText(Path.Combine(output, "status.json"), Persistence.Encode(new { status = "complete", kind = "identity" }));
        return report;
    }

    public static void Verify(string directory)
    {
        var initial = Persistence.Load(Path.Combine(directory, "initial.json"));
        var final = Persistence.Load(Path.Combine(directory, "latest.json"));
        var commands = Persistence.Decode<IdentityCommand[]>(File.ReadAllText(Path.Combine(directory, "identity-commands.json")));
        var replay = PopulationOwnership.Apply(initial, commands);
        Validation.Require(Persistence.Hash(replay.State) == Persistence.Hash(final), "Identity command replay mismatch.");
        VerifyLedger(directory, initial, final);
        using var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "report.json")));
        Validation.Require(report.RootElement.GetProperty("stateHash").GetString() == Persistence.Hash(final)
            && report.RootElement.GetProperty("biologicalProjectionHash").GetString() == PopulationOwnership.BiologicalHash(final), "Identity report mismatch.");
    }

    public static void VerifyLedger(string directory, WorldState initial, WorldState final)
    {
        long count = initial.Identity.EventCount; string hash = initial.Identity.EventHash;
        int tick = initial.CurrentTick;
        var ids = final.Identity.Living.Concat(final.Identity.Deceased).Select(p => p.Id).ToHashSet();
        foreach (var line in File.ReadLines(Path.Combine(directory, "person-events.jsonl")))
        {
            var ev = Persistence.Decode<PersonEvent>(line);
            Validation.Require(ev.Sequence == ++count && ev.Tick >= tick && ev.Tick <= final.CurrentTick && ids.Contains(ev.Person), "Invalid identity event sequence/reference.");
            Validation.Require(ev.Type is "promote" or "set-active" or "death" or "maturation" or "elder-transition" or "migration", "Unknown identity event.");
            tick = ev.Tick; hash = Persistence.Hash(new { previous = hash, ev });
        }
        Validation.Require(count == final.Identity.EventCount && hash == final.Identity.EventHash, "Identity event chain mismatch.");
    }
}
