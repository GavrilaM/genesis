# Phase 0.5 — Simulation Calibration & Correctness

Date: 2026-10-25 (Asia/Jakarta). Implementation commit: `31e2aef`.
Engine **0.2.0**, snapshot schema **2**, scenario schema **1**.
Branch: `codex/genesis-prototype-001`. This report accompanies a subsequent
results commit. No baseline species parameter was retuned and no future system
was introduced.

The requested pass is complete: configurable starting populations, corrected
migration forecasts, controlled sensitivity experiments, demographic approximation
assays, exact annual reporting, tests, benchmarks and reproducible continuation.
The model is better instrumented and mechanically checked. Its demographic
approximations still limit claims about minimum reproductive ages and extinction
risk; those limitations are measured below, not declared solved.

## Changes and compatibility

**Starting populations.** Existing baseline scenario JSON is byte-for-byte
unchanged in Git. Omitting `initialPopulations` (or null) retains the previous
starting cohort allocation. An explicit array is a complete sparse allocation:
unlisted region/species pairs are empty; `[]` creates an empty world. Each entry
specifies three life-stage counts and optionally trait weights. Exact 3x3
`stageVariantCounts` can be provided instead, with rows summing to stage counts.
References, bounds, ordering, duplicates and contradictory input modes are checked.
All zero cohorts remain. `scenarios/sparse-frontier.json` starts 1,580 people with
different species totals in four regions, leaving eight regions empty. A verified
100-year run, seed 42, ended at 11,651 with all four species present.

**Migration forecast.** The old expression was `remaining stock + current month's
regrowth`. It did not create extra food in the consumption ledger, but it could
misestimate prospective food. A regression reproduced a destination holding 20
remaining rations after producing 100 and consuming 80; the old forecast claimed
120 despite capacity 100 (score 1.4997 rather than a maximum 1.2497).

The new forecast adds only estimated NEW production for next month, limited by
remaining storage space. It uses next month's seasonal temperature, configured
rainfall/drought schedule and mean weather; thermal scoring uses that same horizon.
It neither consumes random draws nor reads actual future random weather. It is a
plug-in estimate, not the exact expectation of nonlinear climate/production
functions. Regression tests cover capacity, storage freed by consumption, drought
start/end, no future RNG lookahead, and colonization of an empty region.
Migration remains a frozen two-pass plan with one graph edge per month.

**Version boundary.** These forecasts intentionally change future trajectories.
New annual accumulators also change canonical state. Engine 0.2.0 requires snapshot
schema 2; old saves get a clear compatibility error before checksum checking.
Use the old engine at commit `b0f8d4e` to continue 0.1.0 saves. Existing scenarios
can create fresh worlds under 0.2.0. No silent save conversion is claimed.
Within the supported Windows x64/.NET environment, same version/configuration/
seed still reproduces state, summaries and event hashes. Mid-year and 100-year
continuation are tested; audit/queries do not perturb simulation.

## Controlled population sensitivity

Published evidence: **18 cases x 8 seeds x 300 years = 144 observations**, followed
by **6 cases x 8 seeds x 1,000 years = 48 observations**. Each month is explicitly
simulated with invariant validation. Seeds 1..8 are paired against baseline.
Every row includes version, scenario hash, state/event hashes, per-species initial/
final counts, cumulative flows, demand, consumption, adult exposure and thermal
exposure, plus century checkpoints. Raw rows and protocol manifests are committed
in `docs/results/phase05/sensitivity-300/` and `confirmation-1000/`.

The 300-year sweep changes only the named parameter group in orcs, with low/high
birth rate and food demand at +/-20%, maturation at +/-25%, tolerance narrowed or
widened by 5 C at both ends. Human/orc swaps exchange one factor, or all biological
parameters while preserving names and IDs. Four geographic rotations concentrate
each species in one of regions 0/3/6/9, preserving EXACT species, stage and variant
totals while leaving eight regions empty. They do not change climate or edges.

Mean final orc population share, with paired change relative to each seed's
baseline (percentage points); SD describes the paired seed differences, not a
confidence interval or real-world uncertainty:

