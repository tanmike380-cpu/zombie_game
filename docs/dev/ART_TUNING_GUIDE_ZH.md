# 角色美术微调指南

## 当前状态与目标

目标是东方、明代神机营方向的偏写实 RTS 美术：自然人体比例，布、皮革、暗铁和木材的材质区分，克制的色彩，小尺寸也能辨认的剪影。
参考《亿万僵尸》的整体阅读性和气氛，不复制它的西方蒸汽朋克服装或资产。

当前 Quaternius 人物仍是现代、风格化的临时模型。降低饱和度和修改枪形并不等于完成写实人物，也不会把现代上衣自动变成古代军服。本轮只提供可调的过渡版。
最终需要制作或寻找授权明确的东方服装人物，重新检查蒙皮、持枪、装填与行走动画；不要直接覆盖原始授权模型。

参考：

- https://www.numantiangames.com/TheyAreBillions/ （官方画面及蒸汽朋克/维多利亚风格说明）
- https://www.metmuseum.org/art/collection/search/30587 （中国鸟铳，18–19 世纪；仅作枪形参考，不当作明代军装史料）

## 不写代码也可以调颜色

1. 停止 Unity 顶部的 Play。运行中调整往往不能保存，也不要运行中重新烘焙。
2. 菜单 **Tools > Zombie Game > Characters > Select Art Settings**。
3. 右侧 Inspector 调整 `Assets/_Game/CharacterArtSettings.asset`：
   - **Saturation**：饱和度，0 是灰色，1 是原来的鲜艳颜色，当前 0.62。
   - **Tint**：整体色调；太暗时往白色调。
   - **Ambient Light**：暗面的亮度，当前 0.38；这不是场景灯光的强度。
   - **Wood / Iron / Brass**：木头、铁、铜色。
   - **Exploder Belly / Body**：爆炸僵尸颜色；这里 Alpha 是覆盖原贴图的比例，不是人物透明度。
   - **Barrel Diameter / Stock Width**：枪管粗细、木托宽度，只影响外观，不能改变射程或伤害。
4. 菜单 **Tools > Zombie Game > Characters > Bake Models**。等待完成。
5. **Open Playable Model Sample**，按 Play 看效果。改一项、烘焙、对比，避免一次改太多。

这是全局美术设置，重新烘焙后使用这些角色的样板和大地图都会读取同一批模型。

## 文件分别负责什么

| 文件 | 用途 |
| --- | --- |
| `Assets/_Game/CharacterArtSettings.asset` | 手工可调且进入 Git 的美术参数源 |
| `Assets/_Game/Editor/CharacterArtSettings.cs` | 定义 Inspector 中的美术字段和选择菜单 |
| `Assets/_Game/Editor/MusketMeshBuilder.cs` | 当前程序生成的枪形：细长枪管、连续木托、窄下弯尾托、侧面火绳夹；不是最终写实枪模型 |
| `Assets/_Game/Editor/CharacterBake.cs` | 读取源模型和动画，替换枪，烘焙共享帧和颜色 |
| `Assets/_Game/Scripts/Presentation/CharacterAtlas.shader` | 贴图、顶点颜色、饱和度和简单明暗；不是完整 PBR 材质 |
| `Assets/_Game/Scripts/Presentation/MusketEffects.cs` | 枪烟、火光的粒子表现，不产生额外伤害或噪音 |
| `Assets/ThirdParty/Quaternius/*.gltf` | 原始人物、僵尸、骨骼动画和内嵌贴图，保留来源，不用文本编辑器改形状 |
| `Assets/_Game/Resources/CharacterGenerated/` | 自动生成结果，**不要手工改**，下次烘焙会覆盖 |
| `balance/unit_balance.json` | 唯一战斗数值源，火枪伤害目前 100；与美术设置分开 |

## 想改衣服或人物轮廓时

Unity 负责把模型、材质和动画接进游戏；改衣服剪裁、脸、人体比例要在 Blender 等建模软件里做，保存为新的自有美术源，再导出并接入。
贴图负责布料、污渍和皮肤细节；不能只用一张贴图改变衣服几何轮廓。程序生成的火枪以后也应替换为独立模型资产。

下一阶段先做一名东方火绳枪兵、普通僵尸和爆炸僵尸的近景/实际 RTS 距离样板，认可后再扩展：

- 枪兵：自然头身比例、布衣/护甲/绑腿、木制鸟铳，去掉现代幸存者服饰。
- 普通僵尸：消瘦轮廓、灰败皮肤、破旧东方衣物。
- 爆炸僵尸：明显膨胀的躯干、病变暗紫区域；不靠整身荧光紫或爆炸圈才能认出来。

这三条是美术方向，不代表上述衣服和身体模型已经做完。不要为了写实给 1 万个单位直接挂高面数独立 Animator；接入后仍需重新测 5000 / 10000 的性能。
