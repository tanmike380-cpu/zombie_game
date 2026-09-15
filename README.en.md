# Zombie Game

**Language / 语言:** [中文](README.md) | **English**

A **1–2 player cooperative Survival RTS** set in a fictionalized ancient/medieval Chinese world.

Core experience:

> Build the economy → expand the base → mass an army → stockpile arrows and gunpowder → survive zombie hordes → mistakes trigger infection snowballs.

## Current Design Goals

- Top-down RTS. Single-player first, with the architecture prepared for 2-player co-op.
- PvE-focused rather than a traditional competitive RTS.
- Core differentiation: **cheap recruitment, expensive sustained warfare**.
- Ranged units are relatively inexpensive to recruit, but continuous combat consumes military supplies.
- Zombies are drawn toward battles and deaths through the Blood Scent system, creating a positive feedback loop from local skirmishes into large hordes.
- Buildings captured by zombies are converted into nests that continuously spawn enemies.

## Current Documentation

- `design/01_game_vision.md` / `design/01_game_vision.en.md`: positioning and design principles
- `design/02_resources.md` / `design/02_resources.en.md`: resource system
- `design/03_player_units.md` / `design/03_player_units.en.md`: player units
- `design/04_zombies.md` / `design/04_zombies.en.md`: zombie units
- `design/05_blood_scent_system.md` / `design/05_blood_scent_system.en.md`: Blood Scent and attraction mechanics
- `design/06_infection_system.md` / `design/06_infection_system.en.md`: unit infection and zombie nest mechanics
- `balance/initial_balance.yaml`: initial tunable balance values

## Bilingual Documentation Rules

- Every design document has both Chinese and English versions.
- Each document includes a `中文 | English` switch at the top.
- Machine-readable YAML/JSON configuration keeps a single source of truth. Keys stay in English, while descriptive text is provided in both Chinese and English so numerical values cannot drift between language copies.

## Versioning Principle

All current values are **Prototype / Alpha hypotheses**. They must be tuned later through simulation, playtesting, and telemetry.

Design priorities:

1. Simple rules
2. Easy to understand
3. Capable of producing large hordes and cascading failure
4. Feasible for one primary developer assisted by AI
5. Avoid turning the game into a complex logistics or pure management simulator
