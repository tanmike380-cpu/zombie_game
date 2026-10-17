# V0.10 — same-source rendering and asset-quality diagnosis

Status: **completed technical comparison, not visual acceptance**. No production
art or render setting is replaced. Local snapshot: `v0.10.0-render-review.1`.

## Scope

The user requested a Unity rendering trial before deciding whether concept art
and Tripo assets need to be redesigned. A later question asks whether the Mac
is the obstacle and whether their RTX 5070 Ti desktop should be used instead.

Preserve the frozen defense and five-camera reviews, all authored geometry,
UVs, source textures and rigs, UnitBalance and gameplay. Experiments are opt-in
under `Assets/_Tests/V010RenderQualityReview`, with new evidence under
`evidence/render-quality` in this version folder. Source diagnostics are read-only
under `evidence/render-source`; temporary clay/normal tests are not game assets.

## Questions to separate

1. Image sampling: actual source/effective texture dimensions, mipmaps, anisotropic
   filtering, output size and anti-aliasing; no sharpen filter masquerading as detail.
2. Shading: readable direct/ambient balance, shadows and existing real PBR channels;
   no invented normal/roughness/AO maps from colour.
3. Source shape: silhouette, plane straightness, controlled architectural edges,
   UV density and baked-in image detail. More output pixels cannot repair geometry.
4. Framing: the previous C view primarily shows HQ's less-decorated rear. Front
   and rear diagnostics must not be presented as a same-angle rendering upgrade.

Current project is Unity 6000.6.1f1 / Built-in / Metal, with Gamma colour space.
The existing SourceOpaque material uses Unity Standard surface lighting, with
no manual gamma-to-linear conversion. Gamma is a candidate limitation, not a
confirmed shader bug. A genuine Linear migration requires its own asset/UI/
terrain verification; it is not silently enabled during this trial.

## Hardware boundary

Current machine read locally: Apple M2 Pro, 10-core CPU / 16-core GPU, 16GB
unified memory, Metal 3. Static rendering checks and large live battles are
different workloads. Report static capture timings as capture timings, not FPS.

