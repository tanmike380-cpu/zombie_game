# Ten-tile noise trigger test

Open `Scenes/Tech_NoiseBenchmark.unity` and press Play, or choose
`Tools > Zombie Game > Noise > Open 10 Tile Test`.

10,000 normal-size units stand one tile apart on a 256 x 256 map. Only units
within ten horizontal tiles of the source at (2,0) should respond. Two close
staggered walls stand between the near edge of the horde and the source. There
is no global attraction, compressed unit radius or enormous test sound.

One pulse starts three seconds after reset. It travels at 5 tiles/s, with a .6 s
audible interval at each point. Bands `(0,1]` ... `(9,10]` have 100% ... 10%
strength. The exact ten-tile boundary is included; anything farther is excluded.
The demonstration uses one analytic circular pulse, not a complete multi-source
Noise Grid, realistic acoustics, or audio playback. Walls affect movement only,
not sound, matching the V1 simplification. Hearing is queried at 20 Hz.

Each listener remembers the source and requests a route using Unity's native
CalculatePath/SetPath only when hearing triggers. Silence does not cancel it.
Native medium-quality avoidance remains enabled, radius .25 and height 1.2 are
unchanged. Speed is 1.8 tiles/s (ordinary zombie prototype). Listeners stop near
the remembered source; crowding can prevent everyone reaching that single point.
Do not require all 10,000 units, or even every listener, to occupy the source.

Idle agents are disabled until heard, remain in place and do not participate in
avoidance. All units remain rendered. Thus this workload measures 10K presence +
hearing queries + a local active group, NOT 10K simultaneously navigating units.
Rendering is batched instancing. The camera starts close to the hearing area;
F shows the whole horde, C returns close, wheel zooms, R resets/replays, N emits
another pulse. Gray means idle, red means heard/remembered, blue means walls,
yellow marks the source and radius, green marks the traveling sound front.

At 28 seconds, the Player saves a report under
`Application.persistentDataPath/NoiseBenchmark/<timestamp>.txt`. It compares the
analytically expected listener count with triggered count, checks no out-of-range
triggers, native route failures, wall-centre penetration, or idle displacement,
and confirms listeners kept moving after the pulse expired. Arrival count is
reported, not required to equal all listeners. Frame times include the local
camera, GUI, rendering and checks; they are not comparable to full-map 10K movement.

`Tools > Zombie Game > Noise > Validate Hearing Rules` checks all ten bands,
the exact/diagonal/outside boundary, delayed hearing, pulse expiry, weak-sound
memory, updating source and direct-target priority. The last two are rule-level
tests; the visible fixture has one source and no combat target. Build entry point:
`ZombieGame.EditorTools.NoiseBenchmarkTools.build_player`.

## 中文

场上一万只，只有十格以内的响应。声音强弱不决定走几步；听见后去调查
声源，声音结束仍保留目标。R重新演示，N再次发声，F全图，C近景。
正常尺寸不变，范围外继续待机，不再让一次枪声把全图僵尸都叫来。
