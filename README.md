# Zombie Game

A **1–2 player cooperative Survival RTS** set in a fictionalized ancient/medieval Chinese world.

## Documentation

- [Tripo → Unity combat-animation workflow / 新动画路线](docs/dev/TRIPO_UNITY_ANIMATION_WORKFLOW.md): character binding in Tripo, approved weapon assembly and complex combat-animation prototyping in Unity; Blender remains the source-model and skinning-repair workspace.

- [Infantry roster and equipment / 步兵名单与装备](design/03_player_units.md): Tianchao and Byzantine each have four infantry types, each with militia, veteran and elite variants. Militia and veterans carry 30 rounds; elites carry **50 rounds total** and gain **one tile of attack range** over the same type's base range. Veteran/elite damage increases are intended but their amounts remain undecided; current damage is unchanged. These rules are authored only in `balance/unit_balance.json`; experience and promotion are not implemented by this documentation update.

- [Buildings and ages / 建筑与三个时代](design/08_buildings_and_ages.md): current faction building roster, era unlocks, outpost coverage rules and concept-art scope.

- [Concept art atlas / 概念图总册](art/concepts/README.md): versioned original images, faction/unit references, individual three-view sheets, and clearly separated historical drafts.

The repository now includes a Unity 6000.6.1f1 project. Open the repository root
in Unity and use `Tools > Zombie Game > Run 1K 5K 10K Benchmark` for the isolated
render test. See [the verified local benchmark](docs/dev/BENCHMARK_2026-09-18.md).

For playable RTS controls and Shenji/noise combat, open
`Tools > Zombie Game > Combat > Open Playable RTS Test`, then press Play.
See [controls and test scope](Assets/_Tests/Combat/README.md).

Large-map combat load test (400 vs 10K, fog and terrain):
`Tools > Zombie Game > Combat > Open 400 vs 10000 Stress Test`.
See [measured results and limitations](docs/dev/COMBAT_STRESS_2026-09-19.md).

English is the primary language. Each Markdown file contains the English version first and the Chinese version below in the same file.

- `design/01_game_vision.md`: game vision / 游戏愿景
- `design/02_resources.md`: resources / 资源系统
- `design/03_player_units.md`: player units / 玩家兵种
- `design/04_zombies.md`: zombie units / 僵尸兵种
- `design/05_noise_system.md`: Noise attraction system / 声音吸引系统
- `design/06_infection_system.md`: infection and nest system / 感染与尸巢系统
- `design/07_defensive_buildings.md`: defensive buildings / 防御建筑
- `balance/initial_balance.yaml`: prototype balance values / 原型数值配置
- `docs/dev/TECHNICAL_PROTOTYPE_01.md`: first Unity benchmark / 第一轮 Unity 性能测试
- `docs/dev/RTS_DEVELOPMENT_PLAN.md`: development gates and next movement benchmark / 开发顺序与下一轮移动测试

## Design Principle

> Recruit cheaply, fight expensively.

Current values are Prototype / Alpha assumptions and will be tuned through simulation and playtesting.

Current horde performance target:

- Normal large fights: several thousand zombies.
- Extreme benchmark / late-game target: up to roughly **10,000 active zombies**, subject to profiling on low- and mid-range hardware.

Important current combat rules:

- Weapon Noise radius is currently three times attack range; zombie explosions never emit attraction noise.
- The Archer can outrun slow/medium zombies, but Runner, Exploder, and Zombie Hound can catch it.
- Exploders punish tightly packed ranged formations; soldiers killed by their AOE can immediately trigger infection conversion.
- Defensive buildings share the same Arrow / Gunpowder economy as the field army.

---

# 中文说明

这是一款以中国古代/中古代架空背景为基础的 **1–2 人合作 Survival RTS**。

## 文档规则

最新步兵方案：天朝、拜占庭各4种，共8种步兵，每种分民兵／老兵／精英。民兵与老兵携弹上限均为30发；精英为**总计50发，不是额外50发**，同兵种基础射程增加1格。老兵、精英伤害递增方向已记录，具体加成待定，当前伤害不擅改。数值唯一来源为 `balance/unit_balance.json`；本轮不自动实现经验晋升或启用尚未完成的阵营单位。

人物保持空手 T-Pose，箭筒／弩矢袋／连弩箭盒／葫芦或水囊随人物一起交给 Tripo；仅手持武器单独生成并在 Blender 装配。每个兵种每个等级的正、侧、背都是独立图片，新图版本单独存放，旧图保留。详见[兵种设计](design/03_player_units.md)。

英文是主语言。每个 Markdown 文件只保留一份：上半部分英文，下半部分中文。

- `design/01_game_vision.md`：游戏愿景
- `design/02_resources.md`：资源系统
- `design/03_player_units.md`：玩家兵种
- `design/04_zombies.md`：僵尸兵种
- `design/05_noise_system.md`：声音吸引系统
- `design/06_infection_system.md`：感染与尸巢系统
- `design/07_defensive_buildings.md`：防御建筑
- `balance/initial_balance.yaml`：原型数值配置
- `docs/dev/TECHNICAL_PROTOTYPE_01.md`：第一轮 Unity 性能测试

## 核心设计原则

> 低成本爆兵，高成本开战。

当前所有数值均属于 Prototype / Alpha 初始假设，后续通过模拟和试玩调整。

当前尸潮性能目标：

- 常规大型战斗：数千只僵尸。
- 极端测试 / 后期目标：约 **10,000 只活动僵尸**，最终以低配和中配机器实际 Profiling 结果为准。

当前重要战斗规则：

- 人类武器 Noise 声音吸引半径统一为射程三倍；僵尸爆炸永不产生引怪声音。
- 弓箭手能跑过慢速/中速僵尸，但 Runner、爆裂尸、尸犬可以追上它。
- 爆裂尸负责惩罚密集远程阵型；被其 AOE 炸死的我方士兵会立刻触发感染转化。
- 防御建筑与野战军共享 Arrows / Gunpowder 军需库存。
