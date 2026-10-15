# V0.10 execution plan

Deadline: 2026-10-16 09:00 Asia/Shanghai. Stop new implementation at the deadline;
save the verified state and expose remaining failures, without forcing a release.

1. **Freeze and inventory.** Preserve `63946b1` under `v0.10.0-baseline`, use
   `codex/v0.10`, record source hashes and pre-existing unrelated dirty files.
   Blender specialist audits existing model/rig/action provenance read-only.
2. **Define the adapter.** Unity specialist maps the 128m environment activity
   plane to existing production map/navigation/control/building APIs. No production
   dependency on `_Tests`, no second battle controller, no model distortion.
3. **First playable slice.** Use a bounded population of existing supported units,
   shared balance and original clips; gate the new scenario behind explicit data.
   Preserve old scene behavior. First target is controllable defense and visible
   authored movement/attacks, not all future unit types or a ten-thousand-unit benchmark.
   Existing actors have no dedicated death/knife clips; disclose those gaps and do not
   claim generic whole-body motion is a finished authored animation.
4. **Test and iterate.** Compile; verify actual frames advancing; inspect normal RTS
   view and combat close-up; verify source proportions/materials/ground, navigation
   around the frozen wall, resumed siege intent, selection/HP/ammo feedback and input.
   Run existing applicable regressions, record active-combat timing separately.
5. **Freeze the delivered preview.** Save scene and traceable sources, build a local
   player, capture actual screenshots, list known issues, normal commit/push the version
   branch, add a new immutable preview tag only after the stated checks finish.

Root owns Unity/Blender GUI, Editor CLI, version records and Git. Blender and Unity
specialists have separate scoped file ownership; no concurrent GUI or same-file writes.
Do not touch the live unsaved Zombie file, old tag/main, old art-review scenes, shared
unit balance or approved contact constraints. Missing art is omitted or reported.

## 中文

先冻结起点、盘点已有模型和动作，再做坐标／地形／玩法适配；先交付一个人口受控、
可操作且有完整交战循环的防守版本。通过实际播放、截图及适用回归后再冻结交付版。
不要用“单独测试控制器”、未知数值或自造模型硬凑；不要改旧地图来掩盖新集成问题。
