# V0.10 — authored-model city defense integration

Status: **in progress**, not yet a playable delivery or a new art acceptance.

- Working branch: `codex/v0.10`.
- Frozen starting point: `63946b11b54fe85f9f92d1ecfca542531bb159aa`, tag `v0.10.0-baseline`.
- Deadline: 2026-10-16 09:00 Asia/Shanghai (01:00 UTC).
- [Plan](PLAN.md), [work log](WORKLOG.md), [snapshot manifest](manifest.json).
- Existing approved art-review entry remains `Assets/_Tests/EnvironmentShowcase/Scenes/EnvironmentDenseVision.unity`.
- New playable entry: pending implementation and verification; do not claim the art-review scene is playable.

The version reuses the user's approved Blender/Tripo actors and animations, the
production `FrontierGame`, `BattleSimulation`, `RtsBattleInput`, `UnitBalance` and
NavMesh/contact behavior. It does not authorize replacement world models, new
unapproved unit numbers, a render-pipeline migration or purchase/AI terms acceptance.

The golden no-frame fields, existing walls, six-layer ground, neutral daylight,
forest/mineral/building layout are the starting art baseline. Existing wild-grass
aggregate visibility remains below its unchanged threshold. The wall's source
irregularity and lack of an approved gate leaf remain known source limitations.

## 中文

本版目标是把现有兵／僵尸和动作接入当前城防地图，做成能操作、能交战的一版。
现在仅冻结了起点，**尚未交付可玩V0.10**；完成后补充唯一启动入口和实测截图。

旧地图、青黄满铺麦田、用户模型／骨骼／动作及已验收玩法保留。新集成使用正式
控制／战斗系统，不另造一套简化战斗，不因版本号便声称全部验收通过。
