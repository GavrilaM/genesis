# Architecture and blueprint validation

The blueprint is internally compatible with a small single-threaded monthly
engine. Its equations, thresholds, initialization and age transition distributions
are unspecified; these are explicit provisional rules, not new feature scope.

`Genesis.Core` owns data, deterministic transitions, validation and persistence.
`Genesis.Cli` owns command parsing and disk reports. `Genesis.Tests` uses xUnit v3.
No game engine, external service or UI is required.

Canonical ordering uses stable numeric region/species IDs, then life stage and
trait variant. Zero cohorts remain present. Stable typed identifiers are distinct
from names. Each tick constructs a candidate state; failures leave the input
unchanged. Read queries never advance random streams.

Randomness: versioned counter-based SplitMix64, keyed by seed, month,
system, entity and draw. The seed and next month are the complete RNG cursor.
No wall-clock entropy, mutable global RNG or unordered state iteration.

Snapshots contain compatible rules, scenario, threshold memory and event-chain
cursor, allowing exact continuation without embedding the historical ledger.
The ledger alone is insufficient for reconstruction. Snapshot writes validate
before same-directory atomic replacement. Compatibility is strict, not migrated.

## Monthly mechanics, units and assumptions

Tick 0 is the initial state. Tick 1 completes year 0, month 1. Each subsequent
tick is exactly one month, including every month of long runs. The state contains
all 12 x 4 x 3 life stages x 3 trait variants = 432 cohorts, including zeros.
The baseline graph is an undirected ring with opposite-region shortcuts. Region
and species numeric IDs must be contiguous and sorted; edges are sorted and unique.
Disconnected and directed graphs are allowed for controlled experiments.

1. **Climate.** Temperature is degrees C: base + configured offset + an 8-degree
   annual sine + uniform seeded noise in [-2, 2). Rainfall is mm/month: base x
   rain multiplier x drought multiplier x uniform [0.8, 1.2). Drought applies
   on ticks `(start, end]`. Regions share seasonal phase but have separate noise.
2. **Food.** Stocks are person-month rations. Area is km². Productivity is
   rations/(km² month). Potential regrowth = area x fertility x productivity x
   min(1.5, rain/100) x clamp((temperature+10)/25, 0, 1). Actual regrowth is
   limited by remaining storage capacity. Consumption never exceeds the stock
   after regrowth. There is no spoilage, trade, agriculture or other food flow.
   Demand per person = species demand x (1 + variant x resistance food cost).
   All ages eat equally in this first model. All species share the same fraction
   of their demand: nutrition = min(1, available/total demand); empty demand gives 1.
3. **Survival.** Cold exposure = max(0, species cold tolerance - variant x
   resistance degrees - temperature). Heat exposure = max(0, temperature - heat
   tolerance). Hunger memory increments below 0.8 nutrition and decays by one
   otherwise, capped at 120 months. It belongs to the local group, not migrating
   individuals. Death probability = natural mortality (x1.5 for juveniles) +
   1/elder duration for elders + starvation coefficient x (1-nutrition) x
   (1 + min(hungry months,12)/12) + climate coefficient x total exposure,
   clamped to [0,1]. Temperature thresholds are provisional fitness parameters.
4. **Age and births.** Surviving juveniles and adults move one stage with
   probabilities 1/maturation duration and 1/adult duration. Durations are mean
   residence times (geometric approximation), not exact ages or maximum lifespans.
   Elders eventually die; no population is immortal. Birth expectation per variant
   = surviving pre-aging adults x annual birth rate/12 x nutrition² x
   clamp(1-exposure/30,0,1). The birth rate absorbs sex ratio and eligibility;
   there are no mating pairs. New adults and newborns cannot reproduce this step.
5. **Inheritance.** Each child inherits its aggregate parent's variant, except a
   configured fraction mutates uniformly to either other variant. Variants 0/1/2
   shift cold tolerance by 0/5/10 degrees by default, with 0/4/8% extra food demand.
   No cultural or technological trait is inherited. Counts use floor(expected)
   plus one Bernoulli draw for the fractional remainder. This is unbiased stochastic
   rounding, **not binomial demographic sampling**; variance is deliberately small.
