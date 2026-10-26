param([string]$OutputDirectory = 'artifacts/phase1a/benchmarks', [int]$Years = 1000)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory.' }
$dll = Join-Path $PSScriptRoot '../src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
$cases = @(
    @{Name='anonymous'; Count=0; Active=0; Replenish=0},
    @{Name='initial-64-passive'; Count=64; Active=0; Replenish=0},
    @{Name='initial-256-passive'; Count=256; Active=0; Replenish=0},
    @{Name='maintained-64-passive'; Count=64; Active=0; Replenish=12},
    @{Name='maintained-256-passive'; Count=256; Active=0; Replenish=12},
    @{Name='maintained-256-reserved64'; Count=256; Active=64; Replenish=12}
)
$reports = @()
foreach ($case in $cases) {
    $output = Join-Path $OutputDirectory $case.Name
    $arguments = @($dll, 'benchmark', '--seed','847192','--years',"$Years",'--out',$output,
        '--identities',"$($case.Count)",'--active',"$($case.Active)",'--trace-tick','1','--trace-region','0')
    if ($case.Replenish -gt 0) { $arguments += @('--replenish-every',"$($case.Replenish)") }
    & dotnet @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $($case.Name)" }
    & dotnet $dll verify $output | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Archive verification failed: $($case.Name)" }
    $report = Get-Content -Raw (Join-Path $output 'report.json') | ConvertFrom-Json
    $reports += [ordered]@{name=$case.Name; report=$report}
    Write-Output "$($case.Name): $($report.elapsedSeconds)s; mean living identities $($report.identity.meanLiving); archive verified"
}
$reference = $reports[0].report
foreach ($entry in $reports) {
    foreach ($field in @('biologicalProjectionHash','eventHash','populationSha256','foodSha256','summarySha256')) {
        if ($entry.report.$field -ne $reference.$field) { throw "Biological mismatch in $($entry.name): $field" }
    }
}
$evidence = [ordered]@{seed=847192; years=$Years; serialNewProcesses=$true; allBiologicalProjectionsMatch=$true;
    allSampledPopulationFoodAndAnnualSummaryFilesMatch=$true; allBiologicalEventChainsMatch=$true; cases=$reports}
$evidence | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $OutputDirectory 'comparison.json') -Encoding utf8
Write-Output 'All biological/resource projections, metric files and biological event chains match exactly.'
