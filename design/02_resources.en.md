# Resources

**Language / 语言:** [中文](02_resources.md) | **English**

## Core Resources (6)

### 1. Food
- Primarily used for population growth and unit recruitment.
- The core base resource for mass recruitment.
- V1 does not introduce multiple food types.

### 2. Wood
- Primary material for standard buildings.
- Equipment material for Wooden Shield Guards.
- Raw material for arrow production.
- A major early- and mid-game economic resource.

### 3. Stone
- Primarily used for walls, defensive towers, forts, and other defensive structures.
- Should have little or no role in the technology tree and most normal unit production.
- Its role is similar to the defense-focused use of stone in Age of Empires.

### 4. Iron
- Used for advanced military buildings and equipment.
- Iron Shield Guards, firearm units, cannons, and related systems consume iron.
- A key raw resource for the gunpowder military-supply economy.

### 5. Arrows
- Ongoing combat resource used by Archers, Repeating Crossbowmen, Heavy Crossbowmen, and Heavy Ballistae.
- Primarily produced from wood.
- Heavy ballista shots consume multiple units of arrows.

### 6. Gunpowder
- Ongoing or one-time combat resource used by Firearm Infantry, Suicide Bombers, and Cannons.
- Primarily produced from iron; the exact recipe is subject to balance testing.
- A cannon shot should consume substantially more gunpowder than a firearm infantry shot.

## Non-stockpile Constraint

### Population
- Population is not a stockpile resource.
- Recruiting units consumes population capacity.
- Population cap is provided by houses and settlement buildings.

## Complexity Principle

Six resources are acceptable, but production chains must remain shallow:

- Wood → Arrows
- Iron → Gunpowder

Avoid chains such as:

- Wood → Planks → Arrow Shafts → Arrows
- Iron Ore → Iron Ingot → Steel → Barrel → Firearm

The goal is an RTS, not a complex production simulator.

## Economic Design Principles

- Base resources build and grow the state.
- Military supplies sustain warfare.
- Ranged units themselves are relatively cheap, but prolonged combat requires stable military-supply production.
- Players must choose between expanding the army and increasing supply production capacity.
