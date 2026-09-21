# Three playable art directions — 2026-09-21

## Scope and fidelity

This is an in-engine art-direction comparison, **not a claim to reproduce the production quality of the reference games**. It changes character geometry, equipment, architectural details, roof geometry, vegetation, lighting, materials and palette. It does not import or redistribute the commercial games' assets.

The Eastern matchlock setting remains common to all three directions. StarCraft is interpreted here as **StarCraft II**, with its battlefield readability as the reference, not as a request to turn matchlocks into modern rifles or science-fiction weapons.

The character bodies are still procedural prototypes: joint intersections, faces, hands, cloth and surface detail need an authored production-asset pass. Buildings still share a modular settlement kit and terrain remains relatively simple. These are three playable direction samples, not three finished art packs. No new performance claim for 10,000 visible animated enemies is made by this work.

## Research references and interpretation

- [Blizzard: Reimagining Classic StarCraft Units for StarCraft II](https://news.blizzard.com/en-gb/article/21509420/evolution-complete-reimagining-classic-starcraft-units-for-starcraft-ii): official unit illustrations and discussion of battlefield readability. Our interpretation: chunky segmented armour, strong faction-colour blocks, cool steel with restrained warm accents.
- [Numantian Games: They Are Billions](https://www.numantiangames.com/TheyAreBillions/): official gameplay gallery and steampunk setting. Our interpretation: warm dusty earth, aged timber/copper, workshop hardware, muted infected skin and darker wilderness.
- [Age of Empires IV: Chinese civilization](https://www.ageofempires.com/games/age-of-empires-iv/civilizations/chinese): official Chinese civilization imagery. Our interpretation: natural-looking scale, layered tile roofs, red timber and pale masonry, more neutral daylight and green terrain.

These observations are visual interpretations, not claims about the reference studios' internal rendering techniques.

## Play

Open `Assets/_Tests/Frontier/Scenes/FrontierMap.unity`, enter Play and click the Game view. Alternatively launch the locally built `Builds/Frontier.app`.

| Switch | Sample | Geometry and presentation changes |
| --- | --- | --- |
| F5 / first bottom button | A · 铁甲边军 | Wider pauldrons, segmented chest armour, blue cloth, buttresses and armoured shutters, conifer silhouettes, cool steel palette |
| F6 / second bottom button | B · 暮色围城 | Brimmed hat, cross-body ammunition strap and satchel, infected cyst variation, cisterns/chimneys/lantern details, dusty warm palette |
| F7 / third bottom button | C · 山河明军 (default) | Riveted red armour and helmet ornament, more upturned eaves, timber brackets and entrance banners, restrained green/daylight palette |

On macOS keyboards configured for system function keys, use Fn+F5/F6/F7 or click the buttons. Existing box selection, right-click movement, A+left-click attack move, B headquarters/building controls, T melee, Space pause and mouse-wheel zoom remain unchanged.

Switching is synchronous and may hitch while rebuilding the landscape. It preserves the simulation, units and their orders, resource stock, construction/production state and fog discovery. An active unconfirmed building ghost is cancelled. It does not rebuild navigation or reset the map. Current population remains the existing 400 active soldiers + 200 recruitment slots + 5,000 zombies on the 256×256 map.

## Authoring locations

- `Assets/_Game/Resources/VisualStylePresets.json`: three palettes, head/shoulder proportions, sunlight and camera angle. `roughness` is the existing preset field passed to material smoothness; a higher value currently means a shinier finish.
- `Assets/_Game/Editor/CharacterStyleGeometry.cs`: original bone-attached body and equipment geometry. Preserves the licensed animation rig; no Animator per crowd member.
- `Assets/_Game/Editor/CharacterBake.cs`: bakes the original models plus all three variants, seven poses × twelve frames per character. Revision checks automatically regenerate missing/stale editor art when not playing. Manual fallback: Tools → Zombie Game → Characters → Bake Models.
- `Assets/_Game/Scripts/World/FrontierArchitecture.cs`: style-specific building and settlement details.
- `Assets/_Game/Scripts/World/FrontierLandscape.cs`: shared buildings, trees, roofs and geometry batching.
- `Assets/_Game/Scripts/World/FrontierSurface.shader` and `Scripts/Presentation/CharacterAtlas.shader`: surface treatment, native light/shadow response and instancing.
- `Assets/_Game/Scripts/Presentation/VisualStyles.cs`: presentation-only preset loading and validation.

Generated `Assets/_Game/Resources/CharacterGenerated` remains ignored by Git and is never the authored source. Existing Quaternius CC0 rig/animation attribution remains in `Assets/ThirdParty`. The older playable stress scenes retain the original three model resources; the Frontier map selects the new variants. All combat/movement numbers still come from `balance/unit_balance.json` through `UnitBalance`; this change does not edit balance.

## Verification

Unity 6000.6.1f1, macOS standalone player, Apple M2 Pro:

- Native player build passed.
- `-artSmoke`: all three switches passed; simulation and NavMesh-agent array identity, resource stock and region count unchanged. All nine character variants contain seven poses with twelve nonempty, bounded mesh frames and supported shaders.
- Six actual offscreen Unity renders (three world views and three close-ups) generated and visually inspected. They are not concept art or screenshots from the reference games. World images omit the HUD because the scene camera render is captured directly.
- `-frontierSmoke`: distance distribution, recruitment/refunds, dense-group focus, terrain yield, all seven facility types/placement, carried ammunition and melee, legacy character animations, 256×256 map, native obstacle detour and shared stats passed; geometry intrusion count zero.
- UI automation connection to the user's Unity window timed out. The independent player was tested; foregrounding the user's editor and manually clicking its new toolbar were not verified through that connection.

Repeatable commands:

```sh
Unity -batchmode -quit -projectPath '<project>' -executeMethod ZombieGame.EditorTools.FrontierTools.build_player -logFile /tmp/zombie-art-build.log
'Builds/Frontier.app/Contents/MacOS/Zombie Game' -artSmoke -logFile /tmp/zombie-art-render.log
'Builds/Frontier.app/Contents/MacOS/Zombie Game' -frontierSmoke -logFile /tmp/zombie-art-regression.log
```

Art render output defaults to `/tmp/zombie-art-preview`; the delivery copies are in ignored `Builds/ArtPreview`. `-artStyle 0`, `1` or `2` chooses the initial presentation in a standalone player. The smoke fixture is opt-in and lives under `_Tests`; normal gameplay does not activate its gallery, time freeze or state checks.
