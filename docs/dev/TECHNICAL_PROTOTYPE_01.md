# Technical Prototype 01 — 10K Horde Render Benchmark

Update (2026-09-18): the repository now has a complete Unity 6000.6.1f1 bootstrap.
The one-time bootstrap instructions below are historical. Use the current
[benchmark run instructions and results](BENCHMARK_2026-09-18.md).

## Objective

Establish the rendering baseline for the project before adding gameplay systems.

This first test answers only one question:

> Can the target machine draw 1,000 / 5,000 / 10,000 cheap 3D zombie placeholders smoothly?

It intentionally does **not** test:

- AI
- pathfinding
- combat
- animation
- physics
- Fog of War
- Noise attraction
- networking

Those systems will be added one layer at a time so performance regressions can be attributed to a specific system.

## Current Implementation

The benchmark uses:

- one shared 8-vertex low-poly mesh
- one shared unlit material
- GPU instancing via `Graphics.RenderMeshInstanced`
- no shadows
- no 10,000 GameObjects
- a fixed 256 x 256 tile map, with one tile equal to one Unity world unit
- a full-map orthographic camera so all instances can contribute to render load

The current extreme target is 10,000 active placeholders.

## One-Time Unity Project Bootstrap

The Git repository already contains the benchmark scripts, but it does not yet contain a complete Unity-generated project bootstrap.

Recommended setup:

1. Install Unity Hub.
2. Install **Unity 6.3 LTS** with Mac Build Support / Windows Build Support as needed.
3. In Unity Hub, create a temporary **Universal 3D (URP)** project.
4. Close Unity after the project is created.
5. Copy/merge these folders from the temporary project into this repository:
   - `Assets/`
   - `Packages/`
   - `ProjectSettings/`
6. When merging `Assets/`, keep the existing `Assets/_Tests/` directory from this repository.
7. In Unity Hub, use **Add project from disk** and select the root of this repository.
8. Let Unity finish importing.
9. Commit the generated Unity project bootstrap to Git after confirming it opens successfully.

Do not commit Unity-generated cache folders such as `Library/`, `Temp/`, `Logs/`, `obj/`, or `UserSettings/`. The repository `.gitignore` already excludes them.

## Create the Benchmark Scene

After Unity finishes compiling the scripts:

1. In the Unity menu, choose:
   `Tools > Zombie Game > Create 10K Horde Benchmark Scene`
2. Unity creates:
   `Assets/_Tests/Performance/HordeBenchmark/Scenes/Tech_HordeBenchmark.unity`
3. Open that scene if it is not already open.
4. Press **Play**.

The Game view displays:

- current instance count
- measured FPS
- detected GPU/device name
- buttons for 1,000 / 5,000 / 10,000 instances

## Test Procedure

For each instance count:

1. Select 1,000.
2. Wait about 10 seconds.
3. Record stable FPS.
4. Select 5,000.
5. Wait about 10 seconds.
6. Record stable FPS.
7. Select 10,000.
8. Wait about 10 seconds.
9. Record stable FPS.

Recommended first test record:

```text
Machine:
OS:
CPU / Apple chip:
RAM:
GPU:
Resolution:
Unity version:

1,000 instances FPS:
5,000 instances FPS:
10,000 instances FPS:

Notes:
```

## Important Benchmark Notes

- Maximize the **Game** view while testing. The Unity Editor can also render the Scene view, which can distort results.
- Editor FPS is not final player FPS. Later we will also test a standalone Development Build.
- This test is deliberately easy compared with the final game. Passing it does **not** mean 10,000 fully simulated zombies will run at the same FPS.
- The value of this test is establishing the rendering floor before adding expensive systems.

## Next Technical Spikes

Only after this render-only benchmark is recorded:

1. `TECH-002`: 10K simple movement, no pathfinding.
2. `TECH-003`: shared grid / flow-field navigation.
3. `TECH-004`: low-resolution Noise Grid.
4. `TECH-005`: simple target lookup and combat.
5. `TECH-006`: Fog of War + rendering culling.
6. `TECH-007`: simplified animation / LOD strategy.

Each stage must repeat 1K / 5K / 10K profiling.

