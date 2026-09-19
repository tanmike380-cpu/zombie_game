# Combat stress results — 2026-09-19

Unity 6000.6.1f1 macOS standalone, Apple M2 Pro, 1600×1000, VSync off.
Scene: `Assets/_Tests/CombatStress/Scenes/Tech_CombatStress.unity`.
400 Shenji vs 9,000 Runners + 1,000 Exploders; 256×256 grass map, five rock
obstacles, two ponds, fog enabled (human sight10, zombie sight4).

| Metric | Normal noise | Full assault |
|---|---:|---:|
| Setup incl. native paths (seconds) | 0.774 | 0.862 |
| Observation duration (seconds) | 30.008 | 22.264 |
| Sampled frames, after 3s warmup | 21,042 | 1,448 |
| Mean frame time (ms) | 1.283 | 13.297 |
| p95 / p99 (ms) | 2.605 / 3.617 | 16.355 / 19.192 |
| Frames near a combat event | 3,055 | 1,443 |
| Combat-frame mean / p95 (ms) | 2.160 / 3.181 | 13.302 / 16.355 |
| Zombies ever activated | 784 | 10,000 |
| Peak actually moving | 640 | 10,000 |
| Zombies active at end | 0 | 9,152 |
| Soldier / zombie deaths | 130 / 784 | 400 / 848 |
| Shots / hits | 2,740 / 2,255 | 2,846 / 2,498 |
| Bites / blasts | 573 / 93 | 2,180 / 206 |
| Pending native paths at end | 0 | 4,190 |
| Failed path submissions / geometry violations / dropped bullets | 0 / 0 / 0 | 0 / 0 / 0 |
| GC generation0 collections | 198 | 28 |

Interpretation:

- Full assault averaged roughly 75 FPS, but this is one local run, not a minimum
  hardware guarantee. No claim that all 10K were attacking at melee range at once.
- The 4,190 pending paths are a real retargeting latency warning. Good average
  frame time does not prove responsive navigation under front-line casualties.
- All 400 fixed-position soldiers died in about22 seconds. The test does not
  demonstrate long-duration constant-load combat or good final balance.
- Normal noise attracted only784 zombies, all subsequently killed. Its overall
  average is dominated by idle time; use its combat-frame subset, not an inflated
  headline FPS, when comparing firing cost.
- Fog updates and active offscreen NavMesh agents remain in the timing. Hidden
  enemies are not drawn. Animated models, shadow maps and networking are absent.
- Debug HUD/string allocation and measurement buffers contribute to GC; further
  allocation profiling is warranted before making a steady-frame-time claim.

Evidence directory:
`/Users/tankaixi/Library/Application Support/DefaultCompany/Zombie Game/CombatStress/20260919-231619/`.
Logs: `/tmp/zombie-combat-stress-build.log`, `/tmp/zombie-combat-stress-player.log`.
Raw logs and local build binaries are not committed.

## 中文结论

万人全军推进平均13.3毫秒/帧（约75 FPS），p95为16.4毫秒。
确实测到一万只同时移动，但结束时4190个寻路请求仍排队，重新选目标有明显优化空间。
400个固定站位火枪兵22秒左右全灭；这不是“400必胜1万”或长期满负载的平衡结论。
正常噪音只激活784只，不能把大量待机时的高帧率当成万人战斗性能。