6. **Migration.** After births/deaths/aging, freeze all regional demand. Forecast
   score per region/species/variant = min(2, (remaining food + this month's regrowth)
   / max(1, post-demography demand)) - 0.04 x thermal exposure. A neighboring score
   pays edge cost x travel penalty; inaccessible edges exceed the species travel
   limit. Pick the greatest net score, ties by lowest stable ID. Move only if gain
   exceeds the threshold; expected movers = count x migration rate x min(1,gain).
   Plan from frozen source counts, then commit all deltas together. Movement is at
   most one edge/month. Crowding enters through demand; arrival crowding is not
   iteratively re-planned, so oscillation is a known approximation. Migrants have
   no invented personal memory; hunger exposure is location-level.

All probabilities, rates and durations have validated ranges in `Validation.cs`.
Configurable parameters live in `Rules` and scenario species/region definitions.
Fixed formula coefficients above belong to engine version 0.1.0. Scenario hashes
cover defaults as materialized rules, configuration order and initial conditions.
Changing formulas requires an engine-version change; cross-version compatibility
is deliberately rejected. Exact reproducibility is claimed only on the measured
Windows x64/.NET environment, not across CPUs or runtime versions.

## Accounting and evidence

Each region/species reports start + births - deaths + in - out = end. Each
region reports start food + regrowth - consumption = end food. Trait totals sum
to population. Species consumption sums to regional consumption (within floating
point tolerance). Whole counts are bounded at 1e12/cohort; baseline starts at 38,400.
No population cap controls growth; renewable food and mortality constrain it.

Famine emits start below nutrition 0.65 and end at 0.9, or when the group is
absent. Hysteresis suppresses monthly repeats while a famine persists. Events
contain measured nutrition, stock/demand, rain and hunger inputs. Regional
collapse requires a >=50% monthly loss from >=20 people. Global species extinction
emits only on transition from positive to zero. Migration remains in measured
flows and audit transfers; there is no separate migration-wave event in this slice.
Events include stable IDs, severity, source system, reason and before/after values.

The event ledger is append-only within a run segment. Snapshots store a sequential
event ID and SHA-256 chain cursor, not the ledger itself. Continuation creates a
new segment with its own `initial.json`, so it never rewrites earlier history.
`verify DIRECTORY` checks segment snapshots, ledger ordering, references and chain.
It detects corruption; it is not authentication and does not re-simulate the run.

Metrics are sampled monthly transactions at every Nth tick (default 12), plus the
last tick. **Do not sum annual samples as annual flow totals.** `--sample-every 1`
records every month; report.json holds cumulative flows for the entire run. Two
CSVs separate shared regional food/climate from population flows, preventing
accidental duplicate resource summation. Trait counts appear in population.csv.

Audit capture is opt-in for one tick and optionally one region; it includes food,
demand, cohort exposure/death probabilities and transfers. It consumes no random
draws. Uncaptured queries say `not recorded`; historical reconstruction is absent.
Snapshot checksums cover canonical serialized data. The writer flushes a temporary
file, reload-validates, then replaces the destination on the same filesystem.
This is atomic file replacement, not a multi-file transaction or power-loss-proof
archive. Incomplete run directories are explicitly marked and cannot verify.

## Boundaries and next evidence needed

Terrain is descriptive; fertility, climate and area drive current mechanics.
Species values and initial distributions are illustrative, not calibrated fantasy
biology. Starting per-region population is uniform. Initial trait allocation uses
floors and gives the final bucket the remainder; this introduces at most two
persons' rounding imbalance per stage. No genealogy, sex model, exact age bins,
individual entities, settlement simulation or narrative generation is implemented.

There is no intervention API, save migration, adaptive stepping, threading, SQLite,
remote hosting or replay-on-demand. Future detail must conserve existing IDs,
populations and stocks; no fake detail objects are introduced now.
