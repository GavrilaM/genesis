param([Parameter(Mandatory=$true)][string]$Directory,
      [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
$protocol = Get-Content -LiteralPath (Join-Path $Directory 'protocol.json') -Raw | ConvertFrom-Json
if ($protocol.status -ne 'complete') { throw 'Calibration is incomplete.' }
$rows = @(Get-Content -LiteralPath (Join-Path $Directory 'observations.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
if ($rows.Count -ne $protocol.seeds * $protocol.cases.Count) { throw 'Unexpected observation count.' }
$controls = @{}
foreach ($row in ($rows | Where-Object case -eq 'baseline')) { $controls[[string]$row.seed] = $row }
function Share($row) {
    $total = ($row.species.final | Measure-Object -Sum).Sum
    if ($total -eq 0) { return 0.0 }
    return [double]$row.species[1].final / $total
}
$results = @($rows | Group-Object case | ForEach-Object {
    $group = $_.Group
    $shares = @($group | ForEach-Object { Share $_ })
    $differences = @($group | ForEach-Object {
        if (!$controls.ContainsKey([string]$_.seed)) { throw 'Missing paired baseline.' }
        (Share $_) - (Share $controls[[string]$_.seed])
    })
    $mean = ($differences | Measure-Object -Average).Average
    $squares = ($differences | ForEach-Object { ($_ - $mean) * ($_ - $mean) } | Measure-Object -Sum).Sum
    [ordered]@{
        case = $_.Name; seeds = $group.Count; months = $protocol.months
        orcShareMean = ($shares | Measure-Object -Average).Average
        pairedOrcShareDifferenceMean = $mean
        pairedOrcShareDifferenceSd = if ($differences.Count -gt 1) { [Math]::Sqrt($squares / ($differences.Count - 1)) } else { 0 }
        pairedDifferenceMin = ($differences | Measure-Object -Minimum).Minimum
        pairedDifferenceMax = ($differences | Measure-Object -Maximum).Maximum
        species = @(for ($s = 0; $s -lt 4; $s++) {
            $observations = @($group | ForEach-Object { $_.species[$s] })
            $people = ($observations.personMonths | Measure-Object -Sum).Sum
            $demand = ($observations.demand | Measure-Object -Sum).Sum
            [ordered]@{
                name = $observations[0].name
                meanFinal = ($observations.final | Measure-Object -Average).Average
                minFinal = ($observations.final | Measure-Object -Minimum).Minimum
                maxFinal = ($observations.final | Measure-Object -Maximum).Maximum
                dominantSeeds = @($group | Where-Object { $values = @($_.species.final); $values[$s] -gt 0 -and $values[$s] -eq ($values | Measure-Object -Maximum).Maximum }).Count
                birthsPerPersonYear = if ($people -gt 0) { 12 * ($observations.births | Measure-Object -Sum).Sum / $people } else { 0 }
                deathsPerPersonYear = if ($people -gt 0) { 12 * ($observations.deaths | Measure-Object -Sum).Sum / $people } else { 0 }
                adultPersonMonthFraction = if ($people -gt 0) { ($observations.adultPersonMonths | Measure-Object -Sum).Sum / $people } else { 0 }
                nutrition = if ($demand -gt 0) { ($observations.consumed | Measure-Object -Sum).Sum / $demand } else { 1 }
                meanColdExposure = if ($people -gt 0) { ($observations.coldExposurePersonMonths | Measure-Object -Sum).Sum / $people } else { 0 }
                meanHeatExposure = if ($people -gt 0) { ($observations.heatExposurePersonMonths | Measure-Object -Sum).Sum / $people } else { 0 }
            }
        })
    }
})
$results | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Summarized $($rows.Count) observations from $($protocol.cases.Count) cases."