The user's RTX 5070 Ti has 16GB dedicated GDDR7 per
[NVIDIA specifications](https://www.nvidia.com/en-us/geforce/graphics-cards/50-series/rtx-5070-family/).
That desktop has not been accessed or benchmarked in this task. It is a useful
future Windows/high-quality test target, but no speed multiplier or 10,000-unit
FPS is established. More GPU throughput cannot restore missing source geometry
or missing surface maps.

Unity documents that Gamma can give a washed-out light response and Linear is
more physically accurate; see the
[Unity 6000.6 colour-space documentation](https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/graphics-color/color-spaces/differences-linear-gamma-color-space).
This explains why colour space should be audited, not why every soft edge must
be caused by Gamma or why a global pipeline change is already approved.

## Results

### 结论

这轮不是“把参数开高就好了”。主机位、HQ背侧、墙近景和HQ正面共16张真实Unity
截图，分开测试原设置、采样、光照与组合。采样主要减少麦田碎亮和轮廓锯齿，
没有修正弯曲屋檐／墙端或圆软垛口。光照组合稍暗，**不建议覆盖正式预设**。

1. **不是贴图被Unity降成低清。** 实际HQ和墙的颜色图均为2048×2048，RGB24、
   无压缩、Trilinear、12级mipmap、AF8、active mip limit 0。测试改为AF16、
   3840×2160渲染后缩到1080p，没有升级或重采样源贴图冒充4K细节。
2. **源轮廓及法线有问题。** HQ6889三角形、墙2731三角形；所有源面smooth，
   没有sharp标记，并存在跨大二面角连续的自定义法线。临时硬边对照只改善
   部分檐口，也暴露不规则面，不能一键全flat或直接当作修复交付。
3. **建筑不是完整PBR材质。** 原建筑／墙只有2K颜色图；源NormalMap强度0且
   无贴图输入，没有真实法线／粗糙度图。新接地材质有PBR通道，不等于建筑有。
4. **机位确有误解。** 旧C方向主要看到HQ后侧；UV定位确认入口朝世界+Z。
   新正面机位改善了建筑辨识，但这是朝向区别，不是同角度渲染升级。
5. **Mac不是本轮已证实的画质上限。** 这台M2 Pro能生成这些高采样静态图。
   换5070 Ti可用于后续实机性能／高画质验证，但不能补回缺失形体或贴图通道。

源端逐项证据见 [只读诊断](evidence/render-source/DIAGNOSIS.md)。新概念只做
[主基地与城墙两张方向样板](../../../art/concepts/2026-10-17-production-buildings/README.md)，
保留人类暖色、拜占庭紫色与既有体块，强调规整墙面／檐线、小倒角及分明的结构。
它们是imagegen生成的概念，不是新模型或Unity效果；未替换旧稿、未导入游戏，
也未宣称完成三视图或保证Tripo能自动重建。

### 实际Unity截图

每一行内相机、布局、单位姿态、模拟时间和输出大小相同。四种方案均由Unity
真实渲染；没有AI修图、后期锐化、DLSS/FSR、动态分辨率、假造PBR或几何替换。

| 取景 | 原设置 | 高采样 | 光照试验 | 两者组合 |
| --- | --- | --- | --- | --- |
| 总览40°/45° | [原图](evidence/render-quality/overview-baseline.png) | [采样](evidence/render-quality/overview-sampling.png) | [光照](evidence/render-quality/overview-lighting.png) | [组合](evidence/render-quality/overview-combined.png) |
| HQ后侧40°/45° | [原图](evidence/render-quality/hq-side-baseline.png) | [采样](evidence/render-quality/hq-side-sampling.png) | [光照](evidence/render-quality/hq-side-lighting.png) | [组合](evidence/render-quality/hq-side-combined.png) |
| 墙近景40°/45° | [原图](evidence/render-quality/wall-detail-baseline.png) | [采样](evidence/render-quality/wall-detail-sampling.png) | [光照](evidence/render-quality/wall-detail-lighting.png) | [组合](evidence/render-quality/wall-detail-combined.png) |
| HQ正面40°/209.91° | [原图](evidence/render-quality/hq-front-baseline.png) | [采样](evidence/render-quality/hq-front-sampling.png) | [光照](evidence/render-quality/hq-front-lighting.png) | [组合](evidence/render-quality/hq-front-combined.png) |

总览HQ包围盒约360×353像素，正面近景约864×866像素。扩大整个建筑或提高贴图
分辨率，不会自动增加固定画幅中总览建筑所占像素；这与源轮廓问题应分开处理。

### 取样成本，不是实战FPS

M2 Pro / Unity Editor / Built-in Gamma / Metal；每视图暖身2次，取5次同步离屏
Camera.Render＋GPU回读的中位数（毫秒）。图像统一1920×1080，采样档内部先渲染
3840×2160/4×MSAA，再双线性缩到1080p。数值包含回读等待，不能倒算战斗FPS。
Profiler的Editor全帧三角形计数还包含同一帧多次测试渲染，不能当单张图的提交量。

| 取景 | 原设置 | 高采样 | 光照试验 | 组合 |
| --- | ---: | ---: | ---: | ---: |
| 总览 | 39.05 | 58.18 | 38.70 | 54.57 |
| HQ后侧 | 22.25 | 33.70 | 31.22 | 29.36 |
| 墙近景 | 22.85 | 28.91 | 26.35 | 33.99 |
| HQ正面 | 25.40 | 33.26 | 33.13 | 34.54 |

这说明额外采样有成本，**不是**5070 Ti与Mac速度对比，也不是万人战斗通过。
静态场景原有约909万三角形（大部分来自麦田），原有性能问题仍需单独做剔除／LOD
与战斗阶段分析；本轮不靠删用户模型来换结论。

## Reproduce and preserve

1. Open the formal project, then **Zombie Game → V0.10 → Render quality review →
   Open independent comparison**. Scene:
   `Assets/_Tests/V010RenderQualityReview/Scenes/RenderQualityReview.unity`.
2. **Capture fixed-state comparisons** runs the isolated paused fixture; use only
   with saved scenes and no concurrent Editor operation. The original review and
   playable scene remain unchanged. This is an Editor image experiment, not a new
   standalone player or a 10,000-unit benchmark.
3. Source audit: [render-source-audit.json](evidence/render-quality/render-source-audit.json).
   Images/parameters/timings: [render-capture-report.json](evidence/render-quality/render-capture-report.json).
   Protected files: [baseline-integrity.json](evidence/render-quality/baseline-integrity.json).

All16 images kept simulation SHA
`71fdb6862c906df2405bca3305389b905c5342871d77400699d3dc153f05bc3f`,
Time0 and the same layout. The62-file protected set passed SHA comparison,
including ProjectSettings, frozen V0.10 scene/scenario/environment, camera review,
balance, wall and farm exports. Formal Blender SHA remains
`34ce14d5150a0ae7a9e232775f6f7863180052feea29be4fabc8faf1ad097ef9`.
The live unsaved zombie Blender document was not touched. No previous tag moves.

Editor teardown verified: Play=false, TimeScale=1, saved scene dirty=false,
capture_on_start=false, restore SessionState empty. Console has0 errors but55
pre-existing code/serialization warnings, not a project-wide zero-warning claim.
All1200 referenced animation meshes remain original references; the review owns
only its frame/material containers. See
[editor-verification.json](evidence/render-quality/editor-verification.json).

## Next source-quality gate

Do not regenerate every asset or simply subdivide everything. Review one new
direction, then establish consistent separate front/side/rear views and a real
model bake/material deliverable. Check plumb/straight main forms, selected hard
edges and small bevels, UV allocation, genuine base-color/normal/roughness, then
compare that one asset in this same Unity framing at normal RTS scale. Render
pipeline/Linear migration and desktop benchmarking remain separate decisions.
