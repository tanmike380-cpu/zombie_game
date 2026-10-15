# V0.10 — Existing character assets and animation audit

Audit date: 2026-10-16. This is a **read-only inventory**, not a new art approval or an animation repair.

## Integration decision

Use the existing Coastal archer and existing Coastal zombies for the first integrated preview. Headbutter and Hound are available optional variants. Keep Crossbow explicitly labelled an animation draft until its existing deformation issue is accepted or repaired. **Do not activate a firearm-infantry procedural fallback.** No new unit statistics or replacement models were authored for this audit.

The current roster is `Assets/_Game/Resources/CoastalUnits/Roster.asset`. It binds `archer`, `walker`, `exploder`, `zombie_hound`, `giant`, `boss` and `heavy_crossbowman`. It does **not** bind `firearm_infantry` or `Soldier`.

All numbers used by an integrated battle must continue to come from `balance/unit_balance.json` / `UnitBalance`. Several already implemented units still carry `provisional: true`; this inventory is not permission to activate unimplemented draft entries or invent missing values.

## Existing Unity-ready exports

Paths below are relative to the project root. The clips and geometry were read from the actual FBX files in an isolated background Blender process. All listed meshes have a UV layer and a valid armature modifier. The table omits auxiliary marker-object action takes; use the named rig's clips, not a marker's similarly named take.

| Asset / runtime ID | Existing FBX | Rig / bones | Rig clips and source frame ranges, 30 FPS | Triangles | Status |
| --- | --- | --- | --- | ---: | --- |
| Archer / `archer` | `Assets/_Game/Art/CoastalImports/Archer/Archer.fbx` | `BZT archer Elite`, 67; root `Hips` | `Idle` 1–49; `Attack` 1–49; `Run` 1–33 | 9,923 | Current Coastal baseline; reuse without changing model or rig. Actual Unity release/projectile timing still needs the V0.10 visual check. |
| Crossbow / `heavy_crossbowman` | `Assets/_Game/Art/TripoCrossbow/ByzantineCrossbow.fbx` | `Armature`, 67; root `Hips` | `Idle` 1–61; `Aim` 1–31; `Attack` 1–55; `Run` 1–33 | 10,412 | Animation draft: the review document records waist/armor stretching when arms rise. Do not mistake a successful kiting test for visual approval. `Archived_Lateral_Run` 1–25 is present but must not be selected. |
| Walker / `walker` | `Assets/_Game/Art/CoastalImports/Walker/Walker.fbx` | `zombie 3d model`, 65; root `mixamorig:Hips` | `Idle` 1–41; `Run` 1–25; `Attack` 1–31 | 4,898 | Existing Coastal baseline. Readable strike is present; it is an authored fallback, not the newer Tripo AI attack below. |
| Latest exploder / `exploder` | `Assets/_Game/Art/CoastalImports/ExploderLatest/ExploderLatest.fbx` | `Explode zombie`, 65; root `mixamorig:Hips` | `Idle` 1–31; `Walk` 1–37; `Run` 1–21; `Attack` 1–19 | 7,027 | Current Coastal roster's exploder. Keep distinct from the older 67-bone exploder below. |
| Headbutter / `giant` | `Assets/_Game/Art/CoastalImports/Headbutter/Headbutter.fbx` | `Hammer zombie`, 67; root `Hips` | `Idle` 1–41; `Run` 1–49; `Attack` 1–120 | 9,693 | Existing heavy gait and long head-ram clip. Recheck impact pose versus production attack window; do not change balance to fit the clip. |
| Hound / `zombie_hound` | `Assets/_Game/Art/CoastalImports/Hound/Hound.fbx` | `Hunger Dog`, 34; root `tripo::Root` | `Idle` 1–49; `Run` 1–33; `Attack` 1–31 | 5,528 | Existing grounded quadruped source. Use its own rig and stride markers, never humanoid retargeting. Historical planted-foot report passed; V0.10 still needs an actual moving-camera check. |
| Cage boss / `boss` | `Assets/_Game/Art/CoastalImports/CageBoss/CageBoss.fbx` | `wooden cage zombie`, 67; root `Hips` | `Idle` 1–41; `Run` 1–49; `Attack` 1–31 | 7,533 | Model and generic strike exist. A dedicated cage-unload / throw-small-zombie animation is **not** present. Do not describe the generic strike as that completed ability. |

