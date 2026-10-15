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
