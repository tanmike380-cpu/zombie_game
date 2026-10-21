# Desktop development handoff

Date: 2026-10-21, Asia/Shanghai. Current integration branch: **`codex/v0.10`**.
Repository: <https://github.com/tanmike380-cpu/zombie_game>.

Use Git with Git LFS. Unity sources live in `Assets`, `Packages` and
`ProjectSettings`, with their existing `.meta` GUIDs. Blender sources remain
under `art`; the runtime uses approved exports, not a new model conversion.
`Library`, temporary files, logs, Mac `.app` players and local recovery copies
are intentionally not transferred. A new Editor creates its own platform cache.

The Unity Editor version is **6000.6.1f1**. Current source authoring was inspected
with Blender **5.2.2 LTS**. No render-pipeline or colour-space migration is included.
The repository remains public; existing third-party attribution/licences remain
in place. This handoff does not grant new licences or make the entire art library CC0.

This is a source/dependency handoff, not a Windows runtime, GPU, performance or
animation approval. The current Mac still has unsaved work in the Zombie Blender
window; only saved disk content can be represented by a Git commit unless a
separate migration copy is explicitly saved.

The user chose to transfer saved content only. No live Zombie save or migration
copy was made. The Environment master was repaired for portability only: four
existing original grass images were packed without altering pixels, geometry or
material nodes; two original unused materials received a retention flag to avoid
being discarded on save. See `art/environment/portable_handoff.json`.

## 中文：在台式机上打开

### 1. 拉取正确分支和大文件

安装 Git（含 Git LFS）、Unity Hub，并安装 **Unity 6000.6.1f1**。
Blender建议使用本次源审计的 **5.2.2 LTS**。不需要在新电脑复制Mac的用户名和目录。

在PowerShell中执行，目标目录示例为 `C:\Dev\zombie_game`：

```powershell
git lfs install
git clone --branch codex/v0.10 https://github.com/tanmike380-cpu/zombie_game.git C:\Dev\zombie_game
cd C:\Dev\zombie_game
git lfs pull
git lfs fsck
git status --short --branch
```

不要只点GitHub的Download ZIP：必须确认大文件不是一张只有几行文字的LFS指针。
克隆后如果Git LFS报告配额、网络或认证失败，先解决该错误，不要把缺失模型当导入成功。
第一次拉取较大，Unity第一次导入也需要重建本机缓存。保留所有 `.meta`，不要删掉后重建。

本次为保留已有场景引用，额外纳入102个必要运行资产／.meta，共约1.32GiB：
Coastal用户模型动画、弩手帧、希腊火、旧启动所需的基础动画数据、数值构建快照。
这些是**只读衍生快照，不是新的模型源**；32个旧基础动画仅用于保持现有启动行为，
不会改变V0.10严格的用户模型名单。不要为“清理缓存”而删除这些已跟踪目录，
也不要初次打开就重跑Coastal导入器或角色烘焙，否则可能重建GUID及引用。
唯一数值源仍是 `balance/unit_balance.json`，同名运行副本只由现有构建步骤更新。

### 2. 打开Unity

Unity Hub使用“从磁盘添加项目”，选择 **`C:\Dev\zombie_game` 根目录**，不是Assets子目录，
也不是另一个旧clone。使用6000.6.1f1打开，等待包与资产导入完成。

当前正式试玩入口：**Zombie Game → V0.10 → Open playable V0.10**。
如果手动打开，则选择 `Assets/_Tests/V010Defense/Scenes/V010Defense.unity` 后Play。
不要一上来执行“Prepare frozen defense scene”，它是重生成入口，不是打开入口。

已有两个独立美术评审入口，不覆盖试玩版：

- 视角／接地：`Assets/_Tests/V010PresentationReview/Scenes/CameraGroundingReview.unity`。
- 渲染对照：**Zombie Game → V0.10 → Render quality review → Open independent comparison**。

需要数值构建副本时，使用 **Tools → Zombie Game → Balance → Validate and sync build snapshot**，
其唯一可编辑源仍是 `balance/unit_balance.json`。初次导入中的旧基础外观生成属于历史测试缓存，
不表示可以把程序化士兵替换到V0.10严格用户模型名单。

当前Git中的Mac `.app`构建不用于Windows；本地Builds本来就不上传。
**旧 `build_defense_player()` 函数硬编码Mac目标，不能把它当Windows打包按钮。**
台式机先在Editor里开发／试玩；需要Windows独立版时使用Build Profiles选择Windows及上述场景，
并安装对应模块。Windows构建和5070 Ti实战帧率尚未在本次验证。

### 3. 打开Blender源

相对仓库根目录：

| 内容 | 文件 |
| --- | --- |
| 建筑／城墙总布局 | `art/Blender主文件/ZombieGame.blend` |
| 环境植物／源布局 | `art/Blender主文件/ZombieGame_Environment.blend` |
| 矿产 | `art/Blender主文件/ZombieGame_Resources.blend` |
| 两套保存的Tripo僵尸及动作 | `art/Blender主文件/ZombieGame_with_ani_zombie.blend` |

**Human分类文件目前是默认方块／相机／灯的空壳，不是人物丢失。**
当前实际人物／动作源也在仓库中：

- 弓箭手：`art/models/coastal-polish/InfantryMotion.blend`。
- 弩手：`art/models/tripo-crossbow/ByzantineCrossbow.blend`。
- 红甲火枪静态母版：`art/characters/human_base/human_base.blend`，不是完成动作的成品。
- 尸犬等历史动作：`art/models/coastal-polish/CoastalPolish.blend`。
- 头槌／希腊火动作：`art/models/tripo-siege/SiegeAnimations.blend`。

不要拿Human空壳覆盖这些实际源；分类整理不在本次迁移中偷偷完成。
Mac上未保存的Zombie编辑不等于磁盘中的这个文件，必须保存后才会成为Git内容。
本次你已选择只同步保存过的版本，实时未保存内容没有上传。

Environment原来指向Mac临时目录的4张草贴图已经原样内嵌，独立重开检查通过；
建筑总布局的相对贴图也在项目内。没有擅改模型、重新绑定骨骼或导出另一套外观。

### 4. 两台电脑轮换开发

每次换电脑前：在Unity／Blender中保存自己要保留的工作，然后提交并push。
另一台电脑先确认没有本地未提交改动，再pull后打开软件。不要在两台电脑同时编辑同一个
`.blend`、二进制动作资产或同一个Unity场景；Git无法可靠自动合并这些文件。

不要复制 `Library`、临时文件或生成的IDE工程；不要覆盖冻结标签，也不要把main当当前最新分支。
旧概念草稿、Recovery和旧中间评审文件不因这次迁移而擅自删除。

### 迁移检查记录

- [Unity依赖／GUID／大小与原始SHA检查](DESKTOP_MIGRATION_UNITY_AUDIT.json)。
- [迁移快照／五份正式源SHA与交付清单](DESKTOP_MIGRATION.json)，冻结标记为
  `v0.10.0-desktop-sync.1`，不替换旧标签。
- [环境源贴图打包与原始像素保护](../../art/environment/portable_handoff.json)。
- [V0.10原有试玩边界与已知问题](../versions/V0.10/README.md)。

本次验证的是保存源、依赖完整性与Git交付，不是已经在你的Windows台式机上实测。
换电脑、换GPU不会自动修好原有美术、寻路、动画或性能待办；旧冻结标签保持不动。