| 300-year case | Orc share | Paired change (SD), pp |
|---|---:|---:|
| Baseline | 85.41% | 0 |
| Orc reproduction -20% / +20% | 26.41% / 98.36% | -59.00 (1.84) / +12.95 (0.90) |
| Orc maturation -25% / +25% | 94.96% / 69.62% | +9.55 (0.68) / -15.79 (1.27) |
| Orc food demand -20% / +20% | 85.35% / 85.60% | -0.06 (0.39) / +0.19 (0.78) |
| Orc tolerance narrowed / widened | 17.52% / 96.94% | -67.89 (1.33) / +11.53 (0.63) |
| Swap human/orc reproduction | 35.09% | -50.32 (1.90) |
| Swap maturation | 68.31% | -17.10 (1.33) |
| Swap demand | 85.15% | -0.26 (0.59) |
| Swap climate tolerance | 37.00% | -48.41 (2.19) |
| Swap all biology | 13.58% | -71.83 (1.19) |
| Geography rotation 0 / 1 / 2 / 3 | 90.98 / 81.58 / 90.25 / 62.62% | +5.57 / -3.83 / +4.83 / -22.79 |

Full per-species ranges, dominant-seed counts, paired SD/min/max and normalized
flow metrics are in `sensitivity-300-summary.json`.

| 1,000-year confirmation | Mean humans | Mean orcs | Mean orc share | Dominant species, seeds |
|---|---:|---:|---:|---|
| Baseline | 22.75 | 11,771.25 | 99.807% | Orc, 8/8 |
| Orc reproduction -20% | 12,628.63 | 688.38 | 5.164% | Human, 8/8 |
| Swap reproduction | 11,529.38 | 1,704.75 | 12.871% | Human, 8/8 |
| Swap tolerance | 10,766.63 | 2,393.25 | 18.178% | Human, 8/8 |
| Swap all biology | 11,792.00 | 24.13 | 0.203% | Human, 8/8 |
| Geography rotation 3 | 98.13 | 11,688.13 | 99.168% | Orc, 8/8 |

**Interpretation.** The tested reproduction and tolerance changes can reverse
competitive dominance. Shorter maturation also matters; initial geography alters
the transient substantially but the selected favorable geography did not overturn
the long-run baseline outcome. Swapping biology transfers dominance to the human
label, so there is no scripted orc destiny. These are conditional interventions in
this model, not a complete factorial attribution of interacting effects.

At 300 years the baseline integrates 0.04319 births and 0.04288 deaths per orc
person-year, versus 0.03902 and 0.04807 for humans. Orcs have higher configured
reproduction (0.10 versus 0.09/year) and broader thermal tolerance (-2..32 versus
0..30 C), with faster mean maturation (168 versus 192 months). Recorded thermal
exposure is predominantly heat; heat and cold tolerance were swapped together,
so this does not separately prove heat's causal contribution. Elves/dwarves have
lower reproduction and greater recorded heat exposure in this geography.

Food demand mostly changes total supported population here: reduced orc demand
raises mean final orcs from 10,292 to 12,445.75 without materially changing share.
This follows the shared proportional food allocation: every species in a region
receives the same fraction of its demand. It is a limitation of the current
competition model, not a general ecological claim about metabolism.

Eight **legacy 0.1.0** baseline runs at 1,000 years also remained orc-dominated
(average humans 21.625, orcs 11,737.75). The new forecast alone therefore does not
explain or resolve monoculture. `legacy-1000.json` preserves those observations.
No equality quotas, species caps, scripted rescue or automatic retuning were added.

**Protocol correction.** Initial geography trials re-rounded aggregate traits,
changing elder composition from [312,312,336] per species to [320,320,320]. This
small confound was discovered during review. Exact cohort matrices were added,
tested, and all geographic cases rerun. Published tables/rows use corrected runs;
original exploratory outputs remain ignored under `artifacts/phase05/`. Repeated
baseline rows matched state/event hashes before merging corrected geographic rows
with unchanged non-geographic rows. All final case definitions are reproducible
with the committed calibration command.

## Demographic approximation evaluation

The offline assay uses a closed human colony of 100 newborn juveniles, abundant
food, no climate/migration/trait selection, 600 monthly steps and 64 seeds per
combination. Fixed stage durations use aggregate month-age bins, not individuals.
It compares all-stage fixed residence durations against geometric stages, and
exact binomial draws against stochastic rounding. All four variants conserve
initial + births - deaths = final. Alternatives exist only in the diagnostic;
the production demographic mechanics remain unchanged.

