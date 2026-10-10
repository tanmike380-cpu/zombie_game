# Tripo 独立视图素材包

更新：2026-10-11。**这里的每张 PNG 只有一个单位的一个视角，不是多人合照，也不是三视图拼图。**

当前上传清单为 **99个素材、297张独立PNG**：挂件版空手T-pose人物72张、步兵武器24张、僵尸21张、器械18张、建筑162张。历史人物72张、基础人体18张、旧塔18张、单图武器8张原址保留，不再列为当前目标。另有6张塔纯俯视辅助图，不计入主视图数量。没有上传Tripo、消耗Tripo积分或生成游戏模型；携弹数值另见[正式balance](../../../balance/README.md)。

## 最新上传入口 · 2026-10-11

两国各4兵种、各民兵／老兵／精英，每等级3张。人物保留认可衣甲，只有手持武器单独生成；箭筒、弩矢袋、箭盒、葫芦、水囊等挂件随人物概念生成。民兵裸手、老兵皮甲皮手套、精英锁甲护手，天朝精英保留小型覆手甲。

| 阵营 | 兵种 | 三级人物目录 | 武器三角度目录 |
|---|---|---|---|
| 天朝 | 弓箭手 | [archer](humans/tianchao/archer-kit-v1/) | [bow](weapons/tianchao/bow-views-v2/) |
| 天朝 | 诸葛弩手 | [repeating-crossbowman](humans/tianchao/repeating-crossbowman-kit-v1/) | [repeating-crossbow](weapons/tianchao/repeating-crossbow-views-v2/) |
| 天朝 | 普通弩手 | [crossbowman](humans/tianchao/crossbowman-kit-v1/) | [crossbow](weapons/tianchao/crossbow-views-v2/) |
| 天朝 | 三眼神铳兵 | [three-eyed-handgunner](humans/tianchao/three-eyed-handgunner-kit-v1/) | [three-eyed-handgun](weapons/tianchao/three-eyed-handgun-views-v2/) |
| 拜占庭 | 弓箭手 | [archer](humans/byzantine/archer-kit-v1/) | [bow](weapons/byzantine/bow-views-v2/) |
| 拜占庭 | 普通弩手 | [crossbowman](humans/byzantine/crossbowman-kit-v1/) | [crossbow](weapons/byzantine/crossbow-views-v2/) |
| 拜占庭 | 投矛军团 | [javelin-legionary](humans/byzantine/javelin-legionary-kit-v1/) | [javelin](weapons/byzantine/javelin-views-v2/) |
| 拜占庭 | 火绳枪手 | [matchlock-infantry](humans/byzantine/matchlock-infantry-kit-v1/) | [matchlock](weapons/byzantine/matchlock-views-v2/) |

人物先进入militia、veteran或elite，再取front.png、side.png、back.png；武器目录直接有这三个文件。一次只用同一个模型的三张角度图，不能混不同等级，也不能混人物和武器。单图入口通常先上传front，支持多视图的入口则分别填对应槽位。图片没有拼在一起。

[人物完整提示词与来源](../2026-10-10-infantry-kits/README.md) · [武器提示词与来源](../2026-10-10-weapon-views/README.md)。AI参考不是标定CAD图；细小扣件／纹样差异需在模型生成时统一，弓弦与可隐藏箭束需检查分件。

[用户已选自然矿脉的石／铁／金9张独立三视图](../2026-10-10-resource-nodes/README.md) · [僵尸老巢候选](../2026-10-11-zombie-lairs/)。这些在独立新目录，不计入上述297张。

最新技术路线：Tripo模型与初始绑定，Unity装配独立武器并生成／修正复杂攻击动作；Blender保存正式源模型并修网格、蒙皮和机构分件。[工具核实与限制](../../../docs/dev/TRIPO_UNITY_ANIMATION_WORKFLOW.md)。没有启用Unity AI或完成实际射击样本；生成动作不等于自动做好弓弦、箭和伤害时机。

## 历史上传指南 · 2026-10-05（以下人体与武器单图仅存档）

本节至“历史人物图”说明旧18张基础人体与8张武器单图，不再定义当前输入。最新请使用上方目录；不得把旧版无挂件人体误当作已更新图。下方僵尸、器械、建筑目录继续有效。

