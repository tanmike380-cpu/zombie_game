# Infection & Nest System

## Soldier Infection Rule

V1 does not use per-hit infection probability, incubation periods, or infection progress bars.

Current design:

- When a human unit is killed by a zombie or zombie ability, a small number of low-tier zombies spawn immediately at the death location.
- This includes soldiers killed by the **Exploder's AOE blast**.
- Whether one or multiple zombies are spawned is not yet frozen and should be tested in the balance table.
- Infection itself does not create a separate long-range attraction field. Long-range attraction is handled by combat Noise.

Goal:

> A formation mistake should be able to snowball immediately: an Exploder reaches a dense group, kills several soldiers, and those deaths create new zombies inside the player's formation.

## Building Infection Rule

When a building is captured/infected by zombies, it becomes a **Zombie Nest** rather than simply disappearing.

The nest keeps spawning zombies until the player destroys or purifies it.

## Building Levels and Nest Levels

### Level 1
Examples: tents, small houses, and other low-tier population buildings.

After infection:
- Primarily spawns Walkers.
- Low spawn rate.

### Level 2
Examples: standard houses and more developed urban buildings.

After infection:
- Can spawn Walkers + Runners.
- Medium spawn rate.

### Level 3
Examples: large residences, advanced buildings, and important facilities.

After infection:
- Can spawn Runners + Brutes, with a low chance of special zombies after later testing.
- High spawn rate.

## Core Risk Design

As the city develops:

- Population grows.
- Advanced buildings become more common.
- Infected nests become stronger.

Therefore:

> The more prosperous the economy becomes, the more dangerous an internal collapse becomes if the defense line fails.

## Snowball Examples

### Frontline collapse

```text
Shield line dies
  -> low-tier zombies spawn immediately
  -> ongoing ranged fire generates Noise
  -> nearby roaming zombies join the fight
  -> pressure increases
```

### Exploder formation failure

```text
Exploder reaches dense ranged formation
  -> AOE kills multiple soldiers
  -> killed soldiers immediately convert/spawn zombies
  -> zombies appear inside the ranged formation
  -> local collapse can propagate
```

### City infection

```text
Zombies enter city
  -> building infected
  -> building becomes Zombie Nest
  -> nest continuously spawns enemies
  -> city collapses from inside
```

## V1 Simplification Rules

Do not implement:
- Per-attack infection probability
- Incubation periods
- Disease stats
- Virus transmission simulation
- Multi-stage building infection progress bars
- A second Blood Scent attraction system

Keep only event-driven infection rules:
- Human unit killed by zombie / zombie ability
- Building captured/infected by zombie

Long-range attraction belongs to the separate Noise system.

---

# 感染与尸巢系统

## 士兵感染规则

V1 不做复杂概率感染、不做潜伏期、不做感染进度条。

当前设计：

- 人类单位只要被僵尸或僵尸技能击杀，就会在死亡位置立即生成少量低级僵尸。
- **爆裂尸 AOE 炸死的我方士兵同样触发这条规则。**
- 具体生成 1 只还是多只暂不冻结，后续放在平衡表里测试。
- 感染本身不再生成独立远距离吸引场；远距离吸引统一由战斗 Noise 负责。

目标：

> 一个阵型失误可以快速雪崩：爆裂尸冲进密集远程阵型，一炸死好几个兵，这些兵立刻在阵线内部转化/生成僵尸。

## 建筑感染规则

建筑被僵尸占领/感染后，不是单纯消失，而是转化为 **Zombie Nest / 僵尸巢穴**。

巢穴持续产怪，直到被玩家摧毁或净化。

## 建筑等级与巢穴等级

### Level 1
对应：帐篷、小型住宅等低级人口建筑。

感染后：
- 主要持续生成 Walker。
- 产怪频率较低。

### Level 2
对应：普通住宅、较成熟城市建筑。

感染后：
- 可生成 Walker + Runner。
- 产怪频率中等。

### Level 3
对应：大型住宅、高级建筑、重要设施。

感染后：
- 可生成 Runner + Brute，并有低概率生成特殊僵尸（后续测试）。
- 产怪频率更高。

## 核心风险设计

城市越发达：

- 人口越多。
- 高级建筑越多。
- 被感染后的尸巢等级越高。

因此：

> 经济越繁荣，一旦防线失守，内部雪崩风险越高。

## 雪崩案例

### 前线崩溃

```text
盾兵阵线死亡
  -> 立即生成低级僵尸
  -> 后排持续射击制造 Noise
  -> 附近游荡僵尸加入战斗
  -> 前线压力继续增加
```

### 爆裂尸炸阵型

```text
爆裂尸冲进密集远程阵型
  -> AOE 一次炸死多个兵
  -> 死亡单位立即转化/生成僵尸
  -> 僵尸直接出现在远程阵线内部
  -> 局部失误迅速雪崩
```

### 城市感染

```text
僵尸进入城内
  -> 建筑被感染
  -> 建筑变成 Zombie Nest
  -> 持续产怪
  -> 城市从内部失控
```

## V1 简化原则

不做：
- 每次攻击感染概率
- 潜伏期
- 疾病数值
- 病毒传播模拟
- 多阶段建筑感染条
- 第二套 Blood Scent / 血腥值吸引系统

只保留事件驱动感染：
- Human unit killed by zombie / zombie ability
- Building captured/infected by zombie

远距离吸引统一交给独立的 Noise 系统。