For a 192-month geometric juvenile residence time, without mortality:
22.17% mature by month 48; 63.31% by month 192; 13.46% remain juvenile at month 384.
Thus this parameter is a mean duration, **not a minimum reproductive age**.

| Age / count model | Mean final population | Final SD | Mean births before month 193 | First birth tick range |
|---|---:|---:|---:|---:|
| Geometric / rounding | 351.30 | 34.19 | 47.92 | 4..47 |
| Geometric / binomial | 367.44 | 49.86 | 48.66 | 4..59 |
| Fixed / rounding | 422.08 | 18.28 | 0 | 193..198 |
| Fixed / binomial | 435.98 | 28.61 | 0 | 193..203 |

The fixed-stage comparison changes all three residence-time distributions, so its
final-population effect cannot be assigned solely to juvenile maturation. It is a
sensitivity probe, not an empirically validated replacement world model.

An isolated mortality draw with n=100 and p=0.01 demonstrates the variance issue:
stochastic rounding always produces exactly one death (variance 0). Over 8,192
seeded trials, the exact binomial sampler produced mean 1.02869 and variance
1.00601, against analytic variance 0.99. At n=1 rounding and binomial are both
Bernoulli; suppression is not equally severe at every cohort size.

**Evidence-backed recommendations:** before interpreting reproductive timing,
introduce a minimum juvenile age or staged residence distribution and validate it
against an aggregate exact-age reference. Before interpreting extinction risk or
genetic drift, evaluate bounded/binomial event sampling, at least for small
cohorts, with variance and extinction-distribution gates. The measured early
births and SD changes justify those investigations. This pass does not silently
replace the production model: the assay lacks food-limited feedback and is not
enough evidence to choose/calibrate a global replacement. Raw assay rows and
moments are in `demography.json`.

## Historical observability

Every complete calendar year now records exact region/species totals in
`summaries.jsonl`, independently of sampled CSV cadence. Pending totals live in
canonical snapshot windows, so a mid-year save cannot lose the year's prefix.
`history-summary` returns only recorded, wholly contained intervals. Incomplete
final years remain in snapshot state; absent data returns `not recorded`.

A `population-change` event records >=max(50 people, 20% of year-start population)
absolute change. A `migration-wave` records yearly outward throughput of at least
max(50, 50% of year-start population), once per group/year. Throughput includes
repeated moves and is not unique migrants or an exact wave onset date. Both events
contain births/deaths/inward/outward flow and demand/consumption evidence.

For seed 847192, region 0/humans in year 0: 800 + 11 births - 401 deaths + 0 in -
208 out = 202. A later wave, ticks 24..36, records 63 + 3 - 41 + 72 - 54 = 43.
These are recorded accounting explanations, not inferred personal motives.

The 1,000-year run produced 48,000 annual summaries (9,556,312 bytes), including
empty cohorts' zero flows. Ledger volume: 22,609 events — 8,970 famine starts,
8,965 famine ends, 2,855 annual population changes, 1,817 migration waves, and
2 species extinctions. Summary-file checksums are verified with the archive;
this detects accidental corruption, not malicious rewriting of both report/data.

## Verification, performance and reproducibility

SDK 10.0.101; runtime .NET 10.0.1; Windows 11 x64 build 26200; AMD Ryzen 5 7520U,
8 logical processors, approximately 15.28 GiB reported RAM. Same local machine as
Prototype 001. No new packages, runtime services or graphics dependencies.

Final Release build: **0 warnings/errors**. Tests: **37 passed, 0 failed, 0 skipped**,
approximately 24 seconds. CLI checks: **15 passed**. Raw TRX is local at
`artifacts/phase05/tests/phase05-final.trx`; a compact result is committed.

Tests include all original gates plus sparse/empty/exact initialization, malformed
entries, colonization, forecast regression/drought/capacity, twelve-month accounting,
wave thresholds, summary corruption, sparse mid-year 100-year continuation,
legacy rejection and exact baseline 1,000-year state/event fingerprints.

Release timings include every monthly invariant check, annual summary/CSV output,
event ledger and 25-year snapshots. Each benchmark runs serially in a new process;
initial save creation/report serialization are outside the stopwatch. These are
end-to-end observations, not controlled microbenchmark estimates.

