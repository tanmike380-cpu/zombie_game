# V0.10 preview 1 — final integration review

Review date: 2026-10-16, Asia/Shanghai. This is a source/evidence review, not a new
model approval, physical-input certification or performance pass.

## Verdict

The new scene reuses the production game controller and can complete a real defense
battle with player orders. **Autonomous siege completion is still broken around
some farm-hut corners.** Do not describe the successful scripted-player run as a
fix for that failure. The preview also remains GPU/render-heavy on the tested Mac.

The reviewer made no Unity CLI/GUI, asset, model, gameplay or Git changes during
this final review. Only this document was added; final validation and version
freezing remain with the root agent.

## Confirmed integration boundaries

- `FrontierGame.defense_scenario` is optional. A null scenario retains the old map
  constructor path, 256m bounds, original lighting setup, camera distance, recipe
  filtering and session defaults. Existing input/minimap callers without
  `IRtsMapBounds` also retain the 256m square. This source review is **not** a
  substitute for freshly running every old Coastal regression.
- `_Game` scenario/rendering code does not reference EnvironmentShowcase test
  types. The Editor-only bridge reads the frozen v6 layout and serializes a new
  artifact under `Assets/_Game/Art/V010Defense`; it does not add a second active
  review/battle controller or overwrite the old environment scene.
- One whole-environment translation, Y=-4.2m, moves the activity plane to the
  existing y0 battle plane. Terrain, source roots, foliage instance matrices and
  obstacle/target metadata use that same conversion. Wall assembly children are
  not independently fitted, scaled or reassembled. The source's irregular wall
  shape is retained, not silently repaired in Unity.
- The map remains 128m, with the frozen 81 tree placements/native LOD bands,
  30 building roots, 12 unchanged 14m crop plots, nine mineral roots, six PBR
  terrain slots, sea, enclosed pond and neutral daylight. Preview metre footprints
  do not change the formal building-balance footprint rules.
- Permanent map obstacles use existing native runtime carving; the first static
  box-only approach failed to remove the backing floor and was replaced. The
  256m backing NavMesh is bounded by scenario carvers. All 240 initial zombie
  surfaces/routes and the open north portal passed the integration checks.
- The 19 authored wall components remain dynamic combat obstacles. Their death
  follows `FrontierStructures` removal of the corresponding carver/wall record,
  rather than leaving a baked wall behind. No gate leaf or friendly tower passage
  was invented. The northern opening is an opening, not an operable gate.
- HQ, existing workshops and walls are combat targets. Crops, mineral deposits
  and neutral ruins are scenery in this scenario; only the original hut
  architecture blocks within a farm plot. This is not a complete new resource
  economy. Workshop ammunition-production count now matches its living targets.
- Movement, damage, health, ammunition, noise, sight and contact radii still
  come from `UnitBalance`. The frozen 6325116 contact implementation is unchanged;
  no hard human-human contacts, warps, overlap relaxation or new crowd-wide holds
  were introduced to force completion.
- Camera/input/minimap markers, warning centers and fog sampling share the
  optional 128m extent. The underlying visibility grid remains the production
  one-metre grid. Paused minimap drag/clamp and production control-handler checks
  passed. The yellow viewport rectangle still uses the older orthographic-size
  approximation rather than an exact pitched ground-frustum projection.

## Full-run evidence: successes and retained failures

| Run | Observed result | Interpretation |
| --- | --- | --- |
| Original unrounded, longer normal Editor run | Three waves; 219/240 kills; remaining zombies stationary for over 120 seconds | **Open failure.** Valid HQ intent and a complete path do not guarantee continued movement. |
| 0.25m rounded-hut standalone experiment | 300.11 seconds; 228/240 kills; outcome `Playing`; geometry errors 0; minimum contact ratio 1.0 | **FAIL.** Rounding reduced the remaining count but did not fix completion. [Retained report](evidence/rounded-full-autohold.json). |
| Current crop-shadow-off standalone, explicit scripted player cleanup | Three waves; 240/240 kills; `Won` at 196.20 seconds; geometry errors 0; minimum contact ratio 1.0 | Integration completion **with player orders**, not autonomous siege acceptance. The test issued production select-all/A-move after the third wave; it did not alter enemy health, paths or contacts. |

