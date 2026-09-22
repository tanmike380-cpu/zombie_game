# Project rules

- Runtime unit movement/combat numbers have one authored source: `balance/unit_balance.json`.
- Production code and every playable/benchmark test must read `ZombieGame.Balance.UnitBalance`;
  do not introduce scene-local copies of speed, acceleration, HP, damage, range, attack interval,
  sight, noise propagation, projectile speed or explosion stats.
- `Assets/_Game/Resources/BalanceGenerated` is a generated build snapshot, never an editable source.
- Unknown stats for unimplemented units stay explicitly provisional; do not invent damage/HP to
  activate a draft. Populate and validate the shared record before implementing that unit.
- Test-only holds/warps/health overrides must be clearly named fixtures and must reset afterward.
  Population, map layout, timing samples and scheduling budgets are test parameters, not unit balance.
- Experimental scenes remain under `Assets/_Tests`; reusable balance/movement-command code belongs
  under `Assets/_Game`. Preserve the shared humanoid/musket test models across test scenes.
- NavMesh owns routing/avoidance. Formation destination assignment must not preserve empty gaps
  between separated squads; regression-test regrouping as well as obstacle detours.
- Formation commands fill radial bands from the mouse target outward, assigning nearer units
  to inner bands before farther units. Match approach directions within each band to reduce
  crossing. Do not revert to unit-index-priority assignment or stagger command start times.
- Reusable battle/navigation/perception logic lives under `Assets/_Game/Scripts`; test scenes
  supply population/layout fixtures and must not become dependencies of production modules.
- Preserve accepted RTS feedback across every art/style change: living units show green HP only
  below full health; humans show blue ammunition only below capacity, including an empty-ammo cue.
  These conditions are independent and apply to unselected units too; selected humans have
  ground-position selection rings. Feedback stays readable across camera zoom and lighting.
- All deployed living friendly units and living uninfected buildings supply shared human sight.
  Destroyed/cancelled buildings lose sight; explored terrain remains remembered.
- Edge pan and directional cursors share drawable-window coordinates; test all four exact bounds,
  sustained top/right contact, focus loss, resize and zoom. Keep unit UI framebuffer regressions.
- Extend existing production systems rather than recreating accepted features in new test controllers.
  Every regression fix must extend the existing checks and be saved in Git before handoff.
- Minimap left-click/hold/drag moves the camera, including while paused or defeated. Capture lasts
  until release/cancel/focus loss, clamps outside the minimap, and does not select or command units.
  Preserve minimap right-click movement and A-left-click attack; suppress edge pan over the minimap.
- Friendly unit/building damage produces bounded, merging, expiring red minimap warnings.
  Enemy damage alone must not warn; warning presentation must never reveal fog or emit noise.
- All living zombies, including previously alerted or settled ones, may hear new gunshots.
  Newer audible emissions replace old sound memory; delayed older waves cannot restore old goals.
  Visible-human pursuit has priority. Zombie explosions never emit attraction noise.
- A committed siege is a persistent attack-move objective against the living headquarters, not
  a sound investigation or a rally-point move. Local combat may interrupt it; sounds cannot replace
  it. Resume the HQ objective after local combat and recover exhausted/invalid navigation paths.
- Siege uses simple attack-move: fight visible humans, then resume HQ when they die or leave sight.
  Do not add timed retaliation memory or repeatedly reset valid combat paths. HQ destruction
  completes the strategic objective, not a global building sweep.
- Art direction: coherent low-poly presentation with Eastern architecture and matchlock infantry.
