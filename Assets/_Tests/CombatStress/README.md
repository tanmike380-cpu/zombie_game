# 400 vs 10,000 combat stress test

Open `Tools > Zombie Game > Combat > Open 400 vs 10000 Stress Test`, then Play.
Standalone: `Builds/CombatStress.app`. Build method:
`ZombieGame.EditorTools.CombatStressTools.build_player`.

Default launch is a manually controlled RTS test: 400 Shenji face 9,000 Runners
and 1,000 Exploders on a 256×256 map, starting outside enemy hearing/sight.
The same capsule bodies and yellow muskets as the small Combat sandbox are drawn
with GPU instancing. No replacement cube units or imported character art.
F9 explicitly starts the scripted stationary defensive-line benchmark below;
F10 returns to a fresh manual game. CLI `-combatBenchmark` starts the auto suite.
Eight rock obstacles and two impassable ponds are real NavMesh exclusions.
Three staggered rock barriers lie between the starting army and the zombie field.
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
- Reusable projectile/flash buffers and instanced simple geometry/health bars.
  Living visible units have proportional green/yellow/red health bars; fog-hidden
  enemies have none. No animation, shadows, ammo economy, infection, buildings or networking.
- Damage: bullets 20/.8s; zombie melee 10/1s; explosions 70 in radius2.
  Every Exploder death detonates; zombie explosions generate **no attraction noise**.
  High rocks block shots/blast damage; shallow water does not block sight/shots.
- In the automatic benchmark the human line is stationary. Noise stores an initial investigation destination;
  direct sight can replace it. General moving-squad sound arbitration is not tested.
- Dead units leave the simulation/render workload. Combat can end with most enemies
  alive. This cannot prove a sustained balanced 400-vs-10K battle at constant load.
- Fog culls hidden enemy drawing, **not their active navigation/combat simulation**.

## Viewing

Left click/drag: select humans; Shift adds selection; Ctrl+A selects all humans.
Backquote (`~` key beside 1) or F2: select all living humans.
Ctrl+0–9: replace a control group; 0–9: recall; double-tap within .35s: recall and
center the camera. Dead soldiers are skipped; restarting clears all groups.
H toggles the diagnostic header (hidden by default). It never blocks box selection.
Right click: move only. A then left click: attack a visible enemy or attack-move.
Q then left click: patrol. M then left click: move. S: stop. Esc: cancel command.
Minimap shows the full 256×256 bounds: left pans the camera, right orders movement.
F: whole map. C: selected army/frontline. Wheel: zoom. Arrow keys: camera pan only.
R restarts manual play; F9 enters automatic testing; F10 returns to manual play.
After the suite: V toggles spectator view without fog; R reruns both cases.
Spectator view is unavailable while measuring to preserve the fog-enabled case.
CLI `-rtsSmoke` validates manual orders, selection, terrain rejection, moving fog
and attack/noise reactions, then resets and unlocks manual control.

Logs: `[CombatStress] READY / RESULT / COMPLETE`.
CSV/hardware notes: `Application.persistentDataPath/CombatStress/<timestamp>/`.

## 中文

默认可操作的独立测试：400火枪兵、9000快速僵尸、1000自爆僵尸，256×256地图，
含草地、八组岩石障碍、两处不可通行水域。三组新障碍在起点与敌军之间，必须绕行。
人类视野10、僵尸4；枪射程7、噪音14。
迷雾每秒更新10次，隐藏视野外敌人；视野外正在进攻的僵尸依旧计算寻路和战斗。
自爆只有伤害和效果，不引怪。分别测正常声音触发与明确的全军进攻，不混淆两者。
沿用小场景的人形胶囊和黄色火枪，不再用方块代替兵种。F9才进入固定阵线自动压测。
模型恢复后须重新测量性能，不直接沿用旧方块版本的帧率结论。

### Unity 新手启动

1. 顶部菜单 Tools → Zombie Game → Combat → Open 400 vs 10000 Stress Test。
2. 点顶部中央 ▶ Play 开始。旁边的暂停按钮不要保持蓝色。
3. 选择 Game 标签（Scene 是编辑地图，不是玩游戏），点游戏区域让键鼠生效。
4. 左键框选蓝色火枪兵，右键地面移动；按 A 后左键地面边走边攻击。
5. 滚轮放大看模型；C 回到部队；F 看整张地图。黑色部分是未探索迷雾。
6. 再点 Play 退出试玩。试玩中的位置和战斗状态不会保存到场景。
