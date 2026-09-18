# Horde render benchmark — 2026-09-18

## Reproduce

Open this repository directory in Unity 6000.6.1f1. Select
`Tools > Zombie Game > Run 1K 5K 10K Benchmark` outside Play Mode.
The Game view is maximized automatically. Keep Unity in the foreground and do
not pause or resize it during sampling. Each stage warms up for 3 seconds and
samples for 10 seconds. The completed run stays in Play Mode at 10,000 instances.

The generated scene lives at
`Assets/_Tests/Performance/HordeBenchmark/Scenes/Tech_HordeBenchmark.unity`.
Runtime test code is guarded by `UNITY_EDITOR`; do not include this scene in
production Build Profiles. No gameplay simulation is included.

## Verified local run

- Apple M2 Pro, 16 GB RAM, Metal; macOS 15.7.4.
- Unity 6000.6.1f1, Built-In Render Pipeline with the test's explicit instancing shader.
- Game view: 2560 x 1440, maximized; full-map orthographic camera.
- Map: 256 x 256 world units; 1 unit per reference tile.
- Shared 8-vertex mesh, GPU instancing, no animation, AI, movement or combat.
- VSync disabled, target frame rate unlimited.

| Instances | Average FPS | Lowest instantaneous FPS | Average CPU draw submission |
| ---: | ---: | ---: | ---: |
| 1,000 | 871.6 | 131.6 | 0.01 ms |
| 5,000 | 812.9 | 114.9 | 0.03 ms |
| 10,000 | 760.0 | 114.1 | 0.06 ms |

The 10K screenshot was inspected and shows the red instances. This is an Editor
render-only baseline, not standalone Player performance or a GPU-time measurement.
The lowest FPS is the reciprocal of the longest sampled frame, not a 1% low.
The displayed FPS is simulation/render-loop throughput, not the monitor refresh rate.

Earlier temporary-project results are invalid as performance conclusions: initial
runs had invisible instances; later runs were affected by editor focus/window state.
The table above comes from a repeated foreground run with the corrected shader.

---

本地完整工程已放在当前仓库，可用 Unity 6000.6.1f1 打开。
测试菜单会最大化 Game 视图，依次测 1K、5K、10K，结束后保留万人画面。
本轮 10K 平均 760 FPS，最低瞬时 114.1 FPS；只证明极简静态模型的渲染基线，
不代表加入移动、动画、寻路和战斗后的性能。早先空画面及后台窗口测量均不采用。
