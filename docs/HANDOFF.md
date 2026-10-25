# Prototype 001 initial engineering handoff

Date: 2026-10-25, Asia/Jakarta. Engine: 0.1.0. Branch:
`codex/genesis-prototype-001`. Implementation commit: `ddac9ab`; first tested
core increment: `85535c8`. This report and compact evidence are a following commit.

The first functional headless implementation is delivered. It creates a seeded
world, advances every month of 1,000 years, records measured state transitions,
exports sampled flows and trait distributions, verifies saves/ledgers, and exactly
reproduces continuation. It is a provisional simulation model, not calibrated
ecology. The full blueprint and vision were read before implementation and copied
unchanged to `docs/`. The vision did not expand the implementation scope.

## Environment actually used

- Workspace: `C:/Users/Gavri/Documents/ChatGPT/Matrix`; initially empty Git repo.
- Windows 11 Home Single Language, OS build 10.0.26200, x64.
- AMD Ryzen 5 7520U with Radeon Graphics, 8 logical processors;
  16,407,719,936 bytes reported system RAM (about 15.28 GiB).
- .NET SDK 10.0.101, MSBuild 18.0.6, runtime Microsoft.NETCore.App 10.0.1.
- Git 2.47.0.windows.1; VS Code 1.132.0 available. No C# extension was installed
  at inspection; development and verification used the terminal.
- PowerShell execution, filesystem edits, NuGet restore and local Git commits
  all succeeded. No remote Git repository is configured; no push was attempted.
- xUnit v3 3.2.2, xunit.runner.visualstudio 3.1.5, Microsoft.NET.Test.Sdk 18.0.1.
  Direct versions and transitive package lock files are committed. Restore with
  `--locked-mode` succeeded. Core and CLI have no third-party package dependencies.

