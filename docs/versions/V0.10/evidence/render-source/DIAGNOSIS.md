# Read-only building source diagnosis

2026-10-17. This is evidence, **not an approved art revision**. Unity rendering
is being tested separately first. No Blender source, model, material, export,
gameplay, or live application document was changed by this diagnostic.

## 中文结论

- 不是“Unity只能做粗糙小游戏”，也不是统一加面/换4K就能解决。现有模型的主形体可用，但源端确有不规整檐线、圆钝或扭曲的小轮廓，以及大量只画在颜色图里的细节。
- HQ真正正面是宽台阶、拱门和柱廊；背面是大片重复窗墙。Unity后续已用本报告的UV顶点实测：入口朝世界+Z，旧C机位主要看背侧，不能单凭FBX根的180°旋转猜。
- 全部被检查原网格都是smooth面、0个sharp标记，且存在跨大二面角连续的自定义corner法线。临时60°硬边对照能让部分檐口更明确，但也暴露局部不规整面；不能把一键split或全flat当成正式修模。
- 原建筑/墙只有一张2K颜色图。虽然材质接了NormalMap节点，它的Strength是0、Color未接图；没有真实法线图和粗糙度图。不能把空节点称作PBR细节。
- Unity同机位对照已完成：高采样有限改善混叠，源软轮廓仍在，光照组合稍暗而未替换正式预设。随后只做HQ／墙两张生产向概念提案，不自动修改或替换源模型。详见[渲染评审](../../RenderQualityReview.md)。

## Protected source and evidence

Formal file: `art/Blender主文件/ZombieGame.blend`.

SHA before and after:
`34ce14d5150a0ae7a9e232775f6f7863180052feea29be4fabc8faf1ad097ef9`.

All 492 pre-existing objects and protected mesh/UV/material/armature/light/camera/
world/collection/layout state passed the read-only guard. The live unsaved
`ZombieGame_with_ani_zombie.blend` was not contacted. All renders used a separate
temporary scene in a background Blender process; no `.blend` was saved and no
FBX was exported. Diagnostic lights did not replace authored lights.

Reproduction: `tools/art/render_source_diagnostics.py`; run Blender in background
with the exact formal source. `--metrics-only` skips renders;
`--controls-only` renders only the temporary angle-split comparisons.
Machine-readable results: [source-metrics.json](source-metrics.json).

## Measurements

| Source | Vertices / triangles | Smooth faces / total | Sharp marks | Atlas used, approximate | Mean effective texels/m |
|---|---:|---:|---:|---:|---:|
| HQ `Headquarters12_OriginalArchitecture` | 4,447 / 6,889 | 4,009 / 4,009 | 0 | 61.5% of 2K | 70.3 |
| Workshop `Workshop8_OriginalArchitecture` | 4,480 / 6,438 | 3,656 / 3,656 | 0 | 57.1% of 2K | 96.6 |
| Wall `00_ModularWall_Kit · MiddleWall` | 1,931 / 2,731 | 1,457 / 1,457 | 0 | 51.6% of shared 2K | 75.1 |
| Pier `00_ModularWall_Kit · StartPier` | 819 / 1,247 | 706 / 706 | 0 | 15.0% of same wall atlas | 75.6 |

The wall/pier atlas is shared; the pier's 15% is not evidence that 85% is wasted.
Coverage is a 512-sample raster estimate, not an exact packing score. Texel density
is area-weighted over the asset at the authored preview size, not a guarantee of
real facade detail. A 4K rebake from genuinely sharper source could double these
densities. Upscaling the same blurred color image does not restore missing
carving, joint depth, or geometry.

### Normal continuity, independently checked

The angle between adjacent polygon normals is measured on the actual shared
mesh edge. An independent Newell normal was recomputed from each polygon's
vertices; its 99th-percentile disagreement with Blender's polygon normal was
0 degrees for all four assets. Maximum error was 0.020° HQ, 0° Workshop,
0.143° Wall, 0.903° Pier (small/irregular polygons can be less stable).

Geometric edges at least 60° with continuous corner normals below 5°:
HQ **3,474**, Workshop **3,733**, Wall **1,239**, Pier **500**. All inspected
assets contain custom normals. This is a real smoothing candidate, but these
counts include tiny/irregular surfaces and cannot select intended hard edges
automatically. Counts are not proof that every such edge should be split.

Temporary diagnostic clones cleared custom-normal overrides to automatic
normals, then marked only geometric angles of at least 60° sharp. Faces stayed
smooth. Their geometry, topology, UV and camera were otherwise unchanged. These
are diagnostic comparisons, **not exported fixes**:

- [HQ original clay](headquarters-clay.png) /
  [HQ controlled normals](headquarters-clay-controlled-normals.png).
- [Wall original clay](wall-clay.png) /
  [Wall controlled normals](wall-clay-controlled-normals.png).

The comparison gives some cornices/crenellation transitions firmer separation,
but exposes unwanted facets on irregular parts. It does not straighten an eave,
restore a missing window recess, or rebuild the malformed thin ends of a wall.
Selective authored hard edges and locally repaired planes would be preferable
to a global threshold in a later authorized source revision.

