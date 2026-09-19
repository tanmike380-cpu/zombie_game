# 400 vs 10,000 combat stress test

Open `Tools > Zombie Game > Combat > Open 400 vs 10000 Stress Test`, then Play.
Standalone: `Builds/CombatStress.app`. Build method:
`ZombieGame.EditorTools.CombatStressTools.build_player`.

This is a separate, scripted defensive-line benchmark, not the manual RTS sandbox.
400 stationary Shenji face 9,000 Runners and 1,000 Exploders on a 256×256 map.
Five rock obstacles and two impassable ponds are real NavMesh exclusions.
Grass/rock/water are simple geometry, not authored terrain art or elevation tests.

## Two measured cases

1. Normal noise, 30 seconds: radius 14, propagation 5 tiles/s; only heard/seen
   zombies activate. Most of the 10K population remains idle. This is not presented
   as 10K simultaneous combat.
2. Full assault, at most 60 seconds or until all soldiers die: prepare native paths
   before releasing all 10K on the same frame. This is an explicit global assault,
   **not a louder gunshot**. Setup/precalculation time is reported separately.

Both exclude the first 3 seconds from frame samples. CSV includes all sampled
frames and a separate subset with a combat event in the last .25 seconds. Results
also include activated/moving counts, casualties, shots/hits/blasts, path queue,
request failures, wall/pond/boundary violations, dropped bullets and GC collections.
`path_failures` counts failed initial complete paths / rejected destination submissions;
it does not mean that every later asynchronous request was audited for completion.

## Scope and limitations

- Human sight 10; zombies acquire visible targets within 4; guns range 7, noise 14.
- 256×256 one-tile fog grid, updated at 10 Hz: black unexplored, dark explored but
  currently unseen. Enemy models/projectiles/blasts are hidden outside current sight.
  Circle visibility is cell-centre sampled, without terrain-height vision occlusion.
- Native NavMesh medium avoidance. Spatial grids for combat broad phase, not custom
  pathfinding. Combat decisions at 10 Hz, at most 64 destination submissions/frame.
- Reusable projectile/flash buffers and instanced simple geometry. No animation,
  shadows, per-unit health bars, ammo economy, infection, buildings or networking.
- Damage: bullets 20/.8s; zombie melee 10/1s; explosions 70 in radius2.
  Every Exploder death detonates; zombie explosions generate **no attraction noise**.
  High rocks block shots/blast damage; shallow water does not block sight/shots.
- Human line is stationary. Noise stores an initial investigation destination;
  direct sight can replace it. General moving-squad sound arbitration is not tested.
- Dead units leave the simulation/render workload. Combat can end with most enemies
  alive. This cannot prove a sustained balanced 400-vs-10K battle at constant load.
- Fog culls hidden enemy drawing, **not their active navigation/combat simulation**.

## Viewing

F: whole map. C: frontline. Wheel: zoom. Arrow keys: camera pan only.
After the suite: V toggles spectator view without fog; R reruns both cases.
Spectator view is unavailable while measuring to preserve the fog-enabled case.
Normal RTS selection/A-click controls remain in the separate `Combat` sandbox.

Logs: `[CombatStress] READY / RESULT / COMPLETE`.
CSV/hardware notes: `Application.persistentDataPath/CombatStress/<timestamp>/`.

## 中文

独立自动防守压测：400火枪兵、9000快速僵尸、1000自爆僵尸，256×256地图，
含草地、五组岩石障碍、两处不可通行水域。人类视野10、僵尸4；枪射程7、噪音14。
迷雾每秒更新10次，隐藏视野外敌人；视野外正在进攻的僵尸依旧计算寻路和战斗。
自爆只有伤害和效果，不引怪。分别测正常声音触发与明确的全军进攻，不混淆两者。
这是简模、固定阵线的负载测试，不是完整兵种操作或正式游戏平衡测试。
