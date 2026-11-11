param([string]$OutputDirectory = 'artifacts/phase1b/benchmarks', [string]$ReferenceDirectory = 'artifacts/phase1b/reference', [ValidateRange(1,1000)][int]$Years = 1000, [ValidateRange(1,10)][int]$Repetitions = 3)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory.' }
$currentDll = Join-Path $PSScriptRoot '../src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
$referenceDll = Join-Path $ReferenceDirectory 'src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
$cases = @(
    @{Name='anonymous';Count=0;Active=0;Modes=@('Off')},
    @{Name='identities64';Count=64;Active=0;Modes=@('Off')},
    @{Name='identities256';Count=256;Active=0;Modes=@('Off')},
    @{Name='mind1';Count=64;Active=1;Modes=@('Off','ObserveOnly','Decide')},
    @{Name='minds64';Count=256;Active=64;Modes=@('Off','ObserveOnly','Decide')}
)
$records = @()
for ($rep = 1; $rep -le $Repetitions; $rep++) {
    foreach ($case in $cases) {
        # Rotate old/new ordering by repetition; still execute exactly one job at a time.
        $modes = @('Reference') + $case.Modes
        if ($rep % 2 -eq 0) { [array]::Reverse($modes) }
        foreach ($mode in $modes) {
            $name = "$($case.Name)-$mode-r$rep"; $out = Join-Path $OutputDirectory $name
            $dll = if ($mode -eq 'Reference') { $referenceDll } else { $currentDll }
            $arguments = @($dll,'benchmark','--seed','847192','--years',"$Years",'--identities',"$($case.Count)",'--active',"$($case.Active)",'--out',$out,'--trace-tick','1','--trace-region','0')
            if ($case.Count -gt 0) { $arguments += @('--replenish-every','12') }
            if ($mode -ne 'Reference') { $arguments += @('--cognition',$mode,'--profile') }
            & dotnet @arguments | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $name" }
            & dotnet $dll verify $out | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Archive verification failed: $name" }
            $report = Get-Content -Raw (Join-Path $out 'report.json') | ConvertFrom-Json
            $records += [ordered]@{name=$name;case=$case.Name;mode=$mode;repetition=$rep;report=$report}
            # Checkpoints are not acceptance: preserve compact progress after every run.
            $records | ConvertTo-Json -Depth 16 | Set-Content (Join-Path $OutputDirectory 'observations.json') -Encoding utf8
            Write-Output "$name : $($report.elapsedSeconds)s; living $($report.identity.living); deceased $($report.identity.deceased); verified"
        }
    }
}
foreach ($group in $records | Group-Object case,mode) {
    foreach ($field in @('stateHash','eventHash')) {
        if (@($group.Group.report.$field | Select-Object -Unique).Count -ne 1) { throw "Repeated benchmark differs: $($group.Name) $field" }
    }
    if (@($group.Group.report.identity.eventHash | Select-Object -Unique).Count -ne 1) { throw 'Repeated identity histories differ' }
    if ($group.Group[0].mode -ne 'Reference' -and @($group.Group.report.cognition.eventHash | Select-Object -Unique).Count -ne 1) { throw 'Repeated cognition histories differ' }
}
$summary = @()
foreach ($case in $cases) {
    $reference = @($records | Where-Object { $_.case -eq $case.Name -and $_.mode -eq 'Reference' })
    foreach ($mode in $case.Modes) {
        $rows = @($records | Where-Object { $_.case -eq $case.Name -and $_.mode -eq $mode })
        if ($mode -ne 'Decide') {
            foreach ($row in $rows) { foreach ($field in @('biologicalProjectionHash','eventHash','populationSha256','foodSha256','summarySha256')) {
                if ($row.report.$field -ne $reference[0].report.$field) { throw "No-override mismatch: $($row.name) $field" }
            }
            if ($row.report.identity.eventHash -ne $reference[0].report.identity.eventHash) { throw 'No-override identity mismatch' } }
        }
        $times = @($rows.report.elapsedSeconds | Sort-Object); $oldTimes = @($reference.report.elapsedSeconds | Sort-Object)
        $median = $times[[int][math]::Floor($times.Count/2)]; $oldMedian = $oldTimes[[int][math]::Floor($oldTimes.Count/2)]
        $summary += [ordered]@{case=$case.Name;mode=$mode;medianSeconds=$median;minimumSeconds=$times[0];maximumSeconds=$times[-1];referenceMedianSeconds=$oldMedian;
            ratioToReference=$median/$oldMedian;offRegressionReviewTriggered=($mode -eq 'Off' -and $median/$oldMedian -gt 1.1);noOverrideMatchesReference=($mode -ne 'Decide')}
    }
}
[ordered]@{seed=847192;years=$Years;repetitions=$Repetitions;serialProcesses=$true;referenceCommit='9bd958f';allArchivesVerified=$true;noOverrideControlsPassed=$true;repeatedStatesAndHistoriesEqual=$true;
    summary=$summary;observations=$records} | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $OutputDirectory 'comparison.json') -Encoding utf8
Write-Output 'Benchmark matrix and exact no-override controls completed.'
