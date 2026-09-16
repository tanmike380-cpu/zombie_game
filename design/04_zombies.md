# Zombie Units

> Prototype movement values. Fast special zombies exist specifically to prevent ranged armies from kiting forever.

## Movement Baseline

| Zombie | Move Speed (tiles/s) | Main Purpose |
|---|---:|---|
| Walker | 1.8 | Basic mass pressure |
| Runner | 3.6 | Chase and break ranged formations |
| Brute | 2.2 | Durable frontline pressure |
| Exploder | 3.4 | Anti-clump / formation breaker |
| Zombie Hound | 4.2 | Fastest backline disruption |
| Spitter | 2.6 | Ranged harassment |
| Giant | 1.5 | Slow siege threat |
| Boss Zombie | TBD | Stage-specific rule change |

The Archer prototype speed is 3.2 tiles/s. It can escape Walkers, Brutes, Spitters, and Giants, but not Runners, Exploders, or Zombie Hounds.

## 1. Walker
Role: basic cannon-fodder zombie.

- Movement speed: 1.8 tiles/s.
- HP: low.
- Damage: low.
- Main threat: numbers.
- Hearing sensitivity: normal.

## 2. Runner
Role: fast breakthrough unit.

- Movement speed: 3.6 tiles/s.
- HP: low to medium.
- Damage: medium.
- Faster than the Archer prototype speed.
- Purpose: quickly close on ranged units and punish endless kiting.
- Hearing sensitivity: high.

## 3. Brute
Role: high-HP, high-damage frontline zombie.

- Movement speed: 2.2 tiles/s.
- HP: high.
- Damage: high.
- Characteristic: can kill ordinary soldiers quickly and pressure frontline tanks.
- Hearing sensitivity: normal.

## 4. Exploder
Role: anti-clump formation breaker.

- Movement speed: 3.4 tiles/s.
- HP: low to medium; it should be killable before reaching the formation if focused.
- Faster than the Archer prototype speed.
- Reaches the player formation and detonates at close range.
- Prototype explosion radius: **2 tiles**.
- High AOE damage. Player soldiers killed by the explosion immediately follow the normal infection rule and can spawn low-tier zombies.
- Main counterplay: focus fire it at range and spread ranged units so one explosion cannot wipe a dense formation.
- The purpose is to create RTS micro similar to manually spreading infantry against dangerous AOE threats.

## 5. Zombie Hound
Role: highest-mobility backline disruptor.

- Movement speed: 4.2 tiles/s.
- HP: low.
- Damage: medium.
- Special ability: can vault selected low-level walls/obstacles; exact rules to be prototyped.
- Hearing sensitivity: very high.

## 6. Spitter
Role: ranged harassment that forces formation changes.

- Movement speed: 2.6 tiles/s.
- HP: low to medium.
- Damage: area or damage-over-time.
- Special ability: spits toxic fluid.
- Hearing sensitivity: normal.

## 7. Giant
Role: siege/breach unit.

- Movement speed: 1.5 tiles/s.
- HP: very high.
- Damage: very high.
- Extra threat to buildings and walls.
- Hearing sensitivity: low to normal.

## 8. Boss Zombie
Role: stage-specific boss encounter.

- Exact mechanics will be designed separately.
- Principle: a boss should change battlefield rules rather than simply having more HP.

## Noise Targeting Rule

- If a zombie directly detects a player unit/building, direct combat targeting has priority.
- If it has no direct target, it can move toward active Noise sources.
- Noise guides roaming zombies toward where the player is fighting, not toward corpse locations.

## V1 Principles

- Walkers create numerical pressure.
- Runners chase ranged units and punish kiting.
- Brutes pressure the frontline.
- Exploders punish tightly packed formations and can trigger infection snowballs.
- Zombie Hounds create the highest-mobility backline threat.
- Spitters force formation changes.
- Giants break fortifications.
- Bosses create special-stage challenges.

---

# 僵尸兵种

> 当前移速全部是 Prototype 测试值。快速特殊僵尸的存在，就是为了防止远程军队无限风筝。