Blender's imported action names have the rig prefix, for example `BZT archer Elite|Attack`, `Hammer zombie|Attack`, and `Armature|Armature|Attack` for Crossbow. Unity's current importer chooses the corresponding `Attack`, `Idle`, `Run`, etc. takes. Archer has 18 imported action records including its helpers, but only three skeletal clips; Hound similarly has 15 records including helpers and three skeletal clips.

### Additional existing files — not the default roster

| File | Actual content | Restriction |
| --- | --- | --- |
| `Assets/_Game/Art/TripoExploder/TripoExploder.fbx` | 67-bone `Armature`, root `Hips`; 10,154 triangles; `Idle` 1–31, `Walk` 1–37, `Run` 1–21, `Attack` 1–19 | Older combat benchmark model. It has a different rig/mesh from `ExploderLatest`. Its authored purple texture is `Assets/_Game/Art/TripoExploder/Textures/ExploderPustules.png`; preserve that explicit binding if this variant is deliberately selected. |
| `Assets/_Game/Art/CoastalImports/Soldier/Soldier.fbx` | 65-bone `medieval soldier 3d model`, root `mixamorig:Hips`; 8,583 triangles; `Idle` 1–41, `Run` 1–25, `Attack` 1–31 | Present in the source manifest, **not** mapped into the current imported unit roster. No `Muzzle` marker or verified firearm shooting setup. It is not a ready substitute for the approved matchlock soldier. |
| `Assets/_Game/Art/TripoArcher/OriginalTripoRig.fbx` | 67-bone `Armature`; 9,923 triangles; **zero actions** | Original static rig audit/reference only. Do not deploy it as the animated Coastal Archer or resurrect the rejected custom bow draft. |

## Source files and category-file gaps

### Human category is currently an empty saved shell

The on-disk `art/Blender主文件/ZombieGame_with_ani_human.blend` contains only `Camera`, `Cube`, and `Light`, one collection and zero actions. SHA-256:

`5f273a9ed8d034ad2a5c08cad91a973976d433fcd64508d4e074ca4c07da0ab3`

This is not a populated human master and cannot currently be the source of the shipped Archer/Crossbow exports. No attempt was made to fill, overwrite or migrate it during this audit.

Existing editable authoring files remain:

- Archer: `art/models/coastal-polish/InfantryMotion.blend`.
- Crossbow: `art/models/tripo-crossbow/ByzantineCrossbow.blend`.
- Older exploder: `art/models/tripo-exploder/TripoExploder.blend`.
- Headbutter ram / Greek fire: `art/models/tripo-siege/SiegeAnimations.blend`.
- Grounded Hound history: `art/models/coastal-polish/CoastalPolish.blend`.

These paths establish where the existing exports came from; they do not silently supersede the user's newer category policy. Consolidating them into the proper category files requires a separately scoped source task and protection of live unsaved work.

### Approved static matchlock master exists, but is not battle-ready

`art/characters/human_base/human_base.blend` is the user-approved red-armor, helmet, gourd and matchlock appearance master. Its documented boundary is explicit: only the arm/holding setup exists; full walking, firing, knife/melee and death binding is unfinished, and its procedural materials are not fully baked for runtime. `human_base/README.md` also says the lightweight review variants are static, not production animation assets.

SHA-256: `15c99426bc5d823b526d77330273788a1f9127b70135590462d3c5b83be7aac2`.

There is no ready authored firearm FBX under `Assets/_Game/Art`. The current old `CharacterBake` firearm route uses Quaternius `Characters_Shaun` plus generated weapon/proportion edits. That route is **not** the user's approved Tripo/Blender firearm and must not be enabled as a concealed V0.10 fallback.

### Zombie category has two newer Tripo model/action sets

