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
