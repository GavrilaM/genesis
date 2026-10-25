# GENESIS — World Vision and Future Design Context

**Status:** Creative north star; future-systems reference, not a coding checklist.  
**Date:** 2026-10-25  
**Technical companion:** `GENESIS_PHASE0_BLUEPRINT_v2.md`

## The vision

**Simulate causes, not stories.**

Create an observer-first, open-ended fantasy universe where environments, species, individual decisions, cultural knowledge, civilizations, beliefs, magic and gods interact to produce histories nobody has scripted in advance.

The player can generate a primitive world, observe it at multiple scales for thousands of years, inspect individual lives and historical developments, and sometimes intervene. Observation is the central activity, not winning with one civilization.

## Two roles for the player

- **Observer:** an out-of-world perspective with pausing, inspection, historical queries, speed controls, comparisons, and alternate-world experiments.
- **Deity:** an in-world entity within a **Living Pantheon**. It may acquire followers, prophets, champions, domains, rivals, limited influence, and potentially be defeated or forgotten. The observer remains even if its divine identity vanishes.

The **Living Pantheon** direction is confirmed. The exact source of gods, rules of magic, powers, faith mechanics and warfare are **not yet settled**.

## Intended long-term systems

1. **Environment:** geography, weather, resources, habitats, wildlife, climate change, disaster and movement constraints.
2. **Species and evolution:** humans, orcs, elves and dwarves as configurable starting fantasy peoples; physiology, population dynamics and *lightweight* heritable adaptation. Do not make this a detailed biomedical or DNA simulator.
3. **Autonomous minds:** finite perception and knowledge, goals, memory, experience, relationships, social learning, cooperation, competition, and individual decisions.
4. **Knowledge and technology:** discovery through observation and experimentation, tools and infrastructure, cultural transmission, distortion, loss and rediscovery. Agriculture, metallurgy, writing, advanced science and magical techniques are *possibilities*, never age-gated inevitabilities.
5. **Architecture and society:** functional buildings made from useful parts and available resources; labor, trade, migration, settlements, governance, war, religion and culture gradually reshape them.
6. **Gods and faith:** pantheons, independent believers, varied interpretations, prophets with free will, divine champions, rival powers, religion splitting or fading, and societies that reject the supernatural.
7. **Forbidden knowledge:** a god may teach dark magic, advanced science or metaphysical techniques. Comprehension, requirements, resources, risks, beliefs and the choices of mortals determine consequences.
8. **History:** metrics and evidence-based event records support inspection and eventually an in-universe encyclopedia. A narrator may later express the recorded history, but cannot invent facts.

## Essential design constraints

- Human, orc, elf and dwarf cultures do **not** have hardcoded destinies or fixed progression rates. Environment, traits, learning, competition and historical contingencies decide which populations thrive.
- Separate *biological evolution*, *cultural learning* and *supernatural transformation*.
- An individual cannot act on knowledge they never acquired.
- A revelation of advanced knowledge does not magically provide the tools and infrastructure required to apply it.
- Prophets, scholars, champions and rulers retain agency, can misunderstand instructions, and can refuse their gods.
- Religion and divinity are distinct: one god can have many religions, and a religion can worship nonexistent or multiple gods.
- A distant century can be simulated as aggregates, but the system must not later fabricate detailed personal histories and claim they actually occurred.
- It is better to produce simple, tested causality than beautiful but fake procedural lore.

## Creative inspiration, not intellectual-property copying

The project draws *structural inspiration* from fantasy cosmologies such as the Aedra/Daedra relationship in The Elder Scrolls. Do not copy distinctive names, characters, locations, dialogue, or detailed world lore.

Tentative **original** divinity concepts, not implemented or officially settled:
- **Worldbound:** primordial forces entwined with natural laws.
- **Outsiders:** independent entities from other domains.
- **Thoughtborn:** possible divinities arising from collective belief.
- **Ascended:** former mortals that attain divine status.

Those are thought experiments and extension possibilities, **not mandatory Prototype 001 objects**.

## Examples that are NOT scripted canon

- Orcs invent agriculture first in one world, humans in another, while both can fail in a third.
- An isolated species develops sophisticated magic but little conventional industry.
- A prophet interprets a genuine divine act in a way the deity did not expect.
- Forbidden knowledge transforms a society but produces unintended political, ecological or moral consequences.

These define the *possibility space*. They are not events that the code should ensure happen.

## Prototype 001 hard boundary

Implement **only** the approved Phase 0 contract: 12 connected regions, four species definitions, environment and resources, cohorts, births/deaths, migration, limited inherited traits, monthly headless simulation, seeds, save/load, histories, audits, metrics, reproducibility tests and experiments.

**Do not yet implement:** detailed individual brains, cities, inventions, agriculture, functional buildings, deities, religions, divine magic, forbidden knowledge, graphics, or LLM-based world actors.

## How Astra should use this file

Read this once at project kickoff to understand long-term direction and prevent shortsighted decisions. The *current approved milestone* and technical blueprint take precedence. Before adding any future feature, consult the relevant section only as needed; never assume speculation is an approved requirement. Track future creative decisions with clear labels: **confirmed**, **proposed**, or **unresolved**.
