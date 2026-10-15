# V0.10 reproducibility and source boundaries

## 现有电脑：直接试玩

Unity6000.6.1f1，正式目录 `/Users/tankaixi/Documents/ChatGPT/zombie game`。
菜单 **Zombie Game → V0.10 → Open playable V0.10** 打开已保存的可玩场景。
本地独立版本：`Builds/Versions/V0.10/ZombieGame-V0.10-preview1.app`。
不加任何测试参数；默认暂停说明、60秒部署、三波80只。

冻结标签 `v0.10.0-preview.1` 对应代码、场景、冻结环境数据、源引用和证据。
`v0.10.0-baseline` 仍指向整合前的63946b1。后续在新版本分支推进，不能移动旧标签。
`main`、`vision-1` 和旧美术评审地图都未改。本轮未提交无关的僵尸老巢概念草稿与Recovery。

## 缓存不是源模型

Git保存用户源模型、源清单、脚本和本版固定的环境渲染资产。以下为已有项目规则下
排除的可重建缓存，不应把缓存路径误当作新的建模源：

- `Assets/_Game/Resources/CharacterGenerated`：旧渲染器要求的通用帧缓存，本版严格模式
  不允许把其中的程序化士兵当作缺失兵种的替代品。
- `Assets/_Game/Resources/CoastalUnits`、`CoastalBuildings`：既有用户FBX的动作采样／材质适配。
- `Assets/_Game/Resources/BalanceGenerated`：`balance/unit_balance.json` 的精确构建副本。
- `Assets/_Game/Resources/FreeEnvironment`：既有许可来源的环境缓存。

本机生成缓存完整，已验证实际打包运行。**尚未在全新电脑／干净克隆里验证冷启动重建**，
因此不宣称只拉Git就能无准备运行，也不把本机测试等同于可重复CI认证。

冷启动时先保留当前工作，用对应Unity版本打开，取回所需Git LFS源资产。既有重建入口为：

1. `ZombieGame.EditorTools.CharacterBake.ensure_models()`：旧渲染器基础缓存。
2. `ZombieGame.EditorTools.CoastalTools.import_assets()`：从
   `Assets/_Game/Art/CoastalImports/manifest.json` 及原Tripo资产重建采样帧和希腊火资源。
3. `ZombieGame.EditorTools.BalanceBuild.sync_snapshot()`：检查并同步唯一数值源。
4. 需要重新编译环境时，用版本Editor桥
   `ZombieGame.Tests.V010Defense.EditorTools.V010DefenseTools.prepare_defense_scene()`；它还需要
   既有EnvironmentShowcase的源依赖／环境缓存，会在工作副本重存本版生成资产和场景引用。
   不应在冻结的正式工作副本上反复生成；未来版本先改新版本输出路径。
5. `build_defense_player()` 目前会先执行prepare，再打包
   `Builds/V010Defense.app`。交付的冻结副本不被这个输出路径覆盖。

若某个导出／源依赖缺失，依据 [ASSETS.md](ASSETS.md)、`art/blender_master.json`
和具体handoff补齐，不自造模型。实时Blender的未保存僵尸文件未被本轮关闭、保存或覆盖。

## 验证启动参数（只用于明确测试）

- `-v010Check -v010CheckExit`：50秒整合冒烟；只推进场景波次时钟，不改变单位数值。
- 再加 `-v010FullCheck`：最多300秒、三波的真实胜负检查。
- 再加 `-v010Cleanup`：180秒起通过正式选择／A指令让玩家部队清理；不能当作自动总攻修复。
- `-v010CropShadows`：开启麦穗投影的对比夹具，普通试玩仍关闭细小麦穗投影。

现有 `V010LegacyRegression` 独立场景用来重新编译旧Coastal检查；`-unitUiSmoke`
切换到旧UI像素夹具。它不是本版的第二套战斗控制器，也不部署那些测试外观。

完整通过／失败和测试条件在 [TESTS.md](TESTS.md)；源许可见 [LICENSES.md](LICENSES.md)。
无帧率通过或全自动清场通过的承诺。保留报告中的原始失败与背景运行限制。
