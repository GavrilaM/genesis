# Prototype 001 implementation plan

1. Establish solution, pinned tooling and authoritative reference documents.
2. Implement a monthly vertical slice: configured graph and cohorts, climate,
   finite forage, nutrition, deaths/births/aging, inheritance, two-pass migration.
   Validate every transition; test conservation and deterministic continuation.
3. Persist validated canonical snapshots, threshold events and sampled metrics;
   expose headless creation, advancement and read-only inspection through CLI.
4. Run paired counterfactuals across fixed seeds, 100/1,000-year benchmarks and
   doubled-region/cohort scale. Record actual results and limitations in handoff.

Acceptance follows `docs/GENESIS_PHASE0_BLUEPRINT_v2.md`. Equation choices are
provisional simulation assumptions, not empirically calibrated biology.

## Initial delivery status

- Completed: separate Core/CLI/Tests, pinned .NET and xUnit, 12-region/four-species
  versioned scenario, monthly causal pipeline and invariant validation.
- Completed: canonical snapshot round-trip and exact 100-year continuation;
  actual 1,000-year headless runs and equal final state/event hashes.
- Completed: CLI queries, event ledger, sampled metrics, bounded audit, integrity
  verification, export, comparisons, five paired experiments and scale benchmark.
- Evidence: 21 automated tests and 13 CLI smoke checks pass; see handoff.

## Engineering questions identified after the initial prototype

1. Review baseline biological parameters and long-run diversity: seed 847192
   nearly becomes a single-species world. This is an observed competitive result,
   not an intended species destiny or a validated ecological equilibrium.
2. Demographic variance and juvenile timing are addressed in Phase 0.6 below.
   Adult/elder distributions and migration oscillation remain approximations.
3. Annual accounting and migration-wave summaries were delivered in Phase 0.5.

Not claimed: calibrated ecology, cross-runtime determinism, adaptive stepping,
replay-on-demand for uncaptured traces, intervention logs or save-schema migrations.
Future individual/cultural/divine/visual systems remain outside this milestone.

## Phase 0.5 status

Implemented and tested: optional sparse/exact starting cohorts, capacity-bounded
next-month migration forecasts, canonical annual flow accounting, measured
migration-wave/population-change events, history summaries and summary checksums.
Engine 0.2.0 / snapshot schema 2 explicitly rejects incompatible old saves.

Controlled experiments identify reproduction and thermal tolerance as strong
contributors to orc dominance; maturation and geography also matter. No baseline
species parameter was retuned. Demographic assays demonstrate premature maturation
under geometric stages and suppressed stochastic variance. These are documented
limitations, with evidence-backed recommendations for a minimum-age/staged cohort
model and a bounded-count stochastic model before using the simulator to infer
juvenile timing or extinction probabilities. Alternatives remain offline assays.

See `docs/PHASE05_REPORT.md` and `docs/results/phase05/` for exact results and
commands. This closes the requested calibration pass, not the research questions
about empirical ecology or the authorization boundary for future systems.

## Phase 0.6 — Demographic Integrity

Implemented: aggregate monthly juvenile maturation queues, binomial birth/death
sampling, age-preserving migration, canonical maturation counts and strict engine
0.3.0 / snapshot schema 3 compatibility. Existing scenario files retain their
stage/variant totals; maturation now means minimum residence time. No biological
parameter was retuned. Adult/elder timing and mutation/migration rounding remain
documented approximations.

Evidence compares six age/sampling combinations in full worlds, newborn pulses,
adult founders, newborn bottlenecks and an analytic extinction reference. Annual
age buckets are faster but rejected as the default because of calendar pulses.
See `docs/PHASE06_REPORT.md`, `docs/results/phase06/` and the reproduction scripts.

## Phase 1A — Persistent Individual Identity and Population Ownership

Architecture approved with the user's refinements. Phase 1A implements exclusive
population ownership, shared demographic/resource transactions, atomic promotion,
persistent lifecycle records, passive biological equivalence, read-only inspection
and local-only observation. Budgets reserve representation capacity; no cognition
executes. See `docs/PHASE1A_HANDOFF.md` for verification and benchmarks.

## Phase 1B — First decisions (not implemented)

Use the Phase 1A interfaces and invariants in the handoff. Future perception,
memory, uncertain exploration and intentions must not mint people, consume food
twice, bypass biological deadlines or query omniscient destination conditions.
Phase 1A completion does not authorize Phase 1B implementation.
