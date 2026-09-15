# Resources

## Core Resources (6)

### 1. Food
- Primarily used for population growth and unit recruitment.
- The core base resource for mass recruitment.
- V1 does not introduce multiple food types.

### 2. Wood
- Primary material for standard buildings.
- Equipment material for Wooden Shield Guards.
- Raw material for arrow production.
- A major early- and mid-game economic resource.

### 3. Stone
- Primarily used for walls, defensive towers, forts, and other defensive structures.
- Should have little or no role in the technology tree and most normal unit production.
- Its role is similar to the defense-focused use of stone in Age of Empires.

### 4. Iron
- Used for advanced military buildings and equipment.
- Iron Shield Guards, firearm units, cannons, and related systems consume iron.
- A key raw resource for the gunpowder military-supply economy.

### 5. Arrows
- Ongoing combat resource used by Archers, Repeating Crossbowmen, Heavy Crossbowmen, and Heavy Ballistae.
- Primarily produced from wood.
- Heavy ballista shots consume multiple units of arrows.

### 6. Gunpowder
- Ongoing or one-time combat resource used by Firearm Infantry, Suicide Bombers, and Cannons.
- Primarily produced from iron; the exact recipe is subject to balance testing.
- A cannon shot should consume substantially more gunpowder than a firearm infantry shot.

## Non-stockpile Constraint

### Population
- Population is not a stockpile resource.
- Recruiting units consumes population capacity.
- Population cap is provided by houses and settlement buildings.

## Complexity Principle

Six resources are acceptable, but production chains must remain shallow:

- Wood → Arrows
- Iron → Gunpowder

Avoid chains such as:

- Wood → Planks → Arrow Shafts → Arrows
- Iron Ore → Iron Ingot → Steel → Barrel → Firearm

The goal is an RTS, not a complex production simulator.

## Economic Design Principles

- Base resources build and grow the state.
- Military supplies sustain warfare.
- Ranged units themselves are relatively cheap, but prolonged combat requires stable military-supply production.
- Players must choose between expanding the army and increasing supply production capacity.

---

# 资源系统

## 核心资源（6种）

### 1. Food / 粮食
- 主要用于人口增长与招募单位。
- 是爆兵的核心基础资源。
- 当前不设计复杂食物种类。

### 2. Wood / 木材
- 普通建筑核心材料。
- 木盾兵装备材料。
- 箭矢生产原料。
- 前中期经济核心。

### 3. Stone / 石料
- 主要用于城墙、防御塔、堡垒等防御性建筑。
- 尽量不参与科技树和多数普通单位生产。
- 定位接近《帝国时代》中偏防御用途的石料。

### 4. Iron / 铁矿
- 高级军事建筑和高级装备材料。
- 铁盾兵、火铳单位、火炮等会消耗铁。
- 火药军需体系的重要基础资源。

### 5. Arrows / 箭矢
- 弓箭手、连弩手、重弩手、重弩炮的持续作战资源。
- 主要由木材转换得到。
- 重型弩炮单次射击会消耗多份箭矢。

### 6. Gunpowder / 火药
- 神机营、自爆兵、火炮的持续/一次性作战资源。
- 主要由铁矿转换得到；具体配方后续平衡。
- 火炮单次射击消耗应显著高于火铳兵。

## 非库存型约束

### Population / 人口
- 不算库存资源。
- 单位招募会占用人口。
- 人口上限通过房屋/聚落建筑提供。

## 复杂度原则

资源数量可以有 6 种，但资源链必须浅：

- Wood → Arrows
- Iron → Gunpowder

避免复杂多层加工链。目标是 RTS，而不是复杂生产模拟。

## 经济设计原则

- 基础资源负责“发展国家”。
- 军需资源负责“维持战争”。
- 远程单位本体相对便宜，但持续作战需要稳定军需供给。
- 玩家需要在“继续爆兵”和“提升军需产能”之间权衡。
