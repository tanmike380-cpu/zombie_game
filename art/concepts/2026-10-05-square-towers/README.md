# 防御塔：方形占地与放大背门

新版本单独保存在本目录。旧的 `art/concepts/tripo/buildings/` 六套塔图原址保留；不删除、不覆盖。每种塔有三个独立建模参考：`front-perspective.png`、`side.png`、`rear-perspective.png`，另有 `top.png` 辅助检查俯视比例。

| 阵营 | 建筑 | 占地（正面宽×进深） | 新图目录 |
|---|---|---|---|
| 天朝 | 箭塔 | 2×2格 | [箭塔](tianchao/26-arrow-tower/) |
| 天朝 | 炮塔 | 2×2格 | [炮塔](tianchao/27-cannon-tower/) |
| 天朝 | 长城／蜂巢炮塔 | 3×2格 | [蜂巢炮塔](tianchao/28-great-wall/) |
| 拜占庭 | 箭塔 | 2×2格 | [箭塔](byzantine/26-arrow-tower/) |
| 拜占庭 | 炮塔 | 2×2格 | [炮塔](byzantine/27-cannon-tower/) |
| 拜占庭 | 喷火塔 | 2×2格 | [喷火塔](byzantine/29-flame-tower/) |

下部石墩侧面垂直、不收成梯形。顶部武器、屋顶和阵营配色沿用既有设计。背面闭合木门位于武器发射方向的反面，视觉目标约为下部石墩高35–40%、面宽25–30%；门只表示人员入口，不提供穿塔路径。仅木门、石门供兵通行。

这些是内置图像工具制作的二维概念图，目标占地写入清单，但图片不是测绘蓝图，不能据像素证明三维尺寸。透视参考与纯俯视细节可能有差异，最终需在 Blender 主文件统一。没有改现有模型、灯光或 Unity 场景；建筑模型验收后再适配真实格子和碰撞。

完整提示词、引用图片、尺寸和哈希见 [天朝记录](tianchao/provenance.json)、[拜占庭记录](byzantine/provenance.json)、[纯俯视记录](top-provenance.json)。[完整导入说明](../tripo/README.md)。