On-disk source: `art/Blender主文件/ZombieGame_with_ani_zombie.blend`.

SHA-256: `1946fb5219f609a07cb145a70c97ea6ca0933d9507d267387a13eee1ae2d4584`.

It contains:

| Collection | Rig / mesh | Saved actions | Source FBX |
| --- | --- | --- | --- |
| `Zombie · Normal · Animated` | `Armature`, 65 bones, root `mixamorig:Hips`; `tripo_node_d9e01d9b`, 4,898 triangles | Five AI actions: unsettling idle 1–72; feral charge 1–120; savage melee 1–120; slow walk 1–120; combined walk/spot/grab 1–144 | `art/Blender主文件/zombie_sources/normal/tripo_convert_d9e01d9b-7e4d-470c-8ae2-fad160a003e7.fbx` |
| `Zombie · Explore · Animated` | `Armature.001`, 65 bones, root `mixamorig:Hips`; `tripo_node_28e507cc`, 7,027 triangles | Five AI actions: unsettling idle 1–72; feral charge 1–120; savage melee 1–120; slow shamble 1–120; another slow walk 1–120 | `art/Blender主文件/zombie_sources/explore/tripo_convert_28e507cc-e072-4141-96ec-e6dd00cff48c.fbx` |

The saved category scene uses 24 FPS. `zombie_sources/import_report.json` and `validation_report.json` record nonempty animation channels, valid armature modifiers and 52 vertex groups per mesh. This is technical import evidence, not a complete visual/loop/feet approval. The live Blender Zombie document has unsaved edits, so this inventory covers **disk content only**, not whatever additional work currently exists in memory.

**Do not directly replace the current in-place Coastal clips with these newer AI clips.** A read-only five-frame-per-clip evaluation found substantial root motion:

| AI clip sample | Normal Hips displacement | Explore Hips displacement |
| --- | ---: | ---: |
| Feral charge, first to last frame, forward axis | −4.085 source units | −3.742 source units |
| Savage melee, first to last frame, forward axis | −0.570 source units | −0.523 source units |
| Slow walk / shamble, forward excursion | 1.488 source units | 3.074 source units |

The Normal model's sampled world-space mesh minimum Z also lies roughly −0.076 to −0.036 in the saved layout, rather than on zero. These are not in-place, grounded drop-in clips. Direct playback on top of NavMesh movement can visually walk away from the logical unit, cross walls, or jump at loop reset. A future authorized source repair should remove/extract motion deliberately, ground each model, verify the loop and foot contacts, then produce a new traceable export. This audit made **no such changes**.

## Ground, scale and animation playback baseline

- Current Coastal/Tripo FBX `.meta` settings use `globalScale: 1`, `useFileScale: 1`, Generic animation and uncompressed animation. Do not apply a second arbitrary conversion or stretch individual axes.
- Imported **rest** geometry is normalized to approximately one source unit in stature for Archer, Crossbow, Soldier, Walker, and Latest Exploder. Headbutter rest height is about 0.976; Hound about 0.511 high and 1.000 long. Rest mesh minima were within about `8e-8` of Z=0 for these audited exports. This does not prove every animated foot is grounded.
- The existing Unity frame baker uniformly normalizes the first idle frame to height 2. Production applies zombie `UnitBalance.model_scale` afterward. The current rendered baseline is therefore approximately human 2m, Headbutter 6m, CageBoss 10m and Hound 1.6m. These are observations of the existing path, not new editable art/balance constants.
- Archer/Crossbow carry `ForwardOrigin`, `ForwardAim`, `StrideEnd`, `StatureTop`; Archer additionally has `Muzzle`. Hound has the four stride/facing markers. Preserve them. Current gait timing can follow actual travelled distance instead of a free-running clock.
- `Builds/ArtReview/CoastalPolish/verification.json` records Hound ground error `1.5355e-7` and preserved original mesh/weights; its source stride is `0.3793103448`. This historical check is not a fresh V0.10 performance or animation-quality pass.
- No listed infantry/zombie export contains a dedicated `Death`, `MeleeIdle`, `MeleeRun` or human knife attack clip. `TripoArcherImport.bake_model` currently maps missing presentation slots to existing Idle/Run/Attack. That must remain disclosed as a fallback, not described as completed death/knife animation.
- Long skeletal attack clips are time-scaled by the existing presentation's attack window. Check Archer release, Crossbow reload and Headbutter impact in the actual battle. Do not silently change attack intervals or damage timestamps to conceal an animation mismatch.
- The approved Archer bow is an existing rigidly weighted part of the user's mesh; an independent bow-string/arrow clip is not present. The game's existing projectile presentation supplies the flying arrow. Do not invent another character/weapon mesh in Unity to conceal a missing source feature.