## Shape versus texture

**HQ:** the roof is one broad plane per side, not individually modeled roof
tiles. Many brick joints/window bars/door carvings visible in color disappear in
[clay](headquarters-clay.png). Main columns and stairs are real geometry, but
capitals, arch margins and thin cornice junctions are uneven or partly fused.
The roof ridge, eave ornaments, small dome rim and terrace caps have visibly
irregular silhouettes. Those survive a change of renderer.

**Workshop:** beams, chimney, stacks and the roof silhouette exist, but fine
tiles, wood grain and masonry are primarily color. Small rails, sawn boards,
roof edges and prop ends are lumpy/thin in [clay](workshop-clay.png). Not all
detail should become geometry; stable major beam/roof boundaries matter more
at RTS distance than adding tiny planks everywhere.

**Wall:** the large arches/buttresses and top walkway exist, but fine block
depth is mostly drawn. The tops of crenellations have softened corners, some
arch supports are pinched, and cut/open thin ends contain ragged source geometry
visible in [clay](wall-clay.png). A whole assembled wall may conceal a module
end, so this is not a claim that every isolated cut is visible in the game.

Original 2048 color images contain baked directional shade and some soft or
smeared small details, including HQ wall/window strips and transitions between
roof/stone islands. Other areas, e.g. roof tiles, are fairly well resolved.
Thus “the whole atlas is blurry” would be inaccurate.

Materials each contain one packed color image. Their NormalMap node is connected
to Principled Normal but has **Strength=0**, unconnected Color and the default
flat-normal color. Roughness is a constant, not a texture. No real normal,
roughness or height map was found on these source buildings/walls. New licensed
PBR ground surfaces are separate and are not evidence of PBR building materials.

## Front/back identification

Native Blender **−Y** is the HQ entrance side: broad stairs, large arched door,
central columned facade. Native +Y is the back: windowed walls and a small
rear turret. Compare [front](headquarters-textured.png) with
[rear](headquarters-textured-rear.png).

The source export includes a whole-root Z180 and an FBX axis conversion, followed
by Unity handedness conversion. Root angles alone are insufficient to infer
which facade faces the camera. A deliberately asymmetric low central stair
vertex was measured:

- Source mesh vertex **2215**, native coordinate
  `(0.0007324572, -0.4680175781, 0.03466796875)`.
- With the source architecture local meter transform:
  `(0.00879378, -5.61895466, 0.32017195)` in Blender XYZ.
- UV locator `(0.08500086516, 0.53480535746)`.

Unity matched that UV in the actual renderer mesh. The marker world position is
`(3.991206, 0.344952, -10.381046)`, HQ center `(4,0,-16)`; the real entrance is
world+Z. The new front camera is pitch40/yaw209.91. This resolves front/back,
but changing to that camera is not a same-angle rendering upgrade.

## Concept candidates and production design

No inspected handoff binds the imported Tripo mesh to an exact generation input
SHA. These are **visual correspondence candidates**, not a fabricated provenance
chain:

- Current HQ resembles
  `art/concepts/2026-09-28-buildings/byzantine/01-hq-age1-v2.png`:
  broad pitched roof, rear small dome, central stairs and flanking terraces.
  It is not the same massing as the newer
  `art/concepts/tripo/buildings/byzantine/03-hq-age3/front-perspective.png`.
  Comparing those as the same asset would falsely classify a missing major dome.
- Workshop closely resembles
  `art/concepts/tripo/buildings/byzantine/04-lumber-camp/front-perspective.png`.
- Wall closely resembles
  `art/concepts/tripo/buildings/byzantine/24-stone-wall/front-perspective.png`.

If the rendering review still fails, a production-oriented revision should
preserve the accepted faction palette and overall massing, but explicitly show:

1. Straight wall planes, plumb columns, consistent roof pitches, clean roof/
   wall separation and **small controlled bevels**, not swollen stone edges.
2. Matching front/side/rear orthographic views, the same proportions in every
   view, plus one RTS-scale view. Label the front and roof/terrace heights.
3. Deliberately separated primary architecture (roof, walls, cornice, pillars,
   arches). Less micro-prop clutter; enough depth on a few readable features.
4. Neutral even lighting, no dramatic painted shadows/highlight halos and no
   miniature “soft toy” edge treatment. Texture detail should not pretend to be
   deep silhouette geometry.
5. A true model bake/material deliverable: base color without fixed lighting,
   real tangent normal and material-specific roughness if available. Do not
   generate fake PBR channels from grayscale color and call them measured detail.

## Boundaries for the next decision

Unity can test imported source normals versus unexpected recalculation,
texture size/compression/mips, anisotropy at oblique views, camera pixel size,
anti-aliasing, exposure, directional shading and readable contact shadows. That
can recover presentation lost after import; it cannot make missing geometric
window depth, a warped roof edge or a baked shadow physically correct.

No automatic source change is authorized by this report. Start with the Unity
same-camera result; if necessary, choose one building for a deliberate source
repair or a new production concept, then compare at real RTS scale before
replacing anything else.
