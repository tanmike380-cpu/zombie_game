# Blood Scent System

**Language / 语言:** [中文](05_blood_scent_system.md) | **English**

## Goal

Use a simple, low-cost, scalable hidden value to represent the rule that areas with more bloodshed attract larger groups of zombies.

System name: **Blood Scent / 血腥值**.

## Basic Rules

- Humans, zombies, and buildings can generate Blood Scent when they die or become infected.
- Blood Scent is not a single global value. It is a local stimulus attached to positions on the map.
- A larger value attracts zombies from a greater distance.
- Blood Scent decays over time and/or distance.
- If a zombie already detects a nearby direct target, direct pursuit takes priority over Blood Scent guidance.

## Initial Attraction Radius Draft

All values below are prototype test values:

- Zombie death: 1–2 tiles
- Normal soldier death: 10 tiles
- Advanced soldier death: 15 tiles
- Level 1 building infected: 20 tiles
- Level 2 building infected: 30 tiles
- Level 3 building infected: 40 tiles

These values must later be recalibrated against map size, movement speed, and typical battle duration.

## Recommended Implementation

V1 should not simulate realistic scent physics.

Use a low-resolution heatmap/grid:

1. When an event occurs, write Blood Scent into the corresponding grid cell.
2. Decay the value at a fixed interval.
3. If needed, diffuse part of the value into neighboring cells.
4. Zombies without a direct target only query nearby grid cells and move toward stronger Blood Scent.

This avoids having every zombie repeatedly scan all previous death events.

## Event Priority

Primary Blood Scent sources:

- Human soldier death
- Advanced unit death
- Zombie death at low weight
- Building captured/infected by zombies
- Continuous large-scale combat

V1 does not add a separate gunshot/noise system in order to avoid duplicated mechanics.

## Core Positive Feedback Loop

```text
Human dies
  ↓
High Blood Scent is generated
  ↓
Nearby wandering zombies converge
  ↓
The battle grows
  ↓
More humans die
  ↓
Blood Scent increases further
  ↓
A local skirmish snowballs into a horde
```

Goal: allow a small player mistake to escalate naturally while keeping the underlying rule easy to understand.
