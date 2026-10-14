# 独立环境渲染实验场

## 当前入口：Vision 1 平地地图美术预览

新场景 `Scenes/EnvironmentVision.unity`，菜单 `Zombie Game → Environment Showcase → Vision-1 → Open saved Vision review and Play`。本机 `Builds/EnvironmentVision.app` 默认就是新版；旧 `EnvironmentShowcase.app` 入口及旧预设继续保留。Tab 切聚落／海岸／水池／全景，1／2／3及滚轮缩放，H 隐藏说明。

本轮补充正式环境源的草蕨和原作者完整橡树 LOD、投地阴影、草土自然分区、原建筑及金／铁／石陈列。原五组布局与共享数值检查通过，但新草整体可见贡献不足，**新地图不是全部技术通过／美术验收完成**。细节、来源、失败证据与打开说明见 [Vision 1 地图记录](../../../docs/dev/VISION_1_MAP_REVIEW.md)。它是独立128m美术实验，不修改正式地图、导航、经济或单位表现。

## 冷暗荒野＋原建筑与矿脉（dark-v1，待玩家美术评审）

历史 dark 预览使用 `-showcaseLayout dark`。本机双击 `Builds/OpenDarkWilderness.command`；不要直接双击旧 app，其默认仍是历史 routes。Tab 切草坡／海岸／封闭水池／全景，1／2／3及滚轮缩放，H 隐藏诊断文字。先关闭上一份环境预览，避免多窗口消耗性能。

包含主基地、作坊、牧场及4栋中立建筑，金矿／铁矿／天然石矿候选各3处。模型保持原比例、子件变换与材质；地形直接用 Unity Terrain 编排。矿点仅陈列，不接经济、寻路或战斗，正式地图和 UnitBalance 未改。

源交接见 `Settings/dark-handoff.json`，技术结果见 `validation-dark-wilderness.json`。最终16件源模型／贴地／材质绑定／同帧可见性与3组石矿LOD检查通过，原 routes／cliffs 四区域及 concept 十二视图回归通过，未放宽旧阈值。石矿是许可明确的 Poly Haven Boulder 01 原作者LOD2／3，不是用户缺失的 Tripo 石矿。金、铁的许可未确认，只供本地私人评审。

尚未达到概念图的丰盛程度：浓密树林缺合适来源，草卡片在RTS视角仍细碎，水面和地形平台边缘仍需美术打磨。技术通过不等于画风验收；本版不会自动替换正式场景，也不是万人战斗性能结论。旧版本保留。菜单 `Zombie Game → Environment Showcase → Prepare and build dark wilderness` 可重建这份独立预览。

## 无人荒野概念技术预览（未美术验收）

最终真实播放器技术检查及旧routes／cliffs四区域回归通过；记录见 `validation-wilderness.json`。完整12近中远图及同帧差异图在 `Builds/ArtReview/EnvironmentShowcase/concept-wilderness-final`，旧回归分别在 `concept-routes-regression`／`concept-cliffs-regression`。父协调已实看中景并明确未通过美术目标：草坡仍扁均一、水波斜条纹过明显、树冠疏碎。此版仅保存独立技术预览，不继续以提高密度或放宽测试凑完成。1440×900 M2 Pro、60Hz限帧下12组各180帧p95 17.15–17.46ms，含主动等待；不是稳定战斗／万人性能结论。

最新方向为草原斜坡＋海岸＋封闭水池，不含任何小路、车辙、铺面、河流、矿石或建筑。来源是概念库 `05/07/09-*-wilderness-v2.png`，路径／SHA256、有限二维到128m高度场的转换假设与缺源清单在 `Settings/concept-handoff.json`。旧有路概念保留为历史记录，不再是当前方向；新布局不是概念图的精准高度提取或还原完成。

本机再次打开无路预览：双击项目 `Builds/OpenWilderness.command`（保留现有app，仅传concept参数；Builds为忽略的本机交付目录）。不要直接双击 `EnvironmentShowcase.app` 来检查无人荒野：app默认仍是历史routes，故会出现旧道路／河流。重新下载仓库后若未保留此本机快捷入口，用下方带 `-showcaseLayout concept` 的命令即可。

