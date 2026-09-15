# Zombie Game

一款以中国古代/中古代架空背景为基础的 **1–2 人合作 Survival RTS**。

核心体验：

> 运营资源 → 扩张基地 → 爆兵 → 储备箭矢/火药 → 抵抗尸潮 → 失误触发感染雪崩。

## 当前设计目标

- 俯视角 RTS，单机优先，架构预留 2 人合作。
- PvE 为主，不做传统竞技 RTS。
- 核心差异化：**低成本招兵，高成本持续作战**。
- 远程单位制造成本较低，但持续攻击需要消耗军需资源。
- 僵尸通过“血腥值”被战斗和死亡吸引，形成局部战斗 → 大规模尸潮的正反馈。
- 被僵尸占领的建筑会转化为持续产怪的僵尸巢穴。

## 当前文档结构

- `design/01_game_vision.md`：项目定位与设计原则
- `design/02_resources.md`：资源系统
- `design/03_player_units.md`：玩家兵种
- `design/04_zombies.md`：僵尸兵种
- `design/05_blood_scent_system.md`：血腥值与聚怪机制
- `design/06_infection_system.md`：士兵感染与建筑巢穴机制
- `balance/initial_balance.yaml`：第一版可调数值草案

## 版本原则

当前所有数值均属于 **Prototype / Alpha 初始假设**，后续需要通过模拟、试玩和 telemetry 调整。

设计优先级：

1. 规则简单
2. 玩家容易理解
3. 能制造大规模尸潮和雪崩
4. 单人 + AI 能完成
5. 避免演变成复杂物流/模拟经营游戏
