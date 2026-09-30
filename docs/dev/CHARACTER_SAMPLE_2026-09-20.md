# Animated character / matchlock sample

## What changed

- Separate playable scene: `Assets/_Tests/Characters/Scenes/CharacterSample.unity`.
- Same 8-human / 36-runner / 6-exploder small combat fixture; old capsule scene is retained.
- Licensed Quaternius source rigs, textures and clips in `Assets/ThirdParty/Quaternius` (CC0;
  source URLs and immutable blob IDs documented there). Official Unity glTFast 6.20.0 imports them.
- Human: provisional clothed survivor, original wood-stock/long-barrel **matchlock** replacement
  weapon with side match holder. This is not a final historical Shenji uniform or a reload simulation.
- Basic zombie: green/slim model. Exploder: genuinely different chubby/bloated model with purple-red
  tint, plus its existing blast warning/effect. Both retain shared Balance stats and native routing.
- Idle, run, attack and death presentation. Human firing uses Idle_Gun plus a small original recoil,
  not a claimed authored firing/reload clip. Death falls down instead of squashing the model.
- Pooled white-grey muzzle smoke and short flash per real shot. No extra noise emission/damage.
  Smoke capacity is 2500 particles, flash capacity 500; saturated effects may drop particles, never shots.
- Requested firearm damage is **100** in `balance/unit_balance.json` only. Both current runner and
  exploder have 60 HP, so either dies from a single hit; an exploder still detonates on death, silently.

## Animation/render design

`_Game/Scripts/Presentation` owns visual-only views, shared frame assets and pooled effects.
Editor baking samples 12 skeletal-deformed meshes per pose; instances share the resulting meshes
and materials. The large renderer groups by character/pose/frame and submits GPU-instanced draws.
There is no per-zombie Animator or live skinning. This is sampled mesh animation, **not GPU skeletal
animation**. It trades frame storage for predictable animation CPU work; later work can add mesh LOD,
frame interpolation or a measured GPU-skinning solution. No new navigation, attack or noise mechanism
is introduced by the visual layer. Original skeletons remain available for future workflows.

Generated meshes/materials (about 49 MB currently) are ignored in Git under
`Assets/_Game/Resources/CharacterGenerated`. Rebuild using:

- `Tools > Zombie Game > Characters > Bake Models`
- `Tools > Zombie Game > Characters > Open Playable Model Sample` (bakes if missing)

Player builds ensure generated assets exist. Re-bake after editing source art or bake code.
No Blender installation, paid asset or account login is needed for this sample.

## Controls / comparison

Small model sample: left click/drag select; right click move; A then left click attack;
Q patrol; S stop; M move; F2 or backquote selects all; H hides labels; R resets; wheel zooms.
The small sample does not claim parity with every large-scene control-group feature.

Large existing 400 vs 10000 scene keeps its previous appearance by default. F8 toggles the animated
models without changing the simulation; its existing selection/groups/fog/obstacles remain.
The player switch `-characterModels` starts that renderer enabled.

## Tests and performance interpretation

Build: `ZombieGame.EditorTools.CharacterSampleTools.build_players`.
Small player: `-combatSmoke`; large player: `-rtsSmoke -characterModels`.
Visual assertions cover imported materials, all four poses, actual mesh deformation during run,
lowered death posture and a different mesh for the exploder. Smoke asserts one effect event per shot,
bounded 400-shot burst, expiry and reset. Existing combat tests also cover silent explosions.

Run `CharacterSample.app` with `-characterRenderBenchmark` for the isolated rendering comparison.
These are **rendering-only**, not full combat/NavMesh/noise/fog performance, and exclude smoke load.
Each case uses 3 seconds warm-up + 6 seconds measured, 400 humans, no vsync, 1600 x 1000 on this Mac.
The baseline draws capsules only (not all legacy HUD/weapon effects). All models run their animations.

| Zombies | Renderer | Average ms | p95 ms | Average FPS |
|---|---|---:|---:|---:|
| 5000 | Capsules | 0.878 | 0.901 | 1139.0 |
| 5000 | Animated models | 9.800 | 36.422 | 102.0 |
| 10000 | Capsules | 1.675 | 1.733 | 597.1 |
| 10000 | Animated models | 18.294 | 57.316 | 54.7 |

This short sample exposes significant model cost and frame-time spikes. It does **not** establish a
stable 60 FPS target for the full game. Before committing final art, profile realistic camera views,
combat and smoke together; add LOD and lower distant animation detail based on those measurements.
Raw local log: `/tmp/zombie-character-renderbench.log` (not committed).

## Tripo camera comparison on 2026 September 30

Keep the original model detail before reducing polygons. An isolated comparison on Apple M2 Pro,
16 GB RAM, Unity 6000.6.1f1, Metal, 1440 × 900, MSAA 2 and VSync off measured the following.
Each stage used two seconds of warmup and twelve seconds of sampling. All stages kept 10,000
original Tripo archers, 9,923 triangles each, the original 2K color texture, and no character shadows.
These are static shared-mesh instances, without animation, navigation or combat.

