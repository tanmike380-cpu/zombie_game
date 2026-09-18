# Navigation library selection — 2026-09-19

User direction: do not extend a home-grown pathfinder. Test only 5,000 and 10,000
units. The four-neighbour BFS scene remains a historical functional baseline,
not the intended game navigation implementation.

## Candidates

- Unity NavMesh / AI Navigation: official navigation mesh, path following and
  local avoidance. No additional third-party pathfinding asset purchase. Useful
  baseline; capacity at 10K agents has not been measured in this project.
- A* Pathfinding Project Pro: FollowerEntity uses ECS; RVO/ORCA local avoidance
  is a Pro feature. Strong candidate for the crowd prototype, conditional on a
  valid license and measured results. Do not substitute the old free package
  and claim it includes Pro's crowd features.

No licensed third-party package has been purchased or imported. No performance
comparison between these libraries has been run yet. The earlier BFS Player
results must not be presented as NavMesh or A* results.

## Evaluation protocol

For each available library run 5K and 10K on the same 256 x 256 map, with open
terrain, staggered walls and a narrow gate. Observe direct travel on open ground,
wall clearance, crowd spreading, stuck agents, path request completion, arrival
rate and frame-time percentiles. Compare avoidance on/off separately and record
settings; do not conceal performance losses by disabling avoidance. A shared
arrival region or individual spaced destinations are needed: 10K finite-radius
agents cannot all occupy a single point simultaneously.

Include path-request bursts and steady movement, distinguish pending paths from
unreachable paths, record setup time and resolution, repeat runs, and preserve
the visible standalone-player workflow. Add dynamic obstacle tests after the
static scene works. Keep integration under `Assets/_Tests`.

## Primary sources

- https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/NavInnerWorkings.html
- https://arongranberg.com/astar/docs/followerentity.html
- https://arongranberg.com/astar/docs/localavoidance.html
- https://arongranberg.com/astar/download
- https://arongranberg.com/astar/freevspro
