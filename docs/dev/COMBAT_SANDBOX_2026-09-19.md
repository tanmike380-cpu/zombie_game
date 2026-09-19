# RTS combat slice verification — 2026-09-19

Isolated scene: `Assets/_Tests/Combat/Scenes/Tech_CombatSandbox.unity`.
Unity 6000.6.1f1, macOS standalone, Apple M2 Pro. This is a functional test,
not a crowd performance benchmark or a final balance pass.

## Current playable slice

- 8 Shenji infantry, 36 Runners, 6 purple Exploders.
- Shared test speed multiplier 1.25: 3.5 / 4.5 / 4.25 tiles per second respectively.
- Shenji range 7; noise radius derives from range × 2 = 14.
- All 11 positive-range player-unit/tower entries in the balance YAML use 2× noise.
  Player contact explosions retain 8-tile noise pending separate tuning;
  zombie explosions explicitly produce no attraction noise.
- Low walls block walking but allow shots over them; high walls block sight/shots.
  Both permit hearing. No realistic acoustic simulation.
- Exploders telegraph contact detonation for .65 seconds. Every lethal cause also
  explodes, including remote gunfire. Radius 2, damage 70 to friendlies, high-wall
  occlusion. A world-owned expanding magenta ring lasts .85 seconds even when no
  soldiers are nearby. Explosions do not alert idle zombies. No infection conversion,
  zombie friendly fire or chain blast.

## Verification

Build entry `ZombieGame.EditorTools.CombatSandboxTools.build_player`: PASS.
Standalone `-combatSmoke` silent-explosion run: PASS, 60 shots / 53 hits / 16 enemy deaths in
the attack-move phase; one bite in the isolated melee fixture.
After applying the 10-tile human acquisition radius, the release rerun also passed:
51 shots / 46 hits / 13 enemy deaths; log `/tmp/zombie-combat-smoke-release.log`.

Assertions cover population/speed, enemy order rejection, reverse-selection rectangle,
move without auto-fire, immediate native path assignment, reversed velocity within
100 ms, patrol, stop drift below .1 tile, low/high-wall sight, 2× noise, investigation
behind a high wall beyond gun range, silence outside noise range, remembered source
after pulse expiration, hits/deaths/melee, clustered AOE victims versus outside-radius
survivor, contact explosion, lethal hit during fuse, remote death with no nearby
soldiers still creating an effect, no duplicate explosion, effect cleanup, and
silent explosions leaving idle zombies unaware.

These are command-level checks, not a measured end-to-end mouse latency benchmark.
The final playable scene was visually inspected. Native UI automation did not
reliably select units; mouse feel still relies on the user's hands-on validation.
The user had already accepted the prior movement version before the explosion update.

Final local logs: `/tmp/zombie-combat-build-final.log`,
`/tmp/zombie-combat-player-silent.log`. Logs/build binaries are not committed.

## Why movement changed

Explicit orders use Unity native synchronous path calculation/assignment in this
small sandbox instead of waiting for an asynchronous destination queue. Stop clears
velocity; acceleration is 100 tiles/s²; facing follows desired motion. Armed A/M/Q
commands respond on mouse-down. This does not add a custom pathfinding algorithm,
and must not be extrapolated to calculating 10,000 paths in a single frame.

## 中文摘要

8名神机营对36只快速僵尸及6只紫色自爆僵尸；双方移速同比提高25%。
神机营射程7、噪音14。紫色僵尸贴身自爆或被远程打死都爆炸一次；
附近没人也显示扩散圈。爆炸半径2、伤害70，暂不接感染转化。
构建及最终自动回归通过，玩法代码只放在独立 `_Tests/Combat` 目录。
自动测试暂时锁输入以避免试玩操作干扰，结束后恢复；普通启动不自动操作单位。
