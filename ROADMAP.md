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

## Next engineering decisions within Prototype 001

1. Review baseline biological parameters and long-run diversity: seed 847192
   nearly becomes a single-species world. This is an observed competitive result,
   not an intended species destiny or a validated ecological equilibrium.
2. Quantify sensitivity to stochastic-rounding variance, geometric stage durations
   and migration oscillation before adding biological complexity.
3. Add migration-wave summaries or interval-aggregated metrics only if current
   reports prove insufficient. Existing per-month flows already record migration.

Not claimed: calibrated ecology, cross-runtime determinism, adaptive stepping,
replay-on-demand for uncaptured traces, intervention logs or save-schema migrations.
Future individual/cultural/divine/visual systems remain outside this milestone.
