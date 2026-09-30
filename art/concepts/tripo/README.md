# Tripo 独立视图素材包

更新：2026-09-30。**这里的每张 PNG 只有一个单位的一个视角，不是多人合照，也不是三视图拼图。**

共 91 个模型版本、273 张独立 PNG：人类 72 张，僵尸 21 张，工程器械 18 张，建筑 162 张。本轮只整理二维参考图，没有上传 Tripo、消耗 Tripo 积分、生成游戏模型或修改正式游戏数值。

## 怎么上传

1. 先选一个阵营、兵种和等级，例如 `humans/tianchao/archer/militia/`。
2. 该文件夹内 `front.png` 是正面、`side.png` 是侧面、`back.png` 是背面，三张都是独立文件。
3. **单图生成**时一次上传其中一张，通常先用正面；其余留作修正参考。
4. 如果使用支持多视图的入口，把同一文件夹的三张图分别放到对应视角槽位。不把民兵、老兵、精英九张图混成同一个模型，也不把合并底稿传进去。
5. “批量生成多个模型”与“一个模型的多视图”不同。先试一个兵种，确认没有生成三个人、武器没有粘连，再批量处理。

### 人类：一个兵种九张图

每个下列目录都有 `militia/`（民兵）、`veteran/`（老兵）、`elite/`（精英），各三张。

| 阵营 | 兵种 | 九张图所在目录 |
|---|---|---|
| 天朝 | 弓箭手 | [archer](humans/tianchao/archer/) |
| 天朝 | 诸葛弩手 | [repeater](humans/tianchao/repeater/) |
| 天朝 | 普通弩手 | [crossbowman](humans/tianchao/crossbowman/) |
| 天朝 | 三眼神铳手 | [three-eyed](humans/tianchao/three-eyed/) |
| 罗马／拜占庭 | 军团投矛手 | [javelineer](humans/byzantine/javelineer/) |
| 罗马／拜占庭 | 弓箭手 | [archer](humans/byzantine/archer/) |
| 罗马／拜占庭 | 弩手 | [crossbowman](humans/byzantine/crossbowman/) |
| 罗马／拜占庭 | 火枪手 | [musketeer](humans/byzantine/musketeer/) |

例如天朝民兵弓箭手：[正面单图](humans/tianchao/archer/militia/front.png)、[侧面单图](humans/tianchao/archer/militia/side.png)、[背面单图](humans/tianchao/archer/militia/back.png)。2026-09-30 手部修订：两国均为民兵裸手、老兵皮革手套、精英锁甲手套；天朝精英加小型东方覆手甲。长袖、身体甲胄和兵种玩法不变。拜占庭弓箭手取消手持散箭，箭筒内箭束保留。旧文件已原路径更新，不需寻找另一个版本目录。

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

### 工程器械：两国六种，各三张

- 天朝：[重弩](siege/tianchao/ballista/)、[投石机](siege/tianchao/trebuchet/)、[蜂巢炮](siege/tianchao/nest-of-bees/)。
- 拜占庭：[重弩](siege/byzantine/ballista/)、[投石机](siege/byzantine/trebuchet/)、[希腊火](siege/byzantine/greek-fire/)。

当前参考的是展开／工作状态。行军折叠、车轮、弩弦、投臂需要在建模后拆分和制作动作，不会因三视图自动成为可活动机构。

### 建筑：54 个方案，各三张

[天朝建筑目录](buildings/tianchao/) · [拜占庭建筑目录](buildings/byzantine/)。每个建筑独立文件夹，名称与编号沿用总概念图册。

- `front-perspective.png`：正面方向的斜俯视主图。
- `side.png`：原稿侧面参考。
- `rear-perspective.png`：背面方向的透视参考。

**建筑原稿不是严格正交三视图。** 本轮保留已确认造型并切开，没有擅自重画 54 栋建筑；正、背视角保留透视命名，不能当成精确施工图。原稿较小的侧面／背面不做虚假放大。建筑周围的地面、木堆、树木等可能被 Tripo 一起生成，游戏中应按需要拆分或清理。

主基地三个时代都保留宏伟 v2 版；不是只保留三时代。六张小体量主基地首稿、两张已取消城堡和一张旧拜占庭木屋已从当前目录删除，仍可从 Git 历史恢复。其他功能建筑没有另一个已确认的“宏伟版”，沿用现有最新版。

## 动画是否能共用

Tripo 提供自动骨骼／蒙皮和预设动画，并可导出带骨骼的 FBX 或 GLB；这不等于一定能直接生成符合我们武器结构的拉弓、装填、三眼神铳三连发等完整动作。[官方教程](https://www.tripo3d.ai/blog/tripo-studio-tutorial-english) · [官方自动绑定说明](https://www.tripo3d.ai/features/ai-auto-rigging)。

建议每个兵种的民兵、老兵、精英共用一套标准骨架和动作，在 Blender 中修正或制作，在 Unity 中播放。同一套动作不要求三个模型顶点数相同，但每个模型都必须正确绑定权重；骨骼名称、层级、参考姿态和比例需要兼容。不兼容的独立骨架要先重定向，不能承诺任意三个导出模型直接套用。

这些是持械外观参考，不是自动绑定已验收的模型。制作动画时建议准备 A/T 姿态，武器与手分开，弓弦、弹丸与烟雾独立处理。弓箭手三等级可以共用拉弓动作，但弩手和火枪手不应强行套同一攻击动作。动作复用后还要检查手与武器接触、袖口及铠甲穿插。

## 来源与检查

- [manifest.json](manifest.json)：全部源图路径、裁切区域、遮罩、尺寸及 SHA-256。它是当前输出的权威清单。
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