同一 controller 通过 `-showcaseLayout concept` 启用 `Settings/ConceptLayoutConfig.asset`；未传参数仍默认原 routes。Tab 切草坡／海岸／池塘／全景，1/2/3切近中远，G草，H HUD。概念中B/L禁用以免混入旧区域／河道。旧 `routes` 与 `cliffs` 的高度公式、配置数值、预期高度和验收阈值未修改；`ShowcaseRoutes.enabled` 仅加 `!concept_enabled`，防止本次独立布局误生成旧河流、院落和路径。

六个真实PBR资产槽仍保留；按用户最新要求 Road 权重全图严格0且不再按曲线清草／树。Grass_A与Grass_Dry的宏观干湿层次、断续林土、局部裸岩和实际水边湿泥保持自然空间含义。草坡真实起伏7m，西侧高度场海岸以可变宽度／高度和不规则岸线呈现；不是独立悬崖模型，不支持悬挑。原RockNatural整体2–4倍，只作局部小石，不堆小石冒充概念巨岩。

海水1.1m、池水3.6m分别独立表面／范围；水色读取实际257²高度场形成深浅，池岸有有界不规则扰动，不共享一张高水位plane。水面只有低成本解析波纹及局部岸线提示，没有折射、动态反射、复杂浪花或水体物理；远全景仍可见有限128m测试地块边界。浅岸与陡坡密度不等于真实海岸岩石资产。

概念专用36°斜俯视、11/19/43m正交观察尺度、35°太阳，运行时4xMSAA及同帧capture明确resolve；销毁恢复AA，不改ProjectSettings。地形PixelError0.5／smoothness上限0.08只用于concept。树预算最多110，最终实际数量由源间距及自然水岸清空决定，不用加树数补树冠。两已批准原网格分别81035／46542三角；仅概念投影阈值改为0.065／0.015，旧阈值0.18／0.015不变。概念材质alpha-to-coverage、正mip bias和轻微独立tint不改源UV、网格、图或prefab。

叶片只读核对：源独立alpha PNG实际为16-bit灰度，按真实线性红值1/257与generated8-bit alpha逐像素比较，最大误差1字节、无大于1字节误差，>=0.5覆盖均53.5012%。不能将Pillow的16-bit→RGB8截断当成源alpha误采。生成albedo sRGB、FromInput alpha、三线性mip、coverage cutoff0.5；surface visible与addshadow使用同一UV／albedo alpha和cutoff。批准原图 `art/environment/inspection/tree_lod_comparison_v3.png` 已是开放细叶冠，派生近级大幅减少叶面；打包约80–170px树高使叶片进一步亚像素化。当前没有浓密橡树／桦树／橄榄／柏树、芦苇／睡莲、巨大灰白岩体／悬挑海蚀岩源，不能把技术source_complete当成概念资产齐全。

地表实际Grass_A为2K、tile4.5m、normal0.45，24m宏观变化、邻居混合0.55、normal28–100m淡出。概念仍有明显灰绿均一、地形细节不及参考和源冠稀碎；水纹为有限解析效果。技术PASS只证明运行／像素／几何一致，不代表美术认可或概念还原。未新增单位、采集、阻挡或生产NavMesh；帧时间不是战斗性能。

运行与打包（先确认无其他Unity编辑器进程持有同项目；不要关闭未保存工作）：

```sh
/Applications/Unity/Hub/Editor/6000.6.1f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod ZombieGame.Tests.EditorTools.EnvironmentShowcaseTools.prepare_and_build_concept_preview -logFile Builds/EnvironmentShowcase-concept-build.log
"Builds/EnvironmentShowcase.app/Contents/MacOS/Zombie Game" -showcaseLayout concept -screen-width 1440 -screen-height 900 -screen-fullscreen 0
"Builds/EnvironmentShowcase.app/Contents/MacOS/Zombie Game" -showcaseLayout concept -showcaseCheck -showcaseOutput "$PWD/Builds/ArtReview/EnvironmentShowcase/concept-preview" -screen-width 1440 -screen-height 900 -screen-fullscreen 0
```

`-showcaseStyleCheck`（与-showcaseCheck并用）仅快速4中景＋技术像素检查，30帧采样不能代替完整12组各180帧。完整检查包括实际高度／Collider、五种自然层权重与第六Road=0、无河／无props、128角实际不规则干岸闭合、地形／草／源树／局部小石／海／池／崖材质各自同帧on/off。新概念不再测试道路宽度／坡度，因为用户已取消道路；原routes仍保留全部该项回归。

