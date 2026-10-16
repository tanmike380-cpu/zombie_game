# V0.10 — five-camera and building-grounding review

Status: technically verified review, awaiting the user's camera/art selection.
Local immutable snapshot: `v0.10.0-camera-review.1` on `codex/v0.10`.

This is an opt-in presentation comparison, not a replacement for the frozen
`v0.10.0-preview.1` defense game. The old scene, scenario, environment prefab,
source exports and immutable tags remain reference-only.

## Camera comparison

Pitch is measured downward from the horizontal: 0 degrees is level and 90 degrees
is straight down. Yaw is horizontal rotation. All five images use one scene,
focus `(0, 0, 0)`, orthographic size 25, 1920x1080, the same lighting and the same
paused production simulation state. Only the camera orientation changes.

| Choice | Pitch | Yaw | Purpose |
| --- | ---: | ---: | --- |
| A | 48 | 0 | Original camera direction as a control, not the original focus position |
| B | 45 | 30 | Moderate diagonal view |
| C | 40 | 45 | Lower, diagonal view |
| D | 35 | 45 | Lowest view; inspect wall/actor occlusion |
| E | 40 | 60 | Same pitch as C, more horizontal rotation |

Actual Unity renders: [A](evidence/camera-grounding/A-48-0.png),
[B](evidence/camera-grounding/B-45-30.png),
[C](evidence/camera-grounding/C-40-45.png),
[D](evidence/camera-grounding/D-35-45.png),
[E](evidence/camera-grounding/E-40-60.png).

The global map reveal used for comparison is explicitly an art-review fixture,
not a change to normal fog-of-war. Unity frames advance for camera culling while
the simulation remains paused; the report must verify identical state hashes.
The first framing draft at `(4, 0, -20)` exposed the outer map corner at low
angles and is not part of the final five-image comparison. The focus was moved
uniformly for every candidate; the map itself was not enlarged or rearranged.

## Grounding scope

HQ and workshop have separate integer occupancy and visible floor treatment.
The new source variants must retain the original architecture, proportions,
UVs and placement, while replacing their added thick rectangular dirt plates
with thin PBR surfaces and softened edges. HQ uses existing licensed stone
paving; workshop uses existing soil. The farm remains uninterrupted golden
wheat, and no roads are painted into the wilderness.

Before/after closeups use the same 48/0 camera, focus, size, light and paused
state. These are separate from the five overview angles, so camera changes
cannot disguise the grounding result.

[Before](evidence/camera-grounding/ground-before.png) /
[after](evidence/camera-grounding/ground-after.png): all six affected instances
(one HQ and five workshops, including three outside the city) use the new variant.
No old `IntegerGround` renderer is active in the after state. The original
workshop's small baked soil base and normal building shadows are still present;
this is not a claim that every dark patch or source-model limitation was removed.

Blender source: `art/Blender主文件/ZombieGame.blend`, collection
`建筑接地评审 · 基地石庭＋工坊夯土软边`; source SHA `34ce14d5…`.
The source grows from 486 to 492 objects while all 486 pre-existing objects,
meshes, UVs, materials, armatures, lights, cameras and layout remain protected.
The live unsaved Zombie category document was not saved or switched.
See `art/grounding_review_handoff.json` for the two independent FBX exports and
the CC0 Poly Haven paving/soil channel attribution. No new asset purchase or AI
generation was used.

## Verification and limits

- CPU balance checks: 57 assertions passed on 2026-10-16, zero build errors or
  warnings. This is not evidence of new art quality or rendering performance.
- Blender independent reopen/export roundtrip passed: architecture unchanged,
  maximum FBX coordinate error below 0.0000021m, UV error 0, vertex-alpha error
  below 0.001961. Old wall/farm/HQ/workshop exports retain their previous hashes.
- Seven real Unity PNGs are 1920x1080; all share simulation time 0 and state SHA
  `71fdb686…`, including ammunition, facing, velocity and attack timestamps.
  One lighting signature and 4x MSAA apply to every capture. No pink shader pixels.
- Six Terrain layers remain present; same-frame visible/hidden pixel contribution
  is 0.380485, above the unchanged 0.0003 Terrain threshold. This does not relabel
  the inherited failed wild-grass visibility check.
- Source architecture world bounds/placement match within 0.2mm for all six
  instances. The frozen scene/scenario/environment, farm/wall exports and balance
  match their before hashes. Root visually inspected every final PNG.
- No battle, movement, balance, actor animation, lighting or pipeline redesign is
  in scope. Existing V0.10 gameplay/performance limitations remain unchanged.
- This is not a new performance, physical-input or complete gameplay regression
  certification. The comparison intentionally removes HUD and fog occlusion for
  art inspection, not to change normal gameplay.

Evidence: [capture report](evidence/camera-grounding/capture-report.json),
[import checks](evidence/camera-grounding/grounding-import.json),
[frozen integrity](evidence/camera-grounding/baseline-integrity.json),
[Blender proof](evidence/camera-grounding/blender-source-verification.json).

## Opening

Unity menu: **Zombie Game → V0.10 → Presentation review → Open independent review**.
Scene: `Assets/_Tests/V010PresentationReview/Scenes/CameraGroundingReview.unity`.
The **Capture five angles and grounding comparison** menu reruns the paused
art fixture and writes the evidence above. Ordinary opening does not auto-capture.

The frozen playable entry remains **Open playable V0.10** and is unchanged. Do not
rerun the old **Prepare independent camera review** baseline-draft command over
the grounded review; use the grounded-source preparation command if rebuilding.
