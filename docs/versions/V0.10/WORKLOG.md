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
