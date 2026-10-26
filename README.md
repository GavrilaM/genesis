# Project Genesis — Prototype 001: The World Lives

**Phase 1A:** engine 0.4.0 / snapshot schema 4. Persistent passive identities and
exclusive population ownership; no individual decision-making yet. See
`docs/PHASE1A_HANDOFF.md` for verification, benchmarks and Phase 1B interfaces.
`docs/PHASE06_REPORT.md` retains the accepted demographic evidence.
Phase 0.5 evidence remains in `docs/PHASE05_REPORT.md`.
The initial Prototype 001 handoff remains a historical 0.1.0 record.

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

Core models remain provisional, including geometric adult/elder residence times,
rounded migration/mutation counts, local hunger memory and migration forecasts. See
`ARCHITECTURE.md` for equations and limits, `docs/HANDOFF.md` for measured results,
and `docs/results/` for compact retained evidence. Large run artifacts are local
and Git-ignored. Source and review history: https://github.com/GavrilaM/genesis (branch `codex/genesis-prototype-001`).

## Phase 0.5 population configuration and reporting

The existing `scenarios/baseline.json` is unchanged. To configure populations,
add optional `initialPopulations` to a scenario:

```json
"initialPopulations": [
  { "region": { "value": 0 }, "species": { "value": 0 }, "stageCounts": [120, 360, 40] },
  { "region": { "value": 3 }, "species": { "value": 1 }, "stageCounts": [100, 300, 50], "variantWeights": [0.5, 0.3, 0.2] }
]
```

This is a complete sparse allocation: unlisted pairs start at zero. Omit the
field (or use null) for legacy uniform initialization; `[]` starts an empty world.
Entries must be sorted by region/species, with no duplicates. `stageCounts` means
juveniles/adults/elders; optional variant weights override the global weights.
For exact initial trait counts use `stageVariantCounts`, a 3x3 integer matrix
whose row sums match stageCounts, instead of entry-level variant weights.
`scenarios/sparse-frontier.json` provides a full example with eight empty regions.

```powershell
dotnet $genesis new --seed 42 --scenario scenarios/sparse-frontier.json --out artifacts/frontier.json
dotnet $genesis run --save artifacts/frontier.json --years 100 --out artifacts/frontier-run
dotnet $genesis history-summary --run artifacts/frontier-run --from 0 --to 10 --species 0
dotnet $genesis history --run artifacts/frontier-run --type migration-wave
```

Annual `summaries.jsonl` contains exact interval totals even when the CSV only
samples one month per year. Annual population-change and migration-wave events
link changes to recorded births, deaths, inward/outward migration and food supply.
A wave counts crossing throughput, not unique people. Summaries emitted after a
mid-year resume include the year's prefix preserved in the snapshot. No summary
is fabricated for an incomplete year.

Calibration protocols (output directories must be new):

```powershell
dotnet $genesis calibrate --seeds 8 --years 300 --out artifacts/calibration-300
dotnet $genesis calibrate --seeds 8 --years 1000 --cases baseline,orc-birth-low,swap-birth,swap-tolerance,swap-biology,geography-3 --out artifacts/calibration-1000
./scripts/Summarize-Calibration.ps1 -Directory artifacts/calibration-300 -OutputPath artifacts/calibration-summary.json
dotnet $genesis demography-assay --seeds 64 --out artifacts/demography.json
```

Calibration writes each complete case configuration, protocol and per-seed
observations. It always validates each simulated month. The demographic assay
is an offline diagnostic: its binomial/fixed-age alternatives do not change the
production world model. None of these commands automatically retunes the baseline.

**Save compatibility:** new mechanics produce different future trajectories.
Engine 0.4.0 requires snapshot schema 4; older snapshots are explicitly rejected
before checksum verification. Use Git commit `d408813` for 0.3.0 saves,
`855c478` for 0.2.0 saves or `b0f8d4e`
for 0.1.0 saves in a separate checkout, or create a fresh world with the old scenario
and seed under 0.4.0. Existing scenario inputs and starting stage/variant counts
remain supported; `maturationMonths` now specifies a minimum juvenile residence
time instead of a geometric mean. Initial juveniles default to a uniform age mix.
There is no silent save conversion or cross-version determinism claim.

## Phase 0.6 demographic integrity

The default uses aggregate monthly juvenile age bins and binomial birth/death
sampling. Newborns mature after the species minimum age and can reproduce starting
one month later. Migration carries the age bins; bins partition existing cohorts
and never add people. Monthly/annual reports include exact maturation counts.

