# Noise Attraction System

## Goal

Use combat noise as the primary long-range attraction mechanic for zombies.

> Attack Range and Noise Radius are separate values.

A weapon can have a 7-tile attack range but only a 3-tile Noise radius, or the reverse. Noise should never be interpreted as weapon range.

## Basic Rules

1. Ranged attacks and explosions generate Noise at the attacker / weapon position.
2. Louder weapons generate stronger Noise and attract zombies from farther away.
3. Noise decays quickly over time; sustained firing continually refreshes or accumulates local Noise.
4. A zombie with a directly detected target always prioritizes direct pursuit and combat over Noise.
5. A zombie without a direct target can use Noise to choose a direction or destination.
6. The final global assault does not depend on local Noise.

## Initial Prototype Noise Radii

These values are intentionally kept compact. **10 tiles is the current maximum normal local attraction radius.**

| Noise Source | Attraction Radius |
|---|---:|
| Melee combat | 1 tile |
| Archer shot | 2 tiles |
| Repeating Crossbow shot | 2 tiles per shot; sustained fire accumulates |
| Heavy Crossbow shot | 3 tiles |
| Heavy Ballista shot | 4 tiles |
| Firearm Infantry shot | 6 tiles |
| Player Suicide Bomber explosion | 8 tiles |
| Zombie Exploder explosion | 8 tiles |
| Cannon shot | 10 tiles |
| Arrow Tower shot | 2 tiles |
| Cannon Bastion shot | 10 tiles |
| Flame Bastion burst | 6 tiles; sustained use keeps Noise active |

There is no generic "building destruction = 15 tiles" rule in V1. If a future building or event needs Noise, it should define its own explicit value.

## Recommended Implementation

V1 uses a low-resolution **Noise Grid** rather than one object/event per shot.

Example:

```text
World Map
256 x 256 gameplay tiles

Noise Grid
64 x 64 cells
```

At a fixed interval:

1. Weapon fire writes or accumulates Noise into the weapon's grid cell.
2. The grid stores recent local intensity.
3. Noise decays rapidly.
4. Repeating weapons can accumulate intensity while they keep firing.
5. Idle zombies query nearby Noise cells and move toward stronger active Noise.
6. Once a direct player target is detected, Direct Target overrides Noise navigation.

## Weapon-Economy Interaction

```text
Stronger weapon
  -> higher damage
  -> higher Arrow/Gunpowder consumption
  -> usually more Noise
  -> more roaming zombies attracted
```

Noise is not required to increase perfectly with weapon range. A short-ranged but loud weapon can create more Noise than a quiet long-ranged weapon.

## Global Final Assault

When the final assault begins, remaining eligible zombie groups receive a global attack objective toward the player's territory. Local Noise is ignored as the trigger for that event.

## V1 Simplification Rules

Do not implement:

- realistic sound propagation physics
- echoes
- terrain acoustics
- per-zombie raycast hearing
- one persistent object per gunshot

Use a cheap grid, fast decay, accumulation for sustained fire, and Direct Target override.

---

# 声音吸引系统

## 目标

使用战斗产生的 **Noise / 声音**作为僵尸的主要远距离吸引机制。

> **攻击射程**和**Noise 吸引半径**是两套完全独立的数值。

例如神机营可以攻击 7 格，但声音只吸引 6 格；不要再把 Noise 数值理解成攻击距离。

## 基本规则

1. 远程攻击和爆炸在攻击者 / 武器所在位置产生 Noise。
2. 武器越响，吸引游荡僵尸的范围越大。
3. Noise 会快速衰减；持续射击会不断刷新或累积局部声音。
4. 僵尸一旦直接发现玩家单位/建筑，优先直接追击，不再依赖 Noise。
5. 没有直接目标时，才根据 Noise 选择移动方向或目标区域。
6. 最终总攻不依赖局部 Noise。

## 第一版 Prototype 声音范围

当前普通局部声音的最大吸引半径先限制在 **10 格**。

| 声音来源 | 吸引范围 |
|---|---:|
| 近战 | 1 格 |
| 弓箭手 | 2 格 |
| 连弩手 | 单发 2 格；持续射击可累积 |
| 重弩手 | 3 格 |
| 重弩炮 | 4 格 |
| 神机营火铳 | 6 格 |
| 我方自爆兵爆炸 | 8 格 |
| 爆裂尸爆炸 | 8 格 |
| 火炮 | 10 格 |
| 箭塔 | 2 格 |
| 铁炮要塞 | 10 格 |
| 喷火要塞 | 6 格；持续喷火会持续维持 Noise |

V1 删除“建筑大型破坏固定产生 15 格声音”的泛化规则。以后如果某个特殊建筑/事件需要声音，单独为它定义即可。

## 推荐实现

V1 使用低分辨率 **Noise Grid / 声音网格**，而不是每一发攻击都创建一个声音对象。

例如：

```text
游戏地图
256 x 256 gameplay tiles

Noise Grid
64 x 64 cells
```

固定时间间隔执行：

1. 武器开火时，把 Noise 写入/累加到武器所在的网格格子。
2. 网格保存最近的局部声音强度。
3. 声音快速衰减。
4. 连弩、喷火等持续攻击可以不断累积/刷新声音。
5. 没有直接目标的僵尸查询附近声音格子，向更强的有效 Noise 移动。
6. 一旦发现玩家单位，Direct Target 立即覆盖 Noise Navigation。

## 与军需经济联动

```text
武器越强
  -> 输出越高
  -> 箭矢/火药消耗通常越高
  -> 通常也越响
  -> 更容易把附近游荡僵尸拉进战斗
```

Noise 不需要和射程严格正相关。短射程但非常响的武器，也完全可以比安静的远程武器更容易引怪。

## 最终总攻

终局事件触发后，剩余符合条件的尸群直接获得全局进攻目标，向玩家领地发动总攻，不依赖当前是否存在声音源。

## V1 简化原则

不做真实声学、回声、地形声学、逐僵尸 Raycast 或每枪一个长期声音对象。

只做：Noise Grid + 快速衰减 + 持续攻击累积 + Direct Target 覆盖。
