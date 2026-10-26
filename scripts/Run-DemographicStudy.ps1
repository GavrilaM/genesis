param([string]$OutputDirectory = 'artifacts/demographic-study')
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot '../src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new output directory.' }
$cases = @(
    @{Name='worlds-300'; Suite='worlds'; Seeds=8; Years=300},
    @{Name='worlds-1000'; Suite='worlds'; Seeds=8; Years=1000; Models='geometric-rounding,monthly-binomial'},
    @{Name='pulse'; Suite='pulse'; Seeds=64; Years=50},
    @{Name='extinction'; Suite='extinction'; Seeds=512; Years=50},
    @{Name='bottleneck'; Suite='bottleneck'; Seeds=512; Years=50},
    @{Name='death-only'; Suite='death-only'; Seeds=4096; Years=2}
)
foreach ($case in $cases) {
    $arguments = @($dll, 'demography-study', '--suite', $case.Suite, '--seeds', $case.Seeds,
        '--years', $case.Years, '--out', (Join-Path $OutputDirectory $case.Name))
    if ($case.Models) { $arguments += @('--models', $case.Models) }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Study failed: $($case.Name)" }
}