## 路线与中立地标样场（routes-v1）

2026-10-13：新PBR矿石版已实际打包，四区域routes及四区域旧cliffs回归全通过，摘要 `validation-routes-pbr.json`。每区域三处废墟、新IronOreRock/GoldOreRock各三处；原两矿仅恢复记录，不再使用。1440×900、M2 Pro、60Hz限帧，44组各180帧实测p95范围16.67–17.59ms（包含主动等待），非战斗基准。源码/贴地/每fixture像素/真实通道均通过；近中远截图已人工核对，normal约定仍未由Tripo文件证明，原图不翻绿。完整证据保留在 `Builds/ArtReview/EnvironmentShowcase/routes-pbr-v1` 与 `routes-cliffs-regression`，不删除旧慢帧证据。

本轮仍是同一场景／controller，`route_layout` 保存在独立 Config。L 在新 routes 与旧 cliffs 布局间切换，B 仍切四区域；1/2/3、Tab、G、H 不变。
新增 River ford / Continuous forest belt / Neutral landmark / Ore source detail / Route panorama 五视点；全景与新近中远图用于检查河谷、道路、森林和地标的整体关系，不以近景或 tree_count 声称摆脱大平原。
西侧六节点真实河床接南岸：水面1.1m、河床深 .5m、半河宽2.5m、岸带5.5m，唯一浅滩深 .12m；基础水平水表面不是水流／桥梁／冰模拟。
三条资源化乡土路线核心走廊宽7m，路面目标2.5m；河岸局部渐变到浅滩，绕东北已验收台地和西北7m小高地。道路／河谷用资源节点的Catmull-Rom曲线缓存采样＋距离场平滑边缘，不是路径规划。
欧洲／山区两条连续12m宽森林带，2.55m密度网格＋小扰动，最多200棵批准原树，保留原LOD/剔除/阴影预算；道路、河岸、建筑院落主动清空。覆盖检查看带中实际树冠邻距并对树单独同帧开关，不用数量替代树林连续性；Winter/Desert仍明确缺区域植被源。
每区域三处建筑：欧洲／山区／冬季使用批准EuropeanStoneHouse；Desert用两处NorthAfricanRuins及一处MiddleEasternCourtyard。`BiomeProfile.landmark_source_indices` 可编辑，不复制场景。
`landmark_handoff.json` 与 `resources/handoff.json` 导入五个源：源文件／FBX／图SHA256、UV0、三角数、逐顶点Y-up尺寸与底轴心严格核对；逐piece exact material slots，无几何编辑、独立子件拟合或重拼。
建筑whole scale1，最新 IronOreRock/GoldOreRock各三处whole uniform scale2.5（可配置1–4）；只作分散装饰，无收益、采矿、碰撞或正式NavMesh阻挡。整根一次旋转／等比／平移将原组包围盒投影中心放到预设平台，子件原矩阵保留；report记录原生尺寸、整根缩放、平移、地面与足迹误差。
用户已替换旧两矿：仅启用新 iron/gold PBR 包、正式 `ZombieGame_Resources.blend` 和 schema2 handoff，共八张实际源图（两套 basecolor/normal/roughness/metallic）。旧 Downloads 包及旧 Unity 矿目录由父协调移到 Trash 恢复目录，不继续引用；旧源只作 ignored 恢复。
新矿 Base Color 为 sRGB，normal 为 NormalMap，roughness/metallic 为线性红通道；Built-in Standard metallic 工作流直接使用 source metallic × authored multiplier1，smoothness仅在 shader 转换一次 `1−source roughness`，不先打包反转。Normal convention仍 `unverified`，原图 no green flip，仅预览假设，不宣称 OpenGL；需实际玩家画面对照后核实。无真实 Height/AO，不补造。
建筑维持原非金属值0、原真实法线和有源rough图；缺rough仍用各作者常量，不改变建筑工作流或光照。矿通道校验包含 checksum、类型/色彩空间/noFlip、原金属图非零数据、实际玩家 Material 三数据通道绑定和倍率；报告保留 normal 未核实说明，技术通过不代表法线方向已获视觉确认。
唯一三角例外：MiddleEasternCourtyard FBX SHA256 `a62b35ed830786e8a056e73c1fb0a94cb476deb210da419afca59c307f15a082`，源10028三角中恰有2个重复位置零面积三角，交接明确 effective10026；导入仅对此 exact record/hash/字段严格接受10026。其余源仍严格 authored count，不泛化容差、不删除其他小面、不修改源几何。
许可不是全CC0：farmhouse CC BY4.0、wall atlas CC BY-SA3.0、3TD／clay CC0，出处／归属见 `art/environment/landmark_source_manifest.json` 与交接；用户矿来源许可未核实，限本地私有preview，不公开上传／再分发。