| Run | Ticks | Regions/cohorts | Seconds | Ticks/sec | Peak working set B | Snapshot B |
|---|---:|---:|---:|---:|---:|---:|
| 100 years | 1,200 | 12/432 | 1.358 | 884 | 50,827,264 | 53,659 |
| 1,000 years | 12,000 | 12/432 | 7.011 | 1,712 | 58,437,632 | 53,481 |
| Double scale, 1,000 years | 12,000 | 24/864 | 13.030 | 921 | 62,296,064 | 106,451 |

The earlier 0.1.0 measurement was 4.706 seconds for 1,000 years. This pass adds
canonical accounting validation and more output: current time is about 49% higher
in these separate observations. No optimization claim is made; both instrumentation
and environmental measurement variation can contribute. Doubling regions/cohorts
cost 1.86x elapsed time. The final baseline ledger is 10,754,170 bytes; double scale
is 22,627,701 bytes. Full machine-readable reports are committed.

Seed 847192 ended at 11,962 people: 38,400 + 511,600 births - 538,038 deaths;
17 humans, 11,945 orcs, zero elves/dwarves. Internal migration throughput was
3,073,102. Reloading year 900 and continuing for 100 years matched the fresh
1,000-year run exactly. Both archive and ledger verification succeeded.

- Rules: `BCFF4F2C06122E329F27BD45E9A304538F5C00B3163CD0AE5FECFAD7AA95C918`
- State: `197DA8D3C752026CEA54412E0C37772D6308A4841CCEBB07D08074E4165A83D1`
- Events: `1FBF9D660016EBC9EA6EAC18A22EFD4B43A9B4AB2F3A751E6369AE3E211AB96E`

During development the forecast regression failed as intended before the fix.
An early reporting test wrongly assumed a baseline wave within its first two years;
it was replaced with a deliberate 80-person migration fixture without changing
thresholds. One build attempt failed because a running calibration process held
a DLL open on Windows; after it completed, the clean build and suite passed.
The geographic trait-rounding confound and its correction are recorded above.
No failing acceptance checks remain.

## Exact commands

Run from the repository root. Existing evidence paths below already exist; choose
new output directories to reproduce them. The main README also gives quick-start
creation/inspection/export commands.

```powershell
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-restore --logger 'trx;LogFileName=phase05-final.trx' --results-directory artifacts/phase05/tests
$g = 'src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
dotnet $g calibrate --seeds 8 --years 300 --out artifacts/calibration-300
dotnet $g calibrate --seeds 8 --years 1000 --cases baseline,orc-birth-low,swap-birth,swap-tolerance,swap-biology,geography-3 --out artifacts/calibration-1000
./scripts/Summarize-Calibration.ps1 -Directory artifacts/calibration-300 -OutputPath artifacts/calibration-300-summary.json
dotnet $g demography-assay --seeds 64 --out artifacts/demography-assay.json
dotnet $g benchmark --seed 847192 --years 1000 --out artifacts/phase05/final-1000 --trace-tick 1 --trace-region 0
dotnet $g benchmark --seed 847192 --years 100 --out artifacts/phase05/final-100
dotnet $g benchmark --seed 847192 --years 1000 --double-regions --out artifacts/phase05/final-double-1000
dotnet $g run --save artifacts/phase05/final-1000/snapshot-00010800.json --years 100 --out artifacts/new-resume
dotnet $g compare artifacts/phase05/final-1000/latest.json artifacts/new-resume/latest.json
./scripts/Smoke-Test.ps1 -RunDirectory artifacts/phase05/final-1000
dotnet $g verify artifacts/phase05/final-1000
dotnet $g history-summary --run artifacts/phase05/final-1000 --from 0 --to 1 --region 0 --species 0
dotnet $g history --run artifacts/phase05/final-1000 --type migration-wave
dotnet $g explain event 246 --run artifacts/phase05/final-1000
```

Published calibration datasets combine unchanged non-geographic cases with
corrected geographic reruns; the two full calibration commands above reproduce
the same final dataset directly. Legacy comparison used the preserved 0.1.0
binaries, seeds 1..8, 1,000 years, `--sample-every 12000 --snapshot-every 1000`;
rebuild commit `b0f8d4e` in a separate checkout to repeat that reference.

Large archives and temporary diagnostics remain Git-ignored in `artifacts/`.
Source, tests, examples, protocol/data summaries and raw compact observations are
tracked. Minimum-age demography, statistical extinction validation, forecast
crowding feedback and empirically meaningful parameter calibration remain explicit
research limits. No Phase 1 systems are authorized or implemented by this pass.
