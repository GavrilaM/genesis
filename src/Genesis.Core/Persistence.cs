using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genesis.Core;

public static class Persistence
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Decode<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)
        ?? throw new InvalidDataException("Empty JSON payload.");
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Encode(value))));
    public static readonly string EmptyEventHash = new('0', 64);
    public static string Chain(string previous, SimEvent ev) => Hash(new { previous, ev });
    public static Scenario ReadScenario(string path)
    {
        var value = Decode<Scenario>(File.ReadAllText(path));
        Validation.Scenario(value);
        return value;
    }
    private sealed record Envelope(string Checksum, WorldState State);
    public static void Save(string path, WorldState state)
    {
        Validation.State(state);
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new Envelope(Hash(state), state), Json);
                stream.Flush(true);
            }
            // Reload validates the serialized representation before publishing it.
            _ = Load(temporary);
            File.Move(temporary, full, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static WorldState Load(string path)
    {
        var envelope = Decode<Envelope>(File.ReadAllText(path));
        if (Hash(envelope.State) != envelope.Checksum) throw new InvalidDataException("Snapshot checksum mismatch.");
        Validation.State(envelope.State);
        return envelope.State;
    }
}
