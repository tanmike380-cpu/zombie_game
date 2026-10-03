---
name: zombie-game-dev
description: Coordinate every Zombie Game development task, keeping Blender as the single art source and separating Blender authoring from Unity gameplay integration.
---

# Zombie Game 开发入口

先读仓库 `AGENTS.md`；数值、寻路、接触约束和已验收反馈以该文件为准。

- 模型、材质、骨骼、蒙皮、动作、模型比例/旋转及建筑接合：交给 `zombie_blender`，读 `../zombie-game-blender/SKILL.md`。
- 玩法、索敌、寻路、UI、动画播放和导入：交给 `zombie_unity`，读 `../zombie-game-unity/SKILL.md`。
- 用户已要求这两个专责 Agent 分工。委派给出角色/技能路径、允许修改范围、验收标准；运行时不能选择命名角色时，把对应文件交给普通子 Agent，不声称配置自动加载。
- 跨端任务先完成 Blender 交接，再让 Unity 接入。可并行只读诊断，禁止抢写同一文件、同时控制一个 GUI 或导入半成品。子 Agent 不自行提交/推送，主 Agent 汇总验证和 Git 保存。

读 `art/blender_master.json`，再核对实时 Blender 的 `bpy.data.filepath`。所有最终模型、动作和变体必须在用户同一个总布局可找到，不能只交付散落的独立文件或截图。当前文件与登记不符、缺失或未保存时，先保护工作并核实，不能拿旧文件代替；迁移主文件需告知并取得用户同意。

Unity 不创造替代模型，不用基本体补外观，不独立拉伸模型，不重拼 Blender 已验收建筑。资产与占地冲突时报告并按用户选择处理。画面失真先查源资产、比例/坐标、材质和渲染。

保留用户原对象、灯光、相机、材质、动作和布局。临时导出/渲染可分开，但须追溯到主文件的 Scene/Collection/Object/Action。修复须回归与视觉核对；编译通过不是验收。交接给出主文件、集合、导出、Unity Prefab/场景和实测；未完成项明确标记。保存 Git 不等于获准推送。
