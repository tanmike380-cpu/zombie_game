# 长城门楼 · Unity 三维建筑样板

基于已归档的天朝石门、长城概念图制作，不替换任何现有游戏建筑。

- `GreatWallGatehouse.prefab`：可复用建筑模型，单位为 Unity 米制美术尺寸。
- 展示场景：`Assets/_Tests/Architecture/Scenes/GreatWallGatehouse.unity`。
- Unity 菜单：`Tools > Zombie Game > Architecture > Open Gatehouse Review`。
- `Create Gatehouse Review` 从编辑器建模源码重新生成样板；会更新本目录同名网格和材质，手工改模请先复制 Prefab 和对应 Mesh。
- 源码：`Assets/_Game/Editor/Architecture/GreatWallGatehouse.cs` 和 `GatehouseGeometry.cs`。
- 本地截图：`Builds/ArtReview/GreatWallGatehouse/`，含整体、RTS 视角、近景。

包含贯通拱门、向内打开的双木门、砖石城台、两侧墙段、垛口、红木框架、窗格、斗拱简化构件、曲面灰瓦顶、旗幡。材质分组合并为 13 个 MeshRenderer，约 17.4 万三角形（包括大量真实瓦片及砖石倒角）。目前是近景外观评审样板，不代表最终 RTS 性能预算；量产前需要制作 LOD、烘焙细节及适当碰撞体。

没有加入建筑血量、造价、蜂巢炮、可行走城墙、NavMesh 或门开关逻辑；不修改 balance、士兵、现有建筑或全局画质。通过 Unity 批处理编译、网格/材质存在性、有限顶点检查，并实际渲染三个角度进行外观检查。
