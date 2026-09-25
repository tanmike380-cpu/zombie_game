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

### 握枪与右肩抵托修正（RTSLightweight-v2，待视觉确认）

用户追加了六项修正：手腕连续、食指扣扳机、左手前移、全长护木、手掌包住护木、枪托抵右肩。这次在既有制作链上修改，不覆盖精细母版，不改衣甲/铆钉/脸部材料，不替换生产动画。

- `weapon_pose_presets.json`：新增武器坐标下的手腕偏移；两臂按实际长度解算，腕部保持中立，枪托移至右肩接触区域。
- `matchlock_stock.py`：只对被授权修改的木质枪托做几何变形。保留机匣握点，护木末端由 1.06m 延长到 1.49m（枪管末端 1.50m）；后枪托下沉 9cm，落在肩部而非脸部。保存重读时逐顶点比较预期木托几何，其他零件仍使用原刚性校验。
- `human_rts_hands.py`：整体 C 形开口朝枪管，手指块包住护木侧面与底面；右食指单独弯到实际扳机位置。仍是简化手，不加指甲/细关节。
- `human_wrist_surface.py`：在最终静态轻量网格里移除旧手部残留，从前臂切面桥接到新掌根，连接边共享顶点；不是用两个封口模型相互穿插遮缝。重读检查两侧连接区域没有开放皮肤边。
- 当前输出：`Builds/ArtReview/WeaponPoses-v3/` 为可编辑握姿输入（手部暂时仍是分件）；`Builds/ArtReview/RTSLightweight-v2/` 为已连接手腕的最终静态审看网格及 `review.html`。不要拿前者当作已焊接的交付网格。
- `--verify-only` 检查源文件、肩托/握距/护木长度、保存网格、铆钉与虹膜颜色；`human_rts_lightweight.py -- --render-only` 可重新出图，全身镜头自动包住加长枪口。

这里的「扣扳机」是静态姿态展示，尚未制作手指扣动动画。没有进行万人性能测试。旧 v1 文件留作对照；新的共享动画与 Unity 接入仍需独立验收。

### RTS 轮廓轻量派生（2026-09-25，待确认）

用户允许对整体做轻量简化：参考其提供的《亿万僵尸》俯视截图，不再要求远景看不清的独立指节。此授权只用于派生审看，精细母版仍保留，不直接覆盖战场动画。

- 手部：`human_rts_hands.py` 将四指合为圆润 C 形握块，保留短拇指；右手增加简化食指。旧解剖手通过可逆 Mask 隐藏，原拓扑不删除。参数仍在 `weapon_pose_presets.json`，两个姿势输出到 `Builds/ArtReview/WeaponPoses-v2/`。
- 全身：`human_rts_lightweight.py` 从上述已校验姿势派生，适度加宽/缩短整体比例、稍放大头部，减少细褶网格及高密度装饰；保留圆润法线、红甲、黑色包边、葫芦和火绳枪。可编辑配置为 `rts_lightweight.json`，不是游戏平衡数据。
- **铆钉不删除**：按空间间隔减少数量、放大单颗尺寸，保留胸肩甲和盔甲的金属紧固件。配置 `rivet_spacing_m` / `rivet_scale` 控制甲身；`helmet_rivet_*` 控制头盔和护颈。密度检查防止把铆钉全部删掉。
- 全身候选：`Builds/ArtReview/RTSLightweight-v1/aim/aim_rts.blend` 和 `chest/chest_rts.blend`。同目录保存渲染及网格/铆钉统计。`rts.png` 是 280×280 的小尺寸静态俯视预览，不是 Unity 截图或帧率测试。
- 验证分开：握姿版本校验非手部原始网格/材质冻结、手腕连接及可见手样点；轻量版本检查三角面预算、装备轮廓要素和铆钉保留。轻量版为静态烘焙，不声称保留生产动画绑定；程序化材质还未烘焙为统一游戏贴图。

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python-exit-code 1 --python tools/art/human_weapon_poses.py -- --no-render
/Applications/Blender.app/Contents/MacOS/Blender --background --threads 6 --python-exit-code 1 --python tools/art/human_rts_lightweight.py
```

下面 v1、精细指节等段落保留制作历史，不代表目前推荐的 RTS 手部方案。当前未完成万人性能验证；减面不能替代动画、材质批次、LOD、阴影及导航开销的测量与优化。

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
