# RTS prototype development plan

This is the proposed sequence for this project, not a universal industry SOP.
The goal is to prove the expensive systems, then validate the game's core loop
before investing in content and final art.

## Current gate: static rendering

The 2026-09-18 Editor benchmark renders 10,000 static, 8-vertex placeholders at
2560 x 1440 on an M2 Pro. It does not validate 10,000 animated or simulated units.
Keep that scene as a regression baseline. See [the measured results](BENCHMARK_2026-09-18.md).

## Next task: TECH-002 — movement benchmark

Create a separate test scene under `Assets/_Tests/Performance/MovementBenchmark/`.
Keep the existing render benchmark unchanged as the control.

Scope:

- Fixed 256 x 256 map and repeatable seeded placement.
- 1K / 5K / 10K instances with per-unit position, speed and heading in arrays.
- Move toward a selected destination, stop within a defined arrival radius, and
  allow resetting to the same starting positions.
- Measure idle, moving across the map, and concentrating toward one destination.
- Add camera pan/zoom and a full-map preset so motion is visible and comparable.
- Separate fixed simulation ticks from rendering; compare candidate tick rates
  such as 10 / 20 / 30 Hz before selecting one.
- No navigation, unit collision, combat or skeletal animation in this gate.
  Overlap at the destination is expected and must be labelled as such.

Proposed acceptance criteria (not yet measured or approved as final hardware targets):

- All units move at the intended world speed independent of display FPS.
- Pause/resume and reset work; units remain within the map; no NaN positions.
- Record average/p95/p99 frame time, movement-update time, draw submission time,
  steady-state allocations, memory, resolution and visible/active unit counts.
- Use 10 seconds of warmup and 60 seconds of sampling, three runs per scenario.
- Aim for 60 FPS (16.67 ms total frame budget) on the current M2 Pro; define the
  low-end target machine before making a broader performance claim.
- Report failures and frame spikes; do not silently remove outliers.

Before making a shipping-performance decision, add a separate benchmark Player
build. The current runtime test class is guarded by `UNITY_EDITOR`, so it cannot
be measured unchanged in a Player. A dedicated benchmark build target/project
must opt into the test code while production builds keep it excluded.

## Subsequent gates

| Gate | Deliverable | What must be demonstrated |
| --- | --- | --- |
| TECH-003: navigation | Shared grid and a flow-field prototype; spatial-neighbour queries and simple separation | Walls, narrow gates, unreachable targets, and building placement/destruction do not cause hangs or unbounded queue growth; compare alternatives if profiling warrants it |
| Combat slice | One ranged soldier, one zombie type, wall and base; selection, movement, targeting, damage and death | A short defend-the-base scenario has understandable win/loss conditions; targeting avoids scanning every pair of units |
| Economy loop | Resource throughput, recruitment, arrow production and consumption | Building, recruiting and supplying combat form a playable 5–10 minute loop; running out of arrows changes combat |
| Noise and infection | Low-resolution noise grid, direct-target priority, death conversion and infected nests | Loud combat attracts nearby enemies; a breach can cause the intended local cascade without runaway spawn cost |
| Visual/visibility stress | Representative animation, terrain, effects, fog of war and culling | Performance holds with representative visible-unit density, not just tiny unanimated boxes |
| Co-op and persistence | Two command sources controlling a shared faction; save/load and reconnect policy | Resource and unit state remain consistent; conflicting orders have a defined rule |
| Content and production | More units, buildings, maps, UI, audio and balancing | The small playable slice is fun and stable before expanding its content |

From the first interactive prototype, use a command interface that is independent
of the local mouse/keyboard. This supports the existing Archon-lite direction.
Network implementation follows single-player loop validation, with an early
small replication experiment once movement/commands are stable.

Jobs/Burst and ECS are implementation options, not prerequisites. Profile a
simple array-based movement implementation first and optimize the measured cost.

## Measurement references

- [Unity profiling tools](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/performance-profiling-tools)
- [Profiling an application/Player](https://docs.unity.cn/Manual/profiler-profiling-applications.html)

---

# RTS 原型开发顺序

这是针对当前项目的建议流程，并非唯一的行业 SOP。
当前只验证了一万个静态极简模型的渲染。下一项是 **TECH-002：万人移动压力测试**，
保留原场景作对照，另建 `MovementBenchmark` 测试场景。

下一项包含：固定 256×256 地图、固定随机种子、1K/5K/10K 数量切换、点击目标后移动、
暂停和重置、镜头移动缩放，以及静止/全图移动/向同一目标聚集三种场景。
用数组集中更新位置，将模拟步长与画面帧率分开。此阶段允许单位重叠，尚不包含寻路、
碰撞、战斗和骨骼动画，避免同时引入多个性能变量。

验收建议：速度不受帧率影响、不越界、不出现非法位置；每档预热 10 秒、采样 60 秒、
重复三次，记录平均与 p95/p99 帧时间、移动计算耗时、内存、GC 分配及可见/活动数量。
先以当前 M2 Pro 达到 60 FPS 为候选门槛，再指定低配目标机。独立 Player 测量尚待补齐；
当前测试类使用 `UNITY_EDITOR` 隔离，需要专用测试构建方案才能运行于 Player。

后续顺序：

1. 网格寻路/流场与简易避让：验证墙、窄门、死路和动态建筑，不让一万只各自全图寻路。
2. 最小战斗：一类弓兵、一类僵尸、一段墙、一个基地，具备框选、下令和胜负。
3. 经济闭环：采集产能 → 造兵 → 制箭 → 消耗箭矢守城，验证“低成本爆兵、高成本开战”。
4. 声音与感染：大火力引怪、死亡转化、建筑变尸巢，验证突破后的扩散风险。
5. 代表性动画、地图细节、特效、战争迷雾与裁剪，重复压力测试。
6. 双人共享势力、存档与重连；从早期就让指令入口支持多个来源。
7. 在小型可玩版本成立后扩展兵种、建筑、美术和数值。

不要长时间停留在纯压力测试：移动与导航风险得到控制后，就应做一个可玩 5–10 分钟的
防守版本，让性能验证和玩法验证一起推进。
