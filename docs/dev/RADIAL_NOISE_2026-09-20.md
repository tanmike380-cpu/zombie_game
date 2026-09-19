# Radial regroup + 3× weapon noise — 2026-09-20

This supersedes the first compact formation's unit-index-priority assignment.

## Movement command semantics

1. Collect compact walkable slots around the mouse target, nearest rings first.
2. Rank selected living units by distance to the mouse target, not unit id.
3. Assign the nearest units to inner radial bands and farther units to outer bands.
4. Within each band, minimum-squared-distance matching reduces crossing between
   approach directions. This is endpoint assignment; Unity NavMesh still calculates
   paths and performs local avoidance. All commands start together, without wave delays.

400-unit regression: two groups of 200 separated by >64 empty tiles. Checked all
unit pairs for near/far ring priority and verified every unit converged to within
13 tiles of the click. PASS. Measured destination allocation: **3.651 ms** on this
run (not total command latency, not an FPS benchmark; editor build also running).

## Balance

`balance/unit_balance.json`: human weapon noise multiplier **3**. Shenji range stays
7, noise becomes **21**, human sight stays 10. Speeds and damage unchanged. Zombie
explosions remain silent. The separate 10-tile diagnostic probe reads its own shared
`noise_probe_radius`; it is not a human weapon and must not inherit the 3× multiplier.

Both combat players embedded the same hash: `bb653f0e708650591e08178b7b42244b`.

- Four real zombie listeners placed 15.5 tiles north/south/east/west of a firing
  Shenji: initially outside fog visibility, all activated and approached after the
  propagation delay. A listener 22 tiles away stayed idle. PASS.
- Physical speeds: human 3.500, runner 4.500, exploder 4.250 tiles/s. AI chase gap
  decreased from 3.5 to 2.091 tiles over 2 seconds. PASS.
- Large smoke passed selection, groups, terrain rejection, obstacle detours, moving
  fog, commands and combat. Small smoke separately exercises gun/noise, LOS, melee,
  death and silent explosion behavior.
- Legacy probe build and boundary/delayed-wave/expiry/memory rules passed.

Actual multi-direction attraction requires zombies to exist in those directions;
the radius change does not spawn or awaken enemies outside the circle.
