using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Balance;

namespace ZombieGame.CharacterTests
{
    /// <summary>Automated player inputs only: actual shot, retreat one step, then attack-move.
    /// Production BattleSimulation owns all damage, cooldowns, ammunition and native navigation.</summary>
    public sealed class TripoKitingBattle : MonoBehaviour
    {
        private TripoArcherBattle game;
        private float[] seen_shots, retreat_at, resume_at;
        private Vector3[] retreat_starts;
        private int[] cycles;
        private readonly int[] lane_targets = new int[50];
        private int lane_frame = -1;
        private float started, previous, next_capture;
        private bool automatic = true, complete;
        private string output_directory;
        private readonly List<float> frame_ms = new List<float>();
        private readonly List<float> combat_frame_ms = new List<float>();
        private readonly List<float> window_frame_ms = new List<float>();
        private float victory_seconds = -1;
        private float defeat_seconds = -1;
        private int peak_explosion_events, focused_frames;
        private int peak_charging_zombies, peak_attacking_zombies;
        private readonly List<Snapshot> snapshots = new List<Snapshot>();
        private int retreat_orders, failed_orders, completed_steps, repeat_shooters, peak_moving, shots_while_retreating;
        private float retreat_distance;
        private const float STEP_DISTANCE = 1.2f; // Player command distance, not authored unit speed.
        private const float DURATION = 60;

        [Serializable] private sealed class Snapshot
        {
            public float seconds;
            public float window_fps;
            public float minimum_spacing_ratio;
            public int stopped_zombies;
            public int humans_alive, zombies_alive, shots, hits, bites, ammunition, moving, submitted, culled;
        }
        [Serializable] private sealed class Report
        {
            public string hardware, scope, result;
            public int humans = 400, zombies, width, height, triangles, zombie_triangles, blasts, peak_explosion_events, focused_frames, frames;
            public int shots, hits, kills, human_deaths, bites, melee, ammunition_used, dropped_projectiles;
            public int retreat_orders, completed_steps, repeat_shooters, failed_orders, shots_while_retreating;
            public int geometry_errors, path_failures, peak_moving, activated;
            public int peak_charging_zombies, peak_attacking_zombies, emitted_slime;
            public float mean_retreat_distance, elapsed, fps, p95_ms, p99_ms, combat_fps, combat_p95_ms, victory_seconds, defeat_seconds;
            public Snapshot[] timeline;
        }

        public static void fill_layout(Vector3[] positions, int humans)
        {
            if (humans != 400 || positions.Length != 2400 && positions.Length != 4400) throw new ArgumentException("Kiting fixture requires 400 versus 2000 or 4000");
            float human_spacing = UnitBalance.config.formation_spacing;
            float zombie_spacing = UnitBalance.navigation_radius(positions.Length == 4400 ? UnitBalance.exploder : UnitBalance.get("walker")) * 2 + .1f;
            int columns = positions.Length == 4400 ? 80 : 50;
            for (int i = 0; i < humans; i++) positions[i] = new Vector3((i % 40 - 19.5f) * human_spacing, 0, -i / 40 * human_spacing);
            for (int i = humans; i < positions.Length; i++)
            { int slot = i - humans; positions[i] = new Vector3((slot % columns - (columns-1)*.5f) * zombie_spacing, 0, 12 + slot / columns * zombie_spacing); }
        }

        public void initialize(TripoArcherBattle fixture)
        {
            game = fixture;
            int count = game.current.soldier_count;
            seen_shots = Enumerable.Repeat(float.NegativeInfinity, count).ToArray();
            retreat_at = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
            resume_at = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
            retreat_starts = new Vector3[count]; cycles = new int[count];
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-tripoOutput");
            output_directory = index >= 0 && index + 1 < args.Length ? args[index + 1]
                : Path.Combine(Application.persistentDataPath, "TripoKiting", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output_directory);
            game.current.forced_target = find_charge_target;
            for (int i = count; i < game.current.total_count; i++)
                if (!game.current.prepare_assault_path(i)) throw new InvalidOperationException("Charge route rejected: " + i);
            game.current.release_assault();
            for (int i = 0; i < count; i++)
            {
                game.current.selected[i] = true;
                game.current.issue_order(i, SoldierOrder.Stop, game.current.positions[i]);
            }
            started = previous = Time.realtimeSinceStartup;
            game.benchmarking = Array.IndexOf(args, "-tripoQuit") >= 0;
            if (!game.benchmarking && Array.IndexOf(args, "-tripoPaused") >= 0)
            { game.advance_simulation = false; Time.timeScale = 0; }
            next_capture = 5;
            Debug.Log($"[TripoKiting] READY 400 crossbow / {game.current.zombie_count} {(game.exploder_fixture ? "Tripo exploders" : "walkers")}; all enemies released; original shared balance");
        }

