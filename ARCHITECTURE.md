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

Planned randomness: versioned counter-based SplitMix64, keyed by seed, month,
system, entity and draw. The seed and next month are the complete RNG cursor.
No wall-clock entropy, mutable global RNG or unordered state iteration.

Snapshots contain compatible rules, scenario, threshold memory and event-chain
cursor, allowing exact continuation without embedding the historical ledger.
The ledger alone is insufficient for reconstruction. Snapshot writes validate
before same-directory atomic replacement. Compatibility is strict, not migrated.
