# Official Unity NavMesh benchmark

Open `Scenes/Tech_NavMeshBenchmark.unity` and press Play, or use
`Tools > Zombie Game > NavMesh > Open 5K 10K Test`.

This fixture uses **UnityEngine.AI.NavMeshBuilder + NavMeshAgent**, supplied by
the project's existing `com.unity.modules.ai`. It does not implement path search,
steering, or local avoidance. The separate AI Navigation package's NavMeshSurface
authoring component is not required for this programmatically built fixture.
The older MovementBenchmark/BFS scene is a separate historical baseline.

## Workload

- Four cases: 5K open, 10K open, 5K walls, 10K walls. No 1K performance stage.
- 256 x 256 map; same staggered wall extents and 12-unit gate as the BFS fixture.
- Native agent per GameObject, GPU-instanced cubes (no individual renderers).
- Agent radius .25, height 1.2, speed 12, acceleration 40, angular speed 720.
- Medium-quality native obstacle avoidance ON, deterministic priorities 30–69.
- Voxel size .1, climb .2; obstacles baked as non-walkable boxes.
- Path iteration budget explicitly 10,000/frame, restored when the test ends.
- Deterministic non-overlapping spawn strip. Goals distributed in the right-hand
  exit area. Crossing x >= 96 counts as arrival; the native agent is then disabled
  and its visible cube remains. This tests crowd throughput, NOT holding a formation
  or packing all units at one point. Arrived units no longer contribute avoidance.

## Measurement

Runtime setup (bake + spawn + requests) is timed separately. Agents remain stopped
while waiting up to 30 real seconds for all paths to complete; ready/pending/invalid
counts are logged. Movement then runs 5 seconds warmup + 20 seconds sampling.
Each case continues until all agents exit or 90 real seconds of movement elapse.
Report incomplete arrival as a timeout, never as a pass.

Wall-clock frame times include native simulation, rendering, GUI and validation.
The sample includes agents that may have arrived and ceased simulation; record
arrival count alongside timing. GC0 collection deltas are included; this is not
an allocation measurement. Build is a non-development standalone Player, VSync
off, frame rate uncapped. There is no animation, combat, physics-body simulation,
dynamic obstacle carving, networking or fog of war. One run is an initial baseline,
not a repeated statistical study or shipping performance guarantee.

Runtime checks count each unit once if its centre enters a wall, leaves map bounds,
or becomes NaN. This is not an exhaustive swept-volume collision test. Path validity
is monitored separately from still-pending requests. Local avoidance may create
real congestion and should not be silently disabled to improve FPS.

Results: `Application.persistentDataPath/NavMeshBenchmark/<timestamp>/results.csv`
and `hardware.txt`; individual case rows are flushed as they finish.

Build method: `ZombieGame.EditorTools.NavMeshBenchmarkTools.build_player` creates
only `Builds/NavMeshBenchmark.app`, without changing production build scenes.
The instancing-enabled material is a serialized reference to prevent stripping.
After tests finish: R replays 10K walls, Space pauses, WASD/arrows pan, wheel zooms,
F restores overview. Camera controls are locked during timed tests.

## 中文

本测试直接使用 Unity 官方 NavMesh，不包含自研寻路。
只跑 5000 和 10000 两档；默认开启中等质量避让。
红点为单位、蓝色为墙；到达地图右侧出口的单位退出避让计算。
先单独记录寻路请求耗时，再记录移动帧耗时；90 秒仍未全部到达会如实记录。
不要将此结果与旧版无避让、共享 BFS 的数字作同等负载比较。
