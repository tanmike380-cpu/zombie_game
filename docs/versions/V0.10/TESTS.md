# V0.10 acceptance matrix

Final verification matrix. Record evidence, not just a tick or a successful compile.
Do not relabel the inherited failed wild-grass aggregate as a pass.

| ID | Check | Required evidence | State |
| --- | --- | --- | --- |
|01|Frozen baseline and source integrity|Immutable tag, original scene/source hashes, no model refit|Pass final SHA recheck: five sources match; Combat/Navigation/balance/oldv6/model exports unchanged|
|02|Compilation and live loop|No current compile/runtime errors; actual advancing frame proof|Pass build and50s standalone; earlier Editor frames310→312; latest compiler clear|
|03|Strict existing actor art|Every deployed unit has approved imported frames; no procedural fallback|Pass first standalone; Archer/GreekFire/Walker/Exploder/Hound only|
|04|Movement and attacks|Actual authored walking/attacks, projectile timing and ground contact at RTS distance; disclose missing death/knife clips|Partial: all8 imported Run/Attack slots have24 actual articulated frames; dedicated death/knife and manual release-pose approval remain missing|
|05|Map conversion and obstacles|One common rigid height conversion; no water/cliff/forest/outside-map crossing; portal route exists|Pass native routes for240 spawns, outside boundary and portal, geometry0; wholeY translation only|
|06|Orders and bounds|Selection, F2, attack/move, unreachable destination recovery, edge pan at four drawable bounds|Production handler checks pass; physical input not verified while Mac locked|
|07|HUD and minimap|Same128m mapping for marks/drag/click/warnings/fog; damaged HP/ammo and selection rings retained|Pass128m drag/clamp and fresh legacy UI pixels zoom10/50/140; physical input pending|
|08|Siege intent|Visible human interruption then resume HQ; no noise override or rear-unit global hold|Partial: legacy chase/intent pass; autonomous full-map completion FAIL at farm-hut corners; player orders can clear|
|09|Contact invariant|Original zombie-zombie/zombie-human constraints preserved, no hard human-human contacts|Pass unchanged core; current minimum1.0; fresh full contact/regroup/detour suite pass|
|10|Building outcomes|HQ/active buildings can be damaged; wall/tower destruction does not infect/spawn zombies|Pass fresh legacy suite; real V010 wall deaths burst0; no new infection behavior|
|11|Legacy regression|Applicable old Coastal checks in newly compiled code, not only an old prebuilt binary|Pass fresh V010LegacyRegression default and-unitUiSmoke, exit0; physical desktop not covered|
|12|Packaged player|Built standalone, screenshot and sampled active-combat timings, bounded completion evidence|Pass integration smoke and scripted-player full completion; performance NOT passed|

Current scope is a small authored-model defense integration, not a complete economy,
all unit types, a10,000-unit performance certification or final art acceptance.
No unexplained health/ceasefire/warp overrides may remain in the delivered scenario.

## 中文

所有检查先标待测，依据真实运行／截图／报告更新。不能以编译成功代替播放与操作，
不能以战后帧率代替交战性能，不能把旧野草失败项改名隐藏。缺项如实留在版本说明。

## First standalone / longer editor run, before corner experiment

- Packaged Metal1920×1080 smoke: PASS50 real seconds,251 shots,122 hits,79 kills,
  geometry0, minimum zombie contact ratio1.0. All240 initial positions/routes and
  serialized six-layer Terrain references checked. Mean active frame45.49ms,
  p95 116.69ms on AppleM2Pro; **not a performance pass**.
- Longer normal Editor play reached3 waves but stalled at219/240 kills for over120s.
  Example zombie684 had valid native path and proposed step(−0.184139,0,0), but
  accepted displacement0 between a farm-hut corner and two living neighbours at1.101m.
  This remains an open failure, not erased by the first-wave smoke result. A scenario-only
  rounded native-carving comparison was tested and failed; contact/navigation core stays frozen.
- The Mac is locked; desktop observation/input was unavailable. No unlock/security
  settings were changed. Handler checks are not a claim of physical keyboard/mouse QA.

## Current source verification and shadow comparison

- [Final smoke](evidence/final-smoke.json): PASS50.12s,243shots/120hits/79kills,
  geometry0/contact1.0; actual Terrain pixel contribution0.236599 (threshold unchanged
  0.0003). This is the Terrain test, **not** the inherited failed wild-grass test.
- Eight walking/attack slots across archer/hound/walker/exploder retain authored24-frame
  meshes, consistent topology and sampled vertex displacement0.137–0.879m. This proves
  animation data exists and changes, not professional motion-quality approval.
- Same rebuilt player /1920×1080 /ortho25 /50s camera fixture: crop CASTOff peak45,470,082
  submitted triangles, CASTOn61,178,408. Active means39.96ms OFF versus34.61ms ON;
  p95 100.62 versus100.01ms. Background timing noise defeats an FPS-improvement claim:
  the lower submission count is measurable, a speedup is **not established**.
- [Crop-on comparison](evidence/crop-shadows-on.json),
  [scripted-player completion](evidence/player-cleanup.json),
  [retained failed rounded run](evidence/rounded-full-autohold.json),
  [fresh legacy result](evidence/legacy-regressions.json), [review](REVIEW.md).
- Normal launch has no test flags and `run_on_start=false`:60s preparation, no scripted
  player cleanup, no test camera lock, no schedule advance and no automatic quit.

## Final full run and ordinary-launch teardown

- [Final full player-order run](evidence/final-full-player-orders.json): PASS,
  Won194.65s/3waves/240kills,826shots/479hits/0bites,geometry0/contact1.0.
  Active1919samples mean46.42ms/p95 107.45ms, simulation0.395ms/presentation0.785ms.
  It still uses the explicit180s player-command fixture and is not an auto-hold pass.
- Ordinary Editor menu launch after unused snapshot cleanup: real frames249→251,
  stillPreparing/paused,64defenders/240zombies/0waves and `run_on_start=false`.
  Scene applies2 shadow cascades; stopping Play restores the prior4/150m/High,
  with a clean saved scene and no QualitySettings diff. Initial edit-mode disposable
  component probe was an invalid lifecycle test (non-ExecuteAlways callbacks do not
  represent Play teardown, and Unity normalizes cascade0 to1); global values were
  restored before repeating the actual Play lifecycle test.
- Final source hashes match the frozen baseline. No changes under Combat/Navigation,
  balance, original model exports, Blender master files or oldv6 scene/settings.
- Rebuild/clean-clone limits and generated cache dependencies are explicit in
  [REBUILD.md](REBUILD.md). No clean-machine certification was performed.
