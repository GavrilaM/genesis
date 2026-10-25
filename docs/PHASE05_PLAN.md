# Phase 0.5 — calibration and correctness

Scope: population initialization, migration forecasting, controlled sensitivity,
demographic approximation evaluation, and measured historical summaries. No
individual agents or civilization features. Baseline species parameters will
not be tuned to force equal outcomes.

1. Reproduce migration forecast over-capacity; introduce explicit prospective
   food/climate estimates and a mechanics-version boundary.
2. Add optional sparse region/species starting populations with canonical IDs,
   validation, empty-region coverage and legacy scenario fallback.
3. Sweep paired seeds for reproduction, maturation, demand, tolerance and initial
   geography; test factor swaps to distinguish labels from biology. Evaluate
   geometric stages and stochastic rounding in isolated demographic assays.
4. Add exact annual flow summaries and bounded migration-wave milestones to
   explain gradual changes currently absent from the selective ledger.
5. Run all tests, full 1,000-year/scale benchmarks and continuation checks;
   publish results, assumptions, limitations and verified Git commits.

Compatibility: accept existing scenario JSON unchanged. New engine results will
differ because forecasts change. Retain previous Git history and reject legacy
snapshots clearly rather than silently relabeling them as compatible.
