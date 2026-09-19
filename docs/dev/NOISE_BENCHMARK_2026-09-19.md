# Ten-tile hearing test — 2026-09-19

User-approved scope: 10,000 units are present, but only zombies hearing a normal
ten-tile sound respond. No oversized source, smaller agents, or global order.
See `Assets/_Tests/Performance/NoiseBenchmark` and `design/05_noise_system.md`.

## Fixture and rules

- 256 x 256 map, unit radius .25, mesh width .45, ordinary-zombie speed 1.8 tiles/s.
- Units start at x=-104..-5 and z=-50..49, one tile apart. Source at (2,0).
- Two .5-wide staggered walls at x=-2.5 and x=-.5, each five tiles long.
- Circular horizontal range <=10, ten bands of 100%, 90%, ... 10%. Beyond 10: zero.
- One visual pulse emitted after 3 s, propagation 5 tiles/s, .6 s audible duration
  at each position. Hearing query interval .05 s. These are demo timing parameters.
- Gray units have no active NavMesh agent until hearing triggers; red listeners
  use Unity's native CalculatePath/SetPath and medium-quality avoidance. Wall
  geometry affects movement, not sound. This is a single analytic pulse fixture,
  not a complete multi-source Noise Grid or real acoustic/audio simulation.
- Listener memory survives pulse expiry. Units investigate the source and stop
  within their stopping distance; others can queue around the occupied source.

## Verified result

Standalone non-development Player, Unity 6000.6.1f1, Apple M2 Pro/Metal, 1600x1000,
near camera, VSync disabled. One initial run after correcting the idle-check baseline.

| Check | Result |
|---|---:|
| Total population | 10,000 |
| Expected listeners within ten tiles | 38 |
| Actual unique listeners | 38 |
| Out-of-range triggers | 0 |
| Native route failures | 0 |
| Unit centres entering walls | 0 |
| Untriggered units moving after spawn | 0 |
| Listeners moving after pulse expiry | 38 |
| At source stopping threshold after 28 s | 12 |
| First / last hearing after emission | 1.450 / 2.050 s |
| Pulse active when result saved | No |
| Mean / p95 / p99 frame interval | 1.539 / 1.689 / 2.014 ms |

The runtime acceptance check passed. Arrival of every listener at a single
occupied point is deliberately not required; 26 had not met the stopping-distance
criterion at the snapshot. No result is claimed for 10,000 simultaneous pathfinding
agents: only 38 were triggered, and the camera showed the local hearing area.

Editor validation also passed: every distance band, exactly ten tiles, a diagonal
ten-tile point, 10.001 tiles excluded, propagation timing, pulse expiry, weakest
valid sound creating memory, silence preserving it, new sources updating it, and
direct-target priority. The latter two are rule-level tests, not visible combat
or a multiple-source scenario. Crowding/overlap and swept collision are not fully
validated by the wall-centre check.

## Corrected instrumentation

The first runtime run falsely counted all 9,962 idle units as moved because the
comparison used the requested pre-spawn y=0 against native y=.05. Logged positions
confirmed x/z were unchanged. The check now compares against a snapshot captured
after native placement. The rerun reported idle_moved=0; the failed initial result
is not used as a successful validation.

Logs: `/tmp/zombie-noise-build.log`, `/tmp/zombie-noise-player.log`.
Timestamped reports: `Application.persistentDataPath/NoiseBenchmark/`.
R resets/replays, N emits again, C close camera, F whole-population overview.
