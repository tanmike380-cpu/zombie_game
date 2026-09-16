# Zombie Game

A **1–2 player cooperative Survival RTS** set in a fictionalized ancient/medieval Chinese world.

## Documentation

English is the primary language. Each Markdown file contains the English version first and the Chinese version below in the same file.

- `design/01_game_vision.md`: game vision / 游戏愿景
- `design/02_resources.md`: resources / 资源系统
- `design/03_player_units.md`: player units / 玩家兵种
- `design/04_zombies.md`: zombie units / 僵尸兵种
- `design/05_noise_system.md`: Noise attraction system / 声音吸引系统
- `design/06_infection_system.md`: infection and nest system / 感染与尸巢系统
- `balance/initial_balance.yaml`: prototype balance values / 原型数值配置
- `docs/dev/TECHNICAL_PROTOTYPE_01.md`: first Unity benchmark / 第一轮 Unity 性能测试

## Design Principle

> Recruit cheaply, fight expensively.

Current values are Prototype / Alpha assumptions and will be tuned through simulation and playtesting.

Current horde performance target:

- Normal large fights: several thousand zombies.
- Extreme benchmark / late-game target: up to roughly **10,000 active zombies**, subject to profiling on low- and mid-range hardware.

---

# 中文说明

这是一款以中国古代/中古代架空背景为基础的 **1–2 人合作 Survival RTS**。

## 文档规则

英文是主语言。每个 Markdown 文件只保留一份：上半部分英文，下半部分中文。

- `design/01_game_vision.md`：游戏愿景
- `design/02_resources.md`：资源系统
- `design/03_player_units.md`：玩家兵种
- `design/04_zombies.md`：僵尸兵种
- `design/05_noise_system.md`：声音吸引系统
- `design/06_infection_system.md`：感染与尸巢系统
- `balance/initial_balance.yaml`：原型数值配置
- `docs/dev/TECHNICAL_PROTOTYPE_01.md`：第一轮 Unity 性能测试

## 核心设计原则

> 低成本爆兵，高成本开战。

当前所有数值均属于 Prototype / Alpha 初始假设，后续通过模拟和试玩调整。

当前尸潮性能目标：

- 常规大型战斗：数千只僵尸。
- 极端测试 / 后期目标：约 **10,000 只活动僵尸**，最终以低配和中配机器实际 Profiling 结果为准。
