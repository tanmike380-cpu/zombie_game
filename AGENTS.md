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
- All living zombies, including previously alerted or settled ones, may hear new gunshots.
  Newer audible emissions replace old sound memory; delayed older waves cannot restore old goals.
  Visible-human pursuit has priority. Zombie explosions never emit attraction noise.
