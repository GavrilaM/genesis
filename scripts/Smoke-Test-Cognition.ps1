param([string]$OutputDirectory = 'artifacts/phase1b/cli-smoke')
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory.' }
$dll = Join-Path $PSScriptRoot '../src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
$checks = 0
function Invoke-Genesis {
    param([string[]]$Arguments)
    $value = & dotnet $dll @Arguments
    if ($LASTEXITCODE -ne 0) { throw "CLI failed: $Arguments" }
    return $value
}
$demo = Join-Path $OutputDirectory 'demo'
$result = Invoke-Genesis @('cognition-demo','--out',$demo) | ConvertFrom-Json
if ($result.activeRegion.value -ne 1 -or $result.controlRegion.value -ne 0 -or $result.activePopulation -ne 20) { throw 'Causal demo failed' }; $checks++
$run = Join-Path $demo 'Decide'; $save = Join-Path $run 'latest.json'
$before = (Get-FileHash $save).Hash
$mind = Invoke-Genesis @('inspect','mind','1','--save',$save) | ConvertFrom-Json
if ($mind.memory.Count -ne 3 -or $mind.lastObservedTick -ne 3) { throw 'Mind inspection failed' }; $checks++
if ((Invoke-Genesis @('inspect','mind','999','--save',$save)) -ne 'not recorded') { throw 'Unknown mind invented' }; $checks++
$local = Invoke-Genesis @('inspect','local','1','--save',$save) | ConvertFrom-Json
if ($local.region.value -ne 1 -or $null -ne $local.exits[0].forage) { throw 'Local boundary failed' }; $checks++
if ((Get-FileHash $save).Hash -ne $before) { throw 'Read query changed state' }; $checks++
$history = @(Invoke-Genesis @('cognition-history','--run',$run,'--person','1','--from','2','--to','2'))
if ($history.Count -ne 1) { throw 'Wrong decision history interval' }; $checks++
$decision = $history[0] | ConvertFrom-Json
if ($decision.reason -ne 'explore-unfamiliar-exit' -or !$decision.resolution.accepted -or $null -eq $decision.migrationEventSequence) { throw 'Missing movement resolution' }; $checks++
if ($decision.observation.phase -ne 'post-demography' -or $decision.observation.feeding.tick -ne 2 -or $decision.priorMemory[0].tick -ne 1) { throw 'Observation timing failed' }; $checks++
if (@($decision.priorMemory | Where-Object { $_.region.value -eq 1 }).Count) { throw 'Unobserved destination memory fabricated' }; $checks++
if ((Invoke-Genesis @('cognition-history','--run',$run,'--person','999')) -ne 'not recorded') { throw 'Unknown history invented' }; $checks++
if ((Invoke-Genesis @('verify',$run)) -ne 'verified') { throw 'Cognition verification failed' }; $checks++
$comparison = Invoke-Genesis @('compare',(Join-Path $demo 'ObserveOnly/latest.json'),$save) | ConvertFrom-Json
if ($comparison.sameBiology -or $comparison.sameIdentityHistory -or $comparison.sameCognitionHistory) { throw 'Active difference hidden' }; $checks++
$invalid = Join-Path $OutputDirectory 'invalid.json'
& dotnet $dll new --seed 1 --cognition Omniscient --out $invalid 2>$null
if ($LASTEXITCODE -eq 0 -or (Test-Path $invalid)) { throw 'Invalid mode accepted' }; $checks++
$checkpoint = Join-Path $run 'snapshot-00000002.json'
$profileRun = Join-Path $OutputDirectory 'profiled'; $plainRun = Join-Path $OutputDirectory 'plain'
$profileReport = Invoke-Genesis @('run','--save',$checkpoint,'--years','1','--out',$profileRun,'--profile') | ConvertFrom-Json
Invoke-Genesis @('run','--save',$checkpoint,'--years','1','--out',$plainRun) | Out-Null
$profileCompare = Invoke-Genesis @('compare',(Join-Path $profileRun 'latest.json'),(Join-Path $plainRun 'latest.json')) | ConvertFrom-Json
if (!$profileCompare.sameState -or !$profileCompare.sameCognitionHistory) { throw 'Profiling altered truth' }; $checks++
if ($null -eq $profileReport.profiling -or $profileReport.cognition.eventCount -lt 14 -or $profileReport.serializedComponentBytes.minds -le 2) { throw 'Missing component measurements' }; $checks++
if ((Invoke-Genesis @('verify',$profileRun)) -ne 'verified') { throw 'Continuation verification failed' }; $checks++
$ledger = Join-Path $run 'cognition-events.jsonl'; $good = [IO.File]::ReadAllText($ledger)
[IO.File]::AppendAllText($ledger, "{}`n")
& dotnet $dll verify $run 2>$null
if ($LASTEXITCODE -eq 0) { throw 'Corrupt cognition history accepted' }; $checks++
[IO.File]::WriteAllText($ledger, $good)
if ((Invoke-Genesis @('verify',$run)) -ne 'verified') { throw 'Restored archive failed verification' }; $checks++
[ordered]@{passed=$checks;failed=0;phase='1B'} | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'test-results.json') -Encoding utf8
Write-Output "$checks cognition CLI checks passed."
exit 0
