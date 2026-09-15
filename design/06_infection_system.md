# Infection & Nest System

## Soldier Infection Rule

V1 does not use per-hit infection probability, incubation periods, or infection progress bars.

Current design:

- When a human unit is killed by a zombie, a small number of low-tier zombies spawn immediately at the death location.
- The death also generates a strong Blood Scent signal that attracts nearby zombies.
- Whether one or multiple zombies are spawned is not yet frozen and should be tested in the balance table.

Goal:

> A frontline mistake should not only remove one soldier; it can create new enemies and pull nearby zombie groups into the same fight.

## Building Infection Rule

When a building is captured/infected by zombies, it does not release a one-time burst only. It becomes a **Zombie Nest**.

The nest keeps spawning zombies until the player destroys or purifies it.

## Building Levels and Nest Levels

### Level 1
Examples: tents, small houses, and other low-tier population buildings.

After infection:
- Primarily spawns Walkers.
- Low spawn rate.
- Initial Blood Scent recommendation: 20.

### Level 2
Examples: standard houses and more developed urban buildings.

After infection:
- Can spawn Walkers + Runners.
- Medium spawn rate.
- Initial Blood Scent recommendation: 30.

### Level 3
Examples: large residences, advanced buildings, and important facilities.

After infection:
- Can spawn Runners + Brutes, with a low chance of special zombies after later testing.
- High spawn rate.
- Initial Blood Scent recommendation: 40.

## Core Risk Design

As the city develops:

- Population grows
- Advanced buildings become more common
- Infected nests become stronger

Therefore:

> The more prosperous the economy becomes, the more dangerous an internal collapse becomes if the defense line fails.

## Snowball Chain

```text
Frontline unit dies
  ↓
Low-tier zombies spawn immediately
  ↓
Blood Scent is generated
  ↓
Nearby roaming zombies are attracted
  ↓
Pressure on the defense line increases
  ↓
Zombies enter the city
  ↓
Buildings become Zombie Nests
  ↓
Nests continuously spawn enemies
  ↓
The city collapses from inside
```

## V1 Simplification Rules

Do not implement:
- Per-attack infection probability
- Incubation periods
- Disease stats
- Virus transmission simulation
- Multi-stage building infection progress bars

Keep only event-driven rules:
- Unit killed by zombie
- Building captured/infected by zombie

---

# 感染与尸巢系统

## 士兵感染规则

V1 不做复杂概率感染、不做潜伏期、不做感染进度条。

当前设计：

- 人类单位被僵尸击杀时，立即在死亡位置生成少量低级僵尸。
- 同时触发较高 Blood Scent，吸引周边尸群。
- 具体“生成 1 只还是多只”暂不冻结，放在平衡表里测试。

目标：

> 一个前排失误，不只是少一个兵，而可能制造新的敌人并把附近尸群一起拉过来。

## 建筑感染规则

建筑被僵尸占领后，不是一次性爆兵，而是转化为 **Zombie Nest / 僵尸巢穴**。

巢穴会持续产怪，直到被玩家摧毁或净化。

## 建筑等级与巢穴等级

### Level 1
对应：帐篷、小型住宅等低级人口建筑。

感染后：
- 主要持续生成 Walker。
- 产怪频率较低。
- Blood Scent 初始建议：20。

### Level 2
对应：普通住宅、较成熟城市建筑。

感染后：
- 可生成 Walker + Runner。
- 产怪频率中等。
- Blood Scent 初始建议：30。

### Level 3
对应：大型住宅、高级建筑、重要设施。

感染后：
- 可生成 Runner + Brute，并有低概率生成特殊僵尸（具体后续测试）。
- 产怪频率更高。
- Blood Scent 初始建议：40。

## 核心风险设计

城市越发达：

- 人口越多
- 高级建筑越多
- 被感染后的巢穴等级越高

因此：

> 经济越繁荣，一旦防线失守，内部雪崩风险越高。

## 雪崩链

```text
前线单位死亡
  ↓
立即生成小僵尸
  ↓
产生 Blood Scent
  ↓
附近野外僵尸被吸引
  ↓
防线压力继续增加
  ↓
僵尸进入城内
  ↓
建筑被感染为 Zombie Nest
  ↓
持续产怪
  ↓
城市内部失控
```

## V1 简化原则

不做：
- 每次攻击感染概率
- 潜伏期
- 疾病数值
- 病毒传播模拟
- 多阶段建筑感染条

只保留事件驱动：
- Unit killed by zombie
- Building captured/infected by zombie
