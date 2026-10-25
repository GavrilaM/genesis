# Project Genesis — Prototype 001: The World Lives

Local, headless C#/.NET 10 simulation. Simulate causes, not stories.

The authoritative contract and future vision are preserved in `docs/`. See
`ARCHITECTURE.md` for mechanics and `ROADMAP.md` for the acceptance plan.

Build: `dotnet build -c Release`. Test: `dotnet test -c Release`.
The installed SDK is pinned in `global.json`; runtime dependencies are .NET libraries only.

## Quick start (PowerShell, repository root)

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --no-restore
$genesis = 'src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
dotnet $genesis new --seed 847192 --scenario scenarios/baseline.json --out artifacts/my-world.json
dotnet $genesis run --save artifacts/my-world.json --years 1000 --out artifacts/my-run --snapshot-every 25 --trace-tick 1 --trace-region 0
dotnet $genesis inspect world --save artifacts/my-run/latest.json
dotnet $genesis inspect region 0 --save artifacts/my-run/latest.json
dotnet $genesis inspect species 1 --save artifacts/my-run/latest.json
dotnet $genesis history --run artifacts/my-run --from 0 --to 10 --type famine-start
dotnet $genesis explain event 1 --run artifacts/my-run
dotnet $genesis trace --run artifacts/my-run --region 0 --year 0 --month 1
dotnet $genesis verify artifacts/my-run
dotnet $genesis export --run artifacts/my-run --format csv --out artifacts/my-export
```

Use new output paths on repeat runs. `run` never modifies the input save. A run
directory contains initial/latest snapshots, periodic snapshots, event JSONL,
population/food CSVs, completion status and a measured report. Trace capture is
limited to one tick; querying uncaptured information returns `not recorded`.
Years in trace/history start at zero; trace months are 1..12. History `--from A
--to B` selects ticks `(12A, 12B]`, i.e. complete elapsed years A through B-1.

CSV files sample the **single month's flows** at each sampled tick, not summed
annual flows. Use `--sample-every 1` for every month. `report.json` includes exact
cumulative population flows across the whole run. Inspect trait distributions in
`population.csv`; shared food/climate appears only in `food.csv`.

## Continuation, experiments and performance

```powershell
dotnet $genesis run --save artifacts/my-run/snapshot-00010800.json --years 100 --out artifacts/my-continuation
dotnet $genesis compare artifacts/my-run/latest.json artifacts/my-continuation/latest.json
dotnet $genesis experiments --scenario scenarios/baseline.json --seeds 16 --years 30 --out artifacts/my-experiments.jsonl
./scripts/Summarize-Experiments.ps1 -InputPath artifacts/my-experiments.jsonl -OutputPath artifacts/my-summary.json
dotnet $genesis benchmark --seed 847192 --years 100 --out artifacts/my-benchmark-100
dotnet $genesis benchmark --seed 847192 --years 1000 --out artifacts/my-benchmark-1000
dotnet $genesis benchmark --seed 847192 --years 1000 --double-regions --out artifacts/my-benchmark-double
./scripts/Smoke-Test.ps1 -RunDirectory artifacts/my-run
```

`compare` takes snapshot files. `verify` takes either a snapshot or a complete
run directory; directory verification also checks the event-chain continuation.
History is segment-local: inspect each run directory to view its own events.
All output is plain JSON/JSONL/CSV; no network is used by the running engine.

`experiments` executes five paired treatments on seeds 1..N: drought, cold
resistance, isolated food loss, productivity and connectivity. The cold assay
uses uniform cold and ample finite food to isolate thermal fitness; the original
mixed-climate failed experiment is documented in `docs/HANDOFF.md`.

Core models are provisional, including geometric age stages, low-variance
stochastic rounding, local hunger memory and migration forecasts. See
`ARCHITECTURE.md` for equations and limits, `docs/HANDOFF.md` for measured results,
and `docs/results/` for compact retained evidence. Large run artifacts are local
and Git-ignored. There is no configured remote repository.