        private int find_charge_target(int index)
        {
            var battle = game.current;
            if (lane_frame != Time.frameCount)
            {
                lane_frame = Time.frameCount;
                for (int lane = 0; lane < lane_targets.Length; lane++)
                {
                    float best = float.PositiveInfinity; int target = -1;
                    for (int i = 0; i < battle.soldier_count; i++)
                    {
                        if (battle.health[i] <= 0) continue;
                        float score = Mathf.Abs(battle.positions[i].x - (lane - 24.5f) * UnitBalance.config.formation_spacing * 39 / 49) * 3 - battle.positions[i].z;
                        if (score < best) { best = score; target = i; }
                    }
                    lane_targets[lane] = target;
                }
            }
            int column = Mathf.Clamp(Mathf.RoundToInt(battle.positions[index].x / (UnitBalance.config.formation_spacing * 39 / 49) + 24.5f), 0, 49);
            return lane_targets[column];
        }

        private void LateUpdate()
        {
            if (game == null || complete) return;
            if (Input.GetKeyDown(KeyCode.F8)) automatic = !automatic;
            if (Input.GetKeyDown(KeyCode.Space))
            { game.advance_simulation = !game.advance_simulation; Time.timeScale = game.advance_simulation ? 1 : 0; }
            float now = Time.realtimeSinceStartup, elapsed = now - started;
            if (!game.advance_simulation)
            {
                started += now - previous; previous = now;
                game.status = $"PAUSED | 400 CROSSBOW vs {game.current.zombie_count} | Space start | F8 {(automatic ? "take manual control" : "restore auto kiting")} | R reset";
                return;
            }
            if (elapsed > 2)
            {
                float interval = (now - previous) * 1000;
                frame_ms.Add(interval); window_frame_ms.Add(interval);
                if (Application.isFocused) focused_frames++;
                if (game.current.shots > 0 && game.current.living_soldiers > 0 && game.current.dead_zombies < game.current.zombie_count)
                    combat_frame_ms.Add(interval);
            }
            if (victory_seconds < 0 && game.current.dead_zombies == game.current.zombie_count) victory_seconds = elapsed;
            if (defeat_seconds < 0 && game.current.living_soldiers == 0) defeat_seconds = elapsed;
            peak_explosion_events = Math.Max(peak_explosion_events, game.explosions.visible_events);
            peak_charging_zombies=Math.Max(peak_charging_zombies,game.charging_zombies);
            peak_attacking_zombies=Math.Max(peak_attacking_zombies,game.attacking_zombies);
            previous = now;
            peak_moving = Math.Max(peak_moving, game.current.moving_now);
            if (automatic && game.advance_simulation) update_kiting();
            game.status = $"400 CROSSBOW vs {game.current.zombie_count} {(game.exploder_fixture ? "EXPLODERS" : "ZOMBIES")} | {(automatic ? "AUTO shoot-step-shoot" : "MANUAL")} | {elapsed:F0}/60s | F8 control | Space pause | animation DRAFT";
            if (elapsed >= next_capture)
            {
                record_snapshot(elapsed);
                next_capture += 10;
            }
            if (elapsed >= DURATION) finish(elapsed);
        }

        private void update_kiting()
        {
            var battle = game.current;
            for (int i = 0; i < battle.soldier_count; i++)
            {
                if (battle.health[i] <= 0) continue;
                float shot = battle.attack_started_at[i];
                if (shot > seen_shots[i] && !battle.last_attack_melee[i])
                {
                    if (!float.IsPositiveInfinity(resume_at[i])) shots_while_retreating++;
                    seen_shots[i] = shot;
                    if (++cycles[i] == 2) repeat_shooters++;
                    retreat_at[i] = shot + .18f; // Cosmetic release hold, does not reset the attack cooldown.
                }
                if (Time.time >= retreat_at[i])
                {
                    retreat_starts[i] = battle.positions[i];
                    Vector3 goal = retreat_starts[i] + Vector3.back * STEP_DISTANCE;
                    if (battle.issue_order(i, SoldierOrder.Move, goal)) retreat_orders++;
                    else failed_orders++;
                    retreat_at[i] = float.PositiveInfinity;
                    resume_at[i] = Time.time + Mathf.Min(.9f, battle.stats_for(i).attack_interval * .6f);
                }
                if (Time.time >= resume_at[i])
                {
                    float travelled = Mathf.Max(0, retreat_starts[i].z - battle.positions[i].z);
                    retreat_distance += travelled;
                    if (travelled >= STEP_DISTANCE * .5f) completed_steps++;
                    battle.issue_order(i, SoldierOrder.AttackMove, battle.positions[i] + Vector3.forward * .1f);
                    resume_at[i] = float.PositiveInfinity;
                }
            }
        }

