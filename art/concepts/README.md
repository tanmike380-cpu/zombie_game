# 游戏概念图总册

统一入口：人类两国、工程器械、七种僵尸、两国建筑与三个时代，以及历史人物风格探索。图片直接存放在本目录下并随 Git 分发，不依赖本机绝对路径或被忽略的 `Builds` 文件夹。

**本次新增**：[建筑多视图与雄伟版主基地](#八建筑概念--三时代与两国风格) · [时代解锁与建筑功能](../../design/08_buildings_and_ages.md)。

## 使用规则与版本状态

- **单位最新评审稿**：2026-09-28 十一张独立三视图（两国 8 种步兵 + 罗马 3 种器械），每个单位一张，正面 / 侧面 / 背面。新增建筑见第八节，均待用户评审定稿。
- **罗马统一甲型**：四步兵共用拜占庭头盔、钢制片甲、护肩、护臂、裙甲与靴子，紫衣与少量金饰；主要通过武器与军需挂件区分。天朝继续使用红甲体系。
- **长袖与手套为最新共同要求**：所有人类步兵都用长袖。拜占庭戴锁子甲手套，天朝戴深棕皮革手套，袖口与手套交叠不露手腕。本轮 8 张独立步兵图均遵循此规范；旧整队图中裸手或卷袖均不再是制作依据。
- **重弩修订**：去掉原“钥匙头”空框，强调横弓、中央箭槽、单支粗弩箭、绞盘。旧方案只作存档。
- **概念不等于实现**：图示为 AI 生成的二维外观提案，不是已经完成的 Blender 模型、精确正交施工图或 Unity 实机效果。跨视图的小结构差异须在建模时统一，以文字甲型规范优先；不能直接作为机械制作图。
- **设计与数值**：[兵种设计](../../design/03_player_units.md)、[僵尸设计](../../design/04_zombies.md)；运行时数值仍唯一来自 [unit_balance.json](../../balance/unit_balance.json)，本轮未修改。
- **归档范围**：包含本次盘点找到的原创概念图（含历史版本），不把模型渲染截图、动画帧、第三方商品预览或下载素材混作自有概念图上传。
- **来源**：本轮使用内置 `image_gen`，非 CLI。完整提示词：[独立三视图](2026-09-28-factions/PROMPTS.md)、[长袖手套修订](2026-09-28-factions/GLOVE_REVISION_PROMPTS.md)、[天朝其余三兵种](2026-09-28-factions/TIANCHAO_PROMPTS.md)、[本轮首稿](2026-09-28-factions/history/PROMPTS.md)、[2026-09-27](2026-09-27/PROMPTS.md)。早期探索保留现存原图，不伪造缺失提示词。

### 后续建模减负规范（不代表模型已修改）

1. **手套化握持**：静态握持轮廓使用掌体、拇指、简化食指，其余三指可合为一块；不要逐指雕关节、指甲和掌纹。射击只保留扣扳机的视觉暗示；握持处可由武器遮挡，不能借简化接受明显穿模。
2. **覆盖接缝**：长袖与手套交叠；头盔护耳和领口覆盖耳部/颈部连接；裤脚藏入靴筒或绑腿。后面两项是建模建议，不擅自改变已确认脸与盔甲外形。
3. **纹理代替微几何**：锁子甲细环、缝线、织物纹理、细小皮肤纹理使用法线/颜色/粗糙度贴图，不逐环建模。链甲手套保持连续手套网格。
4. **轮廓保留**：头盔、肩背、武器、手套握持及腰间葫芦保留清晰外轮廓；用户要求的大而稀疏铆钉保留，不把盔甲全部抹平。
5. **共用母版**：同阵营共用人体、甲型、手套和骨骼，通过武器及军需挂件派生，减少重复建模和蒙皮。不在用户修模期间擅自重绑骨骼或覆盖正式资产。
6. **按镜头距离分级**：游戏远景减少面部与手部几何、使用低细节版本；近景保留已认可外观。暂不许诺具体面数或 FPS，导入与万人测试后再定预算。

## 一、罗马（拜占庭）步兵 · 独立三视图

### 01 罗马军团 · 投矛手

远程投矛、具穿透效果；射程比弓箭手少 1 格。穿透数量、衰减与具体伤害未定。盾牌是美术提案，不据图擅自增加减伤数值。

![罗马投矛手三视图](2026-09-28-factions/01-roman-javelineer.png)

### 02 罗马弓箭手

沿用天朝弓箭手机制，拜占庭紫色统一甲型。

![罗马弓箭手三视图](2026-09-28-factions/02-roman-archer.png)

### 03 罗马弩手

沿用普通弩手机制，不是诸葛连弩，保持单发弩识别。

![罗马弩手三视图](2026-09-28-factions/03-roman-crossbowman.png)

### 04 罗马火枪手

单发历史火枪；与天朝三眼神铳的三连发区分。

![罗马火枪手三视图](2026-09-28-factions/04-roman-musketeer.png)

## 二、罗马工程器械 · 独立三视图

本轮罗马阵营使用巨型重弩、投石机、希腊火，希腊火替代蜂巢炮。天朝是否也取消蜂巢炮仍待明确，本图册不擅自删除旧资源。

### 05 巨型重弩 · 修订

![罗马巨型重弩三视图](2026-09-28-factions/05-roman-ballista.png)

### 06 投石机

![罗马投石机三视图](2026-09-28-factions/06-roman-trebuchet.png)

### 07 希腊火喷射器

青铜喷口与轮式底盘，持续喷火方向；图中不点火以便看清外形。喷火伤害、距离、燃料资源、架设需求待定。

![罗马希腊火三视图](2026-09-28-factions/07-roman-greek-fire.png)

## 三、天朝步兵 · 独立三视图

### 08 三眼神铳兵

替代天朝原火枪手，保留红甲。三连发消耗 3 发弹药；本轮将“威力低一点”解释为单发较弱，具体伤害待确认；射程比单发火枪少 1 格。本轮仅概念，不代表运行时已改成三连发。

![天朝三眼神铳兵三视图](2026-09-28-factions/08-tianchao-three-eyed.png)

### 09 天朝弓箭手

红甲、长袖、皮手套；弓与箭袋区分兵种。

![天朝弓箭手三视图](2026-09-28-factions/09-tianchao-archer.png)

### 10 天朝诸葛弩手

与天朝其余步兵同甲型，顶部木质箭匣与操纵杆体现连发弩身份。

![天朝诸葛弩手三视图](2026-09-28-factions/10-tianchao-repeater.png)

### 11 天朝普通弩手

同一红甲长袖皮手套母版，单发弩，不带连弩箭匣。

![天朝普通弩手三视图](2026-09-28-factions/11-tianchao-crossbowman.png)

## 四、已有天朝与僵尸概念 · 2026-09-27

以下保留此前整组原图；本次未伪称它们已改成独立三视图。

### 天朝弓箭手、诸葛弩手、普通弩手

![天朝远程步兵](2026-09-27/01-ranged-infantry.png)

### 天朝工程器械历史方案

其中蜂巢炮不是本轮罗马设计依据；巨弩建模应避免旧图框形结构问题。

![天朝工程器械历史方案](2026-09-27/02-siege-engines.png)

### 普通僵尸、自爆尸、尸犬、胖尸、毒液尸

前三种为低阶，胖尸中阶，毒液尸高阶；不是一图一个等级。

![五种常规僵尸](2026-09-27/03-zombies-01-05.png)

### 头槌巨尸、背负投尸巨尸

头槌消耗自身生命撞墙，不投锤；投尸巨尸将背上小僵尸掷向城墙，撞击后死亡，不生成墙内增援。

![两种攻城Boss](2026-09-27/04-zombies-06-07.png)

## 五、本轮整队初稿 · 已被三视图修订覆盖

仅展示设计演变，不作为最新统一甲型或重弩结构依据。

![罗马步兵初稿](2026-09-28-factions/history/01-roman-lineup.png)

![罗马器械初稿——钥匙头结构不再采用](2026-09-28-factions/history/02-roman-siege-lineup.png)

![天朝三眼神铳动作探索](2026-09-28-factions/history/03-tianchao-three-eyed-action.png)

## 六、早期人物探索 · 历史归档，不等于全部采用

### 火枪兵 1 · 红色

![火枪兵1](2026-09-23-matchlock/01_crimson.png)

### 火枪兵 2 · 靛蓝

![火枪兵2](2026-09-23-matchlock/02_indigo.png)

### 火枪兵 3 · 绿色（用户已淘汰）

![火枪兵3历史](2026-09-23-matchlock/03_sage.png)

### 火枪兵 4 · 黑色

![火枪兵4](2026-09-23-matchlock/04_black.png)

### 火枪兵 5 · 赭色

![火枪兵5](2026-09-23-matchlock/05_ochre.png)

### 老兵风格探索

![老兵风格历史](2026-09-23-matchlock/06-veteran-study.png)

## 七、本轮三视图的裸手旧版 · 已被手套修订覆盖

仅用于追溯，不应照旧版建裸手或卷袖。

- [罗马投矛手旧版](2026-09-28-factions/history/01-roman-javelineer-bare-hands.png)
- [罗马弓箭手旧版](2026-09-28-factions/history/02-roman-archer-bare-hands.png)
- [罗马弩手旧版](2026-09-28-factions/history/03-roman-crossbowman-bare-hands.png)
- [罗马火枪手旧版](2026-09-28-factions/history/04-roman-musketeer-bare-hands.png)
- [天朝三眼神铳兵旧版](2026-09-28-factions/history/08-tianchao-three-eyed-bare-hands.png)

## 八、建筑概念 · 三时代与两国风格

最新建筑设计见 [建筑与时代](../../design/08_buildings_and_ages.md)。27 类建筑，主基地分三个时代，共 **54 张当前建筑多视图**：天朝 27 张，拜占庭 27 张。小村庄与武装长城只有天朝，大浴场与喷火塔只有拜占庭；两国城堡已取消。每张包括 RTS 斜俯视、背面和侧面参考，不是严格正交施工图。

**主基地以雄伟版 v2 为评审依据**：一时代就具备核心主殿体量，二、三时代再增加层级与气势。首稿被指出太小，保留但不作为最新版。其他建筑为首轮评审稿，尚未定稿。

每张图为内置 image_gen 生成的二维提案，未导入 Unity 或制作 Blender 模型。图上的台阶、门窗、装饰不产生新玩法。提示词见 [首轮生成与主基地修订](2026-09-28-buildings/PROMPTS.md)、[大浴场、防御塔与木房修订](2026-09-28-buildings/REVISION_PROMPTS.md)。本轮共保存 63 张原尺寸图片，其中 54 张当前方案、9 张已替换／取消稿。所有跨视图的窗位、后门、屋顶等小差异都需要建模时统一。

### 01 主基地·时代一 · 主基地 · 1 时代

![天朝主基地·时代一](2026-09-28-buildings/tianchao/01-hq-age1-v2.png)

![拜占庭主基地·时代一](2026-09-28-buildings/byzantine/01-hq-age1-v2.png)

### 02 主基地·时代二 · 主基地 · 2 时代

![天朝主基地·时代二](2026-09-28-buildings/tianchao/02-hq-age2-v2.png)

![拜占庭主基地·时代二](2026-09-28-buildings/byzantine/02-hq-age2-v2.png)

### 03 主基地·时代三 · 主基地 · 3 时代

![天朝主基地·时代三](2026-09-28-buildings/tianchao/03-hq-age3-v2.png)

![拜占庭主基地·时代三](2026-09-28-buildings/byzantine/03-hq-age3-v2.png)

### 04 伐木场 · 资源型 · 1 时代

![天朝伐木场](2026-09-28-buildings/tianchao/04-lumber-camp.png)

![拜占庭伐木场](2026-09-28-buildings/byzantine/04-lumber-camp.png)

### 05 采石场 · 资源型 · 1 时代

![天朝采石场](2026-09-28-buildings/tianchao/05-quarry.png)

![拜占庭采石场](2026-09-28-buildings/byzantine/05-quarry.png)

### 06 农田 · 资源型 · 2 时代

![天朝农田](2026-09-28-buildings/tianchao/06-farm.png)

![拜占庭农田](2026-09-28-buildings/byzantine/06-farm.png)

### 07 铁矿厂 · 资源型 · 2 时代

![天朝铁矿厂](2026-09-28-buildings/tianchao/07-iron-mine.png)

![拜占庭铁矿厂](2026-09-28-buildings/byzantine/07-iron-mine.png)

### 08 打猎小屋 · 资源型 · 1 时代

![天朝打猎小屋](2026-09-28-buildings/tianchao/08-hunting-lodge.png)

![拜占庭打猎小屋](2026-09-28-buildings/byzantine/08-hunting-lodge.png)

### 09 渔场 · 资源型 · 1 时代

![天朝渔场](2026-09-28-buildings/tianchao/09-fishery.png)

![拜占庭渔场](2026-09-28-buildings/byzantine/09-fishery.png)

### 10 仓库 · 存储 · 1 时代

![天朝仓库](2026-09-28-buildings/tianchao/10-warehouse.png)

![拜占庭仓库](2026-09-28-buildings/byzantine/10-warehouse.png)

### 11 弹药库 · 存储 · 2 时代

![天朝弹药库](2026-09-28-buildings/tianchao/11-arrow-depot.png)

![拜占庭弹药库](2026-09-28-buildings/byzantine/11-arrow-depot.png)

### 12 火药库 · 存储 · 3 时代

![天朝火药库](2026-09-28-buildings/tianchao/12-powder-magazine.png)

![拜占庭火药库](2026-09-28-buildings/byzantine/12-powder-magazine.png)

### 13 木头房 · 人口建筑 · 1 时代

![天朝木头房](2026-09-28-buildings/tianchao/13-wooden-house.png)

![拜占庭木头房](2026-09-28-buildings/byzantine/13-wooden-house-v2.png)

### 14 石头房 · 人口建筑 · 2 时代

![天朝石头房](2026-09-28-buildings/tianchao/14-stone-house.png)

![拜占庭石头房](2026-09-28-buildings/byzantine/14-stone-house.png)

### 15 大浴场 · 拜占庭独有 · 3 时代

两国城堡取消，天朝不擅自新增替代建筑。大浴场按原分类暂放人口／公共建筑，具体收益与人口作用待定，不自动继承城堡的驻扎能力。

![拜占庭大浴场](2026-09-28-buildings/byzantine/15-grand-bathhouse.png)

### 16 小村庄 · 人口建筑 · 1 时代

![天朝小村庄](2026-09-28-buildings/tianchao/16-small-village.png)

### 17 箭坊 · 弹药型 · 2 时代

![天朝箭坊](2026-09-28-buildings/tianchao/17-arrow-workshop.png)

![拜占庭箭坊](2026-09-28-buildings/byzantine/17-arrow-workshop.png)

### 18 火药坊 · 弹药型 · 3 时代

![天朝火药坊](2026-09-28-buildings/tianchao/18-powder-workshop.png)

![拜占庭火药坊](2026-09-28-buildings/byzantine/18-powder-workshop.png)

### 19 兵营 · 战斗型 · 1 时代

![天朝兵营](2026-09-28-buildings/tianchao/19-barracks.png)

![拜占庭兵营](2026-09-28-buildings/byzantine/19-barracks.png)

### 20 工程营 · 战斗型 · 3 时代

![天朝工程营](2026-09-28-buildings/tianchao/20-engineering-camp.png)

![拜占庭工程营](2026-09-28-buildings/byzantine/20-engineering-camp.png)

### 21 哨站 · 战斗型 · 1 时代

![天朝哨站](2026-09-28-buildings/tianchao/21-outpost.png)

![拜占庭哨站](2026-09-28-buildings/byzantine/21-outpost.png)

### 22 木墙 · 防御型 · 1 时代

![天朝木墙](2026-09-28-buildings/tianchao/22-wooden-wall.png)

![拜占庭木墙](2026-09-28-buildings/byzantine/22-wooden-wall.png)

### 23 木门 · 防御型 · 1 时代

![天朝木门](2026-09-28-buildings/tianchao/23-wooden-gate.png)

![拜占庭木门](2026-09-28-buildings/byzantine/23-wooden-gate.png)

### 24 石墙 · 防御型 · 2 时代

![天朝石墙](2026-09-28-buildings/tianchao/24-stone-wall.png)

![拜占庭石墙](2026-09-28-buildings/byzantine/24-stone-wall.png)

### 25 石门 · 防御型 · 2 时代

![天朝石门](2026-09-28-buildings/tianchao/25-stone-gate.png)

![拜占庭石门](2026-09-28-buildings/byzantine/25-stone-gate.png)

### 天朝 · 箭塔 · 防御 · 1 时代

![天朝 · 箭塔](2026-09-28-buildings/tianchao/26-arrow-tower.png)

### 拜占庭 · 箭塔 · 防御 · 1 时代

![拜占庭 · 箭塔](2026-09-28-buildings/byzantine/26-arrow-tower.png)

### 天朝 · 炮塔 · 防御 · 2 时代

![天朝 · 炮塔](2026-09-28-buildings/tianchao/27-cannon-tower.png)

### 拜占庭 · 炮塔 · 防御 · 2 时代

![拜占庭 · 炮塔](2026-09-28-buildings/byzantine/27-cannon-tower.png)

### 天朝 · 长城·蜂巢炮 · 防御 · 3 时代

![天朝 · 长城·蜂巢炮](2026-09-28-buildings/tianchao/28-great-wall.png)

### 拜占庭 · 喷火塔 · 防御 · 3 时代

![拜占庭 · 喷火塔](2026-09-28-buildings/byzantine/29-flame-tower.png)

### 已取消与被替换的建筑稿

- [天朝城堡（用户取消）](2026-09-28-buildings/tianchao/15-castle.png)
- [拜占庭城堡（已改为大浴场）](2026-09-28-buildings/byzantine/15-castle.png)
- [拜占庭木头房首稿（石材过多，已改成木板墙）](2026-09-28-buildings/byzantine/13-wooden-house.png)

### 主基地首稿 · 已被雄伟版取代

- [天朝 · 主基地·时代一首稿](2026-09-28-buildings/tianchao/01-hq-age1.png)
- [拜占庭 · 主基地·时代一首稿](2026-09-28-buildings/byzantine/01-hq-age1.png)
- [天朝 · 主基地·时代二首稿](2026-09-28-buildings/tianchao/02-hq-age2.png)
- [拜占庭 · 主基地·时代二首稿](2026-09-28-buildings/byzantine/02-hq-age2.png)
- [天朝 · 主基地·时代三首稿](2026-09-28-buildings/tianchao/03-hq-age3.png)
- [拜占庭 · 主基地·时代三首稿](2026-09-28-buildings/byzantine/03-hq-age3.png)

## 保存与修改约定

总册为唯一入口；后续每个单位单独保存，多视图尽量遵循同一甲型母版。修订时保留历史，不用测试脚本覆盖已确认正式模型。本轮图片来源于原始概念目录和内置生成输出，复制到 Git 后保留原文件；同内容重复参考图按校验和去重，不重复上传。所有 PNG 原尺寸保存。
