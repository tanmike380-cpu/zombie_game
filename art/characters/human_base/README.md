# 正式人类美术母版 · human-matchlock-v1

2026-09-25：用户确认当前人物造型，锁定为后续人类兵种共用基础。

## 唯一正式来源

- `human_base.blend`：可编辑、独立完整的 Blender 母版，包含人体、衣服、红甲、头盔、腰带、葫芦、火绳枪、材质及可逆握枪姿势。
- `human_base.verification.json`：母版文件、人体彩形、服装及装备原始几何校验值。
- 本文件夹是正式美术源，不属于 `Assets/_Tests` 或 `Builds`。原来已获认可的审看稿保持原样，不再作为未来兵种制作入口。

## 冻结范围

后续人类兵种基于这一个母版派生，只改**配色和手持武器**。人体比例、脸型、服装与铠甲轮廓、头盔、腰部配件不重做；任何这些形体改动都要单独获得用户确认。

握持姿势可以适配新武器，但不能修改人体的基础顶点或原有两个形体键。不要通过压扁身体、缩短手臂来强行对齐武器。盔甲、衣服与金属材质保持分离，换色不要整体染色。

本轮修正：重解两臂姿势，减小腕部折角；放松手指卷曲；补齐内衬在新姿势下的衔接；皮肤、嘴唇和眼睛着色。原始人体、脸型、服装、铠甲和武器顶点不变。

## 当前交付边界

**这是正式美术母版，不是已经替换战场全部士兵的动画资产。** 当前只有手臂握持骨架；完整的行走、开火、换刀、近战与死亡绑定尚未完成。现有 `CharacterBake` / `CharacterCrowdRenderer` 的动画及游戏反馈保持不变，不能把静态母版当作完成了动画的成品直接覆盖它们。

Unity 静态检查导出位于 `Assets/_Game/ArtGenerated/HumanBase/human_base.glb`，从母版重建，不提交生成物，也不放进 Resources 自动打入游戏包。它仅供导入/比例/造型检查；Blender 程序化木纹、微表面等并非完整烘焙贴图，不保证检查版与离线渲染逐像素一致。运行时拓扑、共享骨架和材质烘焙需要下一阶段完成。

## 验证与查看

在项目根目录运行（macOS）：

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python-exit-code 1 --python tools/art/verify_human_base.py
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python-exit-code 1 --python tools/art/export_human_preview.py
```

Unity 菜单 `Tools > Zombie Game > Characters`：

- `Reveal Approved Human Master`：定位正式 Blender 母版。
- `Inspect Approved Human Model`：选中静态导出模型，在 Inspector 查看。不会清空或重建当前战场。

`tools/art/freeze_human_base.py` 记录本轮从已确认装备稿到母版的迁移。它要求原输入 SHA-256 匹配，拒绝覆盖输入。**日常制作直接在本目录母版基础上另存派生模型，不应重复从旧 Builds 审看稿开始。**

近景、全身、RTS 俯视和手部审看渲染位于本地 `Builds/ArtReview/HumanBase-v1/`；它们是生成预览，不是模型源文件。

## 材质与姿势入口

- `HumanBase | warm skin` 与 `HumanSkin` 顶点颜色：皮肤；手臂和脸共用，避免肤色断层。
- `HumanBase | eyes`：眼白、深棕虹膜和瞳孔。
- `Equipment | editable holding pose rig`：两侧 upper / forearm / hand 骨骼。
- `Equipment pose | relaxed fingers around matchlock`：可逆手指握持形体键。
- `human_hands.py` / `human_skin.py`：本次调整参数，不能在 Unity 场景里另造一套人物。

导出武器以 `Weapon | ...` 分组，服装/人体以 `Human | ...` 分组；它们不是伤害判定或平衡数据。所有兵种数值仍只从既有 `balance/unit_balance.json` 读取。
