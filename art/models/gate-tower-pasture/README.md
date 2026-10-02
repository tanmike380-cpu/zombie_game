# 炮楼通行与牧场材质评审

`GateTowerPasture.blend` 是独立 Blender 评审资产，不覆盖用户导入场景。

## 场景

- `00_OriginalArchitecture_Preserved`：原始炮楼、牧场及材质。
- `01_Tower_FriendlyPassage_2x2`：炮楼 2×2 整数格占地；正门沿炮口轴线，友军往返穿行时炮楼淡化并显示蓝色士兵轮廓，僵尸被阻挡。门没有动画。
- `02_Pasture_StrawMaterial`：替换为用户新导入、带完整贴图的牧场。全部原网格、UV、材质和贴图保留；仅在原有凸起田垄上新增成排的秆、叶片与穗头。旧的白模上色稿及 `Pasture_BaseColor.png` 已撤下（可从 Git 恢复）。

## 验证范围和边界

此处是 **Blender 几何规则与视觉演示，不是 Unity NavMesh 通行实现**。
`tools/art/gate_passage.py` 明确为 review fixture，不供正式战斗逻辑调用。
仅中轴通道允许友军通行；侧面、体积过宽的单位和敌军不获通行权限。
样片运动节奏和人物尺寸为评审参数，不覆盖 `balance/unit_balance.json`。
现有弩手骨架/Run 动作沿用已保存资产；无新建人物骨架。

正式接入时，应保留敌方不可通行的阻挡体，使用友方专用通道/区域过滤，并一并审计战斗里使用 `NavMesh.AllAreas` 的路径查询。不能仅把碰撞体全局关闭；淡化只能影响渲染。
现模型背面为实墙，本版依用户要求采用虚体穿过的视觉表达，未擅自开凿后门。
未启用炮楼攻击、生命值、价格或建造时间；这些数值仍需共享 Balance 正式记录。

## 本地评审

- 页面：`Builds/ArtReview/GateTowerPasture/review.html`
- 检查结果：同目录 `verification.json`
- 构建：`Blender --background <原始模型快照或本文件> --python-exit-code 1 --python tools/art/gate_tower_review.py`
- 复查：`Blender --background art/models/gate-tower-pasture/GateTowerPasture.blend --python-exit-code 1 --python tools/art/gate_tower_review.py -- --verify`
- 动画帧：同上命令，末尾改为 `-- --video`。
- 仅替换牧场：将新导入场景保存为 `Builds/ArtReview/GateTowerPasture/ImportedPastureV2Snapshot.blend`，在本文件上运行脚本，末尾使用 `-- --replace-pasture`。炮楼不重建。

保持原炮楼与新牧场全部贴图，预览不添加 Light 对象。作物为独立覆盖层，不重涂任何建筑或草地源贴图。密度色块参考《帝国时代4》农田截图（https://forums.ageofempires.com/t/holy-roman-empire-priests-dont-always-auto-inspire/178007），不使用游戏原始素材。