| Camera and drawing | Submitted units | Mean FPS | P95 frame time |
|---|---:|---:|---:|
| Whole army overview | 10,000 | 19.42 | 323.20 ms |
| Closer camera only | 10,000 | 19.57 | 320.54 ms |
| Closer camera with per-instance frustum culling | 1,672 | 96.62 | 23.01 ms |

The other 8,328 units remain in the population but are not submitted for drawing. No LOD, texture
reduction or resolution reduction was used. This short run establishes a rendering benefit, not a
stable 96 FPS combat target. Frame spikes remain; the full AI, NavMesh, projectile, fog and smoke
workload still needs an equivalent comparison. Offscreen units must keep their gameplay simulation.

`CharacterCrowdRenderer.begin_frame(camera)` enables the opt-in culling path. Existing callers
without a camera retain their behavior. The main game zoom limit has not changed. Transformed
mesh bounds include weapons and armor, retaining units partially visible at the screen edge.
Regression checks cover visible, partially visible, offscreen, behind-camera and culling-disabled
cases, plus the null-simulation control-group reset. Unity compilation and the standalone build pass.

Reproduce with `Tools > Zombie Game > Characters > Build Original Tripo 10K Baseline`, then run
`Builds/TripoArcher/TripoArcher.app` with `-tripoBenchmark -tripoQuit -tripoOutput <output-directory>`
and Unity player options `-screen-width 1440 -screen-height 900 -screen-fullscreen 0`.
Raw local measurements and screenshots are in `Builds/ArtReview/TripoArcher/camera-comparison/`.
The old report's generic conditions text says overview; the stage names distinguish the overview
(orthographic size 84) and both closer-camera cases (size 14).

## Tripo source and animation review status

The original archer source is `Assets/_Game/Art/TripoArcher/OriginalTripoRig.fbx`, with a packed
Blender copy at `art/models/tripo-archer/OriginalTripoRig.blend`. It has 67 bones but no supplied
animation. The rejected custom bow animation is retained only in the ignored local directory
`Builds/ArtReview/TripoArcher/unapproved-animation/`, not in the Unity asset database.

The newer crossbow source ZIP is locally named `BZT+croosbow+Elite.zip`. It contains 67 bones,
10,412 triangles and color, normal, metallic and roughness maps, but no supplied animation.
`tools/art/tripo_crossbow.py` creates an animation REVIEW DRAFT on those existing bones; it does
not rebuild the skeleton. It expects the extracted original FBX and texture folder under
`Builds/ArtReview/TripoCrossbow/source/`. `art/models/tripo-crossbow/ByzantineCrossbow.blend` and
`Assets/_Game/Art/TripoCrossbow/ByzantineCrossbow.fbx` retain that draft. Original bone rest transforms
are checked unchanged. Weapon skin weights are adjusted to keep the bow rigid.

The crossbow draft is NOT visually approved: raising the arms stretches parts of the waist/armor.
It has idle, run, aim and attack authoring, but no dedicated death or melee animation. Unity's review
slots use explicit fallbacks. Do not deploy this draft to the main game or treat import/vertex-motion
checks as animation-quality approval. Its older local player may predate the latest Blender edits.
The later kiting runs below validate combat integration, not animation visual quality.

Generated sampled meshes under `Resources/TripoArcher` and `Resources/TripoCrossbow` are ignored;
the two review build commands regenerate them. Unit combat numbers still come only from shared
`UnitBalance`; neither the render-only comparison nor review fixtures change authored balance.

## Crossbow kiting and Tripo exploder combat

The user confirmed that friendly congestion and overkill are intended micro-management challenges.
Keep production targeting, collision and damage assignment unchanged. The isolated fixture sends
ordinary movement and attack-move inputs: after a real ranged attack, hold the release pose briefly,
request a 1.2-unit retreat, then resume attack-move. Original cooldowns, ammunition and damage apply.
Blocked units are not warped. All enemies begin charging together using the existing assault fixture
hook and retain local visual combat. There are no invulnerability, resupply or health overrides.

Both scenarios use a flat 256 × 256 test floor, no buildings or obstacles, original model detail,
1440 × 900, MSAA 2, shared sampled animation and optional per-instance culling. Fog calculations,
native NavMesh, projectiles, HP/ammo/selection feedback and combat are active. The map is revealed
for observation. This is not a complete terrain-heavy production-map benchmark.

The 400 crossbow versus 2,000 walker rerun eliminated the zombies at 39.89 seconds, losing 19 humans;
5,619 shots were fired. The first run lost 22 humans instead; avoid interpreting one crowd simulation
as deterministic. The rerun measured 17.49 FPS during combat, not the higher post-combat average
observed in the first run. Reports remain local under `Builds/ArtReview/TripoCrossbow/`.

