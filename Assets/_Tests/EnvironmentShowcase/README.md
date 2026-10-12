# 独立环境渲染实验场

入口：`Zombie Game → Environment Showcase → Create or open independent scene`。
场景：`Scenes/EnvironmentShowcase.unity`；可编辑参数资产：`Settings/EnvironmentShowcaseConfig.asset`。
这两个资产由编辑器入口首次生成，不覆盖已存在资产，也不写 Build Settings。

使用原项目 Built-in 渲染与 `TerrainLayerImport` 六层素材：Grass_A、Grass_Dry、Dirt、Mud、Stone、Road。
仅实验场使用真实高度变化，含湿岸、蜿蜒道路、低密草甸、林缘土壤和按坡度裸露的石层。
没有修改正式地图、寻路、接触系统、UnitBalance 或已经验收的单位表现。

按 B 循环 Europe / Mountains / Winter / Desert，也可按对应 HUD 按钮；1/2/3 或滚轮切换近中远，Tab 切八个观察位置，G 开关草细节，H 隐藏 HUD。
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
用户已批准独立环境分类源 `art/Blender主文件/ZombieGame_Environment.blend`，集合 `环境评审 · Poly Haven 植被`，42 张源图已打包。
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
输出包括地形显示/隐藏的同帧像素对比、八个位置图、近中远草开/关图，以及包含机器、分辨率、真实坡度、六层平均权重、草开/关帧时间的 JSON。
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
Desert 真实 Sand_01、裸土、无植被 Rock_boulder_dry 岩（5m tile）、湿盆地与未铺砌土路；没有批准的棕榈／沙漠灌木／地区建筑，明确缺失，不造替代物。
共享 RockNatural 为原生 rock_07_LOD2 的完整网格，3710 三角形，仅整体 2–4 倍等比摆放，不轴向拉伸；无碰撞，不变地形通行性。
中世纪／区域建筑道具尚缺批准源，本轮不造假。无现代道路或建筑新增。
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
