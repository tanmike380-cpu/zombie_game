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

### 手部专项修正（同日第二轮）

按用户要求，上一轮的非手部结果全部冻结。本轮只修改既有握持形体键中的手部顶点：按实际指节分别弯曲四指、独立处理拇指对掌、保留掌心托枪弧度，并修正手部接触面。枪、服装、脸、肤色、手臂骨骼和手腕位置不变。

研究依据与工序见 `HAND_STUDY.md`。新增 `hand_grip_pose.json`、`hand_contacts.py` 和严格的手部范围校验。检查包括指节三角面不塌陷、基网格手部样点没有超过 2mm 的枪托/枪管穿插，以及模拟脸部、肤色和武器误改时冻结检查必须失败。这个采样检查不等于已经验证完整动画的所有帧。

当前手部审看图保存在 `Builds/ArtReview/HandRefinement/`。上一轮母版另有本地只读对照副本 `frozen_before.blend`，且可从 Git 的前一版本找回。

### 拇指与反折修正（手部 revision 3，待视觉确认）

第二轮虽然通过接触采样，但右手拇指卷到了枪托下方，掌部圆弧与指节旋转叠加也造成左手过度卷曲。第三轮减小掌骨弧度，四指采用正向屈曲限制，并将掌部角度计入总弯曲上限；拇指改为按独立朝向绕过枪托，不再用指尖接触优化把它卷到下方。仍只修改原握持形体键的手部顶点。

`hand_grip_pose.json` 的 `thumb_directions_world` 控制两手拇指各节朝向；`trigger` 是人物右手，`support` 是人物左手。新增正面视角下枪托/枪管对拇指的遮挡采样、拇指高度与掌指累计弯曲检查。这些是静态几何回归，不替代近景美术审看，也不代表完成动画碰撞验证。

本轮近景和上一轮备份位于 `Builds/ArtReview/HandRefinement-v3/`；冻结范围校验沿用上一版基准。非手部造型、皮肤颜色、骨骼姿态与装备没有重新制作。

## 当前交付边界

### 双姿势审看稿（未定稿，不覆盖母版）

用户要求放弃原握姿，改看「举枪瞄准」和「胸前持枪」两个方案。两者均从本目录同一母版派生，人物、衣甲和枪的基础网格/材质不重做。只调整骨骼姿态、握持形体、武器整体位置；瞄准稿另有可逆的轻微低头/侧靠动作，胸前稿不动头部。

- 参数：`weapon_pose_presets.json`。`trigger` 为人物右手，`support` 为人物左手；握点、掌心朝向和手肘参考点分别设置，不能再把原握姿整体旋转成瞄准。
- 制作脚本：`tools/art/human_weapon_poses.py`，复用现有 `human_hands.py` / `hand_contacts.py`，后者新增可选武器坐标变换，默认母版流程不变。
- 本地输出：`Builds/ArtReview/WeaponPoses-v1/aim/aim.blend`、`chest/chest.blend`，各有全身、手部、侧面渲染和验证报告。它们是可重建的未确认审看资产，不覆盖正式母版，也不替换 Unity 战场。
- 校验：重新打开保存文件，检查基础造型和材质不变、枪各部件保持刚性整体、手腕连续且折角小于 35 度、指面未塌陷、静态基网格接触样点无超过 2mm 的枪托/枪管穿插。这不是完整皮肤碰撞或动画验收。

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python-exit-code 1 --python tools/art/human_weapon_poses.py
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python-exit-code 1 --python tools/art/human_weapon_poses.py -- --verify-only
```

主母版的 SHA-256 被姿势参数锁定；原稿变更后需要明确更新参数基准。已接受的运行时动画与游戏反馈保持原样。两份 `.blend` 留本地供用户选择，Git 保存源母版、姿势参数和制作/验证脚本，不提交重复生成的两套完整角色和渲染缓存。

**这是正式美术母版，不是已经替换战场全部士兵的动画资产。** 当前只有手臂握持骨架；完整的行走、开火、换刀、近战与死亡绑定尚未完成。现有 `CharacterBake` / `CharacterCrowdRenderer` 的动画及游戏反馈保持不变，不能把静态母版当作完成了动画的成品直接覆盖它们。

Unity 静态检查导出位于 `Assets/_Game/ArtGenerated/HumanBase/human_base.glb`，从母版重建，不提交生成物，也不放进 Resources 自动打入游戏包。它仅供导入/比例/造型检查；Blender 程序化木纹、微表面等并非完整烘焙贴图，不保证检查版与离线渲染逐像素一致。运行时拓扑、共享骨架和材质烘焙需要下一阶段完成。

## 验证与查看

在项目根目录运行（macOS）：

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python-exit-code 1 --python tools/art/verify_human_base.py
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python-exit-code 1 --python tools/art/verify_human_base.py -- --test-freeze-guard --render-hands
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