The newer `explore+zombie.zip` contains 67 bones, 10,154 triangles and a color texture, no animation.
`tools/art/tripo_exploder.py` adds a draft charge loop to the original skeleton without replacing its
rest bones, topology or skin weights. The packed source is `art/models/tripo-exploder/TripoExploder.blend`.
The crossbow remains a 10,412-triangle animation draft. No mesh simplification was applied.

The 400 versus 4,000 exploder run used the shared `exploder` record, including its faster movement,
fuse, death explosion and friendly-only AOE damage. All 400 humans died at 11.64 seconds. There were
1,019 zombie deaths and exactly 1,019 explosions, 1,082 arrows fired, zero dropped projectiles,
zero failed navigation paths, zero geometry errors and zero observed shots during the retreat state.
The fixture recorded 518 retreats of at least half the requested step and 363 repeat shooters.
These are functional checks, not a balanced encounter or animation-quality acceptance.

Measured performance on M2 Pro: 31.41 FPS during combat, combat P95 62.27 ms, 47.28 FPS over the
60-second observation. The early dense sample was 16.36 FPS. All 2,746 measured frames reported
the player focused. Most of the minute occurred AFTER the humans died, so the 47 FPS figure cannot
establish sustained 4,000-unit combat performance. The existing bounded visual pool reached 256
concurrent explosion events; visual events can replace old slots, but damage is never skipped for
that reason. The performance result is not a stable-60-FPS pass.

The initial `ExplosionFeedback` version extracted the expanding purple ring into a production presentation
component shared by Frontier and this fixture. It only consumes existing `BattleSimulation.flashes`;
it never emits noise or damage. Checks cover active, expired, fog-hidden and side-effect-free events.
That initial version did not change production unit balance, collision, formation allocation or target selection.

Build with `Tools > Zombie Game > Characters > Build Tripo Crossbow Battle`.
Launch `tools/art/play_crossbow_kiting.command` for 400 versus 2,000 walkers, or
`tools/art/play_exploder_kiting.command` for 400 versus 4,000 original-model exploders.
Both open paused: Space starts/pauses; F8 switches automatic kiting and manual RTS control; R resets.
F2 selects all, right-click moves, A then left-click attacks. Automatic inputs are test-only, not a new
production unit ability. Raw exploder measurements/screenshots: `Builds/ArtReview/TripoExploder/kiting-400-4000/`.

## October 1 chase and exploder revision

The shared navigation radii now encode the user's fixed tile sizes: humans 0.6, every zombie 0.55,
including giants and bosses. Near-target movement no longer accepts a full body diameter as arrival.
Zombie pursuit extends its route four tiles beyond the human when the extension is navigable;
actual combat still stops in attack range. Dead-target routes remain usable for distant followers
until replacement, and exhausted paths receive priority. No crowd-wide waiting gate is used.

The 400 versus 4000 follow-through run finished its full 60-second observation on M2 Pro at 1440×900.
It measured 37.07 combat FPS and 46.93 ms combat P95. Humans were eliminated at 11.90 seconds;
697 exploders died and produced 697 silent bursts. All 4000 had charge presentation; peak simultaneous
attack poses were 68. Navigation/geometry errors and failed orders were zero. Focus was recorded for
1049 of 3490 frames, so this is not a consistently foreground-only benchmark. The 60.14 overall FPS
mostly measures the period after defeat and is not the combat result.

**Unresolved:** native avoidance still compresses units under extreme congestion. At five seconds,
the closest live pair was only 0.0046 of its configured contact distance. Functional combat checks
passing do not establish hard minimum separation. Keep the spacing diagnostics visible in reports.
The later art revision below needs its own performance sample; do not reuse these FPS as that result.

The original 67-bone exploder skeleton, rest transforms, UVs and 10154 triangles are retained. Blender
now authors distinct idle, walk, charge and self-detonation wind-up clips. They remain review drafts.
`tools/art/tripo_exploder.py` expands lavender pigment over neighboring pustule surfaces in 3D and
bakes the stronger purple into `Assets/_Game/Art/TripoExploder/Textures/ExploderPustules.png`.
Unity explicitly loads this texture instead of arbitrarily choosing the first texture in the folder.
The painter writes a fresh image rather than re-saving the imported image's stale packed bytes,
then reloads the saved PNG to verify purple pigment coverage. FBX texture extraction is disabled for
this model so it cannot overwrite the authored PNG. Unity also checks pixel coverage at build time,
not only the texture's filename. This fixes a real case where the Blender preview was purple while
the game still received the old pale source image.

The ground ring and uniform spherical droplets are replaced by bounded, tapered, irregular slime
fragments, short torn sheets and shrinking ground residue. The shared effect consumes existing
explosion events only, respects fog, and cannot emit damage or attraction noise. Slots expire after
2.5 seconds; repeats do not duplicate emissions. The model file is
`art/models/tripo-exploder/TripoExploder.blend`; review render is local at
`Builds/ArtReview/TripoExploder/charge-draft.png`.
