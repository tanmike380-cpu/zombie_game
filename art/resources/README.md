# 用户提供资源分类 — 现用四通道 PBR 包

正式源：`art/Blender主文件/ZombieGame_Resources.blend`。Scene `Scene`，父集合 `资源素材 · 用户提供矿石`，仅两个现用子集合：`资源素材 · IronOreRock` / `资源素材 · GoldOreRock`。根分别位于 `(0,0,0)` / `(6,0,0)`，各自贴地；展示区坐标不进入 FBX。旧 OreRock / 旧 GoldOreRock 的网格、对象、材质、贴图均已从活动 .blend 移除。环境分类 RockNatural 与三款地标未改。

当前输入为用户新下载的 `iron+rock+3d+model.zip` 与 `gold+rock+3d+model.zip`。`source_manifest.json` 固定 ZIP 及每包五成员（FBX、RGB、normal、metallic、roughness）的 SHA256。只解读已知 FBX/JPEG，不执行内容。来源作者与许可尚未核实，不是 CC0，不授权公开上传或再分发。IronOreRock 命名来自用户新铁矿包；仍只用作装饰，没有采矿、收益、储量数值。

原 mesh 坐标、拓扑、UV `Attribute`、custom normals 和比例保持不动。FBX 原生单位转换一次，没有手动缩放。只父根平移和底面浮点校正，导出根原点居中且贴地；Unity import scale 1，只可整个根等比放置，禁止按轴拉伸、拆件或重新拟合。

8 张原图全部 packed 并逐通道输出原 JPEG 字节。BaseColor 使用 sRGB；normal、metallic、roughness 使用线性数据。metallic 和 roughness 都绑定提供的真实图像通道，不再将金属全部设为零或以常量冒充材质贴图。没有提供 Height/AO，明确不接。

Normal 的文件没有可证明的 OpenGL/DirectX tangent-Y 声明，因此记录为 `channels.normal`、`normal_convention: unverified`，不假称 GL；Blender 预览采用原像素、无 green flip、Strength 1。Unity 必须视觉核对方向并记录采用的设置。Built-in Standard 一次打包 metallic R + smoothness A（1−roughness），源 JPEG 保留原字节，不能两次反转粗糙度。

`handoff.json`（schema 2）包含 FBX/贴图 checksum、Scene/集合/根、原导入矩阵、尺寸、单位、材质及四通道；`source_validation.json` 记录替换前 hash、可恢复快照位置、已移除旧 datablocks、现用对象与保存重开结果；`native_validation.json` 是新原包 FBX 与正式文件的 mesh/UV/custom-normal 指纹比较；`export_validation.json` 是 FBX 往返的三角数、材质槽、UV、尺寸、底面、中心与四张 JPEG 校验。`previews/` 只含两款现用模型的临时灯光渲染，灯光相机不写回正式源。约六处随机装饰仍为 Unity 测试参数，不是资源玩法。

可重复入口：`art/resources/import_resources.py`。后台命令使用 `--disable-autoexec`，不打开实时未保存僵尸文档。

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --disable-autoexec --python art/resources/import_resources.py -- prepare
```

`prepare` 仅用于正式目标不存在的重建；当前已存在文件不覆盖。用户明确替换时加载旧 Resources 分类并使用 `replace --expected-sha256 <old exact hash>`，它核对仅两款旧对象，保存 ignored 可恢复快照后替换。现用正式源加载后使用 `export --expected-sha256 <current manifest hash>`、`verify-native`、`render`；`verify` 用 factory startup 执行导出往返。

当前替换的旧完整分类、旧 JSON、旧 raw 和旧预览只在 `user-src/recovery/`，不作为活动资产；Downloads 与旧 Unity 导出移入 Trash 由 root 负责。
