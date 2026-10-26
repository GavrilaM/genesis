# Phase 1A — Persistent Individual Identity and Population Ownership

Date: 2026-10-26 (Asia/Jakarta). Engine **0.4.0**, snapshot schema **4**,
scenario schema **1**. Branch: `codex/genesis-prototype-001`.

Verified implementation: [`1e38c28`](https://github.com/GavrilaM/genesis/commit/1e38c280cb5f1b59a832f4c7e86464fd0b2cdead).
Evidence: [test results](results/phase1a/test-results.json),
[benchmarks](results/phase1a/benchmarks.json),
[continuation](results/phase1a/resume-900-to-1000.json).

Phase 1A implements persistent passive identity and exclusive population ownership.
It does not implement Phase 1B: no minds execute, no personal goals or memories are
generated, and no individual chooses movement. `Active` reserves a future cognition
slot only. The user-approved architecture and refinements govern this boundary.

## Ownership and the shared transaction

For every cohort/deadline bucket:

`combined population = anonymous count + living identified members`

Persisted `WorldState.Cohorts` contain anonymous people. `Identity.Living` contains
exclusive owners of single people; `Identity.Deceased` contains zero-population
tombstones. Juvenile bins partition the anonymous juvenile count. Derived combined
arrays add identified juveniles at their existing deadline. They are temporary
inputs, never an extra saved authority.

The engine computes environment, food, survival, stage changes, births and migration
once from the combined view. It retains Phase 0.6's biological random streams and
equations. Each already-sampled death/stage/migration total is conditionally assigned
to identified members using a separate identity stream. Ordered identified members
are selected with probability remaining selected / remaining population. The
unassigned remainder belongs to anonymous people; there is no per-anonymous-person
loop. Frozen pre-aging groups exclude newly matured people from adult aging in the
same month. Frozen migration groups exclude arrivals from moving again.

Juvenile death assignment is within each deadline bin. Juvenile migration uses the
existing aggregate per-bin allocation and conditionally chooses identified movers
inside it. The integer age deadlines and total migration remain exact. All births
enter anonymous newborn bins, including when all identity slots are occupied.
No parent/family assignment is implied by aggregate reproduction.

Food demand and consumption use the combined population exactly once, through the
existing regional forage ledger. There is no personal stock, second food allocation
or individual mortality model. Migrants consume at their origin in that month;
newborns consume starting the following month, as before.

## Identity commands and lifecycle

`IdentityOptions` defaults to 256 living identities and 64 active reservations.
Limits are configurable when creating the world; supported identity limit is
0..10,000 and the active limit is 0..identity limit. They constrain representation,
never births, population size, food or anonymous demography. Active and passive
people receive identical biology and generic migration.

`PopulationOwnership.Apply` accepts 1..10,000 commands as an atomic batch. Every
command must have the current completed tick and exactly the next sequence number.
The service stages ownership changes against copied cohort bins and a new living
list, validates invariants and exact biological projection equality, then returns
the candidate state. Failure returns no candidate, consumes no IDs/sequences and
does not change the input. The caller installs the returned state only on success.
There are no asynchronous command queues or hidden mutable RNG cursors.

- `promote`: requires a nonempty anonymous source cohort. Juveniles require the
  exact existing absolute deadline. It subtracts one anonymous member, allocates a
  monotonic PersonId and retains that person's stage, variant, location and deadline.
- `set-active`: reserves/releases future cognition capacity for an existing living
  person. Deactivation does not return the person to anonymous ownership. Repeating
  an old sequence rejects; a new-sequence no-op reservation is an accepted logged command.
- No dematerialization command exists. On death the person leaves living population,
  loses its active reservation and remains resolvable as a deceased identity. The ID
  is never reused. A later promotion may use the freed representation capacity.

Initial juvenile birth dates derived from initial age bins are explicitly marked
`InitialCondition`, not recovered personal history. Juveniles born after tick zero
have `SimulatedBirth` provenance derived from their preserved deadline. Promoted
adults/elders have unknown birth and maturation dates. Promotion cannot supply a
fictional historical age. Known juvenile dates survive maturation and migration.
No biographies, genealogy, education, personal memories or past perceptions exist.

## Read-only APIs and historical evidence

`InspectPerson` resolves a living or deceased immutable record, or null. `ObserveLocal`
returns a detached value snapshot for a living identity: current tick, local region,
weather, forage, total population and traversable adjacent exits. Exits expose only
destination IDs and travel costs. There is no neighbor-resource/forecast query in
that observation contract. Inspection, observation, promotion planning and projection
comparison neither materialize people nor advance time, IDs, commands, history or RNG.

Identity events have their own sequence/hash chain: promotion, reservation changes,
death, maturation, elder transition and migration. Biological event IDs and hashes
are unchanged by passive detail. Events record actual transitions and command
sequences; they do not explain fictitious thoughts or independently assign causes
to random death. Biological audit/food records remain the source for cohort hazards.

Run archives include `person-events.jsonl`, `identity-commands.json` and identity
occupancy/history sizes in `report.json`. Identity-only archives retain input/output
snapshots, commands and events and verify by replay. Simulation archive verification
checks both event chains, final state/projection, metrics, summaries and command
checksums. This detects corruption, not malicious rewriting of all hashes.
Histories are segment-local: a resumed save preserves cursors and tombstones but
earlier per-event detail still requires earlier archives.

## Compatibility and exact equivalence

Snapshots are now schema 4 / engine 0.4.0 because the meaning of persisted cohort
counts and the identity state changed. Older snapshots reject clearly before
checksum decoding; there is no silent migration. Use `d408813` for accepted 0.3.0
saves, `855c478` for 0.2.0, or `b0f8d4e` for 0.1.0. Existing scenario files remain
valid and new worlds begin entirely anonymous unless explicitly promoted.

`PopulationOwnership.BiologicalHash(WorldState)` hashes the seed/clock, biological
RNG identifier, scenario, environment/food, combined cohorts/deadlines, hunger memory,
annual windows and biological event cursor/hash. It excludes identity representation,
budgets and engine/schema labels. The TickResult overload also includes all monthly
population/food flows, transfers, biological events and annual summaries. Audit
capture settings are deliberately excluded. Full save hashes include representation.

The no-identity 1,000-year test folds state back into the accepted 0.3.0 serialization
shape and retains its exact state fingerprint
`48AB41B6FD5C6CBA6A1D357A0ECFAD4AFAFF7E85A48ACED739C5A86D92D1600B` and biological event hash
`E8857F1E5A128007EE3A4626833FA4043D44F519099639AF7429C4B166F715C4`.
This is an explicit equivalence regression, not permission to load an incompatible save.
Exact determinism is claimed on the tested Windows x64/.NET runtime; cross-runtime
floating-point portability remains outside the validated contract.

## Verification

Locked restore and Release build passed with zero warnings/errors. The complete
suite passed **84 tests, zero failures/skips**. The additional duplicate live/deceased
ownership assertion and all 23 identity tests were also run on the final source.
**18 identity CLI checks and 17 existing CLI checks** passed.
The existing smoke script now returns success explicitly after its intentionally
failing unknown-option check, so a successful suite does not leak that negative
test's process exit code into a calling verification script.

Evidence covers:

- Exact monthly projections for four baseline seeds over 120 months, with 256
  passive/reserved identities, audit capture, and replenishment at month 58.
- Sixteen seeds each under binomial and rounding event controls, all 120 original
  adults identified, with anonymous births and shared deaths over 36 months.
- Sixteen all-identified starvation/extinction worlds, empty continuation, a full
  256-person budget with ongoing anonymous births, and a zero-identity budget.
- Atomic failed batches, repeated/stale/out-of-order commands, double reservations,
  budget failures, unknown IDs, deactivation, retained deaths and allocator reuse rejection.
- Juvenile dates before/after tick zero, ring wrap, maturation without same-step
  reproduction/double aging, and death on the maturation tick before aging.
- All 80 migrants identified, one-hop movement, exact age preservation, no remigration.
- Read-only observation/planning/projection and detached-array mutation checks.
- Malformed ownership, lost/duplicate IDs and invalid dates; old snapshot rejection.
- Exact mid-year save/load plus subsequent commands, and sustained-identity archive
  continuation with the same commands at the resume boundary.
- Conditional identified mortality marginal across 2,048 seeds against its analytic
  Bernoulli probability; six-standard-error tolerance. Aggregate variance tests from
  Phase 0.6 still run unchanged.
- Command archive replay, rejected commands publishing no output, identity event
  tampering, biological ledger/summary corruption, and all previous ecological tests.

Raw TRX files and run archives remain in Git-ignored `artifacts/phase1a/`.
Compact measured reports and verification counters are retained in `docs/results/phase1a/`.

The year-900 snapshot of the initial-256 run resumed for 100 years in 3.328 seconds.
Its full state, biological projection and both event-chain hashes match the fresh
1,000-year result exactly. All 256 original identities are deceased by that boundary;
this checks retention and history cursors. The separate sustained-detail continuation
test resumes with living identities and repeated boundary promotions.

## Performance evidence

| Configuration | Seconds / 1,000 years | Mean living identities | Peak working set B | Final snapshot B |
|---|---:|---:|---:|---:|
| No identities | 17.601 | 0 | 67,538,944 | 125,015 |
| Initial 64, passive | 18.088 | 0.182 | 70,291,456 | 137,724 |
| Initial 256, passive | 19.389 | 1.981 | 74,035,200 | 176,110 |
| Maintain 64, passive | 31.263 | 62.447 | 100,929,536 | 1,148,851 |
| Maintain 256, passive | 53.561 | 249.892 | 228,294,656 | 4,162,252 |
| Maintain 256, reserve 64 active slots | 53.236 | 249.892 | 203,767,808 | 4,162,192 |

The measured runtime is .NET 10.0.1 / pinned SDK 10.0.101, Windows x64 build 26200,
Ryzen 5 7520U, 8 logical processors. Final cases ran serially without other test
or simulation jobs. No dependencies, services or graphics were added.

Each case is a fresh Release process, seed 847192, 12 regions / 432 cohorts,
12,000 monthly steps, full input/output validation each step, annual CSV/summaries,
event writing, and snapshots every 25 years. Initial promotion planning/application
and the initial snapshot are outside the stopwatch; later benchmark replenishment,
history writes and snapshots are inside. Report serialization is outside. Timing
observations are end-to-end measurements, not statistical microbenchmarks.

The maintained cases issue explicit recorded commands every 12 months to restore
the target population of identities; deaths between boundaries reduce occupancy.
They retain all deceased identities. This is a benchmark workload, not gameplay
materialization or a population intervention. The active-reserved case runs no AI.

All six configurations match final biological/resource projections, sampled
population/food files, annual summary files and the full biological event chain.
Initial-only groups eventually die, making their long-run cost unrepresentative of
sustained detail. The maintained configurations expose the cost of lifetime history,
validation, event output and population representation.

Every case ends at **11,883 = 38,400 + 472,263 births - 498,780 deaths**, with
2,818,689 migration crossings, 81 humans and 11,802 orcs. The common biological
projection hash is `68CD46ADC18DB58994A2291BDDBB41F56E4B248FA145F4134B88C74E2FD41105`.
No species parameters or ecological mechanics were retuned.

Sustained 256-person detail costs about **3.04x** the anonymous benchmark. It ends
with 234 living and **19,206 deceased identities**, from 19,440 explicit promotions.
Its 111,502 identity events occupy 31,559,094 bytes, in addition to the unchanged
biological history. Sustained 64 ends with 58 living/4,886 deceased, a 1.15 MB snapshot
and 8.09 MB of identity events. A 256 living-person budget does not bound history.

An initial measurement exposed needless history-sized hash-set allocations and
re-sorting unchanged tombstones. Validation now checks sorted living/deceased ID
lists without that hash set; commits reuse unchanged deceased arrays or merge sorted
new deaths. Full record validation remains enabled. All six full state hashes and
both event-chain hashes match their pre-optimization counterparts exactly. Initial
timings are retained as diagnostic evidence; a CLI check overlapped part of that
initial batch, so use the repeated final timings above for comparisons.

## Precise interfaces for Phase 1B

| Existing interface | Contract Phase 1B must preserve |
|---|---|
| `Simulation.Create(Scenario, ulong, IdentityOptions?)` | Creates canonical anonymous populations; options reserve representation budgets only |
| `PopulationOwnership.PlanPromotions(WorldState, int count, int active = 0)` | Pure deterministic command plan; no ownership change until explicitly applied |
| `PopulationOwnership.Apply(WorldState, IReadOnlyList<IdentityCommand>) -> IdentityTransaction` | Atomic ownership/reservation command transaction; commit returned State and archive Events together |
| `IdentityCommand(Sequence, Tick, Type, Source?, JuvenileDeadline?, Person?, Active)` | Exact current boundary, consecutive sequence, promote or set-active only |
| `PopulationOwnership.InspectPerson(WorldState, PersonId)` | Read-only living/deceased identity lookup; unknown history stays unknown |
| `PopulationOwnership.ObserveLocal(WorldState, PersonId) -> LocalObservation` | Detached local facts plus exits; no neighboring stocks, future RNG or global forecast |
| `Simulation.Step(WorldState, bool validate = true, bool audit = false) -> TickResult` | One authoritative shared biological/resource transition; identities receive conditional outcomes |
| `TickResult.PersonEvents`, `State.Identity` | Record actual lifecycle transitions; never count identity events as births/deaths of additional people |
| `PopulationOwnership.CombinedCohorts(WorldState)` / `BiologicalHash(...)` | Derived reporting/test views, never authoritative writable populations |
| `Persistence.Save/Load` | Validated, versioned snapshot with allocators, budgets, lifecycle, pending accounting and both history cursors |

Phase 1B's decision code should receive `LocalObservation` and its own bounded
recorded knowledge, not WorldState, TickResult or the aggregate forecasting service.
An unfamiliar exit is a legal exploration option; its resources/weather remain
unknown until observed. Unknown conditions must not be fabricated as safe, unsafe
or unavailable. Only the engine boundary resolves legal topology and commits moves.
No decision interface or policy implementation is delivered in Phase 1A.

A future intent API must reserve each identified stay/move exactly once, remove
explicit overrides from generic migration, preserve shared demand and deadlines,
and commit incoming/outgoing changes together. It must reject stale/dead identity
intents. It cannot mutate Cohorts, Living or forage directly. Nutrition receipts,
if added, describe the existing food debit and do not create another entitlement.
Passive/no-override regression projections remain mandatory after that extension;
active behavior is tested separately for intentional causal changes.

## Limitations and stop boundary

- Identity retention bounds living detail, not historical storage. Deceased records
  and event archives grow as more people are explicitly promoted. Full snapshot
  validation visits historical records, so cost depends on accumulated identities
  as well as current living identities. Archival/indexing changes need a measured,
  reviewed follow-up; this milestone does not silently discard history.
- Identity assignment is exchangeable within existing biological buckets. No
  personal hazards, special immunity, sex model, family model or personal inventory
  exists. Adult/elder timing and mutation/migration rounding retain Phase 0.6 limits.
- Identities require monthly juvenile bins. Geometric/annual research scenarios
  still simulate anonymously but reject identity promotion.
- Local observation is a read-only current snapshot, not personal memory. It does
  not infer unrecorded past nutrition, biography, relationships or experiences.
- Public C# model records contain arrays as in earlier phases; callers must treat
  WorldState as immutable and use ownership/step transactions. Query methods return
  detached arrays/value records. There is no concurrent command execution model.

Phase 1A ends with verified source, tests, evidence and these interfaces. **Do not
implement Phase 1B until separately authorized.**

## Reproduction

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --no-restore --logger 'trx;LogFileName=phase1a.trx' --results-directory artifacts/new-identity-tests
./scripts/Smoke-Test-Identity.ps1 -OutputDirectory artifacts/new-identity-smoke
./scripts/Benchmark-Identity.ps1 -OutputDirectory artifacts/new-identity-benchmarks
./scripts/Smoke-Test.ps1 -RunDirectory artifacts/new-identity-benchmarks/anonymous
```

Use fresh output directories. `README.md` documents promotion, inspection, explicit
command JSON, segment-local history, verification and continuation commands.
