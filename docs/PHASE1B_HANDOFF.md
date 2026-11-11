# Phase 1B — Local observation, dated memory and movement decisions

Date: 2026-11-11 (Asia/Jakarta). Engine **0.5.0**, snapshot schema **5**,
scenario schema **1**. Base: `9bd958f`; branch `codex/genesis-prototype-001`.
The user's instruction to execute [PHASE1B_PLAN.md](PHASE1B_PLAN.md) authorized
this implementation. The plan's former approval questions are historical decisions.

Verified implementation: [`824261f`](https://github.com/GavrilaM/genesis/commit/824261ff5756beb5ff0c44508edbeae20f30f15c).
Review evidence: [tests](results/phase1b/test-results.json),
[reference controls](results/phase1b/controls.json),
[all benchmark repetitions](results/phase1b/benchmarks.json),
[worked decision](results/phase1b/demo.json),
[active continuation](results/phase1b/continuation.json).

Implementation and verification are complete. This
milestone adds the smallest headless local decision policy. It adds no individual
physiology, inventories, families, social behavior, LLM services or civilization
features. Species and baseline biological parameters remain unchanged.

## Behavior and ownership

Phase 1A remains the population authority: anonymous cohorts plus exclusive
living identities, one regional food ledger and shared demographic sampling.
Cognition defaults to Off. ObserveOnly records a surviving active person's direct
experience while leaving generic migration unchanged. Decide records experience
and issues one explicit Stay or Move for the current month. Inactive identities
continue generic migration. An explicit Stay is an override, not an absent decision.

`survival-v1` uses the current shared nutrition fraction and at most eight dated
personal feeding observations. A current fraction below 0.8 plus a poor receipt in
the same region at exactly the preceding tick can trigger departure. It first
chooses a remembered adequate adjacent exit, ranked by nutrition, recency, cost
and stable destination ID; otherwise it can choose an unfamiliar/forgotten exit
by cost/ID. If neither exists, it stays. Adequate current feeding or insufficient
consecutive evidence also produces Stay. There is no new cognition random stream.

Destination observations are usable for at most 12 months; stale/evicted knowledge
becomes Unknown. Unknown conditions do not prohibit exploration and are never
replaced with an invented food score. The policy cannot query WorldState, aggregate
forecasts, another mind or unvisited destination conditions. Local weather and
forage are supplied as facts, but this first policy bases its choice on feeding
experience. It does not claim optimal survival or calibrated psychology.

First observation begins after promotion/activation, without backfilled biography
or visits. Dormancy retains memory without recording missed months. Death removes
live mind state while retaining the deceased identity and recorded evidence.
`InspectMind` returns a detached memory array. Reading, comparing, explaining and
saving do not materialize people, append experiences or advance any history cursor.

## Monthly ordering and exact migration accounting

Boundary ownership/activation commands remain separate atomic Phase 1A transactions
at the completed input tick. Their returned state becomes Step's input. A failed
Step preserves that input, including any already committed boundary commands.

Every region completes shared feeding, deaths, maturation, adult aging and births
before any mind observes. Receipts describe the original food allocation and
debit nothing. Dead actors cannot observe or decide. Observations are built from
the staged post-demography, pre-migration frame, not the previous saved month.
All active survivors decide against that same phase in PersonId order.

Validate the intent's current tick/DecisionId, living active actor, source cohort,
choice, destination and legal outgoing edge/travel limit. Stale, future, already
resolved, dead/inactive or illegal requests reserve nobody. Conflicting duplicate
actor sets are rejected as a set. A live person without an accepted override remains
eligible for generic migration. There is no public asynchronous intent queue.

For frozen combined counts C, accepted Stay and Move actors form R. Generic
eligibility is G=C-R, including exact juvenile-bin subtraction. Generic scores and
forecast demand still use full C: a reserved person does not stop needing food.
Generic rounding/age allocation uses G, and generic named-candidate groups exclude
reserved IDs. Explicit moves add one source/destination delta; stays add none.
The engine commits C plus both kinds of delta, then derives anonymous ownership
by subtracting final living identities. It never debits an anonymous person for
an identified person's move.

Sources and candidate groups are frozen before movement. Opposing flows, multiple
arrivals and chains commit together; arrivals cannot move again. Juvenile deadlines
and provenance travel unchanged. Due juveniles already matured or died before
deciding, and no decision grants new adults same-month reproduction. Empty R takes
the original Phase 1A path with the same arithmetic, sampling keys and ordering.

## Recorded demonstration

`cognition-demo` produces and verifies paired three-month archives through normal
simulation. The controlled fixture has one species/variant, demand 1/person,
20 initial adults in A, B initially empty, and one legal cost-1 exit each way.
Generic migration and reproduction are disabled; mortality pressures are disabled
globally for this experiment. This is a test scenario, not a retuned baseline or
personal immunity. Promote P1 from A's existing anonymous population and activate it.

The real finite forage model caps A at 10 rations each month, producing nutrition
**0.50 in both ticks 1 and 2**. The plan's 0.50/0.40 values were illustrative;
the implementation reports the actual inputs rather than manufacturing 0.40.

| Tick-2 phase | Recorded fact/outcome |
|---|---|
| Feeding | Demand 20, 10 rations consumed in A, remaining forage 0. P1 is included once. B consumes 0. |
| Shared demography | Completes before observation. World population remains 20; P1 survives as an adult. |
| Knowledge | Current receipt `(2,A,0.50)` and prior memory `(1,A,0.50)`; a legal exit to B at cost 1. B's resources, population, weather and safety are unknown to P1. |
| Decision | Move(B), DecisionId `(P1,2)`, reason `explore-unfamiliar-exit`. No observation of B is fabricated. |
| Engine resolution | Accept; reserve P1 once. A has C=20, R=1, G=19. Generic movement is zero; explicit delta A -1, B +1. |
| Commit | A owns 19 anonymous people; B owns P1 and zero anonymous people. World=20; one crossing, one location/person. Identity migration event sequence 2 links to cognition record 2. |
| Next experience | No feeding at arrival. Tick 3 supplies the first receipt from B (nutrition 1), while its earlier unknown conditions remain unrecorded. |

ObserveOnly's paired tick-2 result is A=20/B=0, with identical current-month food
and demographic totals. The explicit accepted intent explains the changed location;
later resource consequences follow from that location. P1's adult birth date stays
unknown. A separate regression holds an explicit Stay while the generic control
would otherwise move every member of its source pool.

## Reproducibility and compatibility

Cognition settings and living/dormant minds are persisted outside Scenario, together
with a separate cognition event count/hash. Existing biological RNG identifiers,
equations and generic identity event schemas are preserved. Full save hashes include
minds; biological/resource projections deliberately exclude representation/cognition.
The policy's constants are persisted configuration; algorithm changes require
a new policy/engine compatibility boundary.

Snapshots are only published at completed monthly boundaries. There are no pending
intents, reservations or partial observations to replay on load. Repeated queries
cannot regenerate an already recorded tick. The input snapshot is never modified.
Schema 4/engine 0.4.0 and older saves reject explicitly before checksum decoding.
Use `9bd958f` for Phase 1A saves; no silent converter or fabricated memories exist.
New worlds can reuse existing scenario files and seeds with an explicitly selected
cognition mode. Exact cross-runtime floating-point portability is not claimed.

## Verification evidence

Locked restore and Release build passed with zero warnings/errors. The final
executed full suite passed **110 tests**, zero failures/skips, in about 50 seconds.
**53 CLI checks passed:** 17 existing simulation checks, 18 identity checks and
18 cognition checks. Final coverage includes a fully reserved pool which would
otherwise all migrate, removal of previously observed minds on death, and a full
meal with zero remaining forage being interpreted as adequate feeding.

The isolated control harness links separately against pinned `9bd958f` and Phase 1B.
It compares every monthly `BiologicalHash(TickResult)` and identity event hash,
plus final full identity state. The protocol is seeds 1..8; seven baseline
identified/active configurations over 100 years; frontier 64/16 over 100 years;
24-month scarcity and empty-world cases. It also promotes at tick 58, deactivates
at 73 and reactivates at 87 when the relevant people remain alive.

Each of Reference, Off and ObserveOnly produced **77,184 monthly rows plus 80 final
identity rows**. All three files matched exactly:
`06002AA74CC52770797A9CF28B708D072B2A22F422DF747C4027D1D85E3D4D39`.
This checks biological/resource outcomes and identity history, not merely expected
population means or the final count. Raw rows and harness builds remain under
Git-ignored `artifacts/phase1b/`; compact protocols/digests are retained for review.

New regressions cover the worked Move and explicit Stay; mixed/all named and generic
eligibility; simultaneous opposing/chained moves; deadline-t maturation and later
juvenile migration; death before deciding; bounded/expired/evicted memory; exit ties;
dormant gaps; hidden-destination noninterference; stale/dead/replayed/duplicate intents;
late candidate failure preserving input; read-only query detachment; mode/schema
corruption; policy replay and event tampering; and profile settings without changes
to truth. Active save/resume comparisons split at ticks 1,2,5,12,24 and compare full
state plus biological, identity and cognition records at every subsequent tick.
The accepted long-run biological fingerprint remains a regression.

The maintained 256-identity/64-active Decide run's year-900 checkpoint resumed
for 100 years with its original boundary replenishment workload. Its full state,
biological projection and all three event histories match the uninterrupted run.
The concatenated original prefix and resumed records also match every complete
history file's digest, including boundary promotion events. Final state hash:
`D6D7825DC5A344CC1D7C9E55A16CFE595F5EF12BFD2E6BD6570698AC6C7A5E53`.
This continuation has real active minds and recorded memories, not just tombstones.

## Performance and retained history

| Maintained identified/active target | Mode | Median seconds (min..max) | Matching Phase 1A median s |
|---|---|---:|---:|
| 0 / 0 | Off | 11.608 (11.517..17.063) | 12.248 |
| 64 / 0 | Off | 20.606 (20.054..23.646) | 21.589 |
| 256 / 0 | Off | 37.645 (36.203..38.004) | 38.401 |
| 64 / 1 | Off | 21.639 (20.119..22.390) | 22.560 |
| 64 / 1 | ObserveOnly | 21.966 (21.638..32.953) | 22.560 |
| 64 / 1 | Decide | 23.750 (23.260..32.431) | 22.560 |
| 256 / 64 | Off | 37.259 (36.769..48.404) | 39.390 |
| 256 / 64 | ObserveOnly | 62.290 (59.964..85.305) | 39.390 |
| 256 / 64 | Decide | 70.026 (68.862..91.044) | 39.390 |

All **42 archives verified** (27 Phase 1B and 15 matching reference runs). Every
Off/ObserveOnly case matched its reference biological projection, biological and
identity event chains, population/food metrics and annual summaries. Full state
and event histories also matched across all three repetitions within every
configuration. **No Off median triggered the +10% review threshold**. Off ratios
to reference range from 0.946 to 0.980; the substantial timing ranges mean these
small differences are not a reliable speedup claim.

| Configuration | Median peak working set B | Final snapshot B | Mind component B | Deceased component B |
|---|---:|---:|---:|---:|
| 0 / 0 Off | 69,242,880 | 125,239 | 2 | 2 |
| 64 / 0 Off | 123,912,192 | 1,149,075 | 2 | 1,011,681 |
| 256 / 0 Off | 212,631,552 | 4,162,476 | 2 | 3,988,041 |
| 64 / 1 Off | 118,042,624 | 1,149,074 | 2 | 1,011,681 |
| 64 / 1 ObserveOnly | 110,952,448 | 1,149,652 | 576 | 1,011,681 |
| 64 / 1 Decide | 101,138,432 | 1,120,387 | 488 | 982,503 |
| 256 / 64 Off | 210,644,992 | 4,162,416 | 2 | 3,988,041 |
| 256 / 64 ObserveOnly | 210,468,864 | 4,192,584 | 30,165 | 3,988,041 |
| 256 / 64 Decide | 255,836,160 | 4,051,188 | 30,855 | 3,845,356 |

The empty array's independent serialized size is 2 bytes. Snapshot and event sizes
are identical across repetitions; working-set values are medians. Current runtime
is .NET 10.0.1, pinned SDK 10.0.101, Windows x64 build 26200, Ryzen 5 7520U with
8 logical processors. No other agent-run simulation or test jobs overlapped the
timed matrix. First-repetition timings vary substantially from later observations;
all raw repetitions are published rather than selecting the fastest.

| Active cognition workload | Cognition records | Cognition file B | Final living / active / deceased | Final world population |
|---|---:|---:|---|---:|
| 64 / 1 ObserveOnly | 11,611 | 12,765,433 | 58 / 1 / 4,886 | 11,883 |
| 64 / 1 Decide | 11,775 | 18,104,955 | 58 / 1 / 4,745 | 11,849 |
| 256 / 64 ObserveOnly | 744,328 | 820,074,122 | 234 / 60 / 19,206 | 11,883 |
| 256 / 64 Decide | 751,328 | 1,156,695,967 | 237 / 61 / 18,523 | 11,833 |

Off writes no cognition records. Maintained 256-person worlds averaged 249.892
living identities under Off/ObserveOnly and 250.327 under Decide; active survivor
counts are lower than target between annual refills. ObserveOnly adds about
25.03 median seconds versus its matched 256/64 Off workload with identical
population and deceased history. Decide changes the history itself; its roughly
32.77-second difference is not wholly attributable to policy computation. These
single-seed end-to-end results do not establish a general fitness advantage.

The major storage cost is **recorded evidence**, not live memory: 1.16 GB of raw
cognition records versus 30,855 B of live mind data in the 64-active Decide case.
Each record retains bounded prior facts for replay/explanation; no compression,
pruning or new archival/indexing system is implemented. Existing deceased identity
storage remains separately visible at about 3.85 MB in that case.

The approved matrix uses fresh serial Release processes, seed 847192, 1,000 years,
full monthly validation, annual metrics, 25-year snapshots and three repetitions.
It includes Off at 0/0,64/0,256/0 identified/active, and Off/ObserveOnly/Decide at
64/1 and 256/64, plus matching Phase 1A baselines. Target occupancy is replenished
by explicit logged commands every 12 months; births never auto-materialize.
Do not infer steady occupancy from a fixed initial set whose people later die.

Reports separate observation/policy, migration/intent integration, validation,
identity commit, cognition evidence serialization/hashing and cognition file writes.
The cognition bucket also includes reservation preparation; the migration bucket
includes forecasts, movement, flow/annual accounting and the biological event chain.
These timing buckets do not exhaust elapsed time: other archive writes, snapshots
and boundary commands remain in the residual. Timing never influences simulation.
Initial snapshot/promotion preparation and final report serialization are outside
the stopwatch; ongoing commands, event writes and snapshots are inside.

Retained managed bytes are measured after the run; profile-enabled runs request a
collection outside the stopwatch. Allocated bytes describe the run interval, while
peak process working set includes startup. These measures are distinct and are
not additive. Serialized component sizes are independent encodings of anonymous
cohorts, living/deceased identities, mind memory/configuration and cognition cursors;
they do not sum exactly to the framed snapshot with scenario and envelope metadata.

Cognition event bytes/counts are separate from biological and identity history.
Cursors are cumulative after resume; file bytes are segment-local, so subtract
the initial count for a continuation's bytes/processed-survivor-month calculation.
In fresh benchmark worlds the initial count is zero. Runtime mind memory remains
bounded; cognition evidence and deceased identity history both grow over time.

Matched-input profiles repeat 32 complete Steps from the same validated input,
with Off and ObserveOnly using identical people, resources and retained identities.
The zero-history case has no warm-up. Other cases simulate eight ObserveOnly warm
months to obtain real bounded memory; these months can add deaths, which are shown
explicitly. No tombstones, checks or biological work are removed.

| Source tick / deceased | Measured tick / deceased / active | Off 32 Steps s | ObserveOnly 32 Steps s | Off validation s | ObserveOnly validation s |
|---|---|---:|---:|---:|---:|
| 0 / 0 | 1 / 0 / 64 | 0.1844 | 0.2016 | 0.0257 | 0.0244 |
| 3,000 / 5,036 | 3,009 / 5,043 / 60 | 0.1356 | 0.1913 | 0.0410 | 0.0381 |
| 12,000 / 19,206 | 12,009 / 19,213 / 58 | 0.1293 | 0.1996 | 0.0528 | 0.0573 |

Every pair preserved identical biological and full identity results and left its
source unchanged. Additional observation/reservation plus cognition-evidence
generation took about 0.047, 0.065 and 0.070 seconds over those 32 Steps; these
component profiles exclude archive IO. Population/cohort composition differs
between checkpoints, so comparisons across rows are not pure per-deceased latency
estimates. Micro timings and allocation counters also include managed runtime/JIT
effects; treat them as diagnostic observations, not precise marginal cost constants.

Off versus ObserveOnly comparisons have identical biology/identity histories and
thus isolate the extra observation/evidence work at matched history sizes. Decide
can change migration, survival, occupancy and future promotions; its total timing
is an end-to-end outcome, not a pure cognition-overhead estimate. A 10% Off-mode
median regression against the repeated matching reference is a review trigger,
not something to hide behind unchanged biological projections.

## Interfaces and limitations

| Interface | Contract |
|---|---|
| `Simulation.Create(..., IdentityOptions?, CognitionOptions?)` | Defaults to Off; cognition options live outside Scenario and are part of full saved state |
| `MindPolicy.Advance(DecisionObservation, MindState?, CognitionOptions)` | Pure policy/memory calculation; no world/forecast callback |
| `IntentResolver.Resolve(Scenario, tick, survivors, intents, priorMinds?)` | Returns accepted/rejected results; invalid requests reserve nobody; no delayed-action queue |
| `PopulationOwnership.InspectMind(WorldState, PersonId)` | Detached dated-memory view; unknown/deceased mind returns null |
| `Simulation.Step(..., profile: bool)` | Single private candidate transaction; optional timing is excluded from canonical state |
| `TickResult.CognitionEvents` / `State.Cognition` | One record per processed active survivor; persisted bounded memory and history cursor |
| `cognition-demo`, `cognition-history`, `inspect mind` | Actual headless decision evidence and read-only inspection |
| `cognition-profile` | Matched-input component profiling with actual simulated warm-up and no removal of tombstones |
| `continue-benchmark` | Reproduces a benchmark's logged replenishment boundary after a checkpoint; ordinary `run` never auto-promotes |

The policy is a small deterministic survival demonstrator, not general intelligence.
It can revisit forgotten places, remain in a known poor location or explore a
hostile unknown exit. Current weather does not yet affect its choice. Any explicitly
active surviving life stage can use the same policy; no new cognitive age model
is introduced. The demonstration selects an adult, while movement tests protect
juvenile biology. Personal feeding history does not change the existing location-
level biological hunger exposure or grant individual survival privileges.

Living representation and active budgets do not bound deceased identity storage
or accumulated event files. No history archival/indexing redesign, personal inventory,
family system, social learning, cities or other later feature is included. WorldState
arrays retain the earlier immutable-by-contract convention; use the provided
transactions and detached queries, not arbitrary mutations or concurrent commands.

## Reproduction

Use fresh output paths. Source reference worktree must be pinned at `9bd958f`.

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --no-restore --logger 'trx;LogFileName=phase1b.trx' --results-directory artifacts/new-phase1b-tests
git worktree add --detach artifacts/phase1b/reference 9bd958f
dotnet build artifacts/phase1b/reference/Genesis.slnx -c Release
./scripts/Compare-Phase1A.ps1 -OutputDirectory artifacts/new-phase1b-controls
./scripts/Smoke-Test-Cognition.ps1 -OutputDirectory artifacts/new-phase1b-cli
./scripts/Smoke-Test-Identity.ps1 -OutputDirectory artifacts/new-phase1b-identity-cli
./scripts/Benchmark-Cognition.ps1 -OutputDirectory artifacts/new-phase1b-benchmarks
./scripts/Smoke-Test.ps1 -RunDirectory artifacts/new-phase1b-benchmarks/anonymous-Off-r1
$g = 'src/Genesis.Cli/bin/Release/net10.0/Genesis.Cli.dll'
dotnet $g cognition-profile --warm-months 0 --out artifacts/new-profile-zero.json
dotnet $g cognition-profile --save artifacts/new-phase1b-benchmarks/minds64-Off-r1/snapshot-00003000.json --out artifacts/new-profile-history.json
dotnet $g continue-benchmark --run artifacts/new-phase1b-benchmarks/minds64-Decide-r1 --from-tick 10800 --out artifacts/new-phase1b-continuation
```

This handoff closes only Phase 1B implementation and its measured review evidence.
Subsequent feature development requires a separate scope and authorization.
