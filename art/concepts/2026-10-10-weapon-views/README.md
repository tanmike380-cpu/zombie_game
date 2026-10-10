# 独立武器三角度 · 2026-10-10

每个武器单独一个目录，`front.png`、`side.png`、`back.png` 各为一张独立 PNG，仅含一件完整武器，没有人物、手或拼图。旧单张参考图保留在 `../2026-10-05-modular-infantry/weapons/`。

- `front`：主要外侧结构的清楚三分之四参考。
- `side`：低透视侧向轮廓；弓弩保留可读的弓臂曲线，避免不可辨认的刃边细线。
- `back`：相反侧／后方参考。三眼神铳单独补修为从木柄尾端观察铳身连接与后部。

这些是保持原武器设计的 AI 三维角度概念参考，不是由同一 3D 网格渲染出的校准 CAD 三视图。纹样、细小扣件和接合细节可能有轻微视图差异；Tripo 生成之后需核对结构，尤其三眼神铳三根管、诸葛弩顶箭匣、弓弦及弩弦。

已完成两国八件武器共24张，并逐图视审、检查尺寸与哈希。[当前Tripo目录](../tripo/README.md)为上传入口。`manifest.json`与`provenance.json`记录天朝四武器、拜占庭投矛／弓18张；拜占庭弩和火绳枪6张记录在`byzantine/manifest-additional.json`与`byzantine/provenance-additional.json`。统一发布为`python3 -m tools.art.weapon_views`，保留旧单图。

本批使用内置 image_gen，完整提示词、输入参考、生成原图路径、最终图尺寸与 SHA-256 均在 provenance 中。没有消耗 Tripo 积分，没有修改 Blender／Unity 模型、骨骼或游戏逻辑。
