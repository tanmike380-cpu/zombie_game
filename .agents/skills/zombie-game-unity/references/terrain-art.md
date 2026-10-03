# Zombie Game 地形画面规范

用户于 2026-10-03 指定。这里规范 Unity 环境表现，不授权重造 Blender 的建筑、兵种或僵尸模型。

## 目标

参考用户提供的《帝国时代4》地面层次和可读性，不复制其受保护的贴图。目标流程为 Unity Terrain → PBR Terrain Layers → 地面着色 → 实例化草/石/贴花 → 雾、光照与后处理。不能只用 Grass 一层铺满，不能把大面积纯色色块/立方体当完成地表。

至少维护六个明确层：`Grass_A`、`Grass_Dry`、`Dirt`、`Mud`、`Stone`、`Road`。它们是可调资产/配置，不是散落在场景脚本中的六个硬编码颜色。

- 草地与枯草自然成片变化；地块边缘、建筑周围、行军路线露出土壤。
- 湿泥对应水边/低湿区，石地对应露岩/坡地，道路连接真实基地出入口和设施；不随机把所有层搅成花斑。
- Macro variation：大尺度低对比色差，远景打破同一纹理平铺。
- Tile breakup：合理世界纹理尺度，多尺度/旋转等手段打散重复，不让砖/石尺寸随镜头变动。
- Detail normal：近景细节法线、远景淡出；避免噪点、闪烁和过锐化。
- Slope blending：按真实坡度分配植被/裸岩；平地不假称已验证坡地。
- Height blending：按材质高度图形成自然交界，不把它误称为几何地形起伏。
- 实际高度变化须同步 NavMesh、碰撞、建筑贴地、兵脚落地与输入射线；不能只抬视觉网格，让单位悬空。本轮纯材质任务不得暗改可行走性。

## PBR 与资源

优先审查现有 `art/free_environment_manifest.json` 和环境导入器。不足时可以搜索、下载并接入免费且许可允许游戏使用的素材；记录来源、作者/许可链接、通道、分辨率、校验值与导入路径。免费不等于可任意商用或可把原素材公开到 Git，需逐包核对；付费、需接受协议、云上传或积分消费另行获准。

每层核对 Base Color、Normal、Roughness/Smoothness；有可信 Height/AO 才接入。法线按法线导入，非颜色通道走线性数据；roughness 转 smoothness 一次且不混淆不同管线的通道打包。无金属土地 metallic 应为零，泥湿感来自局部粗糙度而非全地面油亮。缺图明确记录，不将颜色转灰伪称真实高度扫描。

优先 1K/2K 可平铺材质配合理 texel density，实测不足才升级；不能认为统一换4K就一定更好。草/石/枯草/贴花应来自已批准或本次授权的免费资产，分块剔除、实例化、LOD/距离衰减；不要每块草都一个脚本对象，不让装饰暗加阻挡。

## 管线与成本边界

开始前读取实际 Unity 版本、Packages/manifest.json 和 GraphicsSettings。用户指定的 Terrain Shader Graph 是目标工具，但官方所列 Terrain Graph 适用于 URP/HDRP；不能声称 Built-in 已装好，也不能为此悄悄切整个项目渲染管线。

本项目在 2026-10-03 为 Unity 6000.6.1f1，尚未配置 URP/HDRP/Shader Graph 包。先用兼容当前管线的 Terrain/PBR 实现六层基础并说明阶段状态；完整 Graph 迁移需明确计划及用户同意，保护现有角色、雾、VFX、血条和选择反馈。

Unity AI Material Generator 是可选补材途径，不是必需。需 Unity Cloud 关联、条款及 credits；不可自动同意协议/花积分，也不要因该工具不可用停止现有免费素材流程。

着色特性必须按成本评估。六层不代表每像素永远采全六层；控制有效混合层、纹理采样、阴影及透明草过绘。Triplanar 仅在陡坡必要时用，Parallax不是默认必开。GPU草、后处理、体积雾不能一股脑开启后声称完成优化。

## 验收

保存同视角、同曝光、同分辨率的前后截图：基地近景、战斗中景、远景、水边和道路接缝。检查层次、重复、油亮/发白、远景闪烁、材质尺度、单位/建筑辨识及雾遮蔽。

报告六层实际接入情况、哪些着色功能实现/延后/缺素材、素材许可证与源路径。测相同单位数且战斗进行中的帧时间/帧率，附机器/分辨率/视野，不以战后高帧率代替压力结果。多pass、shader错误/粉色、接缝、导航/着地或UI回归失败时不能称验收完成。

## 已核对的官方依据

- https://unity.com/blog/unity-ai-material-generator ：可平铺PBR生成、通道、Cloud/条款/credits前置条件；不是已为本项目启用的证明。
- https://docs.unity.com/en-us/engine/6000.3/manual/creating-environments/script-terrain/terrain-shader-graph ：URP/HDRP Terrain Graph、细节/重复打散/高度混合及采样成本。后续版本实施前复核对应版本支持。
