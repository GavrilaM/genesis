# Phase 0.6 — Demographic Integrity

Date: 2026-10-26 (Asia/Jakarta). Engine **0.3.0**, snapshot schema **3**,
scenario schema **1**. Branch: `codex/genesis-prototype-001`.

Verified implementation: [`82098fe`](https://github.com/GavrilaM/genesis/commit/82098fe1e4e45aeecc03088720146596457a1d0d).
Machine-readable verification: [test-results.json](results/phase06/test-results.json).

The selected model uses **monthly minimum-age juvenile queues and binomial
birth/death events**. It preserves regional aggregate populations, the original
species parameters, finite food, deterministic seeds and monthly transactions.
No individual AI or civilization system is implemented. Phase 1 is a separate
architecture proposal requiring review.

## Decision and scope

The 0.2.0 juvenile transition had probability 1/M every month, including the first
month after birth. M was a mean residence duration, not a minimum age. Stochastic
rounding made every integer expected event count deterministic; for n=100 and
p=.01 it always generated exactly one event, with zero conditional variance.

Three age models were compared: the existing geometric model, exact monthly
minimum-age queues, and twelve-month deadline buckets. Each was crossed with
rounding or binomial births/deaths. Adult transitions and elder hazards were held
fixed, avoiding the all-stage duration confound in the Phase 0.5 diagnostic.
Mutation and migration totals were not changed to new stochastic distributions.

Monthly queues remove early maturation without the synchronized annual pulse of
twelve-month buckets. The latter put **100%** of maturation in calendar month 12;
monthly bins' largest month accounts for about **9%** in the 300-year worlds.
The coarser candidate's extra 0..11-month delay and calendar artifact are not
accepted merely for its speed advantage. It remains an explicit research control.

Binomial sampling is selected because it matches conditional event variance and
an analytically known extinction probability. It is not selected to increase
extinctions, equalize species, or produce a preferred population size. In some
small-colony cases the difference from rounding is modest and inconclusive.

## Mechanics and compatibility

A birth at completed tick B matures after survival at tick B + M. It first becomes
eligible for reproduction at B + M + 1. Each juvenile variant cohort contains an
integer ring buffer of M+1 monthly deadlines. These counts **partition** the
cohort's count; they are not additional people. Deadlines travel with migrants.
Two-pass migration preserves each deadline and prevents incoming migrants from
moving twice. No individual objects or per-person sampling loops are introduced.

Initial juvenile ages are declared initial conditions: the default distributes
each stage/variant total uniformly over residence ages 0..M-1 with cumulative
integer quotas. Such initial juveniles legitimately mature before tick M.
`initialJuvenilesAreNewborn: true` supplies a newborn pulse. Existing scenario
files and exact stage/variant totals remain valid, but the meaning of
`maturationMonths` intentionally changes from geometric mean to minimum residence.
There is no attempt to infer ages from a legacy save.

Births use Binomial(eligible surviving pre-aging adults, existing monthly birth
probability). Deaths use Binomial(count, existing clamped hazard). Juvenile deaths
are independent per age bin with the same hazard, preserving the aggregate
binomial distribution. One eligible adult can contribute at most one birth per
month; the configured rate still absorbs sex ratio and eligibility. This is not
a mating-pair or biomedical model. Food allocation, climate and hunger rules are
unchanged, including their common environmental correlations.

The sampler uses geometric waiting times when n*p < 16 and recursive beta
order-statistic splitting for larger counts. Gamma draws use Marsaglia–Tsang.
It does not approximate event counts with a normal/Poisson distribution or loop
over all people. Finite floating-point arithmetic and pseudo-random uniforms
remain numerical limitations. Sources: [Devroye, chapter X, section 4.5](https://numerical.recipes/devroye/Devroye_files/chapter_ten.pdf),
[author's book edition](https://luc.devroye.org/rnbookindex.html), and
[Marsaglia & Tsang, 2000](https://doi.org/10.1145/358407.358414).

Every variate has a local counter keyed by seed, tick, system, region, species
and slot. Rejection loops cannot consume another variate's random stream.
RNG identifier: `splitmix64-counter-v2-binomial`. Audits and queries do not draw.
Snapshot schema 3 includes juvenile queues and maturation accounting. Versions
0.1.0 and 0.2.0 are rejected before deserialization/checksum validation; use
`b0f8d4e` and `855c478`, respectively, to continue their saves. Scenario schema 1
is still accepted for fresh worlds. No cross-version trajectory guarantee is made.

Optional `demography` defaults are `juvenileBinMonths: 1`, `binomialEvents: true`,
`initialJuvenilesAreNewborn: false`. Width 0 and disabled binomial sampling preserve
the old mechanics as a controlled comparison, not old snapshot/hash compatibility.
Width 12 is the rejected coarse candidate. These options are persisted and hashed.

Adult residence times still use geometric transitions with stochastic rounding;
elder deaths include the existing duration hazard. Mutation splitting and total
migration retain rounding. Juvenile migration uses systematic proportional quotas
conditional on that total, with an independent randomized offset. It preserves
age shares in expectation but is not a hypergeometric composition sample. Exact
128-bit quota arithmetic avoids over-allocation at large integer boundaries.
These remaining approximations are explicit; this pass does not claim calibrated
extinction probabilities for real populations or exact lifetime histories.

## Controlled evidence

The production `Simulation.Step` runs every month with invariant validation.
Seeds are paired 1..N; model execution order rotates by seed. Model/configuration,
initial conditions, per-seed outcomes and state/event hashes are recorded. Pairing
controls seed labels/weather, not identical stochastic demographic events after
the sampling method or age partition changes.

- Full worlds: 6 models x 8 seeds x 300 years = **48 runs**.
- Long-run confirmation: geometric/rounding and monthly/binomial, 8 seeds each,
  1,000 years = **16 runs**.
- Newborn pulse: 6 x 64 seeds x 600 months = **384 runs**.
- Adult founders: 6 x 512 seeds x 600 months = **3,072 runs**.
- Newborn bottleneck: 6 x 512 seeds x 600 months = **3,072 runs**.
- Analytic death-only check: 2 x 4,096 seeds x 24 months = **8,192 runs**.

Total: **14,784 final controlled observations**. Repeated development runs and
end-to-end benchmarks are additional and not counted. Compact evidence in
`docs/results/phase06/` includes complete protocols, raw world observations,
per-seed colony outcomes, summaries and Wilson 95% extinction intervals. Full
local rows live under `artifacts/phase06/final/`. Compressed colony records include
a deterministic digest of sorted per-seed state/event hashes, excluding timings.
They preserve all measured demographic outcomes; column order is documented in
each file. Commands reproduce the original detailed rows.

### Reproduction timing, with other stage durations unchanged

Human colony: 100 newborn juveniles, 192-month maturation, 600 months, 64 seeds,
ample finite food, no thermal/starvation mortality, migration or mutation.
Natural mortality remains configured at .0003/month (juvenile multiplier 1.5).

| Age / events | Mean final | Final SD | Mean births through tick 192 | First birth range |
|---|---:|---:|---:|---:|
| Geometric / rounding | 362.38 | 34.77 | 48.80 | 5..46 |
| Geometric / binomial | 364.41 | 40.81 | 50.22 | 2..49 |
| Monthly / rounding | 276.17 | 23.10 | 0 | 193..196 |
| Monthly / binomial | 276.11 | 22.06 | 0 | 193..198 |
| Annual / rounding | 272.80 | 23.36 | 0 | 193..196 |
| Annual / binomial | 271.41 | 22.94 | 0 | 193..198 |

Unlike Phase 0.5's all-stage fixed-duration assay, only juvenile aging changes
here. The different final-population effect is not a contradiction: all-stage
fixed lifespans and minimum juvenile ages are different interventions. A minimum
age does not guarantee any particular final population or higher variance of the
whole nonlinear trajectory. Calendar pulse tests also cover non-divisible ages,
births after tick zero, queue wraparound and migration.

### Extinction and variance

Death-only reference: 20 newborn juveniles, no births or aging within 24 months,
independent monthly death probability .1, no climate/food stress/migration.
Survival probability is .9^24; final population is Binomial(20, .9^24).
Analytic mean = **1.59533**, variance = **1.46808**, extinction = **18.9654%**.
Observed binomial extinction = **799/4,096 = 19.5068%**; rounding gives
**469/4,096 = 11.4502%**. The binomial result passes the predeclared six-standard-
error analytic extinction gate. This isolates an actual variance-related failure
of rounding, rather than assuming every ecological scenario must show one.

The adult-founder colony starts with 20 adults and raises natural mortality to
.003/month. Extinction is rare by 600 months: 2/512 geometric/rounding,
0/512 geometric/binomial, 2/512 monthly/rounding, 1/512 monthly/binomial.
These rates do not establish a meaningful ordering of the models. A singleton
extinction-time sample has SD null, not a fabricated zero or NaN.

The newborn bottleneck starts with 20 juveniles and natural mortality .006/month
(juveniles .009), retaining 192-month maturation and .09 annual births.

| Age / events | Extinct / 512 at 600 months | Mean final | Mean extinction tick, extinct runs only |
|---|---:|---:|---:|
| Geometric / rounding | 290 | 1.08 | 438.47 |
| Geometric / binomial | 268 | 1.14 | 426.02 |
| Monthly / rounding | 332 | 0.71 | 416.78 |
| Monthly / binomial | 335 | 0.67 | 410.51 |
| Annual / rounding | 335 | 0.71 | 417.04 |
| Annual / binomial | 339 | 0.63 | 400.17 |

Minimum ages remove the geometric model's rapid reproductive rescue. The small
difference between monthly rounding/binomial is within sampling uncertainty; at
very small n, rounding is already close to Bernoulli. Conditional extinction-time
means exclude surviving runs and are not uncensored mean times to extinction.
All colonies are illustrative model diagnostics, not empirically calibrated ecology.

### World behavior

Mean species populations at 300 years (8 seeds). No species value was retuned.

| Model | Humans | Orcs | Elves | Dwarves |
|---|---:|---:|---:|---:|
| Geometric / rounding | 1,598.50 | 10,292.00 | 41.25 | 118.38 |
| Geometric / binomial | 1,640.00 | 10,205.88 | 45.25 | 117.00 |
| Monthly / rounding | 1,783.25 | 10,055.00 | 51.75 | 176.12 |
| Monthly / binomial | 1,827.75 | 9,991.75 | 65.75 | 167.50 |
| Annual / rounding | 1,825.63 | 9,979.38 | 63.75 | 186.75 |
| Annual / binomial | 1,882.25 | 9,919.88 | 70.88 | 183.25 |

All species survive at 300 years in these runs. Correcting demographic integrity
changes trajectories but does not force equality or eliminate competition.

At 1,000 years, geometric/rounding averages 22.75 humans and 11,771.25 orcs;
monthly/binomial averages 49.75 humans and 11,743.50 orcs. Elves and dwarves are
extinct in all eight runs of both models. Humans are extinct in 1/8 old-model
runs and 0/8 new-model runs. Eight seeds cannot establish a general human
extinction-risk estimate; orcs remain dominant in all cases. The old-model
control's per-seed final populations and birth/death totals also match the
accepted Phase 0.5 dataset, confirming that the reference was preserved.

## Verification, observability and performance

Locked restore and Release build passed with **0 warnings/errors**. The full
suite passed **60 tests, 0 failures, 0 skipped**, in about 64 seconds. **17 CLI
smoke checks** passed. The 100-year, 1,000-year, double-region and continuation
archives all verified. Raw test evidence is
`artifacts/phase06/tests/phase06-final.trx`; compact counters are committed.

Tests cover minimum ages and delayed reproduction, post-start births and ring
wraparound, age-preserving migration, exact quotas near 1e12, queue corruption,
input immutability, mid-month/mid-year resume, annual maturation accounting,
audits without state changes and old-save rejection. Sampler tests use 16,384
seeds per moment case, 32,768 for tails/partitioning, probabilities from 1e-12
through .75 plus endpoints, and counts through 1e12. Means must lie within six
analytic standard errors and variances within 7%; exact small-n tail probabilities
and the production birth/death variance are independently checked. The analytic
extinction regression uses 1,024 seeds. The original environmental, conservation,
reproducibility and 1,000-year checks also pass.

The existing annual ledger remains exact. Maturation is recorded as a stage
transfer in monthly flows, CSV, canonical pending windows, summaries and annual
event contributors. It is never counted as an extra birth. Seed 847192, region
0/humans, year 0: **800 + 12 births - 420 deaths - 203 outward = 189**;
seven juveniles matured during that interval. New minimum ages do not prevent
age-mixed initial juveniles from maturing in the first year.

Release .NET 10.0.1, SDK 10.0.101, Windows x64 build 26200, Ryzen 5 7520U,
8 logical processors, about 15.28 GiB machine RAM. No packages or runtime services
were added. Benchmarks are serial new processes with full monthly validation,
annual CSV/summaries, event writes and 25-year snapshots. Initial snapshot creation
and final report serialization are outside the stopwatch. These are individual
end-to-end observations, not microbenchmark confidence estimates.

| Run | Seconds | Ticks/s | Peak working set B | Snapshot B |
|---|---:|---:|---:|---:|
| 100 years, 12 regions / 432 cohorts | 3.237 | 370.70 | 66,007,040 | 124,990 |
| 1,000 years, 12 regions / 432 cohorts | 16.544 | 725.35 | 66,908,160 | 124,789 |
| 1,000 years, 24 regions / 864 cohorts | 31.953 | 375.55 | 72,445,952 | 249,062 |
| Accepted 0.2.0, repeated 1,000 years | 8.838 | 1,357.80 | 55,730,176 | 53,481 |

The new 1,000-year run takes **1.87x** the contemporaneous 0.2.0 reference and
2.36x its previously recorded 7.011-second observation. Doubling regions costs
1.93x. Monthly bins add 31,248 integer cells at baseline; cost follows configured
age span/region/species/variants, not total population. Skipping zero-population
sampling work improved the measured candidate runtime without changing any of
the 48 repeated world state/event hashes. The full benchmark remains fast enough
for this headless milestone, with the cost reported rather than hidden.

Seed 847192 ends at **11,883 = 38,400 + 472,263 births - 498,780 deaths**:
81 humans, 11,802 orcs, no elves/dwarves. Migration throughput is 2,818,689.
There are 21,812 significant events (10,572,171 bytes) and 48,000 annual summaries
(10,151,354 bytes). Year 900 + 100 continuation matches the fresh state/event
hashes exactly. Old 0.2.0 replay also matched its accepted hashes and population.

- Rules: `0EB9F0D54557FCA59570C6E94A17AEA79481B6895C20C23D1EAEC78B11CEFEB8`
- State: `48AB41B6FD5C6CBA6A1D357A0ECFAD4AFAFF7E85A48ACED739C5A86D92D1600B`
- Events: `E8857F1E5A128007EE3A4626833FA4043D44F519099639AF7429C4B166F715C4`

## Reproduction commands

Run from the repository root; choose new output directories on repeat runs.

```powershell
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --logger 'trx;LogFileName=phase06-final.trx' --results-directory artifacts/phase06/tests
$g = 'src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
./scripts/Run-DemographicStudy.ps1 -OutputDirectory artifacts/new-demographic-study
./scripts/Export-DemographicEvidence.ps1 -StudyDirectory artifacts/new-demographic-study -OutputDirectory artifacts/new-demographic-evidence
dotnet $g benchmark --seed 847192 --years 1000 --out artifacts/new-1000 --trace-tick 1 --trace-region 0
dotnet $g benchmark --seed 847192 --years 100 --out artifacts/new-100
dotnet $g benchmark --seed 847192 --years 1000 --double-regions --out artifacts/new-double
dotnet $g run --save artifacts/new-1000/snapshot-00010800.json --years 100 --out artifacts/new-resume
dotnet $g compare artifacts/new-1000/latest.json artifacts/new-resume/latest.json
./scripts/Smoke-Test.ps1 -RunDirectory artifacts/new-1000
dotnet $g verify artifacts/new-resume
```

The study script lists all six suites and exact seeds/durations; CLI help documents
individual suite selection. Rebuild commit `855c478` separately to repeat the
legacy benchmark. Large archives, temporary binaries and raw test logs remain
Git-ignored; final source, tests, protocols and compact evidence are committed.

Development corrections: a summary with a single extinction-time observation
initially produced NaN SD and could not serialize; it now reports null and the
study was rerun. Large-count systematic allocation was changed to integer quotas
to avoid floating-point boundary over-allocation. Full world reruns retained all
48 state/event hashes. Neither correction retuned species or weakened acceptance
criteria. Phase 0.5 fingerprints were explicitly replaced at the 0.3.0 boundary.