## 移速基线

| 僵尸 | 移速（格/秒） | 核心作用 |
|---|---:|---|
| 普通僵尸 Walker | 1.8 | 数量压力 |
| 狂暴僵尸 Runner | 3.6 | 追击、突破远程阵型 |
| 胖尸 Brute | 2.2 | 高血高伤前排 |
| 爆裂尸 Exploder | 3.4 | 反密集阵型 / AOE 威胁 |
| 尸犬 Zombie Hound | 4.2 | 最快的后排切入 |
| 毒液僵尸 Spitter | 2.6 | 远程骚扰 |
| 巨型僵尸 Giant | 1.5 | 慢速攻城 |
| Boss Zombie | TBD | 阶段性规则变化 |

弓箭手当前 Prototype 移速为 3.2 格/秒，可以跑过普通尸、胖尸、毒液尸和巨型尸，但跑不过狂暴尸、爆裂尸和尸犬。

## 1. 普通僵尸 Walker
定位：最基础炮灰。

- 移速：1.8 格/秒。
- HP：低。
- 伤害：低。
- 威胁来源：数量。
- 听觉敏感度：普通。

## 2. 狂暴僵尸 Runner
定位：快速追击与突破。

- 移速：3.6 格/秒。
- HP：低~中。
- 伤害：中。
- 比弓箭手更快。
- 用于快速贴近远程单位，防止玩家无限风筝。
- 听觉敏感度：高。

## 3. 胖尸 Brute
定位：高血量、高伤害前排。

- 移速：2.2 格/秒。
- HP：高。
- 伤害：高。
- 可以快速击杀普通士兵，对前排形成压力。
- 听觉敏感度：普通。

## 4. 爆裂尸 Exploder
定位：反密集阵型、逼迫玩家散兵操作。

- 移速：3.4 格/秒。
- HP：低~中，应该允许玩家在其接近前通过集火击杀。
- 比弓箭手略快。
- 接近玩家阵型后贴脸自爆。
- Prototype 爆炸半径：**2 格**。
- AOE 伤害很高；被爆炸杀死的我方士兵继续按照正常感染规则，立即生成低级僵尸。
- 核心反制：远距离优先集火，并主动拉开远程兵间距，避免一炸死一大片。
- 这个单位的价值不是单纯高伤，而是逼玩家做“散兵”微操。

## 5. 尸犬 Zombie Hound
定位：最高机动性的后排威胁。

- 移速：4.2 格/秒。
- HP：低。
- 伤害：中。
- 特殊能力：可翻越部分低等级墙体/障碍（具体规则待原型）。
- 听觉敏感度：非常高。

## 6. 毒液僵尸 Spitter
定位：远程骚扰 / 迫使阵型移动。

- 移速：2.6 格/秒。
- HP：低~中。
- 伤害：范围或持续伤害。
- 特殊能力：喷射毒液。
- 听觉敏感度：普通。

## 7. 巨型僵尸 Giant
定位：攻坚单位。

- 移速：1.5 格/秒。
- HP：非常高。
- 伤害：非常高。
- 对建筑/城墙有额外威胁。
- 听觉敏感度：低~普通。

## 8. Boss Zombie
定位：阶段性 Boss。

- 具体机制后续单独设计。
- 原则：Boss 应改变战场规则，而不是单纯增加 HP。

## Noise 目标规则

- 僵尸如果直接发现玩家单位/建筑，优先直接追击和攻击。
- 没有直接目标时，才根据当前有效的 Noise 声音源移动。
- Noise 的作用是把游荡僵尸引向玩家正在战斗的位置，而不是引向尸体位置。

## 第一版原则

- 普通尸负责数量压力。
- 狂暴尸负责追击远程兵、惩罚风筝。
- 胖尸负责正面承压。
- 爆裂尸负责惩罚密集阵型，并制造感染雪崩风险。
- 尸犬负责最高速切后排。
- 毒液尸负责逼迫阵型移动。
- 巨型尸负责攻城。
- Boss 负责特殊阶段挑战。
