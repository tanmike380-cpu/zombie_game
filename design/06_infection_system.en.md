# Infection & Nest System

**Language / 语言:** [中文](06_infection_system.md) | **English**

## Soldier Infection Rule

V1 does not use per-hit infection probability, incubation periods, or infection progress bars.

Current design:

- When a human unit is killed by a zombie, a small number of low-tier zombies spawn immediately at the death location.
- The death also generates a strong Blood Scent signal that attracts nearby zombies.
- Whether one or multiple zombies are spawned is not yet frozen and should be tested in the balance table.

Goal:

> A frontline mistake should not only remove one soldier; it can create new enemies and pull nearby zombie groups into the same fight.

## Building Infection Rule

When a building is captured/infected by zombies, it does not release a one-time burst only. It becomes a **Zombie Nest**.

The nest keeps spawning zombies until the player destroys or purifies it.

## Building Levels and Nest Levels

### Level 1
Examples: tents, small houses, and other low-tier population buildings.

After infection:
- Primarily spawns Walkers.
- Low spawn rate.
- Initial Blood Scent recommendation: 20.

### Level 2
Examples: standard houses and more developed urban buildings.

After infection:
- Can spawn Walkers + Runners.
- Medium spawn rate.
- Initial Blood Scent recommendation: 30.

### Level 3
Examples: large residences, advanced buildings, and important facilities.

After infection:
- Can spawn Runners + Brutes, with a low chance of special zombies after later testing.
- High spawn rate.
- Initial Blood Scent recommendation: 40.

## Core Risk Design

As the city develops:

- Population grows
- Advanced buildings become more common
- Infected nests become stronger

Therefore:

> The more prosperous the economy becomes, the more dangerous an internal collapse becomes if the defense line fails.

## Snowball Chain

```text
Frontline unit dies
  ↓
Low-tier zombies spawn immediately
  ↓
Blood Scent is generated
  ↓
Nearby roaming zombies are attracted
  ↓
Pressure on the defense line increases
  ↓
Zombies enter the city
  ↓
Buildings become Zombie Nests
  ↓
Nests continuously spawn enemies
  ↓
The city collapses from inside
```

## V1 Simplification Rules

Do not implement:
- Per-attack infection probability
- Incubation periods
- Disease stats
- Virus transmission simulation
- Multi-stage building infection progress bars

Keep only event-driven rules:
- Unit killed by zombie
- Building captured/infected by zombie