父协调统一执行准备／构建（不可同时有源写入或另一个Unity进程）：

```sh
/Applications/Unity/Hub/Editor/6000.6.1f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod ZombieGame.Tests.EditorTools.EnvironmentShowcaseTools.prepare_and_build_route_preview -logFile Builds/EnvironmentShowcase-routes-build.log
"Builds/EnvironmentShowcase.app/Contents/MacOS/Zombie Game" -showcaseCheck -showcaseBiome all -showcaseLayout routes -showcaseOutput "$PWD/Builds/ArtReview/EnvironmentShowcase/routes-preview" -screen-width 1440 -screen-height 900
"Builds/EnvironmentShowcase.app/Contents/MacOS/Zombie Game" -showcaseCheck -showcaseBiome all -showcaseLayout cliffs -showcaseOutput "$PWD/Builds/ArtReview/EnvironmentShowcase/routes-cliffs-regression" -screen-width 1440 -screen-height 900
```

新QA保留六层、旧悬崖／坡脚材质像素、草／石检查，再验证河床低于水面、浅滩深度、道路核心坡度<32°（仅视觉几何阈值，不是单位通行规则）、唯一河交叉点、7m走廊横向坡度／森林净空、森林带覆盖>=.8、同帧水／树贡献。
九个fixture逐个同帧开关并存近中远截图，核对原源Mesh、材质数据通道与相对子件矩阵、root贴地、native_bounds、建筑足迹平整<.08m／矿足迹地形差<.5m、道路／水／院落净空；每矿不是用另外一个矿的像素代通过。`material_channels_pass` 和 `metallic_max_red` 单独记录，缺真实矿通道或错误倍率会令 fixtures_pass 失败。
旧五高度点输出逐点previous_cliffs_height / expected / actual及变化原因：新路线刻意调整路面／谷地时使用明确新预期，误差阈值仍 .06m；`-showcaseLayout cliffs` 保留旧高度公式回归。新route panorama近中远各60暖帧＋180实测，保留全部慢帧和机器／分辨率／限帧信息。
本轮代码接入不等于人眼／播放器验收；没有建筑活动、资源玩法或生产导航。旧cliffs-final及锁屏慢帧证据不删除，不把纯环境结果宣传成战斗稳定60fps。

入口：`Zombie Game → Environment Showcase → Create or open independent scene`。
场景：`Scenes/EnvironmentShowcase.unity`；可编辑参数资产：`Settings/EnvironmentShowcaseConfig.asset`。
这两个资产由编辑器入口首次生成，不覆盖已存在资产，也不写 Build Settings。

使用原项目 Built-in 渲染与 `TerrainLayerImport` 六层素材：Grass_A、Grass_Dry、Dirt、Mud、Stone、Road。
仅实验场使用真实高度变化，含湿岸、蜿蜒道路、低密草甸、林缘土壤和按坡度裸露的石层。
没有修改正式地图、寻路、接触系统、UnitBalance 或已经验收的单位表现。

按 B 循环 Europe / Mountains / Winter / Desert，也可按对应 HUD 按钮；1/2/3 或滚轮切换近中远，Tab 切八个观察位置，G 开关草细节，H 隐藏 HUD。
（历史cliffs基线为八位置；当前routes共十三位置并新增L布局切换，以上方routes-v1说明为准。）
首次进入 Europe 草甸／林缘中景。四区域是同一场景控制器的资源化配置，不是四个复制控制器。
HUD 显示同一视角本机近 600 帧中位数与 p95 帧时间，属纯环境结果，不代表战斗压力性能。

