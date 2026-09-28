# Blender 僵尸与生物角色流程调研 · 2026-09-28

## 可核查来源

1. [Martin Turner：Zombie redux and a tutorial](https://martinturner3d.wordpress.com/2014/09/03/zombie-redux-and-a-tutorial/)
   作者的僵尸制作分享，讨论在 Blender 使用雕刻与纹理绘制工具。是艺术家自己的公开经验，不把他的模型或贴图下载进项目。
2. [Blender Studio：Creature Factory 2](https://studio.blender.org/training/creature-factory-2/5604151f044a2a00caa7b04c/)
   生物制作课程公开目录包含雕刻与重拓扑阶段；[重拓扑章节](https://studio.blender.org/training/creature-factory-2/chapter/5604151f044a2a00caa7b053/)说明将雕刻转为更适合 UV 与绑定的拓扑。
3. [Blender Studio：Design Sculpting](https://studio.blender.org/training/realistic-human-research/design-sculpting/)
   设计雕刻需要参考与反馈，确认形体之后再继续重拓扑；不把堆细节当成轮廓通过评审的替代品。
4. [Blender 手册：Remesh](https://docs.blender.org/manual/en/3.0/sculpt_paint/sculpting/tool_settings/remesh.html)
   体素重构用于重建较均匀的几何表面；这里引用的是可检索的官方 3.0 原理说明，实际脚本在本机 Blender 5.2.2 执行验证。

访问范围：参考公开页面摘要、课程目录与官方说明；部分正文打开受限。没有观看付费完整课程，不宣称复刻某位艺术家的手工笔刷技巧或达到其成品精度。

## 转化到本项目的方法

1. 先对照既有概念图的大形：头槌的厚额、短颈与前倾；投尸的长臂、驼背、背架。
2. 搭建躯干、肩胛、上臂、前臂、腿和掌指体积，再体素融合成连续皮肤，避免关节像积木插接。
3. 在融合表面切出眼窝与嘴部空间；根据用户反馈缩小眼睛，停止追求 RTS 镜头看不到的眼部细节。
4. 灰褐皮肤、疤斑/粗糙度与暗红破布分层处理；额头骨质通过遮罩融入皮肤，不加现代护具。
5. 麻绳和背架分开保留，可在 Blender 中编辑；小尸是成年感染者比例，不是儿童。
6. 从斜前、斜俯视和背面生成实际 Cycles 渲染，再检查剪影、眼睛大小、材质和附属物。

本轮采用可重建的程序化体块/体素雕刻底稿和程序材质，不冒充手工高精雕刻或已经烘焙的游戏贴图。
模型定稿后才进入重拓扑、UV、烘焙、LOD、绑定和动作；这与直接给当前高密度评审网格减面并声称可用于万人同屏不同。

## “僵尸是否比士兵简单”

这是针对本项目的判断，而不是绝对规律：普通无装备僵尸可少做盔甲分件、枪械机构和精确握持，但皮肤、比例与姿态仍需制作。
两个 Boss 还有特殊攻城轮廓、超常体型、背架与多角色接触，不能因为没有铠甲就认为自然更容易。本轮优先形体识别，不花大量成本在微小五官上。
