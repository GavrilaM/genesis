# GENESIS — Phase 0 Architecture & Prototype 001 Contract

Status: Proposed baseline for implementation. Date: 2026-10-25.

## North star
**Simulate causes, not stories.** The simulation is the source of truth; the event recorder captures significant transitions; a future chronicle writer only interprets recorded evidence. The project is an observer-first, open-ended fantasy world simulator, ultimately supporting independent species, societies, discoveries, religions, magic and competing gods. It must never depend on an LLM at runtime.

## Current scope decisions
- **Function before presentation.** Headless console first; no graphics, Godot integration, assets, animations or elaborate UI in Prototype 001.
- **Language:** C# targeting .NET 10 LTS, simulation core in a standalone class library, CLI in a separate executable, test project separate. No cloud services.
- **World:** 12 graph-connected regions with explicit, stable IDs, terrain, climate, area, fertility and resource stocks. Graph edges may have traversal costs.
- **Species:** four configurable fantasy species (human, orc, elf, dwarf). Biological differences are configurable traits, not hardcoded cultural destinies. No predefined civilization ranking.
- **Population representation:** per-region, per-species aggregate cohorts. **Do not** instantiate a full character for each member of a large population. Do not give an aggregate population fictional personal memories.
- **Biological depth:** population births/deaths, broad age cohorts and initially one inheritable survival trait with a small distribution of variants. Meaningful heritability/selection, not genes or detailed anatomy. Culture/tech/magic are explicitly separate from evolution.
- **Clock:** one month per logical tick in the first prototype (12,000 ticks/1,000 years). `run 1000y` iterates the same underlying simulation in a tight headless loop; it does not fabricate a final state. Coarser adaptive steps may come later after comparison against the monthly reference.
- **Randomness:** one seeded, deterministic random-stream scheme with stable stream IDs for systems/entities and canonical iteration order. Record simulation version, ruleset hash and seed. Same version, inputs and seed must reproduce the same state and event hash on the supported environment. Never rely on wall-clock seeds or unordered iteration.
- **Persistence:** canonical snapshots and structured event/metric records. Start with versioned JSON snapshots, JSONL significant events and CSV sampled metrics. Consider SQLite only if volume/query performance warrants it. Snapshot writes should be atomic and validated on load.
- **Future features:** locally simulated individuals, settlements with buildings, cultural diffusion, composed inventions, religions, gods, forbidden knowledge, and fast-forward-to-detail are reserved extension points. No fake implementation of these systems in Prototype 001.

## Definition of truth
1. **Canonical state** — what exists now: the clock; regions, environmental values, stocks, populations and cohort compositions; the rules and random streams.
2. **Event ledger** — a selective, append-only record of significant *state changes* (famine, migration wave, population collapse). Each event references relevant IDs and measured contributing factors. It is not a log of every monthly transaction.
3. **Metrics series** — regular sampled population, births, deaths, harvest, consumption, stock changes, migration and climate per region and species. These permit statistical inspection even where no event was emitted.
4. **Snapshots** — full canonical state at specified simulation times. A snapshot plus compatible rules, seed and recorded user interventions enables reproducible continuation. Event ledger by itself is **not** sufficient to replay the full simulation.
5. **Interpretations** — later historian/LLM prose; never treated as canonical. Distinguish recorded causes, observed correlations, and conjectures.

## Identifiers and world data
- Stable ID types for region, species, population cohort, event, and (future) person, settlement, religion, god, artifact and knowledge item. Avoid relying on display names as identifiers.
- `WorldState { schemaVersion, engineVersion, rulesetHash, seed, currentTick, regions, species, populations, rngState, ... }`
- `Region { id, name, neighbors, area, terrain, temperature, rainfall, fertility, forageStock, forageCapacity, ... }`
- `SpeciesDefinition { id, name, baseFoodDemand, maturation, lifespan, reproductionParameters, climateTolerance, migrationParameters, ... }`
- `PopulationCohort { regionId, speciesId, lifeStage, inheritedTraitVariant, count }`. Life stages are species-relative; use integer counts. Extinct is a count of zero, not an object deletion that breaks history.
- `Event { eventId, tick, type, actorRefs, regionRefs, measuredChange, contributors, sourceSystem, reasonCode, severity, relatedEventIds? }`.
- Keep read-only queries distinct from commands that mutate state. Shared state belongs to the simulation core; systems must not maintain contradictory private totals.

