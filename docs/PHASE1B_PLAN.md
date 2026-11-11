# Phase 1B — Local observation, dated memory and one movement decision

**Status: approved for execution by the user's "execute" instruction.** Prepared 2026-11-11
(Asia/Jakarta). Base: [`9bd958f`](https://github.com/GavrilaM/genesis/commit/9bd958ff6b4b213ad0f0a09c1be3e0e270ed277a).
This was the review deliverable; execution is now authorized. The [Phase 1A handoff](PHASE1A_HANDOFF.md)
and [approved architecture](PHASE1_ARCHITECTURE_PROPOSAL.md) govern ownership,
shared biology and the limits of individual knowledge. Values and examples below
are proposed specifications, not results of an implemented or executed Phase 1B.

## 1. Smallest demonstrable behavior

One explicitly promoted, surviving active person in a headless two-region fixture
records its current local feeding experience, remembers a bounded set of dated
experiences, and submits either **Stay** or **Move to an adjacent region**. The
engine resolves the request in the same month's single migration transaction.
The demonstration must explain a changed location from recorded inputs and an
accepted intent; it need not produce improved survival or a particular story.

Keep Phase 1A's exclusive ownership, finite regional food ledger, shared birth/death
sampling, anonymous births and retention of identities until death. Default budgets
remain 256 living identities and 64 active minds. There is no inventory, personal
mortality model, family generation, social behavior, learning algorithm or LLM.
No graphics or other civilization systems are included.

Propose three explicit cognition modes, configured and persisted outside the
biological Scenario so its existing hash and defaults remain intact:

| Mode | Active survivor processing | Migration participation |
|---|---|---|
| Off, the default | No cognition work | Existing Phase 1A generic migration |
| ObserveOnly | Record the current observation and update bounded memory; issue no intent | Existing generic migration |
| Decide | Observe, update memory and issue one Stay or Move | Accepted intent replaces generic eligibility for that person |

Inactive identified people remain generic migrants in every mode. **No override is
different from an explicit Stay.** ObserveOnly is the primary control for proving
that cognition without behavioral overrides preserves Phase 1A biology exactly.

## 2. Observation and memory contract

The current `PopulationOwnership.ObserveLocal` reads a completed WorldState. The
decision inside a tick needs a new internal observation builder using the fully
staged **post-demography, pre-migration** frame. Do not call the existing query on
the input save and label its previous month's data as current. Do not publish an
incomplete WorldState to obtain an observation.

The decision input is a detached value containing PersonId, executing tick and
phase, current source cohort/region, current local weather/forage/population and
traversable exits containing only destination IDs and travel costs. Add a read-only
feeding receipt: allocation tick, allocation region and the nutrition fraction
already calculated by shared feeding. It describes this person's existing
allocation; computing or reading it debits no food. End-of-feeding forage alone is
not a measure of whether the person ate adequately. Receipts for survivors refer
to their start-of-month allocation, even if their life stage subsequently changed.

The policy receives this value and its own memory, never WorldState, TickResult,
other minds, regional forecasts, future random draws or a destination-state query.
Engine validation may inspect topology and biological state; the policy may not.

Proposed memory is at most **8 direct observation records per living mind**:
`(observedTick, region, nutritionFraction)`, with an implicit owning PersonId.
These are records of feeding experiences, not estimates of current destination
conditions. Keep the newest eight, evicting the oldest tick deterministically.
Use a remembered destination only when its observation is at most **12 months**
old; expired or evicted knowledge is Unknown. Never convert Unknown into a zero
resource score, a safety claim or an inaccessible edge.

Promotion/first activation starts with empty memory. Only the simulation's
observation phase can append records, once per surviving active person per tick.
Inspection, explanation and save/load append nothing and materialize nobody.
Deactivation retains bounded memory but records no dormant experiences; on
reactivation, elapsed time still expires old information. Existing adult ages stay
unknown, and no pre-promotion visits or memories are inferred from location or age.
Remove runtime mind state on death; retain the deceased identity and recorded
cognition events in history. Do not add a full mind archive to every tombstone.

## 3. Proposed deterministic policy: `survival-v1`

Use a fixed, persisted policy version and the following reviewable rules. The
thresholds are engineering choices for this demonstration, not calibrated biology.

1. If the current nutrition fraction is at least **0.8**, choose Stay
   (`adequate-current-feeding`). Otherwise require a direct observation at exactly
   tick t-1 in the same region with nutrition below 0.8. Without it, choose Stay
   (`insufficient-consecutive-local-evidence`). An old observation or a dormant
   gap cannot substitute for last month.
2. With two consecutive poor local feeding experiences, consider traversable
   adjacent exits only. If any has a retained, unexpired feeding observation of at
   least 0.8, choose the best remembered nutrition, then most recent observation,
   then lowest travel cost, then lowest destination ID (`remembered-adequate-exit`).
   The event must describe that evidence as dated, not as a forecast or guarantee.
3. Otherwise choose an exit with Unknown conditions, lowest cost then lowest ID
   (`explore-unfamiliar-exit`). An unfamiliar or forgotten region is legal to
   explore without first proving that it contains food.
4. If no such exit exists, choose Stay (`no-promising-or-unfamiliar-exit`). This
   also handles isolation or travel costs beyond the species limit.

Use the latest retained observation for each destination. Exits with known poor
feeding are not silently treated as unknown. Sorting and tie rules are explicit;
no new randomness or global optimization is needed. The policy's known limitations
include cost/ID preferences, short memory and potentially unsuccessful exploration.
Do not tune it to guarantee desirable population outcomes.

## 4. One monthly tick and migration ownership

For input `CurrentTick = t-1`, the entire candidate for tick t remains private
until validation and commit. All regions finish each shared phase before any mind
decides; do not run a mind inside the region-by-region demographic loop.
Boundary commands retain Phase 1A's separate `PopulationOwnership.Apply`
transaction at completed tick t-1; its returned state is the input to Step.
A failed Step preserves that post-command input, not a state preceding an already
committed promotion. It neither replays nor silently rolls back boundary commands.

| Phase | Authoritative work and boundary |
|---|---|
| 0. Prepare input | Apply promotions and activation changes in the separate atomic boundary transaction using the existing command sequence, then pass its result to Step. A new mind has no invented prior observations. |
| 1. Shared feeding | Combine anonymous and identified people, update environment/regrowth and allocate finite food once using start-of-month demand. Capture descriptive feeding receipts. |
| 2. Shared demography | Complete deaths, maturation, adult stage exits, births and their identity assignments everywhere using unchanged biological streams. Newborns remain anonymous. Remove dead people's live mind state and any eligibility to decide. |
| 3. Survivor decisions | Build the post-demography observation for each active survivor in PersonId order. Stage one bounded memory update and, in Decide mode, one intent. All minds see the same phase; none sees another's pending intent. |
| 4. Freeze and reserve | Freeze full post-demography combined counts/deadlines and forecast demand. Validate all intents against this frame. Reserve each accepted actor once, for either Stay or Move, and exclude it from generic migration. |
| 5. Plan movement | Compute generic plans from the remaining eligible pool. Compute explicit single-person move deltas separately. No arrival enters either source pool this tick. |
| 6. Commit | Apply all movement deltas together, finalize identified locations, derive anonymous remainders, update exact monthly/annual flows and histories, validate and publish CurrentTick t. Any failed candidate leaves the input unchanged. |

### Exact subtraction rule

For each frozen combined cohort count C, let R count **all accepted Stay and Move
actors** in that cohort. For juvenile bins, retain corresponding per-deadline R_b.
Set generic eligibility G = C - R and G_b = C_b - R_b. These are eligibility views,
not another population authority and not a permanent removal of people.

- Compute generic scores/fractions from **full C and full shared demand**, including
  reserved people. Do not make their food needs disappear or include anticipated
  arrivals in this month's forecast.
- Round generic mover totals using G with the existing biological migration keys.
  Allocate juvenile generic movers across G_b with the existing allocation policy.
  Conditional identity selection uses only non-reserved identified candidates and
  the same corresponding eligible totals. Freeze its groups before any movement.
- An accepted Move supplies exactly one outward and one inward delta at its
  post-demography species/stage/variant/deadline. An accepted Stay supplies no
  movement delta and remains at origin. Both have already been excluded from G.
- Commit **C_next = C + delta_generic + delta_explicit**, not G plus those deltas.
  Then subtract each living identity at its final location to derive anonymous
  ownership. Do not also debit a persisted anonymous count for an identified move.

When R is empty, take the existing migration path with identical arithmetic order,
rounding keys, per-bin allocation and identity-assignment order. Do not independently
resample named and anonymous migration, or select from C and retry if a reserved
actor is chosen. Those approaches would change the Phase 1A control.

### Intent validity and failure handling

Propose `MovementIntent(PersonId, DecisionId=(PersonId,t), tick, sourceCohort,
Stay|Move, destination?)`. Validate that its actor is alive and active in the current
frame, source/stage/variant match, decision/observation tick is t and the decision
has not already resolved. Stay has no destination. Move must use one legal outgoing
edge within the species travel limit. An empty or unfamiliar destination is valid;
its forage is not an admission condition. There is no destination capacity auction.

Reject stale, future, already resolved, dead/inactive-person or illegal-edge intents
with an explicit reason and **no reservation**. A surviving actor with no accepted
intent remains eligible for generic migration; rejection is not an implicit Stay.
For competing/duplicate intents for one actor in a batch, reject the entire actor's
set before reserving, independently of enumeration order. The built-in policy must
never produce such a set; its occurrence fails the acceptance tests. A dead actor
gets no observation or memory update, even if an old request is rejected for it.

Generate requests after demography, so an ordinary current-tick policy request
cannot save somebody who already died. There is no public delayed-action queue in
this slice. The resolver still tests stale/dead inputs defensively. Reservations,
memory changes, decisions, movement and event cursors commit in one tick transaction;
an internal invariant failure aborts the entire candidate rather than partially
publishing a decision or re-running biology.

Opposing A-to-B/B-to-A moves and multiple arrivals use frozen origins and summed
deltas. There is no first-mover resource claim, arrival feeding or arrival remigration.
A juvenile due after t carries its exact absolute maturation deadline and birth
provenance through the move. One due at t has already matured (or died) before
deciding, so reservation uses its resulting stage. Movement cannot reset a clock,
resurrect a juvenile or grant same-month reproduction to a newly matured adult.

## 5. Worked specification: one unfamiliar exit

This is an illustrative expected trace to realize and verify in a controlled
fixture after approval, not a measured run. Create A with 20 people and B empty;
promote one existing adult as P1 and activate it. A then owns 19 anonymous people
plus P1. Its historical age remains unknown. A has one traversable exit to B,
cost 1. Use one species/variant with food demand 1 ration per person per month.
Configure generic migration and reproduction off, no mortality pressure,
and verify that the chosen seed's staged demographic outcomes leave these counts
and P1's adult stage unchanged during the demonstration.
Here the generic migration rate is a propensity, not an edge-access prohibition;
the species travel limit still permits the cost-1 explicit move.

At tick 1, P1 receives nutrition 0.50 in A. Its first observation creates only
`(1,A,0.50)`. With no tick-0 personal observation it explicitly stays. B remains
unobserved. The fixture must produce the stated feeding inputs through the finite
food model; if exact values require different inputs, publish the actual values
and preserve the two-below-0.8 condition rather than falsifying the trace.

At tick 2:

| Step | What exists or happens |
|---|---|
| Feeding | A has 8 available rations for demand 20: nutrition = 0.40, consumption = 8 and remaining forage = 0. P1 was included in that single demand/allocation. B has no consumers. |
| Demography | Shared survival/aging/birth processing finishes. P1 is a living adult in A; world population remains 20. |
| Knowledge | Current local observation: tick 2, A, forage 0, population 20, nutrition receipt 0.40, exit B/cost 1. Prior memory: tick 1, A, nutrition 0.50. B's current food, climate, population and safety are Unknown to P1. |
| Decision | Both consecutive local receipts are poor. There is no remembered adequate destination; B is unfamiliar and traversable. P1 requests Move(B), DecisionId=(P1,2), reason `explore-unfamiliar-exit`. Memory becomes `(1,A,0.50),(2,A,0.40)`; it contains no fabricated observation of B. |
| Validation/reservation | The engine checks P1's survival, source and edge, accepts the intent and excludes P1 from generic eligibility: C_A=20, R_A=1, G_A=19. It does not inspect B's resources to decide whether exploration is allowed. |
| Resolution | Generic migration is zero. Explicit deltas are A -1, B +1. Commit A=19 anonymous, B=0 anonymous + P1, world=20. P1 has one location and one crossing. |
| Resource/history boundary | A's consumption remains 8; arrival does not debit B. Record the knowledge, intent, acceptance and actual crossing. P1 first obtains a feeding observation of B in tick 3 if still alive/active. Operator inspection of B at tick 2 does not update its mind. |

In the paired ObserveOnly/no-override run with identical inputs, P1 stays in A
through generic migration: A=20, B=0. Shared feeding and demography through tick 2
are identical. The accepted explicit request accounts for the changed location;
any tick-3 feeding/survival difference must follow from that changed location.
Do not claim that this one pair proves a generally beneficial exploration policy.
A separate fixture must demonstrate explicit Stay holding P1 when generic
migration would otherwise move it.

## 6. Proposed interfaces, persistence and observability

Keep `Simulation.Step` the authoritative transaction. Introduce small internal
boundaries rather than a second population or resource service:

| Proposed boundary | Responsibility |
|---|---|
| Post-demography decision frame / observation builder | Detached survivor facts and the already-funded receipt; no publication of partial WorldState |
| Mind state | PersonId, policy version, at most eight observations, last observed/decision ticks; stable PersonId ordering |
| Pure `AdvanceMind(observation, priorMind, mode)` | Deterministic bounded memory update, optional intent, reason and exact dated facts used; no engine queries or mutations |
| Intent validator and migration reservation map | Accepted actor set, rejected reasons and eligible-count/bin views; at most one reservation/person |
| Existing migration/identity transaction integration | Generic plus explicit deltas, shared accounting, one final commit |
| Cognition evidence | One canonical record per processed active survivor per tick, containing observation, bounded facts used, memory update/digests, optional intent and its resolution |

Use a separate ordered cognition event chain/cursor. Include source and destination,
observed ticks, policy version, reason code, acceptance/rejection and movement result.
Link an accepted move's cognition record to the existing identity migration event.
Preserve existing biological and generic identity event payloads/order when there
are no overrides; avoid adding serialized null fields that change old hashes.
Explicit Stay records a decision with zero crossing, not a migration event. Archive
verification must validate the cognition chain, its references and new snapshot
cursors as well as the existing biological/identity chains.

For ordinary processing, bound event count to one cognition record per active
survivor-month, with bounded facts and current local exits. Records explain the
inputs actually used, including stale/unknown knowledge; they do not invent motives.
Event history grows over time even though runtime memory is bounded. History and
explanation queries read only; evicted memory may remain in recorded history but
must not be silently available to the policy. Earlier segments remain necessary
for earlier history after a continuation.

Propose **engine 0.5.0 / snapshot schema 5**, keeping scenario schema 1 and existing
biological RNG identifiers/keys. Persist cognition mode, policy/threshold/memory
configuration, living/dormant mind state and cognition event cursor alongside all
Phase 1A state. Include these fields in full state checksums. Policy changes that
alter decisions require a version boundary. Deterministic cost/ID tie-breaking
adds no random draws to any stream.

Reject schema-4/0.4.0 saves explicitly; retain `9bd958f` for running them. **No
converter in the first slice**: restarting the scenario/seed is explicit, and
no past memories are inferred. Cross-version comparison uses a canonical Phase 1A
biological/resource projection, not a direct old-save load or full-save hash match.

Support snapshots only at completed monthly boundaries. Each saved tick contains
resolved movement and no pending intents, reservations or partial observation
updates. On resume, process t+1 once; reading the save cannot append cognition
events or regenerate tick-t decisions. Phase-internal saves would require a
separately reviewed persistence protocol and are outside this plan.

## 7. Acceptance evidence required after implementation

All existing 84 automated tests and 35 CLI checks remain mandatory. Add the
following gates; none is satisfied merely by the illustrative decision above.

| Gate | Required test/evidence |
|---|---|
| Exact Phase 1A control | At base `9bd958f` and Phase 1B, compare every monthly canonical combined count/deadline, resources/environment, hunger memory, demographic/food flows, generic transfers and annual windows/summaries. With Off and ObserveOnly/no overrides, biological and existing identity event histories must be identical, not just statistically similar or equal in final totals. Exclude only new representation/cognition fields from the comparison. Preserve arithmetic/order as well as random keys. |
| Control coverage | Seeds 1..8 for 100 years across anonymous, 64-identified and 256-identified worlds with 0/1/64 active reservations as budgets permit. Exercise mid-run promotions/deactivations, scarcity, extinction and migration. Repeat the accepted 1,000-year seed 847192 reference and retained biological fingerprint. Store protocols and first-difference diagnostics. |
| Active causal case | Run the worked two-region fixture and its no-override pair. Assert the recorded facts, reason, accepted intent, one crossing, exact A/B totals and unchanged current-month feeding/demography. Add an explicit-Stay fixture whose generic control would move P1. Show later resource effects only where the changed location causes them. |
| No omniscient knowledge | Vary unobserved destination food/weather while holding local observation, legal exits and memory fixed: policy inputs and its intent must be identical. Test both promising-looking and hostile hidden destinations. Unknown remains explorable; later direct experience is what updates knowledge. |
| Memory and inspection | Empty initial memory, deterministic eight-record eviction, 12-month expiry, exact consecutive-month rule, dormant gaps, unknown/revisited destinations and no retrospective data. Repeated inspection/explanation/planning must leave full state and all history cursors unchanged, including when no person exists. |
| Exactly-once eligibility | Explicit Stay and Move excluded from totals AND named candidate groups before rounding/age allocation; no override retains the exact old path. Cover all-person overrides, zero generic pool, mixed named/anonymous/overridden buckets, duplicate/stale/replayed/future/dead/inactive intents, changed source/stage, illegal edges and rejection fallback. |
| Simultaneity and dates | Opposing flows, several actors targeting one region, A-to-B/B-to-C chains, empty destinations and generic arrivals. Assert one edge/person, no remigration, exact conservation and no double food debit. Include juvenile deadline carry, deadline-t maturation, death-before-maturation and no premature reproduction. |
| Transaction failure | Force invalid memory, duplicate reservation or an accounting failure late in a candidate. Input population, food, identities, minds, allocators and all cursors must remain unchanged; no partial state or completion marker may publish. |
| Active save/resume | Same seed, policy/options and boundary commands: compare uninterrupted versus resumed full states and biological, identity AND cognition event chains exactly. Split before the second poor observation, just after a move, at mid-year, on a maturation boundary and around activation/death. Include multiple simultaneous active movers and a 900+100-year continuation with sustained active occupancy. Compare concatenated segment histories in order, not segment-local counts alone. |
| Compatibility and evidence | Old-schema rejection before checksum decoding, new mind-state corruption checks, cognition-chain/reference tampering detection, and no old history fabricated on restart. Publish actual test counts, trace/decision records, all failures/corrections and reproducible commands. |

## 8. Performance protocol: isolate cognition from retained identity history

Repeat baseline measurements on the same host; Phase 1A's 17.60-second anonymous
and 53.56-second maintained-256 observations are historical context, not new
performance guarantees. Its maintained-256 case retained **19,206 deceased
identities** and a roughly 4.16 MB snapshot before any cognition existed.

Measure 1,000 years / 12,000 ticks at seed 847192 in fresh serial Release processes,
three repetitions per case, full monthly validation, annual metrics and snapshots
every 25 years. Publish every repetition plus median/range. Record runtime/SDK,
hardware, source revision, mode, command schedule and stopwatch boundaries.

Proposed matrix: Off at 0/0, 64/0 and 256/0 identified/active; Off, ObserveOnly and
Decide at 64/1 and at 256/64. Maintain target representation/active occupancy through
explicit logged boundary commands every 12 months, not automatic materialization
in biology. Report actual survivor-months because deaths reduce occupancy between
boundaries. Repeat matching 0.4.0 baselines with the same reservation inputs.

Distinguish the following measurements:

| Measurement | Required separation |
|---|---|
| Runtime | Total run time plus measured observation/memory/policy, intent integration, validation and cognition serialization/write time. Timing never influences scheduling or decisions. Compare Off versus ObserveOnly using identical biological/identity histories and command schedules to isolate added observation/logging work. |
| Memory | Peak process working set and managed allocation/retention measurements, reporting living, dormant and deceased counts. Matched-history comparisons are required; subtracting unrelated process peaks cannot isolate cognition. |
| Snapshot bytes | Total on-disk bytes plus separately measured serialized components for anonymous/core state, living identities, deceased identities, mind memory/configuration and cognition cursors. Label JSON framing overhead rather than claiming component sizes sum exactly. |
| Event growth | Counts/bytes separately for biological events, existing identity events, cognition events and command logs; bytes per processed active survivor-month and growth over time. Event logs are not part of bounded live memory. |

Add matched-checkpoint profiling with equal living/active counts and equal retained
deceased histories (zero and checkpoints near 5,000 and 19,000 deceased, generated
by Phase 1B simulation in Off mode). Fork those validated inputs for
Off/ObserveOnly measurements; this does not require loading old-schema saves.
Never remove tombstones or disable validation to make cognition appear cheaper.
Verify their no-override biological/identity projections still match. This exposes
how existing history traversal costs interact with new per-mind work.

Decide mode can alter survival, migration and subsequent promotion availability.
Report those end-to-end costs with actual population, deceased count, occupancy
and event volume. Do not label its total difference from a passive world as pure
cognition overhead when its histories differ. Use matched-input stage profiling
for that attribution.

Required structural gates are bounded eight-record live memory, at most 64 active
minds by default and at most one normal cognition record per processed survivor-tick.
Propose a review trigger for an Off-mode median runtime regression above 10% versus
the repeated matching Phase 1A baseline. Active overhead must be published and
reviewed explicitly; do not declare it acceptable solely because old tombstone
cost dominates. Approve these gates before coding rather than inventing a speed
claim after observing timings.

## 9. Implementation sequence after approval

1. Freeze executable Phase 1A reference fixtures and monthly projection comparisons;
   extract internal demographic/migration boundaries without changing results.
2. Add versioned bounded mind state and staged receipt/observation generation in
   Off/ObserveOnly modes; prove biological and identity neutrality and read purity.
3. Implement and validate reservation-aware generic migration plus explicit deltas
   using controlled intents, including Stay, rejections, simultaneous moves and ages.
4. Connect the pure `survival-v1` policy, empty-memory activation and canonical
   cognition evidence; realize the worked demonstration through normal simulation.
5. Complete persistence/archive verification, active continuation, the full test
   suite and performance matrix; publish engineering evidence before acceptance.

This document specifies the approved design and protocol; measured execution
evidence belongs in the Phase 1B handoff. The decisions below were approved by
the user's subsequent instruction to execute this plan.

## 10. Approved review decisions (historical)

1. **Behavior:** approve nutrition-only `survival-v1`, threshold 0.8, two consecutive
   local observations, remembered-adequate then unknown-exit preference and explicit
   cost/ID tie rules. Approve this as a demonstrator, without a success guarantee.
2. **Knowledge:** approve eight direct feeding records, 12-month expiry, empty first
   activation, frozen dormant memory and removal of runtime mind state on death
   while retaining recorded evidence. Approve the descriptive feeding receipt and
   observation timing; unknown destinations remain legal and unscored.
3. **Migration:** approve full-demand scoring with residual eligible-count rounding,
   exclusion of both accepted Stay and Move, simultaneous delta commit, and generic
   fallback after rejected live-person intents. Approve rejecting duplicate actor
   intent sets rather than choosing one by arrival order.
4. **Persistence:** approve engine 0.5.0/schema 5, cognition options outside Scenario,
   default Off mode, completed-month saves only and explicit rejection of schema 4
   without a converter. Approve activating real cognition only through selected
   modes plus existing active-person reservations.
5. **Evidence and cost:** approve the separate cognition chain, one normal record
   per active survivor-month, matched-history benchmark attribution, the stated
   matrix/three repetitions and the 10% Off-mode regression review trigger. Active
   runtime, memory and archive growth require measured review before acceptance.
6. **Authorization boundary:** approve this Phase 1B implementation sequence and its
   acceptance gates before any code is written; expand neither behavior nor future
   systems during implementation without a new review.
