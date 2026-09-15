# Game Vision

## Product Positioning

A **Survival RTS + Zombie Horde + 1–2 player co-op** set in a fictionalized ancient/medieval Chinese world.

It is neither a traditional PvP RTS nor a complex management simulation.

## Core Appeal

1. Grow from a small settlement into a militarized town.
2. Build the economy around food, wood, stone, and iron.
3. Ranged armies depend on arrows and gunpowder, creating an ongoing cost of warfare.
4. Armies can be recruited cheaply, but they lose combat effectiveness if the supply economy cannot keep up.
5. Zombie waves escalate in scale and can eventually reach battles involving tens of thousands of units.
6. Small mistakes can snowball rapidly through infected buildings and the Blood Scent attraction system.

## Core Design Thesis

> Recruit cheaply, fight expensively.

Traditional RTS games often make recruitment expensive while unit usage is effectively free. This project reverses that relationship:

- Recruiting ranged units mainly consumes food plus a small amount of equipment material.
- The major long-term cost is the arrows and gunpowder consumed during combat.
- Players must trade off army size, infrastructure, and military supply stockpiles.

## Development Boundaries

V1 does not currently include:

- Multiple civilizations
- PvP
- Naval warfare
- Diplomacy
- Complex logistics transportation
- Hero equipment systems
- Deep weather/season simulation
- Cavalry as a major core system
- Complex disease-spread simulation

## Co-op Direction

The preferred model is **Archon-lite**:

- Two players share one faction.
- Resources, population, buildings, technology, and armies are shared.
- Both players can issue commands.
- The core loop is validated in single-player first, but the underlying architecture should not hard-code a single input source.

---

# 游戏愿景

## 产品定位

中国古代/中古代架空背景的 **Survival RTS + Zombie Horde + 1–2 人合作**。

不是传统 PVP RTS，也不是复杂模拟经营游戏。

## 核心爽点

1. 从小聚落逐步发展为军事化城镇。
2. 通过粮食、木材、石料、铁矿建立经济基础。
3. 远程军队依赖箭矢和火药，形成持续作战成本。
4. 玩家可以低成本扩军，但如果军需跟不上，军队会失去战斗力。
5. 尸潮规模越来越大，最终形成万级单位的大规模防守。
6. 小失误可能通过感染建筑和血腥值吸引机制迅速雪崩。

## 核心设计 Thesis

> 低成本爆兵，高成本开战。

传统 RTS 往往是“造兵昂贵、使用免费”。本项目希望反过来：

- 招募远程兵主要消耗粮食及少量装备材料。
- 真正高昂的是战斗过程中的箭矢与火药消耗。
- 玩家必须在扩军、建设和军需储备之间做取舍。

## 开发边界

V1 暂不考虑：

- 多文明
- PVP
- 海战
- 外交
- 复杂物流运输
- 英雄装备系统
- 天气/季节深度模拟
- 骑兵主体系
- 复杂疾病传播数值

## 合作模式方向

倾向 Archon-lite：

- 两名玩家共享一个势力。
- 共享资源、人口、建筑、科技和军队。
- 两个玩家都可以下达命令。
- 单机优先验证核心玩法，但底层架构需避免写死“只有一个玩家输入源”。
