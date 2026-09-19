# Shared balance and regroup regression — 2026-09-20

## Fixes

- Previous large-scene movement translated each soldier's offset from the selection
  centroid, preserving all empty space between disconnected squads. It was destination
  assignment, not an invisible obstacle in NavMesh. Both combat scenes now allocate
  compact walkable slots around the clicked location; native NavMesh still routes them.
- Combat stats are authored only in `balance/unit_balance.json`; runtime code and build
  snapshot generation live under `_Game`, not `_Tests`. Migrated small combat, large
  combat, fog, legacy movement, NavMesh and noise fixtures; removed duplicated combat
  numbers from the design YAML. Draft/unimplemented units retain unknown HP/damage.
- Combat zombie configured top speeds were already above Shenji. Chase updates formerly
  waited .5 seconds / 1 tile and agents braked toward stale targets. Shared chase interval
  is now .15 seconds, movement threshold .2 tile, and direct pursuit disables auto braking.
  Acceleration is also shared (100 for the three implemented combat roles). Crowd avoidance,
  obstacles, contact attacks and exploder wind-up still legitimately interrupt movement.

## Verification

Unity 6000.6.1f1, macOS standalone; both scene builds passed.
Embedded configuration hash in both players: `3322618c512bbd8cbe6445a196a99321`.

- Isolated three-lane test, .5-second acceleration warmup then 1-second displacement:
  Shenji **3.500**, Runner **4.500**, Exploder **4.250 tiles/s**.
- Actual AI pursuit of a moving Shenji: initial gap **3.5**, after 2 seconds **2.093 tiles**.
- Regroup fixture: **400** soldiers in two groups of **200**, >64-tile empty gap;
  all 400 valid compact goals, then all reached within 13 tiles of the click before
  the 25-second deadline. The tolerance accounts for the physical size of a 400-unit block.
- Large-scene smoke passed: commands, stop, patrol, selection, groups, terrain rejection,
  obstacle detour, fog, attack/noise loop (4 shots / 4 hits / 95 activated in the fixture).
- Small-scene smoke passed: movement/reversal, wall LOS, noise, combat, melee, health/death,
  all explosion causes, AOE limits and silent exploder deaths. Final fixture: 63 shots,
  56 hits, 18 kills, 1 melee hit. Timing-sensitive combat totals are not balance targets.

This is correctness verification, not a new FPS benchmark. Movement/AI changes mean
old timing reports must not be presented as new-version performance results.
