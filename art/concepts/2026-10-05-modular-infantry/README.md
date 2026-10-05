# 共用人物与独立武器 · 2026-10-05

新概念图单独放在这个文件夹，旧的分兵种人物图保留在 `art/concepts/tripo/humans/` 原目录，不删除、不覆盖。

## 人物：18 张独立图片

- [天朝](bodies/tianchao/)：民兵、老兵、精英，各有 `front.png`、`side.png`、`back.png`。
- [拜占庭](bodies/byzantine/)：同样九张，保留紫色服装与独立甲型。
- 每张仅一个空手 T-Pose 人物的一个视角，不合图；没有武器、箭筒或盾牌。

## 武器：8 张独立图片

- [天朝](weapons/tianchao/)：弓、诸葛连弩、普通弩、三眼神铳。
- [拜占庭](weapons/byzantine/)：投矛、弓、普通弩、火绳枪。
- 每件目录内只有一张 `reference.png`；没有按人物等级重复制作。

把一个人物同目录的三张图作为同一个模型的多视图输入；人物和武器分别生成，不混成同一模型。后续在正式 Blender 主文件里装配武器、检查权重和握持动作。图片尚未经过 Tripo 生成或绑定验证，跨视图微小差异需建模时统一。

这批图片由内置图像工具生成。完整提示词、参考图路径和校验信息见 [人物记录](bodies/provenance.json)、[武器记录](weapons/provenance.json)。[完整导入说明](../tripo/README.md)。
