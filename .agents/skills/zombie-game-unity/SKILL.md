---
name: zombie-game-unity
description: Integrate approved Blender assets and develop Zombie Game gameplay, navigation, animation playback and presentation without inventing or deforming models in Unity.
---

# Unity 专责

先读 `AGENTS.md`、`../zombie-game-dev/SKILL.md` 和 `art/blender_master.json`。

涉及地面、地形、环境材质或画面优化时，必须再完整读取 `references/terrain-art.md`，执行用户指定的六层 PBR 地面规范，不自行退回单草地铺满地图。

- Unity 是编排播放端。使用 Blender 交接的模型、装配和动作；旧文件、缺源路径或未完成导出必须先解决，不能造相似替代品。
- 可做 Prefab、玩法脚本、NavMesh、碰撞/选择范围、动画状态机、挂点、VFX、材质适配和渲染设置。不重塑网格、不独立拉长底座、不拆散重凑验收过的墙塔。模型问题退回 Blender。
- 保留比例、UV、贴图、骨骼和装配相对变换。轴转换与整体等比例放置记录并只应用一次；不对装配子块逐个按包围盒缩放或居中。
- 占地、碰撞、选中圈、射击挂点匹配用户批准尺度；不悄悄改单位数值或冻结接触逻辑。
- 游戏事件驱动已有动作，检查行走方向、射击周期、抛射物与伤害时机，不换骨骼或用整体摆动替代缺失动画。
- 模型变糊/失真先比较同源同视角，查 Import Scale、纹理尺寸/压缩/mipmap、材质色彩空间、光照/曝光、渲染分辨率；不要先减面重画。
- 扩展正式系统，读取 UnitBalance，保持已验收寻路、围攻、UI、视野和操作。测试只提供布局/人口，不另造控制器。
- 防御塔下部为方正实心石墩，无门、梯子或通道，双方单位均阻挡；友军通行仅石门／木门，禁止恢复旧炮楼通行或内部轮廓提示。
- 验证实际玩家画面：战斗前墙体接合、实体塔双方阻挡、石门／木门友军通过与敌军阻挡、遮挡提示、比例和循环。区分受击拆除与出生即裂开，编译通过不是视觉验收。
- 回报源文件、导入资产、Prefab/场景、实测与未通过项目。仅修改分配的 Unity 代码与测试，不改 Blender 资产；源导出期间不导入半成品，Git 由主 Agent 审查。
