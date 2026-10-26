param([string]$OutputDirectory = 'artifacts/phase1a/cli-smoke')
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory.' }
$dll = Join-Path $PSScriptRoot '../src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
$checks = 0
function Invoke-Genesis {
    param([string[]]$Arguments)
    $output = & dotnet $dll @Arguments
    if ($LASTEXITCODE -ne 0) { throw "CLI failed: $Arguments" }
    return $output
}
$save = Join-Path $OutputDirectory 'initial.json'
Invoke-Genesis @('new','--seed','42','--out',$save) | Out-Null
$initialHash = (Get-FileHash $save).Hash
$promotion = Join-Path $OutputDirectory 'promotion'
$result = Invoke-Genesis @('identify','--save',$save,'--count','64','--active','16','--out',$promotion) | ConvertFrom-Json
if ($result.population -ne 38400 -or $result.identified -ne 64 -or $result.active -ne 16) { throw 'Promotion accounting failed' }; $checks++
if ((Get-FileHash $save).Hash -ne $initialHash) { throw 'Input save changed' }; $checks++
$detailed = Join-Path $promotion 'latest.json'
$comparison = Invoke-Genesis @('compare',$save,$detailed) | ConvertFrom-Json
if (!$comparison.sameBiology -or $comparison.sameState -or !$comparison.sameEventHistory -or $comparison.sameIdentityHistory) { throw 'Projection comparison failed' }; $checks++
$detailedHash = (Get-FileHash $detailed).Hash
$world = Invoke-Genesis @('inspect','world','--save',$detailed) | ConvertFrom-Json
if ($world.population -ne $world.anonymous + $world.identified -or $world.active -ne 16) { throw 'World ownership reporting failed' }; $checks++
$person = Invoke-Genesis @('inspect','person','1','--save',$detailed) | ConvertFrom-Json
if ($person.id.value -ne 1 -or $person.promotedTick -ne 0) { throw 'Person inspect failed' }; $checks++
$local = Invoke-Genesis @('inspect','local','1','--save',$detailed) | ConvertFrom-Json
if ($local.region.value -ne $person.cohort.region.value -or $local.tick -ne 0 -or $null -ne $local.exits[0].forage) { throw 'Local-only view failed' }; $checks++
if ((Invoke-Genesis @('inspect','person','999999','--save',$detailed)) -ne 'not recorded') { throw 'Missing person invented' }; $checks++
if ((Get-FileHash $detailed).Hash -ne $detailedHash) { throw 'Inspection mutated save' }; $checks++
$history = @(Invoke-Genesis @('identity-history','--run',$promotion,'--person','1'))
if ($history.Count -ne 1 -or ($history[0] | ConvertFrom-Json).type -ne 'promote') { throw 'Promotion history missing' }; $checks++
if ((Invoke-Genesis @('verify',$promotion)) -ne 'verified') { throw 'Promotion archive failed verification' }; $checks++
$invalid = Join-Path $OutputDirectory 'duplicate'
& dotnet $dll identity --save $detailed --commands (Join-Path $promotion 'identity-commands.json') --out $invalid 2>$null
if ($LASTEXITCODE -eq 0 -or (Test-Path $invalid)) { throw 'Repeated commands accepted or output published' }; $checks++
$deactivate = Join-Path $OutputDirectory 'deactivate.json'
'[{"sequence":65,"tick":0,"type":"set-active","person":{"value":1},"active":false}]' | Set-Content $deactivate -Encoding utf8
$activation = Join-Path $OutputDirectory 'activation'
$change = Invoke-Genesis @('identity','--save',$detailed,'--commands',$deactivate,'--out',$activation) | ConvertFrom-Json
if ($change.identified -ne 64 -or $change.active -ne 15) { throw 'Deactivation lost person' }; $checks++
if ((Invoke-Genesis @('verify',$activation)) -ne 'verified') { throw 'Activation archive failed verification' }; $checks++
$plainRun = Join-Path $OutputDirectory 'plain-run'
$detailRun = Join-Path $OutputDirectory 'detail-run'
Invoke-Genesis @('run','--save',$save,'--years','2','--out',$plainRun) | Out-Null
Invoke-Genesis @('run','--save',$detailed,'--years','2','--out',$detailRun) | Out-Null
$continuation = Invoke-Genesis @('compare',(Join-Path $plainRun 'latest.json'),(Join-Path $detailRun 'latest.json')) | ConvertFrom-Json
if (!$continuation.sameBiology -or !$continuation.sameEventHistory) { throw 'Passive continuation diverged' }; $checks++
if ((Invoke-Genesis @('verify',$detailRun)) -ne 'verified') { throw 'Detailed archive failed verification' }; $checks++
foreach ($file in @('population.csv','food.csv','summaries.jsonl')) {
    if ((Get-FileHash (Join-Path $plainRun $file)).Hash -ne (Get-FileHash (Join-Path $detailRun $file)).Hash) { throw "$file differs" }; $checks++
}
[ordered]@{passed=$checks; failed=0; phase='1A'} | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'test-results.json') -Encoding utf8
Write-Output "$checks identity CLI checks passed."
exit 0
