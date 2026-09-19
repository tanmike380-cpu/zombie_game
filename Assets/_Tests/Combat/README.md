# Playable RTS combat sandbox

Open `Scenes/Tech_CombatSandbox.unity`, press Play, or use
`Tools > Zombie Game > Combat > Open Playable RTS Test`.
Standalone build: `Builds/CombatSandbox.app`.

## Controls

- Left-click a blue Shenji, or drag a rectangle, to select friendly living units.
  Shift adds selection. Left-click is not an attack command.
- Right-click: direct move, including when clicking an enemy's position. Moving
  units do not acquire enemies until their move completes or you change the order.
- A then left-click enemy: attack that target. A then left-click ground: attack-move.
- Q then left-click: patrol between the order origin and destination; fight enemies
  encountered, then resume the route.
- S: cancel order and stop. Can attack visible enemies already in range, but cannot chase.
- M then left-click: move. Esc cancels a pending targeting cursor.
- R: reset battle. Mouse wheel: zoom. No WASD unit controls.

Eight Shenji firearm infantry versus 36 AI Runners and 6 AI Exploders. Native Unity NavMesh
handles movement/avoidance. Blue capsules have simple yellow musket markers; red
capsules are Runners and purple capsules are Exploders. Green rings mark selection. Health bars, visible tracers,
noise rings, order feedback, damage, death, victory/defeat and restart are included.

Prototype values: Shenji range 7, shot noise radius **14 = range × 2**.
This sandbox applies a shared **1.25× speed multiplier**: Shenji 2.8 → 3.5,
Runner 3.6 → 4.5, Exploder 3.4 → 4.25. Design baselines / slow Walker balance are unchanged.
Test-only combat tuning: Shenji HP100, zombie HP60, projectile damage20/.8s cooldown,
zombie melee damage10/1s cooldown, direct sight4 and melee reach1. High walls block sight
and shots; low walls block walking but allow shots over the top. Noise passes through both, propagates at 5 tiles/s with .6s pulse
duration. A listener without a visible target remembers the sound origin; direct
sight supersedes noise. A hit also gives the victim the shooter's last position.

Exploders arm within 1.2 tiles of a visible enemy, stop and display a magenta
2-tile warning ring for .65 seconds, then deal 70 damage to each living friendly
inside that radius (horizontal distance). High walls block damage; low walls do not.
Exploders die on detonation. Every lethal cause triggers the same explosion exactly
once: contact fuse, being shot at range, or being killed during the fuse. The expanding
magenta ring lasts .85 seconds even with no soldiers nearby. Focus fire is useful
because it makes the explosion happen away from the formation, not because it cancels it.
Zombie explosions produce **no attraction noise** and do not wake nearby idle zombies;
only human weapon shots use the 2× range rule here. No zombie friendly fire,
chain explosions, building damage or infection conversion in this slice.
Range-focus purple units or spread the soldiers to reduce clustered damage.

This is a functional slice, not a full combat model. Tracers follow their selected
living target for legibility (not a physics/ballistic accuracy test). No authored
models/animations, ammo economy, infection, squads, fog, networking or performance
claims. Units can crowd around a shared destination; there is no formation system.
Sight/noise and projectile checks are intentionally simple for this small population.

To observe attraction: move near the low wall, then A-click a nearby enemy.
Zombies beyond gun range but within 14 tiles hear the pulse and show INVESTIGATE;
CHASE means a target was directly seen within 4 tiles. Heard counts pulse deliveries,
not distinct zombies. Hits can also trigger investigation in the struck victim.

Responsiveness: explicit move orders resolve native NavMesh paths synchronously;
stop clears velocity; acceleration is 100 tiles/s² and facing follows desired velocity.
Armed A/M/Q commands fire on mouse-down, not mouse-up. This small-population method
is not a recommendation to calculate 10,000 paths synchronously in one frame.

## Verification

Editor build entry: `ZombieGame.EditorTools.CombatSandboxTools.build_player`.
Launch the Player with `-combatSmoke` for an automated command/combat check. It
verifies enemy command rejection, reverse drag rectangle math, move/no auto-fire,
patrol persistence, stopping (<0.1 tile drift), reversal within 100ms, low/high-wall sight,
2× noise radius, noise-only attraction outside gun range behind a high wall, outside-radius rejection,
noise memory, projectiles/damage/enemy death, zombie melee,
friendly death, population/speed values, two clustered AOE victims, an outside-radius
survivor, exploder self-death, lethal hits during the fuse and remote deaths showing
an explosion without nearby soldiers, effect cleanup and duplicate-death protection;
also checks that explosions deliver no hearing events and leave idle zombies unaware;
writes `Application.persistentDataPath/CombatSandbox/smoke.txt` on
success and resets to a fresh playable battle. Failure logs an exception and does
not write a new pass. Mouse selection and key/mouse mappings additionally require
visible UI checks. Automated checks temporarily lock input to avoid interference;
normal launch never auto-controls the player's units.

## 中文

纯RTS操作：左键点选/框选，右键移动，A后左键攻击/攻击移动，Q后左键巡逻，
S停止，M后左键移动，Esc取消待下达指令，R重开。僵尸不可控制。
先用简单模型验证选兵、开枪、噪音、追击、近战、死亡与重开的完整小循环。
8名神机营，36只快速僵尸+6只紫色自爆僵尸。
神机营射程7、声音14；移速同比提升25%，神机营3.5、快速僵尸4.5、自爆僵尸4.25。
矮墙可越墙开枪，高墙挡枪，声音均可穿过。加速只作为本场景试玩参数，不覆盖设计基线。
自爆僵尸贴近1.2格后预警0.65秒，再对2格内我方单位各造成70伤害并自毁；高墙挡伤害。
贴身自爆、远程击杀、预警期间击杀都会爆炸一次；附近没人也播放0.85秒扩散圈。
集火的价值是让它远离兵群时爆炸。先验证集火、散兵与AOE，不接感染、连锁爆炸或建筑伤害。
僵尸自爆没有声音吸引事件，不惊动其他僵尸；紫色爆炸圈只是伤害效果，不是声音扩散圈。
