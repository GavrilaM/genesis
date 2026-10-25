param(
    [Parameter(Mandatory=$true)][string]$InputPath,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$rows = @(Get-Content -LiteralPath $InputPath | ForEach-Object { $_ | ConvertFrom-Json })
$summary = @($rows | Group-Object treatment | ForEach-Object {
    $group = $_.Group
    $metric = switch ($_.Name) {
        'drought' { 'meanScarcity' }
        'connectivity' { 'migration' }
        default { 'population' }
    }
    $diff = @($group | ForEach-Object { [double]$_.treatmentResult.$metric - [double]$_.control.$metric })
    $mean = ($diff | Measure-Object -Average).Average
    $squared = ($diff | ForEach-Object { ($_ - $mean) * ($_ - $mean) } | Measure-Object -Sum).Sum
    $sd = if ($diff.Count -gt 1) { [Math]::Sqrt($squared / ($diff.Count - 1)) } else { 0 }
    $positive = @($diff | Where-Object { $_ -gt 0 }).Count
    $negative = @($diff | Where-Object { $_ -lt 0 }).Count
    [ordered]@{
        treatment = $_.Name
        pairs = $group.Count
        metric = $metric
        controlMean = ($group.control.$metric | Measure-Object -Average).Average
        treatmentMean = ($group.treatmentResult.$metric | Measure-Object -Average).Average
        pairedMeanDifference = $mean
        pairedSampleSd = $sd
        pairedMinimum = ($diff | Measure-Object -Minimum).Minimum
        pairedMaximum = ($diff | Measure-Object -Maximum).Maximum
        positivePairs = $positive
        negativePairs = $negative
        zeroPairs = $diff.Count - $positive - $negative
    }
})
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$summary | ConvertTo-Json -Depth 8
