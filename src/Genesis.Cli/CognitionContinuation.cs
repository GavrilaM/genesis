using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Genesis.Core;

namespace Genesis.Cli;

// Reproduces a measured benchmark's explicit replenishment workload after a
// checkpoint. Ordinary run still performs no automatic materialization.
public static class CognitionContinuation
{
    public static object Run(string original, int checkpointTick, string output)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(original, "report.json")));
        var root = report.RootElement;
        var checkpoint = Persistence.Load(Path.Combine(original, $"snapshot-{checkpointTick:D8}.json"));
        int finalTick = root.GetProperty("endTick").GetInt32();
        Validation.Require(checkpoint.CurrentTick == checkpointTick && checkpointTick < finalTick, "Invalid continuation checkpoint.");
        var commands = Persistence.Decode<IdentityCommand[]>(File.ReadAllText(Path.Combine(original, "identity-commands.json")));
        var boundary = commands.Where(c => c.Tick == checkpointTick && c.Sequence > checkpoint.Identity.LastCommandSequence).ToArray();
        var plan = root.GetProperty("identity").GetProperty("replenishment");
        var replenishment = plan.ValueKind == JsonValueKind.Null ? null : Persistence.Decode<IdentityBenchmarkPlan>(plan.GetRawText());
        if (replenishment is not null) Validation.Require(checkpointTick % replenishment.EveryMonths == 0, "Replenishment continuation must align to its boundary.");
        RunArchive.Run(checkpoint, finalTick - checkpointTick, output, root.GetProperty("sampleEveryMonths").GetInt32(), 300,
            null, null, boundary.Length == 0 ? null : boundary, replenishment, true);
        RunArchive.Verify(output);
        var expected = Persistence.Load(Path.Combine(original, "latest.json"));
        var actual = Persistence.Load(Path.Combine(output, "latest.json"));
        Validation.Require(Persistence.Hash(expected) == Persistence.Hash(actual), "Full continuation state mismatch.");
        var checks = new List<object>();
        foreach (string file in new[] { "events.jsonl", "person-events.jsonl", "cognition-events.jsonl" })
        {
            using var fullHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var combinedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var line in File.ReadLines(Path.Combine(original, file)))
            {
                var bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine); fullHash.AppendData(bytes);
                using var ev = JsonDocument.Parse(line);
                if (ev.RootElement.GetProperty("tick").GetInt32() <= checkpointTick) combinedHash.AppendData(bytes);
            }
            // Boundary identity commands at tick checkpointTick occur after that
            // snapshot. Remove them from the prefix by reconstructing by sequence.
            if (file == "person-events.jsonl")
            {
                combinedHash.GetHashAndReset();
                foreach (var line in File.ReadLines(Path.Combine(original, file)))
                {
                    using var ev = JsonDocument.Parse(line);
                    if (ev.RootElement.GetProperty("sequence").GetInt64() <= checkpoint.Identity.EventCount)
                        combinedHash.AppendData(Encoding.UTF8.GetBytes(line + Environment.NewLine));
                }
            }
            foreach (var line in File.ReadLines(Path.Combine(output, file))) combinedHash.AppendData(Encoding.UTF8.GetBytes(line + Environment.NewLine));
            string full = Convert.ToHexString(fullHash.GetHashAndReset()), resumed = Convert.ToHexString(combinedHash.GetHashAndReset());
            Validation.Require(full == resumed, "Concatenated continuation history mismatch: " + file);
            checks.Add(new { file, sha256 = full, sameConcatenatedRecords = true });
        }
        var result = new { status = "complete", checkpointTick, finalTick, sameFullState = true,
            stateHash = Persistence.Hash(actual), sameBiology = PopulationOwnership.BiologicalHash(actual) == PopulationOwnership.BiologicalHash(expected),
            biologicalEventHash = actual.EventHash, identityEventHash = actual.Identity.EventHash, cognitionEventHash = actual.Cognition.EventHash, histories = checks };
        File.WriteAllText(Path.Combine(output, "continuation.json"), Persistence.Encode(result)); return result;
    }
}
