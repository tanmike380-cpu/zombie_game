# V0.10 — authored-model city defense integration

Status: **frozen preview 1 with known issues**, not final art/gameplay acceptance.

- Working branch: `codex/v0.10`.
- Frozen starting point: `63946b11b54fe85f9f92d1ecfca542531bb159aa`, tag `v0.10.0-baseline`.
- Deadline: 2026-10-16 09:00 Asia/Shanghai (01:00 UTC).
- [Plan](PLAN.md), [work log](WORKLOG.md), [snapshot manifest](manifest.json).
- Existing approved art-review entry remains `Assets/_Tests/EnvironmentShowcase/Scenes/EnvironmentDenseVision.unity`.
- New playable entry: `Assets/_Tests/V010Defense/Scenes/V010Defense.unity`.
- Local standalone: `Builds/V010Defense.app` (Mac only, not committed).
- Frozen local copy: `Builds/Versions/V0.10/ZombieGame-V0.10-preview1.app`.
- Immutable delivery tag: `v0.10.0-preview.1`; [rebuild notes](REBUILD.md).

The version reuses the user's approved Blender/Tripo actors and animations, the
production `FrontierGame`, `BattleSimulation`, `RtsBattleInput`, `UnitBalance` and
NavMesh/contact behavior. It does not authorize replacement world models, new
unapproved unit numbers, a render-pipeline migration or purchase/AI terms acceptance.

The golden no-frame fields, existing walls, six-layer ground, neutral daylight,
forest/mineral/building layout are the starting art baseline. Existing wild-grass
aggregate visibility remains below its unchanged threshold. The wall's source
irregularity and lack of an approved gate leaf remain known source limitations.

## 中文

本版把现有兵／僵尸和动作接入已冻结的城防地图。是第一版可操作防守预览，
不是全部问题已修复的正式成品。最终冻结标签与证据见 `manifest.json`、`TESTS.md`。

### 如何打开

1. Unity 打开正式项目 `/Users/tankaixi/Documents/ChatGPT/zombie game`。
2. 菜单 **Zombie Game → V0.10 → Open playable V0.10**；或打开上述场景后按 Play。
3. 进入后先是暂停说明，按开始按钮；正常流程有60秒部署、三波各80只僵尸。
4. 框选／F2全选，右键移动，A＋左键进攻；滚轮缩放，屏幕边缘移动，左下小地图可拖动镜头。
   空格暂停，B打开已有主基地面板。本版只开放已接入美术的弓箭手／希腊火招募，
   不开放缺少合规模型的建造项目。

### 内容与边界

- 62名已有弓箭手＋2辆已有希腊火，对普通尸、自爆尸、尸犬；沿用已有动作和正式战斗系统。
- 128米地图，保留整面城墙、12片青黄麦田、81棵树、矿脉／建筑布局和六层地面。
- 城墙可被打坏；主基地是总攻目标。北面是原模型的开放缺口，不是假造的可关闭城门。
- 农田、矿脉和中立废墟在本版仍是场景，不能把它当作完整的新资源采集系统。
- 模型／骨骼／动作源没有改动；麦穗只关闭投影，仍接收阴影，房屋与城墙投影保留。

### 必须知道的问题

- 自动防守长测仍有少量僵尸卡在农舍拐角。正常玩家进攻指令可清场；测试中的
  240／240胜利使用了180秒后的玩家A指令，**不是自动围攻停滞已修复**。
- M2 Pro锁屏后台1920×1080最终交战均值46.42毫秒、p95 107.45毫秒，仍偏重；前台体验和实体鼠标操作未实测，
  不宣称流畅，也不是万人压力测试。本轮未动已冻结接触逻辑来换帧率。
- 专用死亡／切刀动作仍缺，弩手／火枪的新动作未验收，未冒充已完成。
- 原模型墙体不齐、缺少门扇，以及旧野草可见性失败项都保留记录，未隐藏或降低标准。

旧地图、青黄满铺麦田、用户模型／骨骼／动作及已验收玩法保留。新集成使用正式
控制／战斗系统，不另造一套简化战斗，不因版本号便声称全部验收通过。

后续版本在此快照上迭代；冻结标签不移动。每个版本目录保留入口、源清单、变更、
实测与已知问题，不复制整套代码到另一份找不到的项目。
