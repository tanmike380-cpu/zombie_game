# Player Units

> Prototype values only. Noise attraction radius is currently **2 × attack range** for ranged weapons (see `05_noise_system.md`).

## Movement / Range Baseline

All human units use **10-tile sight**, independent of weapon range and noise.

The Archer is intentionally faster than slow and medium zombies so the player can reposition, but it must **not** outrun every special zombie. Runner, Exploder, and Zombie Hound are intended to punish endless kiting.

| Unit | Move Speed (tiles/s) | Attack Range (tiles) |
|---|---:|---:|
| Wooden Shield Guard | 2.6 | 1 |
| Iron Shield Guard | 2.2 | 1 |
| Suicide Bomber | 3.5 | 0 (contact detonation) |
| Archer | 3.2 | 5 |
| Repeating Crossbowman | 3.0 | 4 |
| Heavy Crossbowman | 2.8 | 7 |
| Firearm Infantry / Shenji Battalion | 2.8 | 7 |
| Heavy Ballista | 1.5 | 10 |
| Cannon | 1.3 | 10 |

These values should be tested in real gameplay before being treated as balance targets.

## Melee Units

### 1. Wooden Shield Guard
Role: low-cost expendable frontline tank.

- Main cost: Food + Wood.
- Uses no ammunition.
- High defense, low damage.
- Main purpose: delay hordes and protect ranged firepower.
- Losses are tactically acceptable, but deaths can trigger infection mechanics.

### 2. Iron Shield Guard
Role: higher-tier heavy frontline tank.

- Main cost: Food + Iron.
- Uses no ammunition.
- Higher HP / Armor than the Wooden Shield Guard.
- Slower than the Wooden Shield Guard but more population-efficient.

### 3. Suicide Bomber
Role: gunpowder-era disposable area damage.

- Main cost: Food + Gunpowder, plus a small equipment cost if needed after testing.
- Has **0 ranged attack distance**: it must physically reach the enemy before detonating.
- The unit always dies.
- Intended for emergency breach control and high-density zombie groups.
- Its explosion can still generate combat Noise; attack range and Noise radius are separate values.

## Ranged Units

### 4. Archer
Role: basic ranged unit.

- Recruitment cost is mostly Food plus a small amount of Wood.
- Each attack consumes Arrows.
- Attack range: **5 tiles**.
- Move speed is high enough to reposition away from Walkers, Brutes, Spitters, and Giants, but not from the fastest special zombies.
- Main early-game ranged unit.

### 5. Repeating Crossbowman
Role: close-to-mid-range burst ranged unit.

- Recruitment cost: Food + a small amount of Wood / Iron, subject to testing.
- Each attack consumes Arrows.
- Attack range: **4 tiles**, shorter than the Archer.
- Very high attack speed and low damage per projectile.
- Main characteristic: close-range burst damage with extremely fast arrow consumption.

### 6. Heavy Crossbowman
Role: slow-firing, high single-shot damage.

- Recruitment cost: Food + Wood + a small amount of Iron.
- Each attack consumes Arrows.
- Attack range: **7 tiles**.
- Especially effective against high-HP enemies such as Brutes and Giants.
- Penetration can be considered later, but does not need to exist in V1.

### 7. Firearm Infantry / Shenji Battalion
Role: high-intensity mid/late-game gunpowder ranged unit.

- Recruitment cost is mainly Food plus a small amount of Iron.
- Each attack consumes Gunpowder.
- Attack range: **7 tiles**, aligned with the Heavy Crossbowman for a clean prototype baseline.
- Higher single-shot damage than bow/crossbow units.
- Sustained combat cost is significantly higher than the arrow-based military system.

## Heavy Weapons

### 8. Heavy Ballista
Role: heavy weapon in the arrow-based supply system.

- Main construction/training cost: Wood + Iron + Food/Population depending on final implementation.
- Attack range: **10 tiles**.
- Each shot consumes a large amount of Arrows.
- Initial design: one shot costs roughly the same as 10 normal arrow attacks.
- High single-hit damage with penetration and/or anti-large capability.

### 9. Cannon
Role: gunpowder-based area heavy weapon.

- Main construction cost: Iron + Food/Population.
- Attack range: **10 tiles**.
- Each shot consumes a large amount of Gunpowder.
- Initial design: one cannon shot consumes around 5x the Gunpowder of one Firearm Infantry shot; 5-10x remains a prototype test range.
- Large-area AOE and a core late-game answer to hordes.

## Deferred Units

The following may appear in campaigns or later versions, but are outside the V1 core roster:

- Scout cavalry
- Standard cavalry
- Hero units
- Special campaign units

## V1 Unit Count

- Melee: 3
- Ranged: 4
- Heavy weapons: 2
- Total: 9

V1 principle: do not keep expanding the roster. First verify whether the current units create clear battlefield roles and meaningful economic trade-offs.