1. 人物先选阵营和等级，例如 `humans/tianchao/base/militia/`；人物不带武器或兵种专属挂件。
2. 该文件夹内 `front.png` 是正面、`side.png` 是侧面、`back.png` 是背面，三张都是独立文件。
3. **单图生成**时一次上传其中一张，通常先用正面；其余留作修正参考。
4. 如果使用支持多视图的入口，把同一文件夹的三张图分别放到对应视角槽位。不把民兵、老兵、精英九张图混成同一个模型，也不把合并底稿传进去。
5. 武器从 `weapons/` 选择，每件只有 `reference.png`。**人物、武器分别生成**，不要把两张图当作同一物体的两个视角，也不要把不同等级混成一个模型。
6. “批量生成多个模型”与“一个模型的多视图”不同。先试一个基础人物和一件武器，检查身体、骨骼与握持后再批量处理。

### 人类：每个阵营九张，共十八张

两国各有民兵、老兵、精英三个空手 T-Pose 基础人物；每个等级各正／侧／背三张，独立文件，不拼图。长袖、甲型与阵营服装保留；民兵裸手、老兵皮革甲与皮手套、精英锁甲护手，天朝精英保留小型覆手片甲。武器、盾牌、箭筒和投矛束不放在基础人物上，后续作为独立部件装配。

| 阵营 | 等级 | 三张图所在目录 |
|---|---|---|
| 天朝 | 民兵 | [militia](humans/tianchao/base/militia/) |
| 天朝 | 老兵 | [veteran](humans/tianchao/base/veteran/) |
| 天朝 | 精英 | [elite](humans/tianchao/base/elite/) |
| 拜占庭 | 民兵 | [militia](humans/byzantine/base/militia/) |
| 拜占庭 | 老兵 | [veteran](humans/byzantine/base/veteran/) |
| 拜占庭 | 精英 | [elite](humans/byzantine/base/elite/) |

例如天朝民兵基础人物：[正面](humans/tianchao/base/militia/front.png)、[侧面](humans/tianchao/base/militia/side.png)、[背面](humans/tianchao/base/militia/back.png)。同一人物可装配弓、弩或枪；兵种属性和动作不因共用外形而合并。

### 独立武器：每种一张

| 阵营 | 武器 | 独立输入图 |
|---|---|---|
| 天朝 | 弓 | [bow](weapons/tianchao/bow/reference.png) |
| 天朝 | 诸葛连弩 | [repeating-crossbow](weapons/tianchao/repeating-crossbow/reference.png) |
| 天朝 | 普通弩 | [crossbow](weapons/tianchao/crossbow/reference.png) |
| 天朝 | 三眼神铳 | [three-eyed-handgun](weapons/tianchao/three-eyed-handgun/reference.png) |
| 拜占庭 | 投矛 | [javelin](weapons/byzantine/javelin/reference.png) |
| 拜占庭 | 弓 | [bow](weapons/byzantine/bow/reference.png) |
| 拜占庭 | 普通弩 | [crossbow](weapons/byzantine/crossbow/reference.png) |
| 拜占庭 | 单发火绳枪 | [matchlock](weapons/byzantine/matchlock/reference.png) |

弓与普通弩保留两国现有造型，所以各有两个阵营版本；没有按民兵／老兵／精英重复画武器。工程重弩、投石机、蜂巢炮与希腊火仍在 `siege/`，不作为手持武器。

Tripo 先给空手人物绑骨骼；武器独立导出后，在正式 Blender 主文件中固定到手部／武器骨骼，配合握持动作。不依赖 AI 自动识别旁置武器或生成捡武器动作。弓弦、箭和装填机构另处理，不将整张弓当作一块刚性模型。盾牌、箭筒、箭束等挂件后续单独制作，不在本轮八张武器中隐含为已完成。

### 历史人物图

旧的两国八兵种×三级共72张仍在原目录，登记于 `manifest.json` 的 `archived_assets`，仅作服装与武器造型追溯，不再上传生成第二套人物。2026-10-04 的拜占庭旁置武器 T-Pose 图也属于此历史集合。

[本轮源图与提示词](../2026-10-05-modular-infantry/)由内置 image_gen 制作，不是已绑定模型或尺度标定正交测绘；跨视图细节与武器机械结构仍需在建模时统一，尚未验证 Tripo 生成／绑定成功率。

