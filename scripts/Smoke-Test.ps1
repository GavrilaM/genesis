param([string]$RunDirectory = 'artifacts/baseline-1000')
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot '../src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
$save = Join-Path $RunDirectory 'latest.json'
$checks = 0
function Invoke-Genesis {
    param([string[]]$Arguments)
    $output = & dotnet $dll @Arguments
    if ($LASTEXITCODE -ne 0) { throw "CLI failed: $Arguments" }
    return $output
}
$world = Invoke-Genesis @('inspect','world','--save',$save) | ConvertFrom-Json
if ($world.currentTick -ne 12000) { throw 'Unexpected final tick' }; $checks++
$region = Invoke-Genesis @('inspect','region','0','--save',$save) | ConvertFrom-Json
if ($region.cohorts.Count -ne 36) { throw 'Missing regional cohorts' }; $checks++
foreach ($cohort in @($region.cohorts | Where-Object { $_.id.stage -eq 0 })) {
    if ($null -eq $cohort.juvenileBins -or ($cohort.juvenileBins | Measure-Object -Sum).Sum -ne $cohort.count) { throw 'Juvenile age bins do not partition population' }
}; $checks++
$species = Invoke-Genesis @('inspect','species','0','--save',$save) | ConvertFrom-Json
if ($species.cohorts.Count -ne 108) { throw 'Missing species cohorts' }; $checks++
$history = @(Invoke-Genesis @('history','--run',$RunDirectory,'--from','0','--to','2','--type','famine-start'))
if ($history.Count -eq 0 -or $history[0] -eq 'not recorded') { throw 'Missing famine history' }; $checks++
$event = Invoke-Genesis @('explain','event','1','--run',$RunDirectory) | ConvertFrom-Json
if ($event.recordedEvent.id.value -ne 1) { throw 'Wrong event explanation' }; $checks++
$missing = Invoke-Genesis @('explain','event','999999999','--run',$RunDirectory)
if ($missing -ne 'not recorded') { throw 'Missing event was invented' }; $checks++
$trace = Invoke-Genesis @('trace','--run',$RunDirectory,'--year','0','--month','1','--region','0') | ConvertFrom-Json
if ($trace.survival.Count -ne 36) { throw 'Missing survival audit' }; $checks++
if ($trace.forecasts.Count -ne 1 -or $trace.forecasts[0].tick -ne 2 -or $trace.forecasts[0].available -gt $region.definition.capacity) { throw 'Invalid migration forecast audit' }; $checks++
$absent = Invoke-Genesis @('trace','--run',$RunDirectory,'--year','0','--month','2','--region','0')
if ($absent -ne 'not recorded') { throw 'Uncaptured trace was invented' }; $checks++
if ((Invoke-Genesis @('verify',$RunDirectory)) -ne 'verified') { throw 'Archive verification failed' }; $checks++
$comparison = Invoke-Genesis @('compare',$save,$save) | ConvertFrom-Json
if (!$comparison.sameState -or !$comparison.sameEventHistory) { throw 'Self comparison failed' }; $checks++
$annual = @(Invoke-Genesis @('history-summary','--run',$RunDirectory,'--from','0','--to','1','--region','0','--species','0'))
if ($annual.Count -ne 1 -or $annual[0] -eq 'not recorded') { throw 'Missing annual accounting' }
$entry = $annual[0] | ConvertFrom-Json
if ($entry.end -ne $entry.window.start + $entry.window.births - $entry.window.deaths + $entry.window.inward - $entry.window.outward) { throw 'Annual accounting failed' }; $checks++
if ($null -eq $entry.window.matured -or $entry.window.matured -lt 0) { throw 'Missing maturation accounting' }; $checks++
$export = Join-Path (Split-Path $RunDirectory) ('smoke-export-' + [Guid]::NewGuid().ToString('N'))
Invoke-Genesis @('export','--run',$RunDirectory,'--format','csv','--out',$export) | Out-Null
if ((Get-FileHash (Join-Path $export 'population.csv')).Hash -ne (Get-FileHash (Join-Path $RunDirectory 'population.csv')).Hash) { throw 'CSV export mismatch' }; $checks++
& dotnet $dll run --save $save --years 0 --out "$export-invalid" 2>$null
if ($LASTEXITCODE -eq 0 -or (Test-Path "$export-invalid")) { throw 'Invalid duration accepted' }; $checks++
& dotnet $dll inspect world --save $save --made-up-option 1 2>$null
if ($LASTEXITCODE -eq 0) { throw 'Unknown option accepted' }; $checks++
Write-Output "$checks CLI smoke checks passed."
# The last native invocation intentionally fails (unknown option). Do not leak
# that expected failure as this successful script's process exit status.
exit 0
