# 空手 T-Pose 兵种挂件版 · 2026-10-10

已完成天朝、拜占庭各4种步兵，民兵／老兵／精英各3个独立视角，共72张，全部1254×1254。逐图视审及尺寸／SHA-256检查已完成，已发布到[当前Tripo清单](../tripo/README.md)。旧图一律保留。

| 阵营 | 目录名 | 兵种 | 固定在人物上的挂件 |
|---|---|---|---|
| 天朝 | `tianchao/archer` | 弓箭手 | 背侧箭筒 |
| 天朝 | `tianchao/repeating-crossbowman` | 诸葛弩手 | 连弩箭盒 |
| 天朝 | `tianchao/crossbowman` | 普通弩手 | 短弩矢袋 |
| 天朝 | `tianchao/three-eyed-handgunner` | 三眼神铳兵 | 葫芦 |
| 拜占庭 | `byzantine/archer` | 弓箭手 | 背侧箭筒 |
| 拜占庭 | `byzantine/crossbowman` | 普通弩手 | 短弩矢袋 |
| 拜占庭 | `byzantine/javelin-legionary` | 罗马军团投矛手 | 携矛具 |
| 拜占庭 | `byzantine/matchlock-infantry` | 单发火枪手 | 皮革水囊 |

每个目录下分 `militia`（民兵）、`veteran`（老兵）、`elite`（精英）；每等级分别是 `front.png`、`side.png`、`back.png`。不要把九张图混成一个模型；一次只使用一个兵种、一个等级的三视图。

采用内置图像编辑，来源为已认可的[基础人物](../2026-10-05-modular-infantry/bodies/)。保留衣甲、头盔、脸、长袖、三级手套和两国颜色，仅补兵种挂件。新图不是 Blender 模型，未上传 Tripo、消耗 Tripo 积分或替换游戏资产。AI 三视图不是精确建模图，装饰与比例仍需在 Blender 统一。

手为空、没有旁置武器；已有[八张独立武器图](../2026-10-05-modular-infantry/weapons/)保留为旧版参考，新版武器也按正／侧／背独立制图。人物在Tripo初始绑定，独立武器装配与复杂战斗动作优先在Unity调试；Blender保留源模型与蒙皮／分件修复。见[新动画路线](../../../docs/dev/TRIPO_UNITY_ANIMATION_WORKFLOW.md)。箭束需要在后续建模时分成可隐藏部件，才能按弹药隐藏，不保证 Tripo 自动拆好。

携弹批准值：民兵30发、老兵30发、精英总50发；精英基础射程＋1格。伤害加成数值待定。图片不逐根表达30或50支箭，不以概念图改变实际数值。

每国 `provenance.json` 记录完整提示词、输入图、生成文件与最终文件；`manifest.json` 记录各视图尺寸、来源与哈希。天朝普通弩手／三眼神铳的追加记录为 `manifest-additional.json`、`provenance-additional.json`。用 `python3 -m tools.art.infantry_kits` 可重发布当前 Tripo 清单；该工具拒绝缺失兵种／等级／视图及覆盖不同内容的已有文件。