草、树、岩石只接受经来源交接批准的 Prefab 引用。每种引用必须填写 `*_source_record`。
灌木为独立类别，仅在林缘散布，不作为巨大草片使用。HUD 使用 Unity 内建英文字符，避免打包播放器不能加载系统中文字体；不复制系统字体。
资产为空时明确标示缺素材，不生成程序化树、草或世界道具。
所有子网格保留源相对矩阵，只有整体等比变化与绕 Y 轴摆放；不加碰撞与每草脚本。
分块视锥/距离剔除和 Mesh instancing，单批不超过 1023；草无阴影，树石有阴影。
源若已有 LODGroup，保留源层级阈值，按当前正交镜头投影高度选择对应源网格，不把所有 LOD 叠绘。
没有源 LOD 时只做距离剔除，不自动生成/减面模型；透明草渐隐尚未实现。

打包入口：`Zombie Game → Environment Showcase → Build independent macOS player`。
植被交接入口：`Zombie Game → Environment Showcase → Import approved vegetation handoff`，只接受
`art/environment/vegetation_handoff.json` 的 `status: approved`，完整校验源 blend、FBX 和通道 SHA-256。
用户已批准独立环境分类源 `art/Blender主文件/ZombieGame_Environment.blend`；原 `环境评审 · Poly Haven 植被` 七资产／42张源图仍保留，当前文件另含三地标集合和交接依赖图，不把42当最新总图数。
交接 JSON 为兼容保留原文件名 `art/environment/vegetation_handoff_candidate.json`，最新状态
`formal_category_source_test_only`；`prepare_root_approved_preview()` 接受该明确授权状态，不替换正式玩法模型。
`build_prepared_preview_player()` 会校验正式分类源及七个 FBX／贴图 checksum，更新配置里的源追溯，不重新导出模型。
首次一键准备并构建可用 `prepare_and_build_root_approved_preview()`；已完成导入只需
`build_prepared_preview_player()`，仅更新生成 cutout 图的 alpha mip coverage，再构建，不重打包所有源贴图。
Coverage 以源 cutoff（当前 0.5）保住远距叶片，不修改 source/global 贴图导入设置。
原 source 文件不重写；只对指定展示来源设正确导入类型并在 `Art/VegetationGenerated` 新建 Prefab/材质/通道打包图。
Base Color + alpha 走颜色空间，Normal_GL 走 Normal 类型，roughness/alpha 为线性数据；金属零，smoothness = (1 - roughness) × 0.35，只转换一次。
白亮颗粒局部修正：生成 cutout 图中 alpha 小于源 cutoff 的 RGB 采用最近可见源色扩展，所有 alpha 与可见源 RGB 不变；原始通道不改。
独立 StandardSpecular 材质使用有上限的非金属反射：cutout .008、树皮／石 .018，smoothness cap .08/.15；法线 .2/.3，20–65m 渐淡，三线性 mip＋coverage。
实际变好与否须核对打包近中远截图，不把参数修改声称视觉验收完成。
双面 cutout 专用 Built-in PBR shader 保留真实 alpha，不造草片或更改网格；树 LOD 两个源 FBX 共用其原相对变换。
可执行文件使用 `-showcaseCheck -showcaseOutput <绝对目录> -showcaseBiome all` 做四区域验收；也可只测 europe / mountains / winter / desert 或 0–3。应在与前后图相同分辨率运行。
历史cliffs输出包括地形显示/隐藏的同帧像素对比、八个原位置图、近中远草开/关图，以及包含机器、分辨率、真实坡度、六层平均权重、草开/关帧时间的 JSON；当前routes追加五视点和上述新QA。
植被只在 `Camera.onPreCull` 为该观察相机每次 render 提交，含手动 capture，不与 LateUpdate 重复提交；截图必须有真实草／树贡献，不能用数量替代像素验收。
每区域子目录存独立图与 JSON，根目录 JSON 汇总区域结果。包含 source semantics、明确缺失的 art、草有意关闭与否、岩石同帧开关像素；各区域保留近中远草开关及林缘／道路／岸边观察图。
`terrain_passed` 独立报告地形基础检查；`source_complete` 要求该区域配置应有的草／树／灌木与来源岩石齐全。
Winter / Desert 明确没有批准的对应植被模型，关闭绿色植被，草检查要求像素零变化；不能当成缺森林时假装完整绿地。
总 `passed` 需地形／来源／应有草贡献／岩石贡献成立，仅是可渲染技术检查，仍需人眼验收。
测试场临时将 Application.runInBackground=true 并在销毁时恢复，避免焦点转移导致后台暂停污染帧时间；不改 PlayerSettings，不滤除真实慢帧。
实验场 runtime 暂设 targetFrameRate=60、shadowDistance=45m、单级阴影，树实例45m外不投影；由 config 可调，销毁时恢复此前帧率与 QualitySettings。不是全局资源修改，也不代表树成本已优化通过。
报告记录这些预算，帧时间含60Hz主动等待与所有真实慢帧；森林近中远对照保留，另加同帧草甸近景草开关像素，继续原 .0003 阈值，不为稀疏山区降低标准。
初始化、六层贡献、真实坡度、Shader 或打包地形像素失败均退出非零状态。