Optional scenario configuration (these are the defaults):

```json
"demography": {
  "juvenileBinMonths": 1,
  "binomialEvents": true,
  "initialJuvenilesAreNewborn": false
}
```

Width 0 (geometric) and 12 (annual deadlines), and `binomialEvents: false`, are
available as explicit research controls. Annual deadlines cause synchronization
and are not recommended. Adult/elder duration, mutation and migration policies
were not replaced by this pass. No baseline species was retuned.

`demography-study` runs the production transition with factorial controls,
recording complete configurations, per-seed hashes/flows and summary statistics.
Use new output directories:

```powershell
dotnet $genesis demography-study --suite worlds --seeds 8 --years 300 --out artifacts/demographic-worlds
dotnet $genesis demography-study --suite worlds --seeds 8 --years 1000 --models geometric-rounding,monthly-binomial --out artifacts/demographic-confirmation
dotnet $genesis demography-study --suite pulse --seeds 64 --years 50 --out artifacts/demographic-pulse
dotnet $genesis demography-study --suite extinction --seeds 512 --years 50 --out artifacts/demographic-extinction
dotnet $genesis demography-study --suite bottleneck --seeds 512 --years 50 --out artifacts/demographic-bottleneck
dotnet $genesis demography-study --suite death-only --seeds 4096 --years 2 --out artifacts/demographic-deaths
```

## Phase 1A persistent identities

Saved cohorts now contain **anonymous counts only**. Living identified people own
one population member each. Combined totals feed the same food, birth/death,
maturation and migration algorithms. `inspect region/species` labels the combined
`cohorts`, their `anonymousCohorts` component and identified `people` separately.
Do not add combined cohorts to people again.

Promotion is explicit and atomic. Read-only `inspect`, `compare`, history and local
observation never create identities. Identified people remain retained until death;
deceased records have zero population weight. Default budgets are 256 living
identities and 64 active reservations. `active` reserves future mind capacity only;
all Phase 1A individuals remain behaviorally passive. Births stay anonymous and
continue normally when the identity budget is full.

```powershell
dotnet $genesis new --seed 42 --out artifacts/identity-world.json --max-identified 256 --max-active 64
dotnet $genesis identify --save artifacts/identity-world.json --count 64 --active 16 --out artifacts/promotions
dotnet $genesis inspect person 1 --save artifacts/promotions/latest.json
dotnet $genesis inspect local 1 --save artifacts/promotions/latest.json
dotnet $genesis compare artifacts/identity-world.json artifacts/promotions/latest.json
dotnet $genesis identity-history --run artifacts/promotions --person 1
dotnet $genesis verify artifacts/promotions
dotnet $genesis run --save artifacts/promotions/latest.json --years 100 --out artifacts/identity-run
./scripts/Benchmark-Identity.ps1 -OutputDirectory artifacts/new-identity-benchmarks
./scripts/Smoke-Test-Identity.ps1 -OutputDirectory artifacts/new-identity-smoke
```

`compare.sameBiology` compares canonical biological/resource projections while
`sameState` includes representation. `identify` plans round-robin cohort promotions
using the earliest available juvenile deadlines. For exact sources or reservation
changes, `identity --save SAVE --commands FILE --out NEW_DIRECTORY` accepts a JSON
array of consecutive commands. Example for a fresh world:

```json
[
  {"sequence":1,"tick":0,"type":"promote","source":{"region":{"value":0},"species":{"value":0},"stage":1,"variant":0},"active":false},
  {"sequence":2,"tick":0,"type":"set-active","person":{"value":1},"active":true}
]
```

Juvenile promotions also require an existing absolute `juvenileDeadline`; adult
promotions cannot supply an invented age. Commands must use the current completed
tick and next sequence. Repeated, stale, unavailable or over-budget commands reject
the entire batch without changing the input. The resulting archive records the
input, commands, identity events and output and supports exact command replay.
`identity-history --from/--to` uses inclusive **ticks**, unlike biological history's
year interval. Histories are segment-local; retain earlier archives for earlier events.

`benchmark --identities N --active N` promotes a fixed initial group. Optional
`--replenish-every 12` is a benchmark driver issuing recorded boundary commands to
maintain representation occupancy; it is not automatic simulation behavior. Limits
are configurable at world creation, with a supported maximum of 10,000 identities.
No Phase 1B policies, goals, memory or autonomous movement are implemented.
