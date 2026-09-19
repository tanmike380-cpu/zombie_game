# Gameplay core extraction and repeat-hearing regression

## Ownership

The 256 x 256 playable stress scene now runs the production `ZombieGame.Combat.BattleSimulation`.
It receives explicit spawn positions, human count, explosive-unit flags and obstacles; it does not
author the 400/10000 population or the stress scene layout. `CombatStressSimulation` is a thin test
fixture which supplies those inputs and the optional benchmark assault target policy.

Production modules under `Assets/_Game/Scripts`:

- `Navigation/NativeNavMeshCrowd`: native NavMesh bake/agents/route lifecycle. Current box-map
  adapter supports the game's 256 x 256 map. Routing and avoidance are still Unity NavMesh.
- `Movement/FormationDestinations`: unchanged nearest-first radial bands and within-band matching.
- `Noise/NoiseTimeline`: bounded propagation-arrival scheduling shared by the large and small scenes.
- `AI/SoundMemory`: newest audible emission replaces older sound memory.
- `AI/ZombiePerception`: production battle perception, investigation, chase and melee decisions.
- `Combat/BattleSimulation`, `SoldierOrders`, `CombatSpatialGrid`: battle state, weapons/projectiles,
  explosions, player orders and spatial queries.
- `Vision/CombatFog`: visibility/exploration, using the shared human sight stat.
- `Balance/UnitBalance`: sole authored stats remain `balance/unit_balance.json`.

Test scenes, spawning fixtures, input/display adapters, benchmark orchestration and assertions remain
in `_Tests`. The older small sandbox still has its MonoBehaviour presentation/combat adapter, but
uses the same production noise scheduler and sound-memory policy. It is not a second authoritative
source for hearing rules. This extraction does not create a finished campaign or replace test models.

## Bug and fixed rules

Previously the large-scene emitter skipped every already-activated zombie. Its first pending hearing
time also never reset. A second gunshot could not update an active zombie's destination.

Now every living listener in noise range can receive another event. Hearing a newer emission replaces
the old source; a changed investigation goal resets the stale path and requests a new native route.
This works while moving and after stopping at the old source. Visible humans take priority over
investigation. Dead units do not respond and zombie explosions remain silent.

Events have increasing sequence IDs. A late-arriving *older emission* cannot restore a superseded
source. Out-of-range events do not overwrite memory. Propagation speed still comes from Balance;
the scheduler samples listener position at emission, matching the previous large-scene model (not
continuous acoustic simulation or obstacle attenuation). Sound does not require player fog visibility.

The bounded wheel retains only the newest event per listener per 0.1-second AI arrival bucket.
It allocates at battle construction, not one object per shot/listener. Native path submissions retain
the existing 64/frame scheduling budget; this is not a unit stat and does not delay human group orders.

## Regression coverage

- Real large-scene NavMesh listener: source A, reroute to B while active, actual approach movement,
  settle at B, wake for C, then visible-human chase priority. Fixture resets afterward.
- Pure shared hearing: propagation, old delayed wave rejection, same-tick newest ordering,
  outside radius rejection and a long-frame catch-up.
- Synthetic hearing burst: 400 simultaneous events x 10000 listeners; all retain newest event.
  Timing is isolated queue/delivery cost, **not FPS or a full-combat performance benchmark**.
- Small scene: previously alerted listener replaces its remembered source through shared hearing.
- Existing regressions: 400-unit radial regroup, obstacle detours, measured balance speeds, pursuit,
  fog, selection/groups/orders, gun/damage/melee, remote/contact AOE visuals and silent explosions.

Build both with `ZombieGame.EditorTools.CombatStressTools.build_both_players`; run the built large
player with `-rtsSmoke` and the small player with `-combatSmoke`. Both restore a fresh manual fixture
after successful completion. All stats and humanoid/musket models are unchanged by this task.

## Verified result (2026-09-20)

Both player builds and both complete smoke suites passed. Large-scene retarget reported three source
redirects and passed actual movement/settled wake/visual priority. Measured movement remained human
3.500, runner 4.500, exploder 4.250 tiles/s; balance hash `bb653f0e708650591e08178b7b42244b`.
The deliberately worst-case isolated 4-million-delivery burst took 415.26 ms on this run; it verifies
ordering/capacity, not frame-time suitability for placing every listener inside every shooter's radius.
No new full-combat FPS claim is made. Final logs are local `/tmp/zombie-core-final-build.log`,
`/tmp/zombie-core-final-stress.log`, `/tmp/zombie-core-final-small.log` (not committed).