## Missing or unresolved — do not invent

1. A populated formal Human category source and fully animated, textured approved firearm export.
2. Crossbow arm/waist deformation visual approval or a scoped repair on its original rig.
3. Dedicated human melee/knife and unit death clips.
4. Dedicated CageBoss throw/unload motion.
5. Grounded, in-place, loop-checked exports for the two newer Tripo AI action sets.
6. Authored models/animations for other roster concepts not listed above; concept images or provisional balance entries are not runtime assets.

## Verified export fingerprints

These hashes matched the actual files, including the current Coastal reference records. No character source or export was rewritten.

```text
Archer          828458965fdb8b72bd366d7dc1394b07e9ef02481d1a2f9b4cf668a05c093333
Crossbow        b7f5a127dcb5df3bf4bd600abf55d9add8620a15063d9a9e483d17fc16ff81fc
Walker          a02b60316597f1459ed85e97ecb11259dcf5380fb43f432eeee9816fe9200d97
ExploderLatest  c23dee4201a715ed51045badb74de68ce96e6a7a4f969391f98033b4b45bb282
Headbutter      5d0782a970e5a54322927f1290eadce3eb9885d56ddbf858879044a00263f93d
Hound           6d9e0d848d590e5a42fec55021c6680d9c177e979cdd4aaf57a81f9d6248d769
CageBoss        38a64ac7c31d8e388af2bf64e462f2d0d79188cbb5723ae403ed38dd1d2b0507
Soldier         8735b5f3f95273071377eed1c2256fc832703ce8d0b1a90c31b272460c4d1983
Old Exploder    9b8e5fecdc519bf2a389d2ba94afea59fc6c9264ed0ee793c1b9e7bbe2515882
Original Archer 2baedfe6af143b7a40f804af0898372e3cfc654c67d431b2c1a3d4fc4095e077
New Normal FBX  e24fd81628e2e064425eea8fa2cb302b2faeec9ed0f6ae434c7b00e1b740e975
New Explore FBX c4b98e9cd0c5468fc7345f081d2b7efd389240d63590bb0c9d200a44276aea66
```

`art/master_art_handoff.json` explicitly describes its 2026-10-05 66-action / ten-reference-group content as a historical snapshot. Do not use those counts to assert the current general master has those actions or to restore absent collections. The general master remains the frozen 486-object source, SHA-256 `69704fa392d17bcc253d669699b0001031c91884cf8fdc544d582e00d3e55d2d`; its new farm FBX remains `3ee3c15e36456b3a199321ec22bd597dd85d2f0940dac15981354e1548996a8e`.

## 中文摘要

- 首版优先复用现有 Coastal 弓箭手、普通尸、自爆尸；头槌、尸犬已有源和动作，可作为可选单位。弩手仍有抬臂拉腰的历史待验问题。
- Human 分类磁盘文件目前只有默认方块/灯/相机；认可的红甲火枪母版仍在 `art/characters/human_base/human_base.blend`，但没有完整战斗动作，不启用旧程序化火枪替代。
- 新 Tripo 两套僵尸各有五个动作，但冲锋含约3.7–4.1源单位的根位移，不能直接叠加 NavMesh。需要独立授权后做原地化/落地/循环修订。
- 已查清模型路径、骨架、动作、比例、缺项及SHA。当前只是只读盘点，没有保存、导出、替换模型，也没有触碰实时未保存的 Zombie 文件。
