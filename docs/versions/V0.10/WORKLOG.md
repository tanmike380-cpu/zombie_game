# V0.10 work log

## 2026-10-16 03:30 Asia/Shanghai — starting baseline

- User authorized actor/animation integration and overnight iteration until09:00,
  with prior content frozen and future updates organized by version.
- Frozen commit `63946b1`, tag `v0.10.0-baseline`; new branch `codex/v0.10`.
- Reused the existing thread heartbeat, deadline09:00; root remains the only GUI/CLI
  owner. Temporary idle-sleep prevention expires within this work window, without
  disabling lock-screen or security settings.
- Live Blender remains the unsaved `ZombieGame_with_ani_zombie.blend`; no save/switch
  is authorized just to inventory its disk exports.
- Two specialists are doing read-only discovery: authored model/action provenance,
  and production-game integration boundaries. No new scenario code yet.
- Existing concept-draft edits under `art/concepts/tripo/buildings/zombie/` and
  `Assets/_Recovery/` are unrelated in-progress changes. They are preserved and
  excluded from the baseline tag/this version's staged changes.
- Known architecture risk: existing battle terrain uses a different size/height frame
  from the environment review. Do not simply overlay the two controllers or offset
  source models independently. Await explicit adapter design.

## Next

Approve a minimal source-preserving integration plan, assign exact file ownership,
then implement and test the first playable slice. Do not repeat finished art work.

## 2026-10-16 — adapter approved, implementation started

- Starting branch and baseline tag both pushed; `main` and `vision-1` remain unchanged.
- Root approved opt-in production scenario data plus an Editor-only frozen-environment
  bridge. No active EnvironmentShowcase controller in the battle scene and no `_Game`
  C# dependency on test types. Existing native NavMesh APIs are reused; no new navigation
  component package, pipeline migration or UI framework installation.
- One rigid environment Y translation of−4.2m aligns the frozen activity plane to the
  existing y0 battle simulation. Terrain, wall, building and tree metadata use the same
  transform; native source child geometry/materials remain unchanged.
- First population target64 humans /240 zombies,60s deployment and3 waves of80, all
  scenario parameters. Actors use shared UnitBalance. First supported roster is existing
  archer/crossbow, conditional GreekFire and verified imported zombie types; no firearm fallback.
- Crop surfaces may be traversed; original farm house bounds remain obstacles. HQ,
  workshops and walls participate in combat; farms/mines/neutral ruins stay decorative
  in this first scenario, not a newly implemented resource economy or infection rule.
- Human category file appears empty; old red-armour matchlock static master still exists
  but lacks complete game animation. New Tripo AI charge clips contain root motion and
  are candidates, not replacements for in-place Coastal animation. See upcoming asset audit.
- Unity specialist owns the approved production adapter, bounds mapping, read-only foliage
  snapshot and V010 Editor/check source files. Root owns all asset generation/GUI/CLI,
  version documents/tests review/Git. Blender specialist owns only `ASSETS.md` inventory.

## 2026-10-16 04:23 Asia/Shanghai — inventory and unchanged-balance checks

- Read-only actor audit finished in `ASSETS.md`. First slice now prefers62 Coastal
  archers plus up to2 existing GreekFire engines, and Coastal walker/exploder/hound.
  Crossbow stays an explicitly unaccepted animation draft, not the default roster.
- No source Death/knife clip exists for these actors. Movement and attack reuse the
  authored clips; this version must not claim a completed dedicated death/melee set.
- Rechecked SHA-256 of general/environment masters, golden farm and whole wall FBXs,
  and `balance/unit_balance.json`: all five match the frozen manifest. The old v6 scene,
  settings, Combat source and balance have no diff from63946b1 at this checkpoint.
- Ran `tools/balance/run_rank_checks.sh`: PASS57 assertions using real production
  `UnitBalance.cs`, zero build errors/warnings. This is CPU balance evidence, not a
  claim that the new battle scene or navigation has passed.
- Adapter implementation is still under specialist ownership; root has not refreshed
  or compiled the Editor during partial source writes.

## 2026-10-16 04:35–05:00 Asia/Shanghai — first real failures and repairs

- First compilation exposed a thin-check namespace error and this installed Unity's
  error-obsolete `GetInstanceID`; fixed the test reference and snapshot dictionary keys.
- First real Play advanced frames310→312, initialized64/240 and retained81 trees. Init,
  strict imported art and paused128m minimap checks passed. Outer-boundary navigation
  failed: the static box bake retained the backing floor beneath the boundary volumes.