明确延后：可信六层高度通道/height blending、Terrain Shader Graph（未迁移管线）、缺源的多级植被 LOD、风动和复杂水体。
独立 `ShowcaseGroundImport.prepare_ground()` 可把已核对的 CC0 grass_ground 2K 接到独立 Grass_A clone，不替换正式地图层。
独立 Built-in Terrain shader 维持原生 terrain 顶点／雾／标准 PBR 4+2 pass；加入 24m 低对比 macro 颜色变化、
两组偏移邻居采样渐混以打散重复（道路不做 ghost 混合），真实 Normal 28–100m 渐淡。
没有伪造 Height/AO，也没有称偏移采样为全功能 stochastic／triplanar。
新增东北台地/崖边由原生 Terrain 高度场承担：中心(.77,.81)、半径17×11m，Europe/Mountains/Winter/Desert 平顶分别11/17/13/15m、边缘2.2m，沙漠有风蚀轮廓及第二级台阶。
非沙漠轮廓仅 .03 轻微不规则，沙漠 .08；第二级台阶由独立 dry_basin / _PlateauLedge 控制，不能因欧洲也有轮廓扰动而启用沙漠台阶。
CPU 高度、Shader 坡脚、石头散布和 QA 采用同方向真实椭圆径向米制，避免用短半径放大长轴的边缘／碎石宽度。
完整崖边、风蚀最大范围与6m坡脚严格在地图内；无加1m容差。原草甸/路/岸/林/坡观察点的高度作为回归检查，不使用clamp掩盖越界。
这不是90°垂直面或悬挑模型；Terrain heightfield不能提供overhang。本轮未造独立岩壁模型。
Stone层只在约49–74°的陡坡渐入真实世界空间三轴投影；欧洲/高山/冬季用rock_face_03，沙漠用cliff_side，4m展示尺度。真实法线投影回Terrain切线空间，保留近远detail fade。
6m坡脚采用gravelly_sand 2.5m PBR，在Stone权重内局部替换并增加合理裸土过渡，不全图替换原Stone；小石只重新分配既有预算55%至坡脚、原RockNatural整体2–4倍不变。
新增Cliff face / Plateau top / Talus foot观察点；QA记录全高度范围、Collider一致性、>65°陡坡、平顶高度/坡度、旧5观察点高度回归、坡脚权重与同帧崖/石像素。
新增 cliff-material-on/off、talus-material-on/off 同帧图与 RGB delta：只暂时关闭对应 _HasCliffPbr / _HasTalusPbr、刷新 Terrain 后 render 并 finally 恢复；保持高度场、草和石头不变，独立验证新贴图实际贡献。两项分别要求 >.0001，不以几何显示／隐藏代替材质验收。
对照原批准的biomes-final/{biome}/view-0..4与新版同名图，保持同分辨率/相机/曝光核对；新增cliff近/中各60暖帧＋180实测帧，非只引用原树林性能。
草主要集中在可调草甸样区，少量散于全图，避免把极小草源均匀撒满后在近镜头也几乎看不到。
新增区域材质由 `art/environment/showcase_biome_sources.json` 固定来源／作者／CC0 许可／SHA-256。`ShowcaseBiomeImport.prepare_biomes()` 只生成独立 layers。
Europe 使用真实 Grass_A、枯草、森林腐殖土、湿泥、自然岩地、既有乡土道路。Rocky_terrain_02 是大尺度航拍，实验 tile65m，不缩成砖块。
Mountains 真正增加 1.25 倍独立山坡起伏，并按实际海拔接 Snow_02，降低植被至低谷；不是调色冒充高山。
Winter 广域 Snow_02＋林土／泥／无植被 Rock_boulder_dry 岩／道路，没有批准的枯树或积雪树，故隐藏绿色植被，基础水面仅冰色而非模拟结冰。
Desert 真实 Sand_01、裸土、无植被 Rock_boulder_dry 岩（5m tile）、湿盆地与未铺砌土路；仍没有批准的棕榈／沙漠灌木，当前routes已有北非废墟／中东院落源，历史cliffs基线不摆建筑。
共享 RockNatural 为原生 rock_07_LOD2 的完整网格，3710 三角形，仅整体 2–4 倍等比摆放，不轴向拉伸；无碰撞，不变地形通行性。
历史cliffs阶段缺中世纪／区域建筑道具；当前routes已接三种批准中立地标（非建筑玩法），仍缺更多时代道具。无现代道路或建筑新增。
正式战斗中的血条/弹药/选择圈/雾与性能，需另跑既有测试，不能用本纯环境场景替代。

