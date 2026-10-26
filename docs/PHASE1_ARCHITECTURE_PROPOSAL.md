# Phase 1 — The First Minds: architecture proposal

Status: **proposed for review, not approved or implemented**. Date: 2026-10-26.
Prepared after Phase 0.6 passed 60 automated tests, 17 CLI checks and its
reproducibility/benchmark gates. This document does not authorize Phase 1 work.

## Recommendation

Keep the aggregate regional engine authoritative. Introduce a bounded number of
identified people by **transferring population out of anonymous cohorts**. A
single population transaction service owns both representations, shared biology,
resource allocation and migration. Minds submit intentions; they never edit
counts or stocks directly.

Begin with headless, monthly, local perception, small factual memories, and a
choice to stay or migrate. No per-person simulation of the entire population,
second ecology engine, graphics, LLM runtime, cities, agriculture, religion or
magic is proposed. Prove the representation boundary with passive people before
allowing any autonomous action to change the world.

## Exclusive ownership of people

For each logical biological bucket B (region, species, stage, variant, and
juvenile maturation deadline when applicable), define:

`Total(B) = AnonymousCount(B) + Count(LivingIdentifiedPeople in B)`

An identified person is never also included in AnonymousCount. Aggregate totals
used by food, migration scores and reports are derived views of that equation,
not separately mutable authoritative populations.

| Proposed record | Owns | Does not own |
|---|---|---|
| Anonymous cohort | Integer count; juvenile deadline partition | Named people, fictional memories |
| Living person | One person; region/species/stage/variant/deadline; identity | A second copy of a cohort member |
| Mind state | Perceptions, bounded memories, goal and pending intention | Population or resource totals |
| Historical identity | Stable ID and recorded events, including death | A living person after death |
| Regional stock ledger | The only forage stock and food transactions | Duplicate personal food allowances |

Promotion is an atomic command at a tick boundary: validate availability, reserve
one eligible anonymous member, decrement the source count (and the correct
juvenile bin), create one person, then validate all totals before commit. Failure
commits nothing. Several promotions cannot spend the same anonymous count.
Promotion is a representation transfer, never a birth or immigration event.

Use a persisted monotonic PersonId allocator, with commands applied in canonical
sequence. Reject duplicate commands or IDs. Materialization selection must be
explicit or use a separate deterministic selection stream; it must not consume
biology/weather randomness. Read-only inspection does not materialize people.
No camera/view state controls who exists or who thinks.

Initially materialize only from the default monthly-age model. Juvenile deadlines
are preserved exactly. Initial age-mixed juveniles have declared synthetic initial
ages; a derived birth tick must retain that provenance. Adult/elder birth dates
are unknown in current aggregates: keep them unknown rather than inventing exact
ages or past events. The current adult/elder hazards need only stage, not a fake
birth date. There is no retroactive family, education or personal-memory generator.

## Deactivation and retained identities

Separate biological identity from active cognition. Turning off a mind leaves
one living person with a small demographic record, still counted and exposed to
birth/death/migration once. A dormant mind does not imply suspended biology.

For the first slice, retain all promoted identities until death rather than
merging named people back into anonymous counts. This avoids losing their known
fates or keeping ambiguous identity reservations inside a cohort. Death removes
the living record from population totals and preserves a nonliving historical
identity; IDs are never recycled.

Returning an unimportant person to anonymous representation is a later explicit
transaction: remove its live person ownership and increment exactly one matching
anonymous bucket, with a recorded identity-retirement policy. Do not implement
that operation until the review decides what historical identity guarantees are
required. Never keep a live registry entry and also increment its cohort.

A proposed initial budget is 256 living identified people, including dormant
ones, with a separately configurable active-mind budget. These are engineering
limits to benchmark, not population caps. When the identity budget is full,
leave additional people anonymous and report that detail is unavailable; do not
delete people, silently recycle identities or auto-materialize every newborn.

## One food ledger, one biological update

Calculate start-of-month demand from the combined logical population exactly
once. Use the existing species/variant demands and shared finite regional food
allocation. The first slice has no personal inventories or private forage pool.
An individual's nutrition receipt describes its share of the regional transaction;
it is not permission to consume a second ration. Newborns enter after allocation,
as in Phase 0.6. Migrants consume at their origin that month and at their
destination starting next month.

Maintain both identities:

`endPeople = startPeople + births - deaths + inward - outward`

`endForage = startForage + regrowth - consumed`

Representation transfers have zero contribution to either equation. If inventories
are approved later, the conservation boundary must include regional stocks plus
all inventories, with explicit debit/credit transfers. Adding inventory is not
part of the proposed first slice.

### Passive detail must not change aggregate biology

Do not independently draw anonymous and identified birth/death totals. Splitting
an existing binomial into independent draws preserves its distribution but changes
fixed-seed outcomes merely because detail was enabled.

Instead, first draw the existing biological totals from the combined logical
bucket using its stable demographic stream. Then assign outcomes to identified
members and anonymous remainder using a separate conditional allocation stream.
For a death total D in a bucket of N exchangeable members, visit identified IDs
in canonical order. Select each with probability remainingD/remainingN, decrement
N, and decrement D when selected; remaining deaths belong to anonymous members.
This implements sampling without replacement without enumerating anonymous people.
Apply the same conditional allocation principle to existing adult stage exits
and eligible-parent birth opportunities. For juveniles, the bucket includes the
exact deadline so maturation cannot affect an ineligible person.

Birth totals, mutation/variant splits and resource effects are still generated
once. Conditional parent/child assignment must preserve those existing totals.
Children enter anonymous newborn bins by default. A subsequently identified
newborn is transferred from that count; creating its identity is not a second
birth. Parent references are recorded only where assignment was actually simulated.
The first slice need not add family mechanics or detailed genealogy.

