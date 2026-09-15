# Game Vision

**Language / 语言:** [中文](01_game_vision.md) | **English**

## Product Positioning

A **Survival RTS + Zombie Horde + 1–2 player co-op** set in a fictionalized ancient/medieval Chinese world.

It is neither a traditional PvP RTS nor a complex management simulation.

## Core Appeal

1. Grow from a small settlement into a militarized town.
2. Build the economy around food, wood, stone, and iron.
3. Ranged armies depend on arrows and gunpowder, creating an ongoing cost of warfare.
4. Armies can be recruited cheaply, but they lose combat effectiveness if the supply economy cannot keep up.
5. Zombie waves escalate in scale and can eventually reach battles involving tens of thousands of units.
6. Small mistakes can snowball rapidly through infected buildings and the Blood Scent attraction system.

## Core Design Thesis

> Recruit cheaply, fight expensively.

Traditional RTS games often make recruitment expensive while unit usage is effectively free. This project reverses that relationship:

- Recruiting ranged units mainly consumes food plus a small amount of equipment material.
- The major long-term cost is the arrows and gunpowder consumed during combat.
- Players must trade off army size, infrastructure, and military supply stockpiles.

## Development Boundaries

V1 does not currently include:

- Multiple civilizations
- PvP
- Naval warfare
- Diplomacy
- Complex logistics transportation
- Hero equipment systems
- Deep weather/season simulation
- Cavalry as a major core system
- Complex disease-spread simulation

## Co-op Direction

The preferred model is **Archon-lite**:

- Two players share one faction.
- Resources, population, buildings, technology, and armies are shared.
- Both players can issue commands.
- The core loop is validated in single-player first, but the underlying architecture should not hard-code a single input source.