- New scenario only now uses the existing native dynamic carving for permanent map
  obstacles; original NavMesh/contact code is unchanged. Each region has exactly one
  wall record. Second run passed all240 routes to the authored north portal and recorded
  272 shots /127 hits /79 kills, geometry0, contact ratio1.0 across50 real seconds.
- That second run is **not visual acceptance**: screenshot review revealed checkerboard
  ground. Persisting a TerrainData clone cleared references to its unsaved TerrainLayer
  objects before dependencies were copied. Snapshot dependency traversal now reads the
  still-live source; six nonnull layer/PBR checks were added. Regeneration/retest pending.
- Editor50-second-window mean122.7ms is poor and includes approach/idle/tool overhead;
  it is not a performance pass. Added separate active-combat frame sampling plus machine,
  framebuffer and Editor/player context. A newly built standalone must be measured.
- Also fixed opt-in zoom camera-distance refresh and initialized arrow-workshop count
  from the actual authored combat workshop regions. No new economy system or unit values.

## 2026-10-16 05:00–06:15 Asia/Shanghai — standalone and retained failures

- Corrected Terrain dependency persistence and loaded source configuration after scene
  switching. Standalone now shows the six-layer ground rather than a checkerboard.
- An apparent missing-actor capture was a same-frame camera/culling mismatch. Waiting
  two frames at the detail camera shows the actual imported defenders; no shader-stripping
  workaround or replacement actors was necessary.
- First standalone50s passed251shots/122hits/79kills, geometry0/contact1.0. Longer original
  Editor play stalled at219/240. Native proposal remained nonzero but frozen contact
  acceptance was zero among packed neighbours at farm-hut corners. This remains a real failure.
- Tested scenario-only0.25m rounded native hut carving: standalone300s reached228/240,
  still `Playing`, FAIL. Retained `evidence/rounded-full-autohold.json`; rounding is OFF
  in the current generated scenario. No movement/contact core or unit values changed.
- Static art inspection found roughly8.52M of9.07M source static triangles in the12 crop
  plots. New scenario disables CAST on24 crop renderers only, retains RECEIVE and all
  source meshes/materials/transforms. Wall/house/actor shadows and oldv6 are unchanged.
- Full current run with explicit production player cleanup at180s: Won196.20s,
  844shots/499hits/240kills,0bites/geometry0/contact1.0. This tests player-command completion,
  not an autonomous-stall fix. Exact report preserved in `evidence/player-cleanup.json`.
- Active2105samples mean49.03ms/p95 107.85ms, simulation0.43ms/presentation0.83ms,
  peak submitted45.62M triangles. These are not a smooth-play pass; locked desktop/background
  conditions and different runs also prevent attributing all differences to crop shadows.

## 2026-10-16 06:15–06:35 Asia/Shanghai — legacy regression and scope isolation

- Freshly compiled `V010LegacyRegression.app` default suite exited0: old Coastal model/
  assembly, gates/solid towers, building infection, GreekFirecone/ammo, partialA, corner
  formations, regrouping, contact/chase recovery passed. Unchanged edge-pan pure maths
  passed300 sustained top/right samples; this is not physical desktop input QA.
- Fresh `-unitUiSmoke` player exited0: actual pixels for conditional greenHP/blueammo
  at zoom10/50/140, emptyammo/heal/resupply, rings/death/fog, all friendly-building sight,
  cursor directions, siege timing and pressure passed. No old test sources modified.
- CPU `run_rank_checks.sh` again PASS57, zero compile errors/warnings.
- Final source review completed in `REVIEW.md`; no code/GUI changes by reviewer.
- Found scene preparation had mutated global QualitySettings.cascades4→2. Removed that
  Editor write; presentation settings now apply only during scenario lifetime and restore
  prior project values on teardown. Project defaults restored to their frozen values.
- Added opt-in same-frame Terrain pixel contribution and authored multi-frame mesh-motion
  checks. Final rebuilt player and final evidence are pending at this entry.

## 2026-10-16 06:35–06:55 Asia/Shanghai — final verified preview

- Rebuilt the player with scoped shadow settings. Terrain on/off same-frame comparison
  contributes23.66% of pixels, passing the unchanged0.0003 Terrain threshold; the old
  wild-grass failure remains separate. All8 deployed walking/attack slots have24 real
  articulated frames with consistent topology; no missing clip was invented.
