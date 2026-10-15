# V0.10 acceptance matrix

All rows start pending. Record evidence, not just a tick or a successful compile.
Do not relabel the inherited failed wild-grass aggregate as a pass.

| ID | Check | Required evidence | State |
| --- | --- | --- | --- |
|01|Frozen baseline and source integrity|Immutable tag, original scene/source hashes, no model refit|Pending|
|02|Compilation and live loop|No current compile/runtime errors; actual advancing frame proof|Pending|
|03|Strict existing actor art|Every deployed unit has approved imported frames; no procedural fallback|Pending|
|04|Movement and attacks|Actual authored walking/attacks, projectile timing and ground contact at RTS distance; disclose missing death/knife clips|Pending|
|05|Map conversion and obstacles|One common rigid height conversion; no water/cliff/forest/outside-map crossing; portal route exists|Pending|
|06|Orders and bounds|Selection, F2, attack/move, unreachable destination recovery, edge pan at four drawable bounds|Pending|
|07|HUD and minimap|Same128m mapping for marks/drag/click/warnings/fog; damaged HP/ammo and selection rings retained|Pending|
|08|Siege intent|Visible human interruption then resume HQ; no noise override or rear-unit global hold|Pending|
|09|Contact invariant|Original zombie-zombie/zombie-human constraints preserved, no hard human-human contacts|Pending|
|10|Building outcomes|HQ/active buildings can be damaged; wall/tower destruction does not infect/spawn zombies|Pending|
|11|Legacy regression|Applicable old Coastal checks in newly compiled code, not only an old prebuilt binary|Pending|
|12|Packaged player|Built standalone, screenshot and sampled active-combat timings, bounded completion evidence|Pending|

Current scope is a small authored-model defense integration, not a complete economy,
all unit types, a10,000-unit performance certification or final art acceptance.
No unexplained health/ceasefire/warp overrides may remain in the delivered scenario.

## 中文

所有检查先标待测，依据真实运行／截图／报告更新。不能以编译成功代替播放与操作，
不能以战后帧率代替交战性能，不能把旧野草失败项改名隐藏。缺项如实留在版本说明。