## Prototype systems: causal update pipeline
Use explicitly documented ordering and atomic/validated transitions. Suggested monthly tick:
1. Update regional temperature/rainfall (seeded climate variation).
2. Regenerate renewable forage subject to area, rainfall, fertility, stock/capacity and limiting factors.
3. Calculate food availability and species/population demand. Allocate finite food; do not allow double consumption or negative stocks.
4. Apply health/mortality and species-appropriate births, based on nutrition, climate, age cohorts and trait differences. Births cannot immediately reproduce in the same step.
5. Plan migrations in a **two-pass** process: calculate origin outflows and destination intents first; commit together only across valid graph edges with movement constraints. Check all population accounting.
6. Commit state and compute metrics. Emit *significant* events based on defined threshold crossings plus actual measured contributors. Suppress duplicate event spam; include start and end events where appropriate.
7. Validate invariants (always in tests, configurable in production) and advance the month.

Track flows separately for reports. Example accounting: `endPopulation = startPopulation + births - deaths + inwardMigration - outwardMigration`. `endFood = startFood + regrowth - consumed - spoilage + imports - exports` with any future flows explicit. Formulas and units must be documented and dimensionally coherent (for example food as person-month rations).

### Minimal decision mechanics
- **Migration** must respond to prospective net survival conditions (food scarcity, climate suitability, crowding, travel difficulty, accessible neighbors); it must not be an independent random chance disconnected from conditions.
- **Mortality** must increase under sustained starvation or hostile climates; births depend on eligible cohorts and survival conditions.
- **Carrying capacity** emerges from renewable food rates and demand instead of a hard maximum population cap. Avoid immortal populations without replenishment.
- **Adaptation:** initial inheritable trait distribution has at least three coarse variants or equivalent small buckets. Children inherit variants with defined mutation/mixing rules; environment affects differential survival. Report distributions over generations. No magical species stat leveling.
- **Species descriptions** are parameters, not scripted social behaviors. Geography can make orcs prosper first in one seed and humans in another.

## Observability: an inspector, not a storyteller
Three levels of instrumentation:
- **Metrics:** periodic summaries for trends, comparison and charts: population by species/region, births/deaths, food produced and consumed, migration flows, climate, trait-frequency change, species extinction count, runtime and memory.
- **Significant events:** threshold-based milestones with explicit before/after values and reason codes. Event severity and deduplication prevent gigabytes of repetitive noise.
- **Audit trace (opt-in, bounded):** granular explanation of one selected tick, region or species showing inputs, computed demand/survival and applied transfers. Disabled by default; audit mode may cost more CPU/storage.

### Console interaction contract
Suggested CLI verbs (exact spelling can vary but document final syntax):
- `genesis new --seed 847192 --scenario baseline`
- `genesis run --years 1000 --snapshot-every 25`
- `genesis inspect world`
- `genesis inspect region <id>`
- `genesis inspect species <id>`
- `genesis history --from <year> --to <year> --type <type>`
- `genesis explain event <event-id>`
- `genesis trace --region <id> --year <year> --month <month>` (only if data captured or a compatible replay from an earlier snapshot is available)
- `genesis export --format csv`
- `genesis compare <run-a> <run-b>`
- `genesis verify <save>`
- `genesis benchmark --seed ... --years ...`

A command must say **"not recorded"** if information isn't available. Never hallucinate the thoughts of untracked historical agents or retrospective exact monthly events from aggregate data. An `explain` command should separate model-logged contributing inputs from inferred causes; it should not claim experimentally proven causation from a mere correlation.

## Future scale bridging contract
- An abstract region's total population, economic stocks, established species composition, important named actors, and historical facts are constraints on any subsequently materialized detailed locality.
- Detail expansion allocates individuals/buildings from conserved parent stocks; zooming must not freely mint new people or resources.
- If personal histories were not simulated, generated background details are *reconstructions* labeled as such, not previously recorded canonical history.
- Detailed entities can be aggregated back into their parent populations with conserved totals and preserved important identities.
- Core simulation should support stable data IDs and versioned extensible records. Do not implement both execution modes in Prototype 001.
- Early 'fast-forward' means no rendering plus repeated monthly ticks, **not** arbitrary multi-year jumps. Add adaptive stepping only after quantified consistency testing.

