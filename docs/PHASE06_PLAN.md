# Phase 0.6 — Demographic Integrity

Scope: juvenile maturation and birth/death sampling only. Preserve aggregate
regions, species, life stages, variants, finite food and monthly transactions.
No Phase 1 implementation. Prepare its architecture proposal after verification.

1. Reproduce Phase 0.5 limitations; preserve the accepted executable as a local
   reference and keep its Git history intact.
2. Compare geometric juveniles against minimum-age release queues at one-month
   and twelve-month resolution. Keep adult/elder rules unchanged. Newborns must
   spend at least `maturationMonths` in the juvenile stage and cannot reproduce
   during their maturation step. Initial juveniles have a documented age mix.
3. Compare stochastic rounding against bounded binomial births/deaths. Validate
   moments, tails, endpoints, large counts and aggregate partition behavior.
   Counter streams remain local to each system/cohort/age bin.
4. Run factorial controlled worlds across paired seeds, newborn pulse experiments
   and small-colony extinction experiments. Measure runtime, reproduction timing,
   extinctions, variation and end populations. Select the simplest adequate model;
   do not tune species toward equality.
5. Test conservation, age-preserving migration, input immutability, exact resume,
   event/summary continuity, incompatible-save rejection and all existing checks.
   Repeat 100/1,000-year and double-region benchmarks. Publish compact evidence.
6. Document versions, changed semantics, limitations and exact commands; commit
   and verify the GitHub push. Write a review-only Phase 1 architecture proposal
   establishing exclusive ownership of people and resources across detail levels.

Candidate acceptance: zero births before a newborn can mature; event moments and
tail frequencies consistent with analytic binomial expectations within predefined
sampling tolerances; no accounting or reproducibility failures. Coarse ages must
demonstrate a useful performance benefit without unacceptable timing artifacts.
World population changes are observations, not pass/fail equality targets.
