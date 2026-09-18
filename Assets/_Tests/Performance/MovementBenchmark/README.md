# Movement and navigation benchmark

Isolated prototype; no production scenes or build settings are changed.

Open `Scenes/Tech_MovementBenchmark.unity` and press Play, or use
`Tools > Zombie Game > Movement > Open Movement Benchmark`.

- Red boxes: units; blue cells: walls; GOAL: destination.
- Right click an empty map cell to change destination.
- Space pauses; R resets; WASD/arrows pan; wheel zooms; F restores overview.
- Toggle obstacles switches between open terrain and the fixed wall layout.
- Run 6-case suite measures 1K/5K/10K in both layouts (5 s warmup + 20 s sample each).

The map is 256 x 256 one-unit cells. Four-neighbour reverse BFS computes one shared
shortest-path field per goal. Units follow cell centres at 12 units/s, simulated at
20 Hz. The two long staggered walls force a detour; the last wall has a 12-cell gap.
Deterministic starts occupy a 64-column strip on the left.

Units intentionally overlap and stack at the goal. There is no avoidance, crowd
pressure, animation, combat, dynamic building placement or networking. This is
not a NavMeshAgent-per-unit test. All units share a destination. Grid paths are
axis-aligned and the 20 Hz render positions are not interpolated yet.

## Repeatable validation

`Tools > Zombie Game > Movement > Validate Navigation` verifies 10K complete both
layouts and return journeys; checks every outward tick for wall occupancy,
out-of-map/NaN positions; rejects invalid/blocked goals; tests disconnected regions
and compares equal simulated durations at 10 versus 30 Hz. These are accelerated
functional tests, not rendered FPS measurements.

Batch entry point `ZombieGame.EditorTools.MovementBenchmarkTools.build_test_player`
runs validation, creates the test scene and builds only that scene to
`Builds/MovementBenchmark.app`. It does not alter the production build scene list.
The serialized instancing-enabled material must remain referenced by the scene,
otherwise standalone shader variant stripping can cause invisible geometry.

Run the Player with `-movementSuite` to start automatic sampling. Output is in
`Application.persistentDataPath/MovementBenchmark/performance.csv` and `hardware.txt`.
Frame timings use wall-clock intervals; simulation and draw submission use CPU
stopwatch timing (not GPU time). CSV includes resolution, frame percentiles and
arrival/error counters. Some open-map agents may arrive before the sample ends;
these are whole-scenario results, not an all-agents-always-moving guarantee.
Results do not yet include GC/allocated memory, field-rebuild latency, GPU timing
or repeated-run confidence intervals. GUI and all transforms are included in the
Player workload. Keep the window visible during measurements.

## 中文

这是独立的万人移动与共享网格寻路验证。打开本目录场景按 Play 即可。
红色为僵尸，蓝色为墙。右键改目标，空格暂停，R 重置，F 回到全图。
单位之间暂不碰撞，所以窄门处会重叠；不能据此认定真实拥堵问题已经解决。
功能验证检查能否到达以及是否进墙；独立 Player 的采样才用于性能观察。