The latest result was read from
`Builds/ArtReview/V010Defense/integration-report.json`. Its `player_order_fixture`
records cleanup commands scheduled at 180/220/260 seconds, stopping on session end.
The observed win at 196.20 seconds followed the first scheduled command; later
scheduled commands were unnecessary. Root should preserve that report with the
version evidence before subsequent runs overwrite the working report.

The original stall was diagnosed with native proposed movement but zero accepted
movement: for example zombie 684 proposed approximately (-0.184,0,0) while two
living neighbours were about 1.101m away at a hut corner. This is a real
contact/corner interaction, not absent siege orders or an off-mesh spawn. The
bounded rounding comparison was tested and failed. New generated scenario data
therefore has `nav_rounding_radius: 0`; the generic optional helper remains unused.

## Rendering and performance limits

- Standalone `defenders-detail.png` visibly contains the authored defenders and
  GreekFire equipment. Waiting two frames after camera changes resolved the
  misleading empty-actor capture. There is no current evidence of a stripped
  actor shader. A visible still is not proof that every animation/release pose is
  approved; source gaps remain in [ASSETS.md](ASSETS.md).
- The source has about 9.07 million static triangles; 12 crop plots account for
  about 8.52 million. Eight crop material submeshes amplify shadow-pass cost.
  This is not primarily a simulation-CPU bottleneck in the measured run.
- `DefenseScenario.crop_cast_shadows` defaults to true for compatibility. The new
  preview data sets it false. `FrozenEnvironmentView.set_crop_shadows()` changes
  only the 24 renderer components named `Pasture…_crop` / `…_crop_completion`:
  casting off, receiving on. Original v6/source prefabs, geometry, UVs and material
  values are unchanged; houses, walls and actors keep their existing shadow policy.
- Rounded/shadows-on failure recorded peak 61,244,476 submitted triangles. The
  current off run recorded peak 45,616,844. These are maxima from different runs,
  not a controlled same-frame A/B or a promise of a specific speedup.
- Current packaged Metal 1920×1080, ortho 25, Apple M2 Pro/Mac14,9: 2,105 active
  samples, mean 49.03ms, p95 107.85ms; mean simulation 0.43ms and presentation
  submission 0.83ms. These numbers are **not acceptable smooth-play certification**.
  The Mac was locked, so representative foreground performance remains unverified.
- Inherited wild-grass aggregate contribution still fails its unchanged 0.0003
  threshold. Wall irregularity, missing gate leaf and missing dedicated death/
  knife/cage-throw clips remain disclosed, not fixed by this integration.

## Normal play versus explicit test fixtures

The saved scene has `run_on_start: 0`. Opening the ordinary playable menu entry
does not supply test arguments. Without `-v010Check`, the check coroutine exits:
no pacing advance, scripted cleanup, camera fixture or automatic exit runs.
Normal play retains the 60-second preparation period and player-controlled orders.

`-v010Check`, `-v010FullCheck`, `-v010Cleanup`, `-v010CropShadows` and
`-v010CheckExit` belong only to explicit verification launches. A cleanup win must
not be advertised as an unassisted completion test.

Physical mouse/keyboard edge bounds, sustained edge pan, focus/resize behaviour,
manual playability and representative unlocked foreground timings still need an
unlocked machine and direct testing. No security or lock-screen settings were
changed to manufacture that evidence.

## Root verification addendum after source review

Fresh legacy/default and UI players both exited0; full details are in TESTS/evidence.
Final rebuilt source also passed same-frame Terrain contribution0.236599 and all8
authored Run/Attack frame sets. A final explicit-player-order full run Won194.65s,
240kills/geometry0/contact1.0, active mean46.42ms/p95 107.45ms. Original auto-hold
failures remain unchanged and retained.

Final presentation scope fix: map preparation no longer writes project QualitySettings.
Scenario playback snapshots/restores the previous shadow distance/cascades/resolution.
Follow-up static review found no normal destruction/restart lifecycle omission; root
then tested actual ordinary Play2cascades→stop4cascades,150m/High restored, no project
settings diff. Temporarily disabling/reusing the same view in future would need an
explicit reapply; this preview destroys and recreates it on restart.

## Required handoff wording

Deliver as an **unfinished, controllable defense preview with known corner stalls
and rendering cost**, not a fully accepted release. State that player commands can
clear the battle, that autonomous stragglers remain, and that the rounding experiment
is disabled. Do not move the frozen baseline tag or imply version numbering itself
constitutes art, animation, performance or legacy-regression approval.
