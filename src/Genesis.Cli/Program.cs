using System.Globalization;
using System.Text.Json;
using Genesis.Core;
using Genesis.Cli;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
try
{
    var cli = new Arguments(args.Length == 0 ? ["help"] : args);
    var verb = cli.Positionals.FirstOrDefault() ?? "help";
    cli.ExpectPositionals(verb switch { "inspect" => cli.Pos(1) == "world" ? 2 : 3, "explain" or "compare" => 3, "verify" => 2, _ => 1 });
    Scenario Scenario() => Persistence.ReadScenario(cli.Get("scenario", "scenarios/baseline.json"));
    WorldState Save() => Persistence.Load(cli.Required("save"));
    void Print(object value) => Console.WriteLine(Persistence.Encode(value));
    string RunPath(string file) => Path.Combine(cli.Required("run"), file);
    IEnumerable<SimEvent> Events() => File.Exists(RunPath("events.jsonl"))
        ? File.ReadLines(RunPath("events.jsonl")).Select(Persistence.Decode<SimEvent>) : [];
    switch (verb)
    {
        case "new":
            cli.Allow("seed", "scenario", "out");
            var path = cli.Required("out");
            Validation.Require(!File.Exists(path), "Save exists; choose another path.");
            var created = Simulation.Create(Scenario(), cli.Seed()); Persistence.Save(path, created);
            Print(new { save = Path.GetFullPath(path), population = created.Population, stateHash = Persistence.Hash(created) }); break;
        case "run":
        case "benchmark":
            if (verb == "run") cli.Allow("save", "years", "out", "sample-every", "snapshot-every", "trace-tick", "trace-region");
            else cli.Allow("years", "out", "sample-every", "snapshot-every", "trace-tick", "trace-region", "seed", "scenario", "double-regions");
            var initial = verb == "run" ? Save() : Simulation.Create(cli.Has("double-regions") ? Experiments.DoubleRegions(Scenario()) : Scenario(), cli.Seed());
            Print(RunArchive.Run(initial, checked(cli.Int("years", 1000) * 12), cli.Required("out"), cli.Int("sample-every", 12),
                checked(cli.Int("snapshot-every", 25) * 12), cli.OptionalInt("trace-tick"), cli.OptionalInt("trace-region"))); break;
        case "inspect":
            cli.Allow("save"); var w = Save();
            switch (cli.Pos(1))
            {
                case "world": Print(new { w.CurrentTick, w.Seed, w.RulesetHash, population = w.Population, w.Regions, species = w.Scenario.Species.Select(s => new { s.Id, s.Name, population = w.Cohorts.Where(c => c.Id.Species == s.Id).Sum(c => c.Count) }), w.EventCount, w.EventHash }); break;
                case "region":
                    int r = int.Parse(cli.Pos(2)); Validation.Require(r >= 0 && r < w.Regions.Length, "Unknown region.");
                    Print(new { definition = w.Scenario.Regions[r], state = w.Regions[r], cohorts = w.Cohorts.Where(c => c.Id.Region.Value == r), memory = w.Memory.Where(m => m.Region.Value == r) }); break;
                case "species":
                    int s = int.Parse(cli.Pos(2)); Validation.Require(s >= 0 && s < w.Scenario.Species.Length, "Unknown species.");
                    Print(new { definition = w.Scenario.Species[s], cohorts = w.Cohorts.Where(c => c.Id.Species.Value == s) }); break;
                default: throw new ArgumentException("inspect requires world, region <id>, or species <id>.");
            }
            break;
        case "history":
            cli.Allow("run", "from", "to", "type");
            var selected = Events().Where(e => e.Tick > cli.Int("from", 0) * 12L && e.Tick <= cli.Int("to", int.MaxValue / 12) * 12L && (!cli.Has("type") || e.Type == cli.Required("type"))).ToArray();
            if (selected.Length == 0) Console.WriteLine("not recorded"); else foreach (var ev in selected) Print(ev); break;
        case "explain":
            cli.Allow("run"); Validation.Require(cli.Pos(1) == "event", "Use explain event <id>.");
            var found = Events().FirstOrDefault(e => e.Id.Value == long.Parse(cli.Pos(2)));
            if (found is null) Console.WriteLine("not recorded");
            else Print(new { recordedEvent = found, interpretation = "Contributors are model-logged inputs; no experimental causal attribution or personal thoughts were recorded." }); break;
        case "history-summary":
            cli.Allow("run", "from", "to", "region", "species");
            var summaryPath = RunPath("summaries.jsonl");
            var summaries = File.Exists(summaryPath) ? File.ReadLines(summaryPath).Select(Persistence.Decode<PopulationSummary>) : [];
            var matching = summaries.Where(s => s.Window.StartTick >= cli.Int("from", 0) * 12L && s.EndTick <= cli.Int("to", int.MaxValue / 12) * 12L
                && (cli.OptionalInt("region") is null || s.Window.Region.Value == cli.OptionalInt("region"))
                && (cli.OptionalInt("species") is null || s.Window.Species.Value == cli.OptionalInt("species"))).ToArray();
            if (matching.Length == 0) Console.WriteLine("not recorded"); else foreach (var summary in matching) Print(summary);
            break;
        case "trace":
            cli.Allow("run", "region", "year", "month");
            int month = cli.Int("month", 1); Validation.Require(month is >= 1 and <= 12, "month must be 1..12; year is zero-based.");
            int requested = checked(cli.Int("year", 0) * 12 + month);
            var traceFile = RunPath("trace.json");
            if (!File.Exists(traceFile)) { Console.WriteLine("not recorded"); break; }
            using (var trace = JsonDocument.Parse(File.ReadAllText(traceFile)))
            {
                int? recordedRegion = trace.RootElement.GetProperty("region").ValueKind == JsonValueKind.Null ? null : trace.RootElement.GetProperty("region").GetInt32();
                if (trace.RootElement.GetProperty("tick").GetInt32() != requested || recordedRegion != cli.OptionalInt("region")) Console.WriteLine("not recorded");
                else Console.WriteLine(trace.RootElement.GetRawText());
            }
            break;
        case "verify":
            cli.Allow(); var target = cli.Pos(1);
            if (Directory.Exists(target)) RunArchive.Verify(target); else _ = Persistence.Load(target);
            Console.WriteLine("verified"); break;
        case "compare":
            cli.Allow(); var a = Persistence.Load(cli.Pos(1)); var b = Persistence.Load(cli.Pos(2));
            Print(new { aTick = a.CurrentTick, bTick = b.CurrentTick, sameRules = a.RulesetHash == b.RulesetHash, sameSeed = a.Seed == b.Seed,
                populationA = a.Population, populationB = b.Population, difference = b.Population - a.Population,
                sameState = Persistence.Hash(a) == Persistence.Hash(b), sameEventHistory = a.EventHash == b.EventHash }); break;
        case "export":
            cli.Allow("run", "format", "out"); Validation.Require(cli.Get("format", "csv") == "csv", "Only csv export is supported.");
            var dest = cli.Required("out"); Validation.Require(!Directory.Exists(dest), "Export directory exists.");
            Validation.Require(File.Exists(RunPath("population.csv")) && File.Exists(RunPath("food.csv")), "Metrics not recorded.");
            Directory.CreateDirectory(dest); foreach (var file in new[] { "population.csv", "food.csv" }) File.Copy(RunPath(file), Path.Combine(dest, file));
            Print(new { exported = Path.GetFullPath(dest) }); break;
        case "experiments":
            cli.Allow("scenario", "seeds", "years", "out");
            var experimentPath = cli.Required("out"); Validation.Require(!File.Exists(experimentPath), "Experiment output already exists.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(experimentPath))!);
            using (var writer = new StreamWriter(experimentPath))
                foreach (var row in Experiments.Run(Scenario(), cli.Int("seeds", 16), checked(cli.Int("years", 30) * 12))) writer.WriteLine(Persistence.Encode(row));
            Print(new { output = Path.GetFullPath(experimentPath), seeds = cli.Int("seeds", 16), years = cli.Int("years", 30), treatments = Experiments.Treatments }); break;
        case "calibrate":
            cli.Allow("scenario", "seeds", "years", "out", "cases");
            Calibration.Run(Scenario(), cli.Int("seeds", 8), checked(cli.Int("years", 300) * 12), cli.Required("out"), cli.Has("cases") ? cli.Required("cases") : null);
            Print(new { output = Path.GetFullPath(cli.Required("out")), status = "complete" }); break;
        case "demography-study":
            cli.Allow("scenario", "suite", "seeds", "years", "out", "models");
            DemographicStudy.Run(Scenario(), cli.Get("suite", "worlds"), cli.Int("seeds", 8), cli.Int("years", 300), cli.Required("out"), cli.Has("models") ? cli.Required("models") : null);
            Print(new { output = Path.GetFullPath(cli.Required("out")), status = "complete" }); break;
        case "demography-assay":
            cli.Allow("scenario", "seeds", "out");
            var assayPath = cli.Required("out"); Validation.Require(!File.Exists(assayPath), "Assay output exists.");
            var assay = DemographyAssay.Report(Scenario().Species[0], cli.Int("seeds", 64));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(assayPath))!);
            File.WriteAllText(assayPath, Persistence.Encode(assay)); Print(new { output = Path.GetFullPath(assayPath) }); break;
        case "help":
            Console.WriteLine("Genesis Prototype 001\nnew --seed N --scenario FILE --out SAVE\nrun --save SAVE --years N --out NEW_DIRECTORY [--sample-every MONTHS --snapshot-every YEARS --trace-tick T --trace-region ID]\ninspect world|region ID|species ID --save SAVE\nhistory --run DIRECTORY [--from YEAR --to YEAR --type TYPE]\nexplain event ID --run DIRECTORY\ntrace --run DIRECTORY --year Y --month M [--region ID]\nverify SAVE_OR_RUN_DIRECTORY\ncompare SAVE_A SAVE_B\nexport --run DIRECTORY --format csv --out NEW_DIRECTORY\nbenchmark --seed N --years N --out NEW_DIRECTORY [--double-regions]\nexperiments --seeds N --years N --out JSONL\nhistory-summary --run DIRECTORY [--from YEAR --to YEAR --region ID --species ID]\ncalibrate --seeds N --years N --out NEW_DIRECTORY [--cases baseline,swap-birth,...]\ndemography-assay --seeds N --out JSON\ndemography-study --suite worlds|pulse|extinction|bottleneck|death-only --seeds N --years N --out NEW_DIRECTORY [--models geometric-rounding,monthly-binomial]\nAll scenario defaults: scenarios/baseline.json. All years are integers; months are 1..12, years start at 0."); break;
        default: throw new ArgumentException($"Unknown command '{verb}'. Use help.");
    }
    return 0;
}
catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or JsonException or FormatException or OverflowException or InvalidOperationException)
{
    Console.Error.WriteLine($"error: {e.Message}"); return 1;
}

