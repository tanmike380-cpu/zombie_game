# Defensive Buildings

Defensive buildings are intentionally strong enough to appeal to tower-defense players, but they consume the same military supplies as mobile ranged units. This keeps static defense connected to the core economy instead of becoming free damage.

## 1. Arrow Tower
Role: basic sustained defense.

- Attack range: **5 tiles**.
- Ammo: **Arrows**.
- Prototype ammo consumption: **1 Arrow per shot**.
- Noise radius: **2 tiles**.
- Gameplay role: a static alternative to basic Archers.
- Best against ordinary low-tier hordes.
- Low operating cost, but weaker against high-HP special zombies.

## 2. Cannon Bastion
Role: long-range heavy AOE defense.

- Attack range: **10 tiles**.
- Ammo: **Gunpowder**.
- Prototype ammo consumption: **5 Gunpowder per shot**.
- Noise radius: **10 tiles**.
- Slow firing, high impact, large AOE.
- Designed for large late-game hordes and heavy targets.
- Very strong, but sustained firing can rapidly drain the player's Gunpowder economy and attract nearby roaming zombies.

## 3. Flame Bastion
Role: extreme close-range anti-horde defense.

- Attack range: **4 tiles** in a forward cone / area.
- Ammo: **Gunpowder** in V1. No seventh fuel resource is added.
- Prototype ammo consumption: **10 Gunpowder per burst**.
- Noise radius: **6 tiles**; sustained firing keeps the local Noise active.
- Very high AOE damage against dense hordes.
- Short range means it must be protected by walls / frontline defenses.
- Intended as a high-cost emergency horde-melting structure rather than a general-purpose tower.

## Supply Rule

V1 does not implement manual ammunition logistics for towers.

- Defensive buildings consume Arrows / Gunpowder directly from the faction's shared stockpile.
- No ammo crates, caliber types, or worker delivery routes are required in V1.
- The strategic trade-off is global: ammunition spent by towers is ammunition unavailable to the field army.

## Design Goal

The three towers create a simple defense progression:

```text
Arrow Tower
cheap sustained fire
        ↓
Cannon Bastion
long-range heavy AOE
        ↓
Flame Bastion
short-range extreme AOE / emergency defense
```

The goal is not to turn the game into a pure tower-defense title. Static defense should be powerful, but map expansion, mobile armies, resource capture, and attacking zombie nests remain necessary.

---

# 防御建筑

防御建筑需要足够强，让喜欢塔防的玩家也能获得爽感；但它们和移动军队共享箭矢 / 火药军需，所以不会变成“建完以后永久免费输出”。

## 1. 箭塔 Arrow Tower
定位：基础持续防御。

- 攻击射程：**5 格**。
- 弹药：**Arrows / 箭矢**。
- Prototype 消耗：**每次攻击 1 箭矢**。
- Noise 吸引半径：**2 格**。
- 玩法定位：固定版基础弓箭手。
- 主要处理普通低级尸潮。
- 持续成本低，但面对高血特殊僵尸效率较差。

## 2. 铁炮要塞 Cannon Bastion
定位：远距离重型 AOE 防御。

- 攻击射程：**10 格**。
- 弹药：**Gunpowder / 火药**。
- Prototype 消耗：**每炮 5 火药**。
- Noise 吸引半径：**10 格**。
- 射速慢、单发冲击强、AOE 大。
- 用来处理后期大型尸潮和高血目标。
- 很强，但持续开炮会快速烧掉火药库存，同时把附近游荡僵尸吸引过来。

## 3. 喷火要塞 Flame Bastion
定位：超高近距离清尸 AOE。

- 攻击射程：**4 格**，建议做成前方扇形 / 区域攻击。
- 弹药：V1 继续抽象成 **Gunpowder / 火药**，不新增第七种燃料资源。
- Prototype 消耗：**每次喷射 10 火药**。
- Noise 吸引半径：**6 格**；持续喷火会持续维持局部 Noise。
- 对高密度尸群造成恐怖 AOE。
- 射程短，因此需要城墙或前排保护。
- 定位是昂贵的紧急清尸建筑，不是万能塔。

## 补给规则

V1 不做复杂塔防弹药物流。

- 防御建筑直接从全阵营共享的 Arrows / Gunpowder 库存扣除弹药。
- 不做弹药箱、不同口径、工人搬运路线。
- 战略取舍来自全局军需：塔打掉的弹药，就是野战军不能用的弹药。

## 设计目标

三种防御建筑形成非常简单的梯度：

```text
箭塔
便宜持续输出
   ↓
铁炮要塞
远程重型 AOE
   ↓
喷火要塞
近距离超高 AOE / 紧急防线
```

目标不是把游戏做成纯塔防。固定防御可以很强，但地图扩张、移动军队、资源点占领和主动清理尸巢仍然必须存在。