- Final50s smoke PASS, then identical crop-shadow-on comparison PASS. Submitted peak
  triangles fall61.18M→45.47M when off, but measured active frame means34.61msON vs
  39.96msOFF show background variability; no FPS improvement is claimed.
- Final new-source full run Won194.65s,826shots/479hits/240kills,0bites/geometry0,
  contact1.0,3waves. Uses actual player select-all/A cleanup at180s. Active mean46.42ms,
  p95 107.45ms; still no performance or autonomous-siege acceptance.
- Saved all successful and failed raw reports, close-up/combat/terrain images, attribution,
  source audit and review. Copied the actual tested Mac app to the independent local
  `Builds/Versions/V0.10/ZombieGame-V0.10-preview1.app`; future temporary builds will not
  overwrite this copy. App binary SHA is in the manifest; binary is not committed.
- Backed up RenderData under `Builds/Recovery/V010-before-snapshot-prune/`, then used
  AssetDatabase dependency checks to delete96 unreferenced derived snapshot files,
  58,917,867bytes. No source model or referenced render asset was removed. Backup is local.
- Fresh ordinary menu launch after pruning advanced frames249→251 and remained paused
  with0waves; no QA fixture runs by default. Actual Play teardown restores shadow values
  2→4 and leaves the saved scene clean. Editor currently opens the V010scene with a real
  MainCamera; no lock-screen or other security setting was changed.
- Five frozen SHA checks match. Combat/Navigation, balance, Blender sources, exported farm/
  walls and originalv6 scene/settings have no diff. CPU balance again passed57assertions.
- Code/test/art commits246fa50/47a46e3/4161762 isolate this integration. Final docs/tag/push
  complete the preview; unrelated concept drafts andRecovery are intentionally excluded.

## 2026-10-16 — camera comparison and building grounding review

- User requested five same-scene Unity camera screenshots and a repair of the dark,
  hard rectangular building bases. The final gameplay camera remains undecided.
- Preserve `v0.10.0-baseline`, `v0.10.0-preview.1`, the original V010 scene/scenario/
  FrozenEnvironment, accepted farm and wall exports, and UnitBalance. Work stays on
  `codex/v0.10` in an opt-in presentation review, not an overwrite of the frozen preview.
- Compare A pitch48/yaw0 (control), B45/30, C40/45, D35/45 and E40/60 with one focus,
  orthographic size25, 1920x1080, identical lighting and paused unit positions.
- Blender specialist owns the new traceable HQ/workshop grounding variants and handoff;
  Unity specialist owns the derived review scene, import, capture and regression evidence.
  No concurrent GUI ownership or partially written source imports are allowed.
- Live Blender is the user's unsaved Zombie category file; do not save, switch or
  replace it. Building-source processing is isolated from that live document.
- Initial source check: HQ/workshop contain both original Tripo architecture/ground
  and an added 12x12m/8x8m rectangular dirt plate, 5cm thick. The latter's top sits
  2.5cm above Terrain, with a normal brown PBR material, not a pure-black material.
  Preserve logical integer footprints while improving the visible ground transition.
- Completed source handoff `art/grounding_review_handoff.json`: master34ce14d5,
  492 objects, all prior486 protected after independent reopen. New HQ stone/Workshop
  soil surfaces have no sidewalls and carry authored vertex-alpha fade. Both FBX
  geometry/UV/alpha roundtrips passed; original architecture, old exports and live
  unsaved Zombie document remain untouched. Original Workshop soil base stays visible.
- Unity imports are isolated in `Assets/_Game/Art/BuildingGroundingReview`; all six
  HQ/Workshop instances align within0.2mm, with no active old IntegerGround in After.
  Frozen V010scene/scenario/prefab, wall/farm exports and Balance SHA all match.
- First framing at(4,0,-20) exposed the map corner at low angles. Retained local draft
  evidence; final five all use focus(0,0,0), size25 and unchanged map/layout/light.
- Seven final1920x1080 Unity images share time0/state71fdb686 and one lighting
  signature; Terrain contribution.380485, magenta0. Root inspected all five angles
  and same-angle ground before/after. These are camera/art selection evidence,
  not final art acceptance or a performance/physical-input/full-gameplay certification.
- CPU UnitBalance checks again pass57assertions, zero warnings/errors. Details and
  reproducible Unity entry: `CameraGroundingReview.md`; camera remains unselected.
