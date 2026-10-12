# 用户提供资源分类

正式源：`art/Blender主文件/ZombieGame_Resources.blend`。Scene `Scene`，父集合 `资源素材 · 用户提供矿石`，子集合 `资源素材 · OreRock` / `资源素材 · GoldOreRock`。两个根位于 `(0,0,0)` / `(6,0,0)`，网格各自贴地；Gallery 坐标不进入 FBX。不替代环境分类的 RockNatural。

用户提供原 ZIP 留在 Downloads；`source_manifest.json` 固定归档与两个 FBX/JPG 成员校验值。来源作者及许可尚未核实，不是 CC0，不授权上传或公开再分发。OreRock 是暂定中性名称，不判断为铁矿；只用作装饰，不含采矿收益或数值。

原几何、UV `Attribute`、custom normals 和比例保留。采用 FBX 原生单位转换，没有额外等比缩放。仅父根平移和极小底面浮点校正；未编辑顶点。颜色图为原 2048×2048 JPG 并 packed。没有 normal/roughness/metallic/height/AO 图；roughness 0.85、metallic 0 明确是显示常量，不是真实 PBR 扫描。

`handoff.json` 的 `records[]` 给出模型/颜色图路径与 checksum、Scene/集合/根、原导入变换、尺寸、单位及材质。Unity 根整体等比放置、import scale 1；不能拆件、按轴拉伸或重新拟合。约六处随机装饰是后续 Unity 测试参数，不是资源玩法。

`source_validation.json`：保存及重新读取验证；`native_validation.json`：逐个原 FBX 与正式分类 mesh/UV/custom-normal 指纹比较；`export_validation.json`：FBX 往返几何、UV、材质槽、尺寸、底面、中心轴心与 JPG checksum。`previews/` 是不写回正式分类的临时灯光渲染。

重复导入入口 `import_resources.py`：

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --disable-autoexec --python art/resources/import_resources.py -- prepare
```

`prepare` 只允许目标正式文件不存在，绝不覆盖已有分类。其他只读/派生模式：加载正式分类后运行 `export --expected-sha256 <manifest hash>`、`verify-native`、`render`；factory startup 运行 `verify` 做导出往返。全部后台操作，不触碰用户实时未保存僵尸文件。

初次父节点 cache 偏移修正有独立可恢复 snapshot：`user-src/recovery/Resources-before-parent-pivot-fix.blend`（ignored）。修正过程只平移新根所属网格，原 mesh/UV/custom-normal 指纹不变；当前导出已剔除展示区偏移。