        private void record_snapshot(float elapsed)
        {
            var battle = game.current;
            int stopped = 0;
            for (int i=battle.soldier_count;i<battle.total_count;i++)
                if(battle.health[i]>0 && battle.crowd.agents[i].isStopped) stopped++;
            snapshots.Add(new Snapshot { seconds = elapsed, window_fps = window_frame_ms.Count > 0 ? 1000 / window_frame_ms.Average() : 0, humans_alive = battle.living_soldiers,
                minimum_spacing_ratio = CrowdSpacingChecks.minimum_ratio(battle), stopped_zombies = stopped,
                zombies_alive = battle.zombie_count - battle.dead_zombies, shots = battle.shots, hits = battle.hits,
                bites = battle.bites, ammunition = battle.ammunition.Sum(), moving = battle.moving_now,
                submitted = game.crowd_renderer.submitted, culled = game.crowd_renderer.culled });
            ScreenCapture.CaptureScreenshot(Path.Combine(output_directory, $"battle-{elapsed:00}.png"));
            Debug.Log("[TripoKiting] " + JsonUtility.ToJson(snapshots.Last()));
            window_frame_ms.Clear();
        }

        private void finish(float elapsed)
        {
            complete = true;
            var battle = game.current;
            frame_ms.Sort();
            combat_frame_ms.Sort();
            bool passed = battle.shots > 0 && battle.hits > 0 && battle.dead_zombies > 0 && peak_moving > 1000
                && repeat_shooters > 20 && completed_steps > 20 && shots_while_retreating == 0
                && failed_orders == 0 && battle.geometry_errors == 0 && battle.dropped_projectiles == 0;
            if (game.exploder_fixture) passed &= battle.blasts == battle.dead_zombies && peak_explosion_events > 0
                && peak_charging_zombies>0 && peak_attacking_zombies>0 && game.explosions.emitted_splashes>0;
            var report = new Report { hardware = SystemInfo.processorType + "; " + SystemInfo.graphicsDeviceName,
                scope = "Production combat/navigation/fog/HP/ammo and silent purple slime; full-detail Tripo animation DRAFT; shared sampled meshes; frustum culling; no terrain/obstacles/buildings. Larger shared native collision footprints, no custom separation solver. All enemies charge lane-front humans, stop when none survive. No stat overrides or resupply. First 2s/setup excluded. Combat FPS only while both sides survive after first shot. Screenshot/diagnostic costs included. Player step 1.2 units, original cooldown and overkill preserved.",
                result = passed ? "PASS combat/retreat only; spacing is diagnostic, NOT a hard-separation pass; animation pending review" : "FAIL see counters",
                width = Screen.width, height = Screen.height, triangles = game.archer_frames.poses[0].frames[0].triangles.Length / 3,
                zombies = battle.zombie_count, zombie_triangles = game.exploder_fixture ? game.exploder_frames.poses[0].frames[0].triangles.Length / 3 : 0,
                blasts = battle.blasts, peak_explosion_events = peak_explosion_events, focused_frames = focused_frames, frames = frame_ms.Count,
                shots = battle.shots, hits = battle.hits, kills = battle.dead_zombies, human_deaths = battle.dead_soldiers,
                bites = battle.bites, melee = battle.melee_strikes,
                ammunition_used = battle.soldier_count * battle.stats_for(0).ammunition_capacity - battle.ammunition.Sum(),
                dropped_projectiles = battle.dropped_projectiles, retreat_orders = retreat_orders, completed_steps = completed_steps,
                repeat_shooters = repeat_shooters, failed_orders = failed_orders, shots_while_retreating = shots_while_retreating,
                geometry_errors = battle.geometry_errors, path_failures = battle.path_failures, peak_moving = peak_moving, activated = battle.ever_active,
                peak_charging_zombies=peak_charging_zombies,peak_attacking_zombies=peak_attacking_zombies,emitted_slime=game.explosions.emitted_splashes,
                mean_retreat_distance = retreat_distance / Mathf.Max(1, retreat_orders), elapsed = elapsed,
                combat_fps = combat_frame_ms.Count > 0 ? 1000 / combat_frame_ms.Average() : 0,
                combat_p95_ms = combat_frame_ms.Count > 0 ? combat_frame_ms[(int)((combat_frame_ms.Count - 1) * .95f)] : 0,
                victory_seconds = victory_seconds,
                defeat_seconds = defeat_seconds,
                fps = 1000 / frame_ms.Average(), p95_ms = frame_ms[(int)((frame_ms.Count - 1) * .95f)],
                p99_ms = frame_ms[(int)((frame_ms.Count - 1) * .99f)], timeline = snapshots.ToArray() };
            File.WriteAllText(Path.Combine(output_directory, "kiting-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[TripoKiting] COMPLETE " + JsonUtility.ToJson(report));
            game.status = $"{report.result} | {report.fps:F1} average FPS | R restart";
            game.advance_simulation = false;
            Time.timeScale = 0;
            game.benchmarking = false;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoQuit") >= 0) Application.Quit(passed ? 0 : 2);
        }
    }
}