internal sealed class Arguments
{
    private readonly Dictionary<string, string> options = new(StringComparer.Ordinal);
    public List<string> Positionals { get; } = [];
    public Arguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var key = args[i][2..];
                var value = key == "double-regions" ? "true" : ++i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal) ? args[i] : throw new ArgumentException($"Missing value for --{key}.");
                if (!options.TryAdd(key, value)) throw new ArgumentException($"Duplicate --{key}.");
            }
            else Positionals.Add(args[i]);
    }
    public void Allow(params string[] allowed)
    {
        foreach (var key in options.Keys) if (!allowed.Contains(key)) throw new ArgumentException($"Unknown option --{key}.");
    }
    public bool Has(string key) => options.ContainsKey(key);
    public void ExpectPositionals(int count)
    {
        if (Positionals.Count != count) throw new ArgumentException("Unexpected or missing positional argument; use help.");
    }
    public string Required(string key) => options.TryGetValue(key, out var value) ? value : throw new ArgumentException($"--{key} is required.");
    public string Get(string key, string fallback) => options.GetValueOrDefault(key, fallback);
    public int Int(string key, int fallback) => int.Parse(Get(key, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
    public int? OptionalInt(string key) => Has(key) ? Int(key, 0) : null;
    public ulong Seed() => ulong.Parse(Required("seed"), CultureInfo.InvariantCulture);
    public string Pos(int index) => index < Positionals.Count ? Positionals[index] : throw new ArgumentException("Missing positional argument.");
}
