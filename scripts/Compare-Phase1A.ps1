param([string]$ReferenceDirectory = 'artifacts/phase1b/reference', [string]$OutputDirectory = 'artifacts/phase1b/control', [ValidateRange(1,256)][int]$Seeds = 8, [ValidateRange(1,1000)][int]$Years = 100)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new output directory.' }
if ((git -C $ReferenceDirectory rev-parse HEAD).Trim() -ne '9bd958ff6b4b213ad0f0a09c1be3e0e270ed277a') { throw 'Reference must be pinned at 9bd958f.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$referenceRoot = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
# Identical harness source, separately linked to each engine. CURRENT enables only
# construction of the new representation; projected rows contain no new fields.
$source = @'
using Genesis.Core;
var baseline = Persistence.ReadScenario(args[0]);
var frontier = Persistence.ReadScenario(args[1]);
int seeds = int.Parse(args[2]), months = int.Parse(args[3]) * 12;
string mode = args[4]; long rows = 0;
using var writer = new StreamWriter(args[5]);
var cases = new List<(string Name, Scenario Scenario, int Count, int Active, int Months)>();
foreach (var (count, active) in new[] {(0,0),(64,0),(64,1),(64,64),(256,0),(256,1),(256,64)})
    cases.Add( ($"baseline-{count}-{active}", baseline, count, active, months) );
cases.Add(("scarcity", baseline with { InitialStockFraction = 0, Rules = baseline.Rules with { Productivity = 0 } }, 64, 16, 24));
cases.Add(("empty", baseline with { InitialPopulations = [] }, 0, 0, 24));
cases.Add(("frontier", frontier, 64, 16, months));
foreach (var c in cases) for (ulong seed = 1; seed <= (ulong)seeds; seed++)
{
#if CURRENT
    var w = Simulation.Create(c.Scenario, seed, cognitionOptions: new(Enum.Parse<CognitionMode>(mode)));
#else
    var w = Simulation.Create(c.Scenario, seed);
#endif
    if (c.Count > 0) w = PopulationOwnership.Apply(w, PopulationOwnership.PlanPromotions(w, c.Count, c.Active)).State;
    PersonId? dormant = null;
    for (int t = 0; t < c.Months; t++)
    {
        if (t == 58 && c.Count > 0)
        {
            int missing = Math.Min(c.Count - w.Identity.Living.Length, (int)Math.Min(c.Count, w.Cohorts.Sum(x => x.Count)));
            if (missing > 0) w = PopulationOwnership.Apply(w, PopulationOwnership.PlanPromotions(w, missing, Math.Min(missing, c.Active - w.Identity.Living.Count(p => p.Active)))).State;
        }
        if (t == 73 && w.Identity.Living.FirstOrDefault(p => p.Active) is { } p)
        {
            dormant = p.Id;
            w = PopulationOwnership.Apply(w, [new(w.Identity.LastCommandSequence + 1, w.CurrentTick, "set-active", Person: p.Id)]).State;
        }
        if (t == 87 && dormant is { } id && w.Identity.Living.Any(p => p.Id == id))
            w = PopulationOwnership.Apply(w, [new(w.Identity.LastCommandSequence + 1, w.CurrentTick, "set-active", Person: id, Active: true)]).State;
        var step = Simulation.Step(w); w = step.State;
        writer.WriteLine($"{c.Name}|{seed}|{w.CurrentTick}|{PopulationOwnership.BiologicalHash(step)}|{w.Identity.EventHash}"); rows++;
    }
    writer.WriteLine($"{c.Name}|{seed}|final-identity|{Persistence.Hash(w.Identity)}"); rows++;
}
Console.WriteLine(Persistence.Encode(new { rows, seeds, years = months / 12, cases = cases.Count, mode }));
'@
$builds = @(@{Name='reference';Root=$referenceRoot;Define=''}, @{Name='current';Root=$projectRoot;Define='CURRENT'})
foreach ($build in $builds) {
    $folder = Join-Path $outputRoot $build.Name
    New-Item -ItemType Directory -Path $folder | Out-Null
    $source | Set-Content -LiteralPath (Join-Path $folder 'Program.cs') -Encoding utf8
    $corePath = [Security.SecurityElement]::Escape((Join-Path $build.Root 'src/Genesis.Core/Genesis.Core.csproj'))
    "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><DefineConstants>$($build.Define)</DefineConstants></PropertyGroup><ItemGroup><ProjectReference Include=`"$corePath`" /></ItemGroup></Project>" | Set-Content -LiteralPath (Join-Path $folder 'Control.csproj') -Encoding utf8
    & dotnet build (Join-Path $folder 'Control.csproj') -c Release --verbosity quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Control build failed: $($build.Name)" }
}
$results = @()
foreach ($mode in @('Reference','Off','ObserveOnly')) {
    $build = if ($mode -eq 'Reference') { 'reference' } else { 'current' }
    $rowsPath = Join-Path $outputRoot "$mode.rows"
    $summary = & dotnet (Join-Path $outputRoot "$build/bin/Release/net10.0/Control.dll") (Join-Path $projectRoot 'scenarios/baseline.json') (Join-Path $projectRoot 'scenarios/sparse-frontier.json') "$Seeds" "$Years" $(if ($mode -eq 'Reference') {'Off'} else {$mode}) $rowsPath
    if ($LASTEXITCODE -ne 0) { throw "Control failed: $mode" }
    $results += [ordered]@{mode=$mode;protocol=($summary | ConvertFrom-Json);sha256=(Get-FileHash -LiteralPath $rowsPath).Hash}
    Write-Output "$mode controls complete: $summary"
}
if (@($results.sha256 | Select-Object -Unique).Count -ne 1) {
    foreach ($mode in @('Off','ObserveOnly')) {
        $left = [IO.File]::OpenText((Join-Path $outputRoot 'Reference.rows')); $right = [IO.File]::OpenText((Join-Path $outputRoot "$mode.rows"))
        try { do { $a = $left.ReadLine(); $b = $right.ReadLine(); if ($a -ne $b) { throw "First difference ($mode): $a versus $b" } } while ($null -ne $a) } finally { $left.Dispose(); $right.Dispose() }
    }
    throw 'Phase 1A comparison mismatch.'
}
[ordered]@{referenceCommit='9bd958ff6b4b213ad0f0a09c1be3e0e270ed277a';allMonthlyProjectionsAndIdentityHistoriesEqual=$true;results=$results} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'comparison.json') -Encoding utf8
Write-Output 'Exact monthly Phase 1A biological/resource and identity history controls passed.'