This scheme requires equal hazards within a logical bucket. Do not introduce
personal biological modifiers, separate food treatment or privileged immunity in
the first slice. If later traits create heterogeneous hazards, the bucket model
and sampling invariants need a reviewed extension.

With minds passive and no overrides, a projection that folds people back into
their logical buckets must match the aggregate-only run's populations, juvenile
deadlines, stocks and demographic flows tick for tick at a fixed seed. Full world
hashes will differ because identity/memory records differ, and representation
events change history. Compare a canonical demographic/resource projection and
biological event projection, not equality of entire representation-dependent saves.

## Monthly integration and decisions

Proposed ordering extends the existing causal pipeline:

1. Apply accepted representation commands atomically at the boundary.
2. Update environment and regenerate food once; allocate food to the combined
   population and produce individual receipts where identities exist.
3. Sample shared survival, stage transitions and births, assign their identities
   conditionally, and remove dead people's pending intentions.
4. Give surviving active minds a bounded local observation; update factual memory
   and produce at most one stay/move intention for this month.
5. Freeze post-demography populations and migration forecasts. Reserve identified
   movers once; resolve their legal intentions and the remaining aggregate plans
   together, then commit all population/age/location deltas in a second pass.
6. Validate combined population/resource identities, record measured outcomes and
   advance the clock. A rejected candidate state leaves the input world intact.

A person without a behavioral override participates in the existing generic
migration pool; conditional allocation identifies which generic movers are named.
An explicit stay or move removes that person from the generic pool for the tick,
so it cannot be selected again. Generic movement probabilities still depend on
the shared total demand. Named and anonymous arrivals cannot remigrate, inaccessible
edges fail validation, and every identity has exactly one location after commit.
Active choices may intentionally alter aggregate outcomes; that is causality,
not a violation of passive representation neutrality.

Minds receive current local conditions and dated observations of places they
actually know. They cannot inspect future RNG, arbitrary neighbor stocks, or
worldwide state through a query shortcut. A proposed first policy has one survival
goal, a small observation memory, and stay/move actions; uncertainty about a
destination remains uncertainty. Decisions reference recorded observations and
reason codes. Aggregate migration can keep its current forecast while detailed
decisions use limited information; this intentional behavioral difference must
be tested separately from representation changes.

No individual hunger physiology is added initially. The current hunger memory
remains a region/species exposure variable, and a mind can remember its received
nutrition without causing an additional mortality or consumption update.

## Concrete accounting example

Region A begins with 100 anonymous people and no identified people. Promote three:
97 anonymous + 3 identified = 100. Demand and food consumption remain unchanged.
Shared biology produces two deaths and one birth. Suppose conditional assignment
selects one anonymous and one identified death, and the newborn remains anonymous:
97 anonymous + 2 identified = 99. One identified survivor migrates to B:
A has 97 + 1 = 98; B gains one. The world still has 99 people.

The total is 100 + 1 birth - 2 deaths. Promotion and migration create no people.
The dead identity has zero population weight. Regional forage was debited once
before these demographic/movement changes; no mind can claim another debit.

## Determinism, persistence and evidence

Persist person records, active/dormant flags, minds, identity allocator, accepted
command sequence and pending yearly accounting in a new versioned snapshot.
Keep independent streams for identity selection, conditional outcome assignment
and decisions; leave ecological stream keys independent of detail count and order.
Use canonical person/command ordering and the same monthly clock. No wall-clock
budgets, rendering order, dictionary order or background AI threads may affect truth.

Retain the existing 0.3.0 engine for its saves. A Phase 1 version/schema and any
explicit 0.3.0 migration must be reviewed, not silently inferred. A simple possible
conversion starts with no identified people and all current cohorts anonymous;
it cannot invent past individuals or missing history. Its exact compatibility
contract must be tested before release.

Reports continue to expose combined species/region totals and add separate
anonymous/identified/active counts, plus representation-transfer records that
are excluded from birth/death/migration totals. Person events reference stable
IDs and only recorded perceptions, decisions and outcomes. Unknown memories or
historical details return "not recorded". Deceased identities remain resolvable.

## Proposed implementation gates after approval

| Gate | Required evidence |
|---|---|
| Passive representation bridge | Repeated promotion/deactivation preserves every population/age/stock total; unavailable or duplicate reservations fail atomically |
| Biological neutrality | Many seeds and varied detail fractions produce exact folded population/resource/flow equality when behavior is passive, including birth/death assignment and extinction |
| Resource ownership | Scarcity tests show combined demand counted once, receipts sum to consumption, and no negative or duplicate stock debits |
| Movement ownership | Opposing flows, simultaneous promotions and rejected intents preserve identities, deadlines, edge limits and one location/person |
| Minimum age | Materialization cannot reset or bypass a maturation deadline; new adults and newborns cannot reproduce immediately |
| Reproducibility | Same seed/commands gives identical full state/events; mid-month-boundary snapshots and 100-year continuation are exact |
| First decisions | One headless locality demonstrates stay/move choices based only on recorded local knowledge; no hidden resource or person creation |
| Performance | Repeat 1,000-year runs at 0/64/256 identified people and varying active fractions; publish runtime, memory, snapshot and event volume before choosing final budgets |

First implement only the passive bridge and its tests, then local perception and
one action policy. Do not use emergent-looking stories as acceptance criteria.

Review decisions requested before code: approve the exclusive-ownership and
conditional-allocation design; decide whether retaining every promoted identity
until death is acceptable for the first slice; agree on the initial identity/mind
budgets and stay/move-only behavior; approve the snapshot compatibility boundary.
**Development stops at this proposal until that review is complete.**
