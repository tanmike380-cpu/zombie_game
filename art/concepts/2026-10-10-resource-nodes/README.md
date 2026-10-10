# 矿物资源候选 · 五种风格

2026-10-10：15张独立概念图已生成并作缩略图视觉检查，全部1254×1254。每种风格分别包含石矿、铁矿、金矿；金与黄金是同一种资源。用户已选第1套自然矿脉，2026-10-11已补齐这套的正／侧／背9张独立图；另外四套保留作候选历史。不自动启用资源规则或替换正式游戏模型。

## 已选方案：自然矿脉三视图

| 资源 | 正面 | 侧面 | 背面 |
|---|---|---|---|
| 石矿 | [front](01-natural/stone/front.png) | [side](01-natural/stone/side.png) | [back](01-natural/stone/back.png) |
| 铁矿 | [front](01-natural/iron/front.png) | [side](01-natural/iron/side.png) | [back](01-natural/iron/back.png) |
| 金矿 | [front](01-natural/gold/front.png) | [side](01-natural/gold/side.png) | [back](01-natural/gold/back.png) |

每个资源三张分别上传同一模型的视角槽位，不把石／铁／金混到同一个模型；原reference.png保留作最初选款。三张是AI外观参考而非相机标定数据，背面为同造型的合理补画，跨角度岩块／矿脉细节仍需在生成模型时统一。金矿首个背面重复正面识别特征，已重新生成背面并记录废弃输出，不把废弃图作为上传输入。

[三视图来源与实际提示词](01-natural/views-provenance.json) · [九文件尺寸与哈希](01-natural/views-manifest.json)。

| 编号／风格 | 石矿 | 铁矿 | 金矿 |
|---|---|---|---|
| 1 自然矿脉：写实层理、低矮矿石露头 | [石](01-natural/stone/reference.png) | [铁](01-natural/iron/reference.png) | [金](01-natural/gold/reference.png) |
| 2 厚块手绘：圆厚大块、明亮笔触与色差 | [石](02-painted/stone/reference.png) | [铁](02-painted/iron/reference.png) | [金](02-painted/gold/reference.png) |
| 3 暗黑尖岩：高耸黑色破碎岩、矿脉裂隙 | [石](03-dark/stone/reference.png) | [铁](03-dark/iron/reference.png) | [金](03-dark/gold/reference.png) |
| 4 阶梯采坑：低矮方形开采坑、环形层阶 | [石](04-quarry/stone/reference.png) | [铁](04-quarry/iron/reference.png) | [金](04-quarry/gold/reference.png) |
| 5 棱晶矿簇：几何矿柱、清晰切面与强轮廓 | [石](05-crystalline/stone/reference.png) | [铁](05-crystalline/iron/reference.png) | [金](05-crystalline/gold/reference.png) |

每个链接都是一张独立PNG，可单独交给Tripo。这里是造型候选，不是严格地质复原、可平铺地面贴图、PBR贴图组或已经验收的3D模型。方案5刻意采用幻想矿物轮廓；采坑方案生成模型后需检查坑内网格与地面接缝。

图中底座仅表达格子模块方向，不是精确1×1占地测绘；最终尺度、碰撞、矿区拼接和每分钟产量需在模型验收与资源系统实现时确定。本轮未确定产量或经济平衡值。

来源为内置image_gen，完整实际提示词与生成文件记录见[provenance.json](provenance.json)；[manifest.json](manifest.json)记录各独立文件尺寸与SHA-256。旧概念图未删除或覆盖。
