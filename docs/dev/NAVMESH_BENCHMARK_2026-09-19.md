# Unity NavMesh initial test — 2026-09-19

Historical asynchronous-request run. The user correctly identified staggered
starts. The fixture was then revised to prepare complete native paths with
CalculatePath/SetPath and release all agents together. See the simultaneous-start
follow-up below; do not apply the old request-queue conclusion to that newer mode.

Implementation: `Assets/_Tests/Performance/NavMeshBenchmark`. Unity's native
NavMeshBuilder and NavMeshAgent are used, not the previous custom BFS.

Environment: Apple M2 Pro, macOS 15.7.4, Unity 6000.6.1f1, Metal,
standalone non-development Player at 1600 x 1000, VSync off, uncapped FPS.
The previous BFS Player was closed before measurement. One run per case;
these are initial observations, not repeated statistical guarantees.

## Protocol and limits

- Same 256 x 256 map, staggered walls and 12-unit gate.
- Native agent per GameObject, .25 radius, medium-quality avoidance enabled,
  speed 12, acceleration 40, 10,000 pathfinding iterations/frame.
- Native paths requested individually in one burst. Units stopped for at most
  30 seconds while requests resolve, then released even if some remain pending.
  Thus timed movement can INCLUDE pending path computation and staggered starts.
- Warmup 5 seconds, frame sample 20 seconds, then observe until every agent exits
  or 90 seconds after release. The observation endpoint is not necessarily the
  exact first all-arrived timestamp when all arrived during the sampling period.
- Crossing x >= 96 means reaching the exit region. Arrivals stop native simulation
  and avoidance but remain rendered. Average frame time therefore includes a
  changing active population; do not describe it as constant all-agent movement.
- Counters detect unit centres inside walls, out of map, NaNs and current invalid
  paths. This is not proof of perfect swept-radius collision or overlap avoidance.
- No animations, combat, dynamic obstacles, networking or fog of war. Frame time
  includes the test GUI and validation. GC deltas are collections, not bytes.

## Request burst observations

| Layout | Agents | Waiting time | Ready on release | Still pending on release |
|---|---:|---:|---:|---:|
| Open | 5,000 | 10.987 s | 5,000 | 0 |
| Open | 10,000 | 30.003 s (timeout) | 6,278 | 3,722 |
| Walls | 5,000 | 30.001 s (timeout) | 4,645 | 355 |
| Walls | 10,000 | 30.004 s (timeout) | 2,400 | 7,600 |

## Measured frames and arrival

| Layout | Agents | Average frame | p95 frame | p99 frame | Arrived at sample end | Arrived at observation end | Observation duration after release |
|---|---:|---:|---:|---:|---:|---:|---:|
| Open | 5,000 | 1.642 ms | 3.990 ms | 4.253 ms | 5,000 | 5,000 (100%) | 25.003 s |
| Open | 10,000 | 5.766 ms | 9.144 ms | 9.773 ms | 8,003 | 10,000 (100%) | 33.744 s |
| Walls | 5,000 | 5.064 ms | 5.627 ms | 5.886 ms | 0 | 5,000 (100%) | 47.449 s |
| Walls | 10,000 | 10.039 ms | 10.787 ms | 11.232 ms | 0 | 7,756 (77.56%) | 90.003 s timeout |

At each observation end, pending paths, invalid paths and unique wall/bounds
error counters were all zero. For 10K walls, 2,244 agents had not reached the
exit within the observation window. This does not prove permanent deadlock;
the Player continues simulating after saving that snapshot. Late path starts
and crowd congestion must be investigated separately.

Setup (bake + creation + request submission) took 118.923 / 124.367 / 98.154 /
115.888 ms respectively. GC0 collections during the samples were 99 / 44 / 26 /
28. Allocation profiling remains outstanding; do not claim a GC-free benchmark.

The request burst is already an important usability failure: the units do not
all respond promptly to an order. Good steady frame times do not resolve this.
The timeout cases are not all-ready start benchmarks.

Conclusion: the static routes are traversable and the renderer is not the sole
limiting factor, but this naive native-agent-per-unit / individual-request burst
configuration does NOT pass an immediate-response 10K crowd acceptance gate.
Before production adoption, profile native path processing versus avoidance,
test request scheduling/shared destinations using supported NavMesh APIs, and
evaluate congestion. Do not rewrite the pathfinding algorithm or disable avoidance
without labeling that as a separate workload.

## Visual check

The standalone window was brought to the foreground and inspected. Geometry
rendered correctly. Units approach corners diagonally and use the gate; they
still converge near narrow turns. Native local avoidance is enabled; it does
not guarantee a uniformly distributed horde or zero congestion.

Raw run folder: `Application.persistentDataPath/NavMeshBenchmark/20260919-050102`.
Build log: `/tmp/zombie-navmesh-build.log`; Player log: `/tmp/zombie-navmesh-player.log`.
Only the compact measured report is tracked, not binaries or machine logs.

## Follow-up: strictly simultaneous native-path release

Run `20260919-050827` replaced asynchronous SetDestination requests with Unity's
native synchronous CalculatePath + SetPath during setup. All agents remain stopped
until a strict gate confirms every path is complete. There is no replacement
pathfinding algorithm. All four cases logged ready == population, pending == 0,
invalid == 0 before releasing movement in one frame. A failed gate aborts release.

| Layout | Units | Setup incl. native paths | Readiness check | Average frame | p95 | p99 | Final arrived | Observation duration |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Open | 5,000 | 160.987 ms | 0.294 s | 1.666 ms | 4.139 ms | 4.376 ms | 5,000 | 25.004 s |
| Open | 10,000 | 222.001 ms | 0.015 s | 3.422 ms | 9.472 ms | 10.064 ms | 10,000 | 25.001 s |
| Walls | 5,000 | 181.235 ms | 0.009 s | 6.008 ms | 6.538 ms | 6.803 ms | 5,000 | 46.204 s |
| Walls | 10,000 | 288.925 ms | 0.016 s | 11.939 ms | 12.650 ms | 13.194 ms | 10,000 | 45.995 s |

All final pending, invalid and geometry counters were zero. Open-map agents had
all exited by the end of sampling, so those averages include retired agents;
both wall samples ended with zero arrivals (all agents still active). Wall sample
mean rates are approximately 166 FPS and 84 FPS respectively. GC0 collections
were 126 / 43 / 25 / 12; instrumentation allocation has not been isolated.
Spawn-strip row count changes with population, so the last-arrival times are not
a controlled comparison of congestion alone.

This fixes the observed request-order stagger for the pure movement test. It does
NOT simulate sound propagation, hearing range, reaction time or noise priority.
Synchronous preparation can cause a visible frame stall; its setup cost must not
be hidden in a real-time sound-response feature. Simultaneous orders do not imply
every agent can maintain speed through a congested corner. The main game still
needs a separate noise-response test with explicit hearing rules.

Follow-up logs: `/tmp/zombie-navmesh-sync-build.log` and
`/tmp/zombie-navmesh-sync-player.log`. Runtime output is in the corresponding
timestamped NavMeshBenchmark folder. Both builds succeeded.
