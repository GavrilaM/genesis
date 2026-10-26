param([Parameter(Mandatory)][string]$StudyDirectory,
      [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new evidence directory.' }
$intervals = @()
foreach ($suite in @('worlds-300','worlds-1000','pulse','extinction','bottleneck','death-only')) {
    $source = Join-Path $StudyDirectory $suite
    $summary = Get-Content (Join-Path $source 'summary.json') -Raw | ConvertFrom-Json
    $rows = @(Get-Content (Join-Path $source 'observations.jsonl') | ConvertFrom-Json)
    if ($rows.Count -ne $summary.seeds * $summary.models.Count) { throw "Incomplete study: $suite" }
    $destination = Join-Path $OutputDirectory $suite
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source 'protocol.json'),(Join-Path $source 'summary.json') -Destination $destination
    if ($suite.StartsWith('worlds-')) {
        Copy-Item -LiteralPath (Join-Path $source 'observations.jsonl') -Destination $destination
    } else {
        # Compact, lossless per-seed demographic outcomes; clocks/hashes and repeated
        # metadata stay reproducible from the full protocol, not duplicated 14k times.
        $data = @($rows | ForEach-Object {
            ,@($_.model, $_.seed, $_.final[0], $_.births[0], $_.deaths[0], $_.matured[0],
                $_.extinctionTick[0], $_.firstBirthTick, $_.earlyBirths)
        })
        $fingerprints = ($rows | Sort-Object model,seed | ForEach-Object { "$($_.model):$($_.seed):$($_.stateHash):$($_.eventHash)" }) -join "`n"
        $digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($fingerprints)))
        [ordered]@{
            schemaVersion=1
            columns=@('model','seed','final','births','deaths','matured','extinctionTick','firstBirthTick','earlyBirths')
            deterministicDigest=$digest
            digestFormat='SHA256 UTF8 model:seed:stateHash:eventHash lines sorted by model then numeric seed; LF separator, no final LF'
            rows=$data
        } | ConvertTo-Json -Depth 8 -Compress | Set-Content (Join-Path $destination 'outcomes.json') -Encoding utf8NoBOM
    }
    foreach ($model in $summary.models) {
        for ($species = 0; $species -lt $model.extinct.Count; $species++) {
            $n = [double]$model.runs; $p = $model.extinct[$species] / $n; $z = 1.959963984540054
            $denominator = 1 + $z*$z/$n
            $center = ($p + $z*$z/(2*$n)) / $denominator
            $radius = $z * [math]::Sqrt($p*(1-$p)/$n + $z*$z/(4*$n*$n)) / $denominator
            $intervals += [pscustomobject]@{suite=$suite;model=$model.model;species=$species;n=$n;extinct=$model.extinct[$species];rate=$p;wilson95Low=$center-$radius;wilson95High=$center+$radius}
        }
    }
}
$intervals | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'extinction-intervals.json') -Encoding utf8NoBOM
$death = Get-Content (Join-Path $StudyDirectory 'death-only/summary.json') -Raw | ConvertFrom-Json
$survival = [math]::Pow(.9, 24); $pZero = [math]::Pow(1-$survival, 20)
$model = $death.models | Where-Object model -eq 'monthly-binomial'
$observed = $model.extinct[0] / [double]$model.runs
$margin = 6 * [math]::Sqrt($pZero * (1-$pZero) / $model.runs)
if ([math]::Abs($observed-$pZero) -gt $margin) { throw 'Analytic extinction gate failed.' }
@{initial=20;months=24;deathProbability=.1;analyticMean=20*$survival;analyticVariance=20*$survival*(1-$survival);analyticExtinction=$pZero;observedBinomialExtinction=$observed;sixStandardErrorMargin=$margin;passed=$true} |
    ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'analytic-extinction.json') -Encoding utf8NoBOM
Write-Output "Exported demographic evidence to $OutputDirectory"
