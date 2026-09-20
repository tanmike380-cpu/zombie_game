# 美术方向（2026-09-20 用户确认）

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