### 箭筒与剩余弹药：后续模型制作建议，尚未接入运行时

不需要生成一整套“空箭筒人物”。在 Blender 中将人物、箭筒、筒内箭束分为独立部件，补好空筒内壁与筒底、UV 和材质，避免箭束被烘焙进筒口贴图。若 AI 网格粘连，需要先清理或局部重建，不能保证一键分离成功。

Unity 读取现有弹药状态，零弹药隐藏箭束，补给后恢复；箭筒保留。首版建议仅有箭／空筒两态，后续可做满／半／少／空档位，不逐发替换整个人物，也不把箭束显示当作实际弹药数据来源。箭筒挂在骨骼上，箭束跟随箭筒，同一人物骨架和动作无需为此复制。注意批量单位的额外渲染开销，最终需实机验证。

参考：[Blender 分离网格](https://docs.blender.org/manual/en/2.90/modeling/meshes/editing/mesh/separate.html)、[Unity 显隐对象](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/gameobject/setactive)。

### 僵尸：七种，各三张

| 类型 | 目录 |
|---|---|
| 普通僵尸 | [01-common](zombies/01-common/) |
| 自爆尸 | [02-exploder](zombies/02-exploder/) |
| 犬尸 | [03-hound](zombies/03-hound/) |
| 胖尸 | [04-fat](zombies/04-fat/) |
| 毒液尸 | [05-venom](zombies/05-venom/) |
| 头槌巨尸 | [06-headram](zombies/06-headram/) |
| 背负投尸巨尸 | [07-carrier](zombies/07-carrier/) |

头槌巨尸保留厚额骨的撞墙轮廓，不是手持锤子的怪物。投尸巨尸于 2026-09-30 再修订：躯干更前倾佝偻、手臂加长至接近脚踝、背笼改为宽高接近 1:1 的正方形木笼，保留小头与长发。新版已覆盖原正／侧／背文件，不另留旧版供上传；旧版可从 Git 提交 `4ee1e74` 恢复。被投掷的小僵尸应复用独立普通僵尸模型，在 Unity 中挂载，避免生成时把多具身体融为一体。

2026-10-04：头槌巨尸的三个独立文件已替换为 **T-Pose 绑定参考图**，保留骨质头壳、体型、绳索及破布配色，改为展开双臂的中立姿态。原动作姿态仍可从 Git 历史恢复。[生成来源与提示词](../2026-10-04-tpose/provenance.json)。其他类型尚未改为 T-Pose，不要把整目录误认为已全部转换。三张是 AI 外观参考而非标定正交图，尚未验证 Tripo 生成或绑定成功；生成后仍需检查手臂与躯干分离、肩肘位置及蒙皮。游戏中的弯腰、撞击由动画恢复，不用绑定姿势替代战斗动作。

### 工程器械：两国六种，各三张

- 天朝：[重弩](siege/tianchao/ballista/)、[投石机](siege/tianchao/trebuchet/)、[蜂巢炮](siege/tianchao/nest-of-bees/)。
- 拜占庭：[重弩](siege/byzantine/ballista/)、[投石机](siege/byzantine/trebuchet/)、[希腊火](siege/byzantine/greek-fire/)。

当前参考的是展开／工作状态。行军折叠、车轮、弩弦、投臂需要在建模后拆分和制作动作，不会因三视图自动成为可活动机构。

### 建筑：54 个方案，各三张

[天朝建筑目录](buildings/tianchao/) · [拜占庭建筑目录](buildings/byzantine/)。每个建筑独立文件夹，名称与编号沿用总概念图册。

最新修订：两国箭塔、炮塔及拜占庭喷火塔俯视 **2×2格正方形**；天朝长城／蜂巢炮塔**正面宽3格、进深2格**。六种塔共18张独立主视图，位于新建的 `*-footprint-v2/` 目录，旧目录18张图保留。下部石墩垂直，不做梯形。背面门放大到约石墩高35–40%、面宽25–30%，仍为朴素关闭木门，处于武器发射方向反面。前侧无门，无梯子或贯通开口，顶部造型、武器和阵营配色保留。**门仅外观，塔仍阻挡双方；防线通行只使用木门、石门。** 本轮没有改变真实游戏占地或网格。

新版直接复制完整单视图源图，`manifest.json` 标为 `copy_source=true`，旧塔登记在 `archived_assets`，重复导出不覆盖旧图。[六种新版塔目录、俯视图和提示词](../2026-10-05-square-towers/README.md)是最新入口。之前的[实心石墩图](../2026-10-05-solid-towers/)、[首轮小背门图](../2026-10-05-rear-entry-towers/)与合并母版仅供历史追溯。所有图片是 AI 参考，不是尺度标定蓝图；跨视图细节需要 Blender 统一，真实2×2/2×3须以建模测量和游戏格子验收。

- `front-perspective.png`：正面方向的斜俯视主图。
- `side.png`：原稿侧面参考。
- `rear-perspective.png`：背面方向的透视参考。

**建筑原稿不是严格正交三视图。** 初始整理保留已确认造型并切开，没有重画 54 栋建筑；上述六种防御塔现按用户要求单独编辑，其他建筑仍沿用原稿。正、背视角保留透视命名，不能当成精确施工图。原稿较小的侧面／背面不做虚假放大。建筑周围的地面、木堆、树木等可能被 Tripo 一起生成，游戏中应按需要拆分或清理。

主基地三个时代都保留宏伟 v2 版；不是只保留三时代。六张小体量主基地首稿、两张已取消城堡和一张旧拜占庭木屋已从当前目录删除，仍可从 Git 历史恢复。其他功能建筑没有另一个已确认的“宏伟版”，沿用现有最新版。

## 动画是否能共用

Tripo 提供自动骨骼／蒙皮和预设动画，并可导出带骨骼的 FBX 或 GLB；这不等于一定能直接生成符合我们武器结构的拉弓、装填、三眼神铳三连发等完整动作。[官方教程](https://www.tripo3d.ai/blog/tripo-studio-tutorial-english) · [官方自动绑定说明](https://www.tripo3d.ai/features/ai-auto-rigging)。

建议每个兵种的民兵、老兵、精英共用兼容骨架和动作。最新为Tripo初始绑定、Unity复杂战斗动作与武器装配、Blender源模型与蒙皮修复。同一套动作不要求三个模型顶点数相同，但每个模型都必须正确绑定权重；骨骼层级、参考姿态和比例需要兼容。不兼容骨架要先重定向，不能承诺任意导出直接套用。

当前人物是带兵种挂件的空手T-pose，武器单独输入。武器与手分开，弓弦、弹丸与烟雾独立处理。弓箭手三等级可共用拉弓动作，但弩手和火枪手不应强行套同一攻击动作；换武器会切换动作集。动作复用后需检查握持与穿插。这批图不是自动绑定已验收的模型。

## 来源与检查

- [manifest.json](manifest.json)：当前 `assets` 与历史 `archived_assets` 分开；包含源图、视角、裁切或直接复制方式、尺寸及 SHA-256。`assets` 是当前上传清单，历史人物与旧塔不会重复导出为新上传任务。
- [补画提示词](generation-prompts.json) 与 [实际生成来源](../2026-09-30-turnarounds/provenance.json)：16 个民兵／老兵、七种僵尸、三种天朝器械的新增角度，由内置 `image_gen` 生成。合并底稿只用于保持各角度外观一致，不用于 Tripo 输入。
- [手部修订记录](../2026-09-30-hand-revision.json)：以原多视图局部编辑手部和拜占庭弓箭手散箭；原补画提示词及 provenance 为初始生成历史，不代表当前手套标准。器械不变。
- 裁切仅分离视图、遮掉邻图与添加留边；未用图像处理脚本编造缺失视角。AI 补画仍可能存在跨角度装饰或结构差异，最终以建模时统一为准，并非摄影测量数据。
- 已逐页查看全部输出缩略图，并修正长城、打猎小屋、大浴场、石门的相邻视图混入及精英弩手的武器裁切；未进行 Tripo 生成成功率测试。

在仓库根目录运行（Python 3 + Pillow）：

```sh
python3 tools/art/concept_views.py export
python3 -m tools.art.concept_review validate
```

开发用面板定位器需要 NumPy、SciPy。它只用于初始候选遮罩，不能替代视觉检查；重新运行候选定位／生成清单后，需执行 `python3 -m tools.art.concept_review correct` 恢复人工修正并重新验图。