---

# 玩家兵种

> 当前全部是 Prototype 测试值。远程武器统一采用 **声音吸引半径 = 攻击射程 × 2**，详见 `05_noise_system.md`。

## 移速 / 射程基线

所有人类兵种统一 **10格视野**，与武器射程、声音半径分开。

弓箭手应该能够跑过慢速和中速僵尸，方便玩家拉扯和重新布阵，但不能比所有特殊僵尸都快。Runner、自爆尸、尸犬就是用来惩罚无限风筝的。

| 单位 | 移速（格/秒） | 攻击射程（格） |
|---|---:|---:|
| 木盾兵 | 2.6 | 1 |
| 铁盾兵 | 2.2 | 1 |
| 自爆兵 | 3.5 | 0（贴脸自爆） |
| 弓箭手 | 3.2 | 5 |
| 连弩手 | 3.0 | 4 |
| 重弩手 | 2.8 | 7 |
| 神机营 | 2.8 | 7 |
| 重弩炮 | 1.5 | 10 |
| 火炮 | 1.3 | 10 |

这些值后续必须通过真实战斗手感继续测试。

## 近战单位

### 1. 木盾兵 Wooden Shield Guard
定位：低成本消耗型前排。

- 主要成本：Food + Wood。
- 不消耗弹药。
- 高防御、低伤害。
- 主要职责：拖延尸潮、保护远程火力。
- 死亡属于可接受战术损耗，但会触发感染机制。

### 2. 铁盾兵 Iron Shield Guard
定位：更高等级的重装前排。

- 主要成本：Food + Iron。
- 不消耗弹药。
- 比木盾兵更高 HP / Armor。
- 比木盾兵更慢，但人口效率和正面承伤能力更高。

### 3. 自爆兵 Suicide Bomber
定位：火药时代一次性范围杀伤。

- 主要成本：Food + Gunpowder（以及少量装备成本，待定）。
- **攻击射程为 0**，必须真正冲到敌人身边才能自爆。
- 自身必死。
- 用于处理局部突破和高密度尸群。
- 爆炸本身仍然可以产生 Noise；攻击射程和声音吸引范围不是一回事。

## 远程单位

### 4. 弓箭手 Archer
定位：最基础远程单位。

- 招募成本以 Food 为主，附带少量 Wood。
- 每次攻击消耗 Arrows。
- 攻击射程：**5 格**。
- 移速足以拉开普通尸、胖尸、毒液尸、巨型尸，但跑不过最快的特殊僵尸。
- 前期主力。

### 5. 连弩手 Repeating Crossbowman
定位：近中距离高爆发远程单位。

- 招募成本：Food + 少量 Wood / Iron（后续测试）。
- 每次攻击消耗 Arrows。
- 攻击射程：**4 格**，比弓箭手更近。
- 射速高、单发伤害低。
- 核心特点：近距离爆发很强，但箭矢消耗极快。

### 6. 重弩手 Heavy Crossbowman
定位：慢射速、高单发伤害。

- 招募成本：Food + Wood + 少量 Iron。
- 每次攻击消耗 Arrows。
- 攻击射程：**7 格**。
- 对胖尸、巨型尸等高 HP 单位效果更好。
- 可考虑穿透效果，但 V1 先不强制实现。

### 7. 神机营 Firearm Infantry
定位：中后期高强度火药远程主力。

- 招募本体主要消耗 Food + 少量 Iron。
- 每次攻击消耗 Gunpowder。
- 攻击射程：**7 格**，Prototype 阶段先与重弩手保持一致。
- 单发伤害高于弓弩系。
- 持续作战成本显著高于弓箭体系。

## 重武器

### 8. 重弩炮 Heavy Ballista
定位：箭矢体系重型武器。

- 主要建造/训练成本：Wood + Iron + Food/Population（取决于最终实现）。
- 攻击射程：**10 格**。
- 每发消耗大量 Arrows。
- 初始设计：1 发约等于普通远程单位 10 发箭矢消耗。
- 高单发、高穿透/对大型目标能力。

### 9. 火炮 Cannon
定位：火药体系范围重武器。

- 主要建造成本：Iron + Food/Population。
- 攻击射程：**10 格**。
- 每发消耗大量 Gunpowder。
- 初始设计：单发火药消耗约为神机营一次射击的 5 倍；5-10 倍继续作为测试范围。
- 大范围 AOE，是尸潮后期核心输出。

## 暂缓单位

以下单位可用于剧情或后续版本，但不纳入第一版核心兵种：

- 侦察骑兵
- 正规骑兵
- 英雄单位
- 特殊剧情兵种

## V1 兵种总数

- 近战：3
- 远程：4
- 重武器：2
- 合计：9

第一版原则：不继续扩兵种，优先验证现有单位之间是否形成明确职责和经济取舍。
