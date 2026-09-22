# 美术素材筛选：2026-09-22

## 免费优先（用户后续选择）

现在只考虑免费路线，不购买付费包。已核对官方页面：

- **Terrain Asset Pack / Terrain Sample**（下表链接）：Unity 官方免费，411 条评价，列出 Unity 6 Built-in 兼容。优先地表。
- **[Free English Oak Set](https://assetstore.unity.com/packages/3d/vegetation/trees/free-english-oak-set-260312)**：免费，8 MB；2022.3 Built-in 兼容，评价不足。适合先测试真实树冠轮廓，不保证 Unity 6000.6 无需适配，也不把英国橡树当成完整东方植被库。
- **[European Forests – Realistic Trees](https://assetstore.unity.com/packages/3d/vegetation/trees/european-forests-realistic-trees-229716)**：免费，10 条评价、588 收藏，198.1 MB；明确不支持 Built-in，URP/HDRP 可用。暂不为它迁移整个项目。
- **[Ultimate Nature – Starter](https://assetstore.unity.com/packages/3d/environments/landscapes/ultimate-nature-starter-176906)**：免费，约 32 条评价，当前 Unity 6 版本 URP-only、偏风格化；不是这次首选。

以上页面标为 Standard Unity Asset Store EULA，不代表素材源文件可以随意再分发。需要通过用户 Unity 账户获取合法包。当前未在本地找到这些包，商店交互工具连接超时；已请求用户下载/提供包。**尚未导入，不能称为画面已替换。** 后续只把模型/材质接入既有地图表现层，不替换相机、小地图、框选、战斗及平衡实现。

目标：东方历史 RTS，可信的比例和材质，鲜明但不荧光的自然色彩；拒绝玩偶比例与简单几何树。当前程序生成模型仅是玩法占位，不能当成最终商业美术。本轮尚未购买或导入以下素材。

## 优先候选

| 素材 | 官方页面数据（查询时） | 适合用途 | 风险/取舍 |
| --- | --- | --- | --- |
| [Forest Environment – Dynamic Nature](https://assetstore.unity.com/packages/3d/vegetation/forest-environment-dynamic-nature-150668) | 188 条评价、6625 收藏；官方分类页 4.7；标价 $60，详情页促销 $30，结算为准 | 首选写实树木、岩石、林地；再做暖阳和秋色，保持鲜明色彩 | 3.4 GB；Unity 6 内置/URP/HDRP 均列为兼容，但仍须在本项目 6000.6 上验证；不能把演示场景全量植被密度照搬到万人 RTS |
| [Pure Nature](https://assetstore.unity.com/packages/3d/environments/pure-nature-188246) | 60 条评价、3481 收藏；官方分类页 4.2；$50 | 较鲜艳的风格化自然环境，备选方向 | 不是纯写实；当前 Unity 6 兼容表仅 URP，项目目前 Built-in，不建议先买再解决管线问题 |
| [Terrain Asset Pack / Terrain Sample](https://assetstore.unity.com/packages/3d/environments/landscapes/terrain-asset-pack-terrain-sample-145808) | Unity 官方、免费、411 条评价、24592 收藏 | 免费地表材质/地形样板优先 | 不是完整东方场景或角色包；1.6 GB；仍需通过用户 Unity 账户获取/导入 |

热度依据是评价数和收藏数，不等于销量。来源：[Unity 官方 3D 分类页](https://assetstore.unity.com/3d)、各商品页。

NatureManufacture [官方介绍](https://naturemanufacture.com/)说明森林资产使用扫描素材并提供 LOD。LOD 对大量单位战场有帮助，但性能必须实测，不能因素材宣传“优化”就承诺不卡。

## 东方建筑：找到，但暂不推荐盲买

- [Stylized Chinese Village – Vibrant URP Assetpack](https://assetstore.unity.com/packages/3d/environments/fantasy/stylized-chinese-village-vibrant-urp-assetpack-257866)：$29.69，3 条评价、75 收藏，URP-only。题材/鲜艳色彩合适，但风格偏卡通，未满足本项目“写实 + 高热度”两个条件。
- [Modular Ancient Chinese City Pack](https://assetstore.unity.com/packages/3d/environments/modular-ancient-chinese-city-pack-186607)：$4.99、84 收藏、评价不足；2021 年版本。不能仅凭便宜判定可直接量产。
- [Pure Nature 2: Asian Mountains](https://assetstore.unity.com/packages/3d/environments/pure-nature-2-asian-mountains-341972)：东方山体环境，不是建筑包；评价不足，不能替代成熟度验证。

## 建议实施次序

1. 先定一个森林/地表包，只改一个 32×32 格样板：森林、矿点、农田、主城边缘，保持真实游戏斜视角、单位比例、雾与 UI。
2. 保持 `_Game` 战斗、NavMesh、出生点、血条、弹药条和框选逻辑不动；艺术模型挂接在独立表现层，碰撞/可采集格仍由地图数据定义。
3. 在同一镜头对比原版/新样板，验收树形、材质、色彩、单位辨识，再扩到 256×256；测 5000/10000 僵尸时的帧耗时、显存/内存、实例化和阴影开销。
4. 最后采购或定制东方建筑/衣甲/火绳枪。不要买一套欧洲幻想骑士强行当神机营；角色和僵尸必须统一比例、纹理密度、LOD 与动画。

付费素材需要用户选定并购买/提供合法包；不要把付费源文件推到公开 Git。免费包也要核对商用许可和再分发条款。购买价格以结算页为准。