---

# 技术原型 01 —— 1 万单位渲染基准

## 目标

在真正加入玩法系统之前，先建立本项目的基础渲染性能数据。

第一轮只回答一个问题：

> 当前机器能不能顺畅画出 1,000 / 5,000 / 10,000 个廉价 3D 僵尸占位模型？

本轮**不测试**：

- AI
- 寻路
- 战斗
- 动画
- 物理
- 战争迷雾
- Noise 声音吸引
- 联机

后续一层一层往上加，这样一旦 FPS 掉下去，我们能明确知道到底是哪套系统造成的。

## 当前实现

第一版 benchmark 使用：

- 一个共享的 8 顶点低模 Mesh
- 一个共享 Unlit 材质
- `Graphics.RenderMeshInstanced` GPU Instancing
- 关闭阴影
- 不创建 10,000 个 GameObject
- 固定 256 x 256 格地图，每格对应一个 Unity 世界单位
- 覆盖整张地图的正交相机，确保所有单位都可能产生渲染负载

当前极限测试目标为 10,000 个活动占位单位。

## 第一次初始化 Unity 项目

Git 仓库里已经有 benchmark 脚本，但目前还没有完整的 Unity 自动生成工程文件。

建议：

1. 安装 Unity Hub。
2. 安装 **Unity 6.3 LTS**，并按需要安装 Mac / Windows Build Support。
3. 在 Unity Hub 临时创建一个 **Universal 3D（URP）** 项目。
4. 创建完成后关闭 Unity。
5. 把临时项目中的以下目录复制/合并进当前 Git 仓库：
   - `Assets/`
   - `Packages/`
   - `ProjectSettings/`
6. 合并 `Assets/` 时必须保留仓库已有的 `Assets/_Tests/`。
7. Unity Hub 选择 **Add project from disk**，选择当前 Git 仓库根目录。
8. 等待 Unity Import / Compile 完成。
9. 确认工程能正常打开后，把 Unity 自动生成的工程骨架 Commit 到 Git。

不要提交 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/` 等本地缓存；仓库里的 `.gitignore` 已经排除了它们。

## 一键生成 Benchmark Scene

Unity 编译完成后：

1. 菜单点击：
   `Tools > Zombie Game > Create 10K Horde Benchmark Scene`
2. Unity 会创建：
   `Assets/_Tests/Performance/HordeBenchmark/Scenes/Tech_HordeBenchmark.unity`
3. 打开这个 Scene。
4. 点击 **Play**。

Game 窗口左上角会显示：

- 当前单位数量
- FPS
- 当前 GPU / Device
- 1,000 / 5,000 / 10,000 三个测试按钮

## 测试方法

分别：

1. 选择 1,000，运行约 10 秒，记录稳定 FPS。
2. 选择 5,000，运行约 10 秒，记录稳定 FPS。
3. 选择 10,000，运行约 10 秒，记录稳定 FPS。

第一次把下面数据发回来：

```text
机器型号：
系统：
CPU / Apple 芯片：
内存：
GPU：
分辨率：
Unity 版本：

1,000 单位 FPS：
5,000 单位 FPS：
10,000 单位 FPS：

其他现象：
```

## 注意

- 测试时尽量把 **Game** 窗口最大化。Unity Editor 同时显示 Scene View 会额外消耗性能。
- Editor FPS 不等于正式游戏 FPS。之后还会专门测 Standalone Development Build。
- 这一轮故意非常轻。即使 10,000 个占位块很流畅，也不代表 10,000 个完整 AI 僵尸最终还能保持同样 FPS。
- 这一轮的意义是先拿到“纯渲染地板价”。

## 下一轮 Technical Spike

拿到这轮数据之后再按顺序增加：

1. `TECH-002`：10K 简单移动，不寻路。
2. `TECH-003`：Grid + Flow Field 共用寻路。
3. `TECH-004`：低分辨率 Noise Grid。
4. `TECH-005`：简单目标搜索和战斗。
5. `TECH-006`：战争迷雾 + Render Culling。
6. `TECH-007`：简化动画 / LOD。

每加一层，都重新测 1K / 5K / 10K。