## Test and scientific quality gates
**Mechanical invariants** (test continuously): counts and resource stocks never negative; no duplicate consumption; movement obeys connectivity and travel constraints; population/resource flow identities balance; dead/zero cohorts cannot reproduce; snapshots round-trip; event references resolve to valid historical entity IDs.

**Reproducibility**: same version/ruleset/seed and inputs -> identical canonical output and event hash; saved snapshot continued 100 years -> identical final state to uninterrupted 100-year continuation. Cross-version reproducibility is not promised unless migration support is explicitly implemented.

**Counterfactual experiments** (repeat across many seeds, compare distributions):
- baseline vs multi-year drought -> higher food scarcity and/or mortality/migration, not necessarily every individual seed;
- change region connectivity -> migration destination options change;
- increase inherited cold resistance in one group under cold conditions -> measurable population fitness or trait-frequency effect;
- severe food loss with no alternatives -> contraction or extinction rather than limitless growth;
- raise productivity with fixed inputs -> generally improves food availability, subject to demographic feedback.

**Performance**: benchmark monthly ticks, 100/1,000-year run duration, peak memory, snapshot size, event volume, and scaling when regions and cohorts double. Do not promise specific seconds on unbenchmarked laptop hardware. Start with a single-threaded deterministic engine; profile before parallelism.

## Deliverables for Astra's first build
1. Repository with solution and separate `Core`, `CLI`, `Tests` projects and a basic README.
2. Versioned scenario configuration with 12 connected regions and four species.
3. Working 1,000-year headless simulation from a deterministic seed.
4. Explicit resource/population accounting, climate, food, mortality, birth, migration, and minimal heritable trait distribution.
5. Queries, statistics, history event records, metrics CSV, snapshot save/load.
6. Automated tests and 3+ counterfactual scenarios; benchmark output with machine/environment context.
7. Clear documentation of mechanics, assumptions and every simplified or not-yet-implemented area.
8. A final handoff report with exact commands for build/test/run and the results actually obtained.

### Hard exclusions for this milestone
No Godot integration or visuals; no LLM agents/API; no individual dialogue or personalities; no detailed genealogy; no detailed 3D buildings; no arbitrary discoveries, agriculture, empires, gods, religion or magic. **Do not fabricate these as event-generator placeholders.** These are future milestones and design hooks, not missing tasks.

## Development discipline
- Commit in small testable increments; do not invent systems beyond the milestone.
- Define units and parameter ranges; use data/configuration for tuning.
- Validate schema versions and reject incompatible saves clearly. Preserve old save files when adding future migrations.
- Keep random generation isolated and reproducible; avoid hidden non-deterministic services and concurrent mutations.
- Report exact tests and benchmarks, including failures or unimplemented areas; never claim a successful simulation just because a plausible story was printed.
- Maintain `VISION.md`, `ARCHITECTURE.md`, `ROADMAP.md` as living, concise references.

## Definition of done
Prototype 001 is done when the user can create a seeded world, advance it headlessly for 1,000 years, inspect population and environment changes, see an evidence-based historical ledger, load a save and reproduce its continuation, and compare multiple controlled scenarios showing coherent and statistically measurable differences—without graphics or any LLM runtime cost.

## External development tools and dependency policy (Phase 0 addendum)

The core simulation must run locally and headlessly **without** cloud APIs, runtime LLM calls, game engines, graphics tools, or paid services. External development tools help *build and verify* it; they are not dependencies for the simulated world.

