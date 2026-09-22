# Persistent headquarters siege and compact settlement

## Behavior

- `BattleSimulation.order_siege` resolves the living headquarters as the strategic objective, not a rally offset.
- A committed zombie may fight visible humans or an immediately attackable building along the route, then resumes headquarters pursuit. A merely closer remote building does not replace the headquarters objective.
- Gunshot memory still updates, but cannot overwrite a committed siege route. Uncommitted zombies keep the existing latest-audible-sound behavior.
- Building approach paths that are absent, partial, invalid or exhausted retry on the existing scheduling interval. No unit balance values were changed.

## Layout

Initial settlement remains nine buildings and 400 deployed soldiers, with 200 recruitment slots and 5000 zombies. The building footprint is reduced from roughly 50×49 to 34×26 tiles. Main roads are shortened to match; the northern muster area and gaps between buildings remain. The depot retains its accepted position to cover the headquarters recruitment exits. The map remains 256×256. No new soldier/building source models were substituted in this change.

The user's reference is [Diplomacy is Not an Option](https://store.steampowered.com/app/1272320/Diplomacy_is_Not_an_Option/). This pass uses compact settlement organization, not its copyrighted assets. Switching the previously requested Eastern realistic direction to low-poly art has not been assumed; that art decision remains open.

## Regression entry points

- `-siegeObjectiveSmoke`: silent attack from a distant old rally point, HQ chosen over a nearer remote decoy, gunshot cannot divert an attacking zombie, path reset recovery. Temporary ceasefire/warp are confined to the disposable test process; shared balance is untouched.
- `-defenseSmoke`: 5000 committed zombies; observed moving peak 4996, zero path failures and zero geometry errors in the short full-map run. This verifies routing startup, not complete destruction or sustained FPS.
- `-humanRosterSmoke`: ordinary newest-noise delivery retained, including 400 shots×10000 listeners.
- `-frontierSmoke`: compact footprint bound, each building perimeter connected to muster, existing spawning/navigation/minimap/selection/construction/supply checks.
- `-settlementSmoke`: actual building damage and infection, building loss stops production/supply, HQ loss still ends the match.

Source changes intentionally extend the existing siege, perception, map and test files instead of adding a replacement game controller.