Tooling references checked at kickoff: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
and [official xUnit v3 guidance](https://xunit.net/docs/getting-started/v3/cmdline).
The versions above describe this tested machine, not a claim of latest patch level.
Other available app connectors were unnecessary; no game engine, graphics tools,
cloud services or LLM calls are used by Genesis.

## Validation actually executed

```powershell
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-restore --logger 'trx;LogFileName=final.trx' --results-directory artifacts/tests
./scripts/Smoke-Test.ps1 -RunDirectory artifacts/baseline-1000
```

Build: **0 warnings, 0 errors**. Final automated suite: **21 passed, 0 failed,
0 skipped**, approximately 14 seconds. CLI smoke script: **13 passed**.
Machine-readable test record: `artifacts/tests/final.trx` (local, ignored).

Tests exercise per-month conservation and references, immutable-input transitions,
food allocation, empty populations, birth eligibility, graph/cost restrictions,
inheritance, thermal mortality, bounded non-perturbing audit, incompatible/null/
unknown configuration rejection, save corruption, failed-save preservation,
event-chain tampering, famine hysteresis, 1,000-year invariants, same-seed output,
and snapshot continuation for 100 years. Five paired counterfactual tests each
use 12 seeds and 20 years. A separate experiment run uses 16 seeds and 30 years.

The smoke script invokes the built executable to check world/region/species
inspection, filtered history, explanations, captured and missing traces,
verification, comparison, byte-identical CSV export, and invalid CLI input.
Creation and actual run/benchmark commands were separately executed below.

Earlier results are retained locally: initial core tests 8/8 passed; first broader
suite 16/18 passed with two cold-assay failures; corrected assay 18/18 passed;
additional audit/thermal/configuration tests brought the final suite to 21/21.

## End-to-end reproducibility

Commands executed from repository root (paths below already exist):

```powershell
$g = 'src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
dotnet $g new --seed 847192 --out artifacts/seed-847192.json
dotnet $g run --save artifacts/seed-847192.json --years 1000 --out artifacts/baseline-1000 --trace-tick 1 --trace-region 0
dotnet $g run --save artifacts/baseline-1000/snapshot-00010800.json --years 100 --out artifacts/resume-900-to-1000
dotnet $g compare artifacts/baseline-1000/latest.json artifacts/resume-900-to-1000/latest.json
dotnet $g verify artifacts/baseline-1000
dotnet $g verify artifacts/resume-900-to-1000
```

Comparison returned `sameState: true` and `sameEventHistory: true`. Both archives
verified. A fresh final-build 1,000-year benchmark independently matched the same
state and event hash. A run starting at year 900 stores its own event segment,
continuing the original hash and event numbering without duplicating prior history.

- Seed: `847192`; final tick: `12000`.
- Ruleset hash: `FE271C407FAC8641A730625C2BA61841BDA1ED64AE4E2784FD15C6F3FF8DC994`.
- Final state hash: `14337B7B1BBFA89123D3E594518F4C70CAAA245F2A2FF972542A2203CD05C1AE`.
- Final event hash: `B577C3F4E783A9557BCE7410959A172DD364ECEA1522BA5C8EF057276F5D9466`.
- Population: 38,400 initial + 510,487 births - 537,014 deaths = **11,873**.
- Internal migrations: 3,196,682 transfers of people across regional boundaries,
  with equal inward/outward totals; the same person can move in multiple months.
- Final species counts: human 2, orc 11,871, elf 0, dwarf 0. Extinct cohorts remain.

## Performance measurements

Final-build Release runs, seed 847192, one process per run, full validation each
tick, annual sampled metrics and 25-year snapshots. Single observations rather
than statistically controlled microbenchmarks; startup/JIT and machine load
affect short-run throughput. Timed section includes loop, reports' source data
files and periodic/final snapshots; it excludes initial snapshot creation and
writing the report itself. Peak process memory includes startup and initial save.

| Case | Ticks | Regions / cohorts | Seconds | Ticks/sec | Peak working set | Final snapshot | Events / ledger bytes |
|---|---:|---:|---:|---:|---:|---:|---:|
| 100 years | 1,200 | 12 / 432 | 1.025 | 1,171 | 48,517,120 B | 46,764 B | 2,232 / 1,006,797 B |
| 1,000 years | 12,000 | 12 / 432 | 4.706 | 2,550 | 55,803,904 B | 46,587 B | 16,593 / 7,537,251 B |
| Double scale, 1,000 years | 12,000 | 24 / 864 | 7.774 | 1,544 | 63,000,576 B | 92,688 B | 36,225 / 16,481,570 B |

Doubling regions and cohorts took 1.65x elapsed time in these observations. The
scale scenario duplicates geography and adds matching cross-copy edges; it is a
workload measurement, not an assertion that the dynamics remain identical.

```powershell
dotnet $g benchmark --seed 847192 --years 100 --out artifacts/final-benchmark-100
dotnet $g benchmark --seed 847192 --years 1000 --out artifacts/final-benchmark-1000 --trace-tick 1 --trace-region 0
dotnet $g benchmark --seed 847192 --years 1000 --double-regions --out artifacts/final-benchmark-double-1000
```

Compact machine-readable reports are committed in `docs/results/benchmark-*.json`.
Large run directories and snapshots remain under `artifacts/` and are Git-ignored.

## Counterfactual results and the failed hypothesis

```powershell
dotnet $g experiments --seeds 16 --years 30 --out artifacts/experiments-controlled.jsonl
./scripts/Summarize-Experiments.ps1 -InputPath artifacts/experiments-controlled.jsonl -OutputPath docs/results/experiment-summary.json
```

All experiments use paired seeds 1..16, 360 explicit monthly ticks and identical
initial inputs except the stated treatment. Differences are treatment minus
control. SD is the sample SD of paired differences, not a confidence interval or
real-world uncertainty estimate. Raw paired observations and summary are committed.

| Treatment | Metric | Control mean | Treatment mean | Paired mean difference (SD) | Direction agrees |
|---|---|---:|---:|---:|---:|
| Rain x0.15, ticks 25–144 | Mean unmet food fraction | 0.040156 | 0.065174 | +0.025018 (0.000759) | 16/16 |
| Strong vs weak initial cold resistance, controlled cold/food | Final population | 8,743.56 | 73,724.44 | +64,980.88 (181.09) | 16/16 |
| No food stocks/regrowth, no edges in either arm | Final population | 12,221.50 | 0 | -12,221.50 (108.56) | 16/16 |
| Productivity x1.5 | Final population | 13,018.13 | 19,516.63 | +6,498.50 (175.21) | 16/16 |
| Remove all edges | Cumulative migration | 90,893 | 0 | -90,893 (3,467.95) | 16/16 |

These validate conditional model responses over fixed seeds, not all possible
worlds. Drought migration was *lower*, not higher, after major population loss;
increased scarcity satisfies the intended drought comparison. Starvation's
time-average scarcity falls after extinction because empty demand is zero.

The first cold test shifted the mixed-climate baseline by -18 degrees and expected
strong resistance to improve final population. It failed: over the first four
20-year pairs, mean effect was -102.5 and mixed resistant frequency was 0.3137.
The model charges a food cost for resistance, and warm refuges remain available.
Controlled diagnosis changed one factor at a time:

- Ample food alone: population effect +12,833.75; resistant frequency 0.3689.
- Uniform -5-degree base climate alone: +1,992; frequency 0.5540.
- Both controls: +47,271.25; frequency 0.5559.

The revised assay uses uniform -5-degree base temperature and finite 1e9-ration
storage per region, initially full. Both arms use the same environment and stocks.
This isolates thermal fitness without changing survival equations or removing the
food cost. Separate tests check differential mortality directly and selection in
a mixed starting population. The original result is not erased or characterized
as a universal benefit of resistance. Diagnostic data are in
`docs/results/cold-diagnostic.jsonl`; the original 16-seed experiment remains in
`artifacts/experiments.jsonl`.

## Limitations and next decision

Mechanics, units and assumptions are fully listed in `ARCHITECTURE.md`. The main
remaining modeling question is long-run diversity and parameter sensitivity: the
baseline seed nearly becomes a single-species world. No cultural ranking or
scripted dominance exists, but these illustrative biology parameters are not yet
balanced or calibrated. Do not present this outcome as a designed species destiny.

Known simplifications: stochastic rounding rather than binomial variation;
geometric life stages rather than exact ages; group-level hunger; uniform initial
populations; shared seasons; finite forage as the only resource; one-step migration
forecasts that can oscillate. Migration is observable in metrics/audit but has no
separate migration-wave ledger event. CSV sampling records sampled months, not
annual totals. Archives are single-writer, multi-file outputs with an incomplete
marker, not transactional databases. Malicious tampering is not authenticated.

No intervention editor, uncaptured trace replay, schema migrations, cross-platform
determinism guarantee, adaptive stepping, graphics, individuals, settlements,
culture, religion, magic or LLM runtime has been implemented. The next useful
work is parameter/sensitivity review within Prototype 001, before any future system.