### Tier A — Set up for Prototype 001
| Tool | Purpose | Policy |
| --- | --- | --- |
| .NET 10 SDK and `dotnet` CLI | Compile/run the standalone C# engine, tests, and console interface | Required; verify SDK and OS before development |
| Git | Local commits, diffs, branches, safe rollback | Required; initialize repository before substantive edits |
| Private GitHub repository | Optional remote backup and review across coding sessions | Recommended, but not required for the engine to function; never push secrets |
| VS Code + C# extension/C# Dev Kit **or** Visual Studio 2026 | Human review, debugging, diagnostics | Choose one if an editor is needed; avoid installing both automatically |
| xUnit.net v3 **or** a single compatible .NET test framework | Mechanical invariants, reproducibility, snapshot round-trip, scenarios | Prefer xUnit v3; select one framework and stick to it |
| `System.Text.Json`, CSV/JSONL files, built-in timing/logging | Serialization, event ledgers, metrics, baseline throughput | Use built-in .NET libraries wherever reasonable |

### Tier B — Add when there is evidence of need
| Tool | Trigger | Usage |
| --- | --- | --- |
| GitHub Actions | Repo is hosted on GitHub and basic tests exist | Minimal workflow: restore → build → test on each change; optional |
| Coverlet (compatible collector) | Need code coverage visibility | Report coverage as diagnostics, not as proof of simulation correctness |
| BenchmarkDotNet | Stable hotspots or performance regressions identified | Controlled microbenchmarks; first keep simple end-to-end timing |
| `dotnet-trace` / Visual Studio Profiler | Profiling shows CPU or allocation bottlenecks | Diagnose actual hot paths before architecture rewrites |
| Python + pandas/matplotlib | Statistical comparison of multiple seeded CSV exports becomes tedious | **Offline analysis only**; no Python dependency inside the C# runtime |
| SQLite | JSONL/CSV history querying or volume becomes troublesome | Future persistence/query adapter, not part of the initial build |

### Tier C — Research references or later game development
- **NetLogo** and **Mesa**: useful references for agent-based modeling, emergence, and experiment design; **do not** add them as runtime dependencies or replace the initial C# core with Python/NetLogo.
- **Godot .NET**: visualization layer *after* the headless core passes tests. Maintain engine independence.
- More elaborate ECS engines, distributed simulation, cloud services, LLM agents, Docker, procedural art, and 3D assets: **not authorized in Prototype 001**.

Official reference links:
- .NET SDK/support: https://dotnet.microsoft.com/en-us/platform/support/policy
- C# editor: https://code.visualstudio.com/docs/languages/dotnet
- xUnit: https://xunit.net/
- GitHub .NET CI: https://docs.github.com/en/actions/tutorials/build-and-test-code/net
- BenchmarkDotNet: https://benchmarkdotnet.org/articles/guides/getting-started.html
- .NET profiling: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/profilers
- Mesa: https://mesa.readthedocs.io/stable/index.html
- NetLogo: https://www.netlogo.org/
- Godot C# docs: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html

### Tooling acceptance and safeguards
- First report which tools **Astra actually has access to**: terminal, filesystem, `dotnet --info`, Git, editor, package install permissions, repository access, test execution. Never assume connected tooling exists.
- Prefer pinned, compatible dependencies with documented versions. Avoid prerelease packages unless there is a specific demonstrated need.
- Use local Git commits even if GitHub connection is unavailable. Request user action only where permissions, credentials or installations require it.
- Do not consume time/tokens implementing dashboards or configuring broad automation before simulation fundamentals work.
- Always record build command, test counts/outcomes, benchmark environment, and output artifact paths in the milestone report.

## Vision vs. lore: what Astra needs to know

Read `GENESIS_WORLD_VISION.md` once to understand the eventual design **and the boundaries of what must remain emergent**. It documents our long-term direction, not Prototype 001 implementation requirements.

Instruction priority in a coding session:
1. Current approved milestone and its acceptance tests.
2. This Phase 0 architectural contract.
3. `GENESIS_WORLD_VISION.md` for conceptual direction and extension compatibility.
4. Historical brainstorm examples and speculative lore, **non-binding** until approved.

Astra should NOT invent fixed lore, tech trees, predetermined human/orc dominance, religion progression, divine character scripts, or species destinies. These belong to future discussions and must not contaminate the mechanics of Prototype 001.

At session start, Astra should briefly summarize the constraints, check tools, build the smallest vertical slice, and run actual tests. Do not paste the entire lore into every per-feature prompt.
