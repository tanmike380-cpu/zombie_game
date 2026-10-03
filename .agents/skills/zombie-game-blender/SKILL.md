---
name: zombie-game-blender
description: Author or repair Zombie Game models, rigs, animation and modular assemblies in the user's single Blender master layout, then hand traceable exports to Unity.
---

# Blender 专责

先读 `AGENTS.md`、`../zombie-game-dev/SKILL.md` 和 `art/blender_master.json`。

1. 核对实时主文件、场景和未保存状态，修改前做可恢复快照。不得打开旧文件或清空场景覆盖用户当前布局。
2. 修改既有对象/集合，新变体放入主场景可见且命名明确的集合，摆在空闲区域，不搬动无关模型。多种状态分组排布，不堆叠；拒绝放置的例子标为辅助物，不当作游戏资产导出。
3. 沿用用户网格、UV、材质和骨骼。模型细节、部件轴心、轮子旋转、握持、走路、攻击都在 Blender 做好。不擅改灯光，不自创另一套模型冒充原件。
4. 墙身、墙墩、炮塔先在 Blender 组好，检查接合、底面、轮廓和比例。记录共同根变换和连接位置；破坏拆分件保持共享装配坐标，禁止 Unity 对每片独立居中/缩放猜接合。
5. 动画优先复用用户骨骼，检查循环、脚触地、关节、握持及移动方向；缺少动作才补做，不能用整体抖动冒充完成。
6. 保存并重新读取验证。后台处理允许，但最终成果须合回主布局，不能覆盖用户期间新增改动。临时独立文件不是最终交付。
7. 交接主文件路径、Scene/Collection/Object/Action、导出选择、轴向/单位/缩放、材质贴图、装配信息、导出路径和验证证据。缺源资产就报告，不造替代品。

仅修改分配的 Blender 资产/导出脚本，不改玩法数值。独占操作 Blender GUI，与其他 Agent 协调；完成时报告集合名，让用户能在总布局找到成果。