2026-10-11 最终播放器四区域技术检查全通过，1440×900、M2 Pro、60fps 限帧下，六组各180帧的 p95 为16.68–17.60ms（包含主动等待）。
摘要见 `validation-20261011.json`；本地完整截图与报告在 `Builds/ArtReview/EnvironmentShowcase/biomes-final`。
主协调已核对欧洲林缘以及冬季／沙漠裸岩截图；仍是地表样场，不能视为完成中世纪场景布置或最终美术验收。

## 可复现准备

悬崖扩展的四区域打包检查与岩壁／坡脚材质像素对比通过，见 `validation-cliffs.json`。
完整证据保留在 `Builds/ArtReview/EnvironmentShowcase/cliffs-final`；本轮重复采样 p95 16.67–17.60ms，包含60Hz等待。
早先锁屏运行 `cliffs-v1` 出现明显慢帧，证据保留，不据单次重测保证稳定帧率，也不视为万人战斗结论。

从项目根目录运行：

```sh
tools/download_free_environment.sh
tools/download_terrain_materials.sh
python3 tools/download_showcase_vegetation.py
```

下载器使用已保存的许可／URL／校验值记录，拒绝覆盖不同 checksum 的本地源。
`art/environment/polyhaven-src/` 是可重新下载、被 Git 忽略的原始缓存；项目需一并保存便携正式环境分类 blend、交接／validation／导出 recipe 和七个 FBX＋实际依赖贴图。
随后 Unity 执行 `ZombieGame.Tests.EditorTools.EnvironmentShowcaseTools.prepare_and_build_root_approved_preview()`；
已准备的工作区可用 `build_prepared_preview_player()` 避免全量重导入。
地表来源追加后用 `build_refreshed_biome_preview()` 只刷新独立材质与区域配置＋构建，不重导入批准网格。

### 独立平地预览（2026-10-15，未美术验收）

双击 `Builds/OpenFlatWilderness.command`，或运行以下命令；双击原 app 的默认 routes 行为未变。

```sh
'Builds/EnvironmentShowcase.app/Contents/MacOS/Zombie Game' -showcaseLayout flat -screen-width 1440 -screen-height 900 -screen-fullscreen 0
```

同控制器的 `FlatWildernessConfig.asset`／`FlatConceptLayout.asset` 提供独立版本，内陆同高4.2m，仅海／封闭池塘边界下凹；无路无河、不设独立建筑高台。冻结源整体等比保留，七建筑形成聚落、金铁石各三件成簇。草24000、灌木140、树0，数量不是美术质量证明。

仅 flat 修正正交镜头相机位置导致的距离剔除偏置，改以地面焦点计距离；旧模式策略保持。当前小草仍像细点，灌木为细枝轮廓；检查不能替代浓密整树、较宽自然草丛、芦苇和长崖模块缺源。技术 pass 与 `art_visual_accepted=false` 分开，未接玩法、阻挡或经济，也不声称战斗帧率。

交接见 `Settings/flat-handoff.json`，实测见 `validation-flat-wilderness.json`；构建入口 `ZombieGame.Tests.EditorTools.FlatWildernessTools.prepare_and_build_flat_preview()`，玩家检查追加 `-showcaseCheck -showcaseOutput <directory>`。`Tab`切聚落／海岸／池塘／全景，`1/2/3`近中远，`G`草开关，`H` HUD。
