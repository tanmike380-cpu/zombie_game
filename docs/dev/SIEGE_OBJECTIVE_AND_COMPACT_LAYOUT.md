# Persistent headquarters siege and compact settlement

## Behavior

- `BattleSimulation.order_siege` resolves the living headquarters as the strategic objective, not a rally offset.
- A committed zombie may fight visible humans or an immediately attackable building along the route, then resumes headquarters pursuit. A merely closer remote building does not replace the headquarters objective.
- Gunshot memory still updates, but cannot overwrite a committed siege route. Uncommitted zombies keep the existing latest-audible-sound behavior.
- Building approach paths that are absent, partial, invalid or exhausted retry on the existing scheduling interval.
- Siege uses simple attack-move: visible humans interrupt HQ pursuit; death or loss of sight resumes HQ. No timed retaliation memory or damage-source threat table. Route recovery checks once per second only when needed; valid combat paths are not reset.
- HQ destruction completes the strategic task; zombies do not start a global sweep of remaining buildings. Local visible-human combat may still continue.
- Ordinary sound memory has a separate pending-investigation flag. Seeing a human temporarily overrides sound; losing visual contact restores an unfinished latest sound investigation rather than treating the last human position as the sound origin. Arrival completes that investigation without forgetting sequence ordering.

## Layout

Initial settlement remains nine buildings and 400 deployed soldiers, with 200 recruitment slots and 5000 zombies. The building footprint is reduced from roughly 50×49 to 34×26 tiles. Main roads are shortened to match; the northern muster area and gaps between buildings remain. The depot retains its accepted position to cover the headquarters recruitment exits. The map remains 256×256. No new soldier/building source models were substituted in this change.

The user's reference is [Diplomacy is Not an Option](https://store.steampowered.com/app/1272320/Diplomacy_is_Not_an_Option/). Art direction is now confirmed as low-poly with Eastern architecture and matchlock infantry. Model replacement is not part of this AI patch and has not been claimed complete.

## Regression entry points

- `-siegeObjectiveSmoke`: passed silent attack from a distant old rally point, HQ chosen over a nearer remote decoy, sound diversion protection, path reset recovery, encountered-human interruption, dead-human return to HQ and HQ completion. Ordinary sight-over-sound, resumed unfinished sound investigation and arrival settling also passed. Temporary ceasefire/death/warp fixtures are confined to the disposable test process; shared balance is untouched.
- `-defenseSmoke`: 5000 committed zombies; observed moving peak 4996, zero path failures and zero geometry errors in the short full-map run. This verifies routing startup, not complete destruction or sustained FPS.
- `-humanRosterSmoke`: ordinary newest-noise delivery retained, including 400 shots×10000 listeners.
- `-frontierSmoke`: compact footprint bound, each building perimeter connected to muster, existing spawning/navigation/minimap/selection/construction/supply checks.
- `-settlementSmoke`: actual building damage and infection, building loss stops production/supply, HQ loss still ends the match.

Source changes intentionally extend the existing siege, perception, map and test files instead of adding a replacement game controller.
