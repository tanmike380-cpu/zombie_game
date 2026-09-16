# Noise Attraction System

## Goal

Use combat noise as the primary long-range attraction mechanic for zombies.

The key rule is simple:

> The more powerful and sustained the player's firepower is, the farther away it can attract roaming zombies.

Noise is generated at the **attacker / weapon position**, not at the corpse position. This prevents zombies from pathing toward a dead target while ignoring the actual player army that produced the threat.

## Basic Rules

1. Ranged attacks and explosions generate Noise at the attacker's current position.
2. Louder weapons generate stronger Noise and attract zombies from farther away.
3. Noise decays quickly over time; sustained firing continually refreshes it.
4. A zombie with a directly detected target always prioritizes direct pursuit and combat over Noise.
5. A zombie without a direct target can use Noise to choose a direction or destination.
6. When the zombie reaches the last known noisy area and the Noise has disappeared, it returns to normal roaming unless it detects a target or a new Noise source.

## Initial Prototype Noise Levels

All numbers are placeholders and must be tuned after map scale and movement speed are known.

| Source | Initial attraction radius |
|---|---:|
| Melee combat | 2 tiles |
| Archer shot | 4 tiles |
| Repeating Crossbow shot | 6 tiles |
| Heavy Crossbow shot | 7 tiles |
| Heavy Ballista shot | 10 tiles |
| Firearm Infantry shot | 15 tiles |
| Building collapse / major destruction | 15 tiles |
| Suicide Bomber explosion | 20 tiles |
| Cannon shot | 30 tiles |

The values do **not** mean every attack must individually search all zombies within the radius.

## Recommended Implementation

V1 should use a low-resolution **Noise Grid** rather than one object/event per shot.

Example:

```text
World Map
256 x 256 gameplay tiles

Noise Grid
64 x 64 cells
```

At a fixed interval:

1. Weapon fire writes or accumulates noise into the shooter's grid cell.
2. The grid keeps the strongest/recent noise values.
3. Noise decays rapidly over time.
4. Optional limited diffusion can spread noise into nearby cells.
5. Idle zombies query nearby grid cells and move toward stronger noise.
6. Once a direct player target is detected, direct targeting overrides Noise navigation.

This allows thousands of attacks to be aggregated without thousands of independent perception queries.

## Weapon-Economy Interaction

Noise is intentionally linked to the military-supply system:

```text
Stronger weapon
  -> higher damage
  -> higher Arrow/Gunpowder consumption
  -> more Noise
  -> more zombies attracted
```

Therefore late-game firepower is not a pure upgrade. Cannons and firearms can solve an immediate horde while increasing local pressure from roaming zombies.

## Global Final Assault

The final assault does **not** depend on local Noise.

When the end-game global assault begins, remaining eligible zombie groups receive a global attack objective toward the player's territory. This guarantees the final horde attacks even if the player is temporarily quiet.

## V1 Simplification Rules

Do not implement:

- realistic sound propagation physics
- echoes
- terrain acoustics
- per-zombie raycast hearing
- one persistent object per gunshot

Use a cheap grid, fast decay, and direct-target override.

---

# 声音吸引系统

## 目标

使用战斗产生的**声音 / Noise**作为僵尸的主要远距离吸引机制。

核心规则：

> 玩家的火力越强、持续开火越久，就越容易把更远处的游荡僵尸吸引过来。

声音产生在**攻击者 / 武器所在位置**，而不是僵尸尸体的位置。这样可以避免远程单位射杀僵尸以后，其他僵尸只跑到尸体旁边，却没有继续向真正的玩家军队发动攻击。

## 基本规则

1. 远程攻击和爆炸在攻击者当前位置产生 Noise。
2. 武器越响，声音吸引范围越大。
3. Noise 会快速随时间衰减；持续开火会不断刷新声音。
4. 僵尸一旦直接发现玩家单位，优先直接追击和攻击，不再依赖声音寻路。
5. 没有直接目标的僵尸，可以根据 Noise 判断移动方向或目标区域。
6. 僵尸抵达最后的声音区域以后，如果声音已经消失且没有发现目标，则恢复游荡或等待新的声音源。

## 第一版测试声音范围

以下全部是 Prototype 占位值，后续必须根据地图尺寸、移动速度和实战重新调整。

| 声音来源 | 初始吸引范围 |
|---|---:|
| 近战 | 2 格 |
| 弓箭手 | 4 格 |
| 连弩手 | 6 格 |
| 重弩手 | 7 格 |
| 重弩炮 | 10 格 |
| 神机营火铳 | 15 格 |
| 大型建筑倒塌/破坏 | 15 格 |
| 自爆兵 | 20 格 |
| 火炮 | 30 格 |

这些范围不意味着每次攻击都要遍历范围内的全部僵尸。

## 推荐实现

V1 使用低分辨率 **Noise Grid / 声音网格**，而不是每一发攻击都创建一个独立声音对象。

例如：

```text
游戏地图
256 x 256 gameplay tiles

Noise Grid
64 x 64 cells
```

固定时间间隔执行：

1. 武器开火时，把声音写入/累加到射手所在的 Noise Grid 格子。
2. 网格保存较强、较新的声音值。
3. 声音快速衰减。
4. 必要时只向附近格子做简单扩散。
5. 没有直接目标的僵尸查询周围声音格子，向更高 Noise 的方向移动。
6. 一旦发现玩家单位，Direct Target 立即覆盖 Noise Navigation。

这样即使几千个单位不断攻击，也不需要生成几千套独立感知计算。

## 与军需经济的联动

```text
武器越强
  -> 输出越高
  -> 箭矢/火药消耗越高
  -> Noise 越大
  -> 吸引来的僵尸越多
```

因此后期火器不是纯粹的无脑升级。火炮和神机营可以迅速处理眼前尸潮，但也可能把附近更远的僵尸一起吸引过来。

## 最终总攻

最终总攻**不依赖局部 Noise**。

当终局事件触发后，所有符合条件的剩余尸群直接获得全局攻击目标，向玩家领地发动总攻。这样即使玩家暂时没有开枪，也不会出现终局僵尸停在地图角落不进攻的问题。

## V1 简化原则

不做：

- 真实声学传播
- 回声
- 地形声学
- 每只僵尸单独做声音 Raycast
- 每一枪生成长期存在的声音对象

只做：低成本 Noise Grid + 快速衰减 + 发现直接目标后切换 Direct Target。
