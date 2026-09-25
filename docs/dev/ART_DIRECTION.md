# 美术方向（2026-09-20 用户确认）

## 2026-09-25 人物母版冻结（优先于下文旧样稿描述）

用户确认的东方红甲火绳枪人物现以 `art/characters/human_base/human_base.blend` 为正式美术源；后续人类兵种只派生配色和手持武器，不重做已确认的人体与服装。详见该目录 README、校验文件和来源说明。

母版已完成握枪姿势修正及面部着色，但完整游戏动画绑定尚未完成。现有战场动画模型暂时保留；不能将“母版已冻结”描述为“所有战场兵种已完成替换”。Unity 有独立静态检查入口，不依赖测试场景。

## 既有运行时样稿

- 建筑、士兵、僵尸：参考《帝国时代4》的可信比例、清晰轮廓、木石布铁材质层次；背景为东方火绳枪时代，不使用现代步枪、现代服装符号或西方蒸汽建筑作为目标。
- 整体画面色彩：参考《亿万僵尸》的赭土、暗橄榄绿、烟灰、暖侧光与阴影层次。避免玩具感、大片纯色、荧光地面选择块；保留兵种与地形可读性，不能靠模糊/整体压黑假装写实。
- 官方参考：https://www.ageofempires.com/games/age-of-empires-iv/civilizations/chinese/ 和 https://www.numantiangames.com/TheyAreBillions/ 。只参考艺术方向，不复制游戏模型/贴图。
- 本轮落地：重檐镇守府、瓦垄/梁柱/窗棂/基座/旗幡；六类扩建设施；枝团树冠、岩壁轮廓、地表材质变化；暖光冷暗部；人物瘦长比例和按骨骼分区的旧布/铁色；细边选择框。
- 尚未达到：人物仍基于 CC0 Quaternius 低多边形底模，缺少正式东方铠甲雕刻、写实皮肤与服装贴图、手工拓扑和完整重定向动作。当前是可运行的方向样稿，不宣称与商业参考作品同等美术精度。需要后续授权的写实角色资产或专门建模制作。

## 修改入口

- `Assets/_Game/Scripts/World/FrontierLandscape.cs`：建筑与地形几何、颜色。
- `Assets/_Game/Scripts/World/FrontierSurface.shader`：不依赖外部纹理的表面变化，非模糊滤镜。
- `Assets/_Game/Scripts/World/FrontierGame.cs`：场景光照。
- `Assets/_Game/CharacterArtSettings.asset`、`Assets/_Game/Editor/CharacterBake.cs`：人物颜色与形体烘焙。修改后执行 Characters / Bake Models。
- `Assets/_Game/Resources/CharacterGenerated`：可重建输出，不编辑、不提交。
