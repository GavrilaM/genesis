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
