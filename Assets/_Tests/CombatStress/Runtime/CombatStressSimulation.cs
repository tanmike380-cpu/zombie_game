using System;
using ZombieGame.Balance;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.PerformanceTests;

namespace ZombieGame.CombatStressTests
{
    /// <summary>Fixed defensive line, native crowd navigation, spatial combat queries.</summary>
    public sealed partial class CombatStressSimulation : IDisposable
    {
        public const int SOLDIERS = 400, ZOMBIES = 10000, TOTAL = SOLDIERS + ZOMBIES;
        public readonly Vector3[] positions = new Vector3[TOTAL];
        public readonly float[] health = new float[TOTAL];
        public readonly bool[] activated = new bool[TOTAL];
        public readonly bool[] exploder = new bool[TOTAL];
        public readonly NavMeshCrowd crowd;
        public readonly bool assault;
        public readonly bool playable;
        public readonly Vector3[] soldier_facing = new Vector3[SOLDIERS];
        public int shots, hits, bites, blasts, heard, dead_soldiers, dead_zombies, ever_active;
        public int active_now, pending, path_failures, dropped_projectiles, moving_now;
        public int peak_active, peak_moving, geometry_errors;
        public float last_combat_time = -10;
        public readonly Shot[] projectiles = new Shot[2048];
        public readonly Flash[] flashes = new Flash[256];
        public struct Shot { public bool active; public Vector3 position; public int target; }
        public struct Flash { public Vector3 origin; public float expires; }
        private readonly CombatSpatialGrid zombie_grid = new CombatSpatialGrid(TOTAL);
        private readonly CombatSpatialGrid soldier_grid = new CombatSpatialGrid(TOTAL);
        private readonly float[] next_attack = new float[TOTAL], hearing_due = new float[TOTAL], fuse = new float[TOTAL];
        private readonly Vector3[] memories = new Vector3[TOTAL];
        private readonly bool[] needs_path = new bool[TOTAL], bad_geometry = new bool[TOTAL];
        private readonly int[] lane_targets = new int[100], path_target = new int[TOTAL];
        private int path_cursor = SOLDIERS, shot_cursor, flash_cursor;
        private float next_tick;

        public CombatStressSimulation(bool global_assault, bool player_controlled = false)
        {
            assault = global_assault; playable = player_controlled;
            var walls = new[] {
                new Bounds(new Vector3(10, 1.5f, -35), new Vector3(3, 3, 28)),
                new Bounds(new Vector3(27, 1.5f, 20), new Vector3(3, 3, 34)),
                new Bounds(new Vector3(47, 1.5f, -15), new Vector3(4, 3, 32)),
                new Bounds(new Vector3(68, 1.5f, 39), new Vector3(3, 3, 30)),
                new Bounds(new Vector3(85, 1.5f, -42), new Vector3(4, 3, 26))
                ,new Bounds(new Vector3(36, .2f, -53), new Vector3(22, .4f, 16))
                ,new Bounds(new Vector3(64, .2f, 8), new Vector3(24, .4f, 14))
                ,new Bounds(new Vector3(-62, 1.5f, 0), new Vector3(3, 3, 22))
                ,new Bounds(new Vector3(-40, 1.5f, 14), new Vector3(3, 3, 20))
                ,new Bounds(new Vector3(-40, 1.5f, -18), new Vector3(3, 3, 16))
            };
            for (int i = 0; i < SOLDIERS; i++)
                positions[i] = playable ? new Vector3(-90 + i % 20 * .9f, 0, -8.55f + i / 20 * .9f)
                    : new Vector3(-15 - i / 100 * .85f, 0, -79.2f + i % 100 * 1.6f);
            int filled = SOLDIERS;
            for (int slot = 0; filled < TOTAL; slot++)
            {
                Vector3 point = new Vector3(-8.1f + slot % 100 * 1.16f, 0, -66 + slot / 100 * 1.16f);
                if (point.z > 120) throw new InvalidOperationException("Stress spawn capacity exceeded");
                bool blocked = false;
                foreach (var wall in walls)
                {
                    Bounds expanded = wall; expanded.Expand(1.2f); point.y = wall.center.y;
                    if (expanded.Contains(point)) blocked = true;
                }
                point.y = 0;
                if (!blocked) positions[filled++] = point;
            }
            crowd = new NavMeshCrowd(TOTAL, true, positions, walls, false, UnitBalance.runner);
            for (int i = 0; i < TOTAL; i++)
            {
                hearing_due[i] = fuse[i] = float.PositiveInfinity;
                path_target[i] = -1;
                exploder[i] = i >= SOLDIERS && (i - SOLDIERS) % 10 == 0;
                health[i] = stats_for(i).health;
                crowd.agents[i].speed = stats_for(i).move_speed;
                crowd.agents[i].acceleration = stats_for(i).acceleration;
                if (i < SOLDIERS)
                {
                    crowd.agents[i].enabled = true; crowd.agents[i].isStopped = true;
                    crowd.agents[i].angularSpeed = 1440;
                    order_targets[i] = -1; last_soldier_goal[i] = Vector3.positiveInfinity;
                }
            }
            refresh_positions(); rebuild_grids();
        }

        public bool prepare_assault_path(int index)
        {
            int target = lane_targets[lane(positions[index].z)];
            path_target[index] = target;
            bool ready = crowd.investigate_position(index, positions[target]);
            crowd.agents[index].isStopped = true;
            if (!ready) path_failures++;
            return ready;
        }

        public void release_assault()
        {
            for (int i = SOLDIERS; i < TOTAL; i++)
            { activated[i] = true; crowd.agents[i].isStopped = false; }
            ever_active = ZOMBIES;
        }

        public void step(float now, float delta)
        {
            refresh_positions();
            if (now >= next_tick)
            {
                next_tick = now + .1f; // No unlimited catch-up loop after a slow frame.
                rebuild_grids(); update_soldiers(now); update_zombies(now);
            }
            process_paths(); update_projectiles(now, delta);
            peak_active = Math.Max(peak_active, active_now); peak_moving = Math.Max(peak_moving, moving_now);
        }

        private void refresh_positions()
        {
            active_now = moving_now = pending = 0;
            for (int i = 0; i < TOTAL; i++)
            {
                if (health[i] <= 0) continue;
                positions[i] = crowd.transforms[i].position;
                if (i < SOLDIERS && crowd.agents[i].velocity.sqrMagnitude > .01f)
                    soldier_facing[i] = crowd.agents[i].velocity;
                if (i < SOLDIERS || !activated[i]) continue;
                active_now++;
                var agent = crowd.agents[i];
                if (agent.pathPending) pending++;
                if (agent.velocity.sqrMagnitude > .01f) moving_now++;
                if (bad_geometry[i]) continue;
                Vector3 p = positions[i];
                bool invalid = float.IsNaN(p.x) || Mathf.Abs(p.x) > 128 || Mathf.Abs(p.z) > 128;
                foreach (var wall in crowd.walls)
                    if (p.x > wall.min.x && p.x < wall.max.x && p.z > wall.min.z && p.z < wall.max.z) invalid = true;
                if (invalid) { bad_geometry[i] = true; geometry_errors++; }
            }
        }

        private static int lane(float z) => Mathf.Clamp(Mathf.RoundToInt((z + 79.2f) / 1.6f), 0, 99);

        private void rebuild_grids()
        {
            zombie_grid.clear(); soldier_grid.clear();
            for (int i = 0; i < TOTAL; i++)
                if (health[i] > 0) (i < SOLDIERS ? soldier_grid : zombie_grid).insert(i, positions[i]);
            for (int row = 0; row < 100; row++)
            {
                int best = -1; float score = float.PositiveInfinity;
                for (int i = 0; i < SOLDIERS; i++)
                {
                    float candidate = Mathf.Abs(row - i % 100) * 2 + i / 100 * .1f;
                    if (health[i] > 0 && candidate < score) { score = candidate; best = i; }
                }
                lane_targets[row] = best;
            }
        }

        private int nearest(CombatSpatialGrid grid, Vector3 origin, float radius)
        {
            int best = -1; float squared = radius * radius;
            for (int z = CombatSpatialGrid.cell(origin.z - radius); z <= CombatSpatialGrid.cell(origin.z + radius); z++)
                for (int x = CombatSpatialGrid.cell(origin.x - radius); x <= CombatSpatialGrid.cell(origin.x + radius); x++)
                    for (int i = grid.heads[x + z * CombatSpatialGrid.SIDE]; i >= 0; i = grid.next[i])
                    {
                        if (health[i] <= 0) continue;
                        float length = (positions[i] - origin).sqrMagnitude;
                        if (length <= squared && visible(origin, positions[i])) { squared = length; best = i; }
                    }
            return best;
        }

        private bool visible(Vector3 from, Vector3 to)
        {
            from.y = to.y = 1;
            Vector3 delta = to - from; float distance = delta.magnitude;
            if (distance < .001f) return true;
            var ray = new Ray(from, delta / distance);
            foreach (var wall in crowd.walls) if (wall.IntersectRay(ray, out float hit) && hit <= distance) return false;
            return true;
        }

        private void update_soldiers(float now)
        {
            for (int i = 0; i < SOLDIERS; i++)
            {
                if (health[i] <= 0) continue;
                int target = playable ? update_player_order(i, now) : nearest(zombie_grid, positions[i], UnitBalance.human.attack_range);
                if (target >= 0) soldier_facing[i] = positions[target] - positions[i];
                if (target < 0 || now < next_attack[i]) continue;
                next_attack[i] = now + UnitBalance.human.attack_interval; shots++; last_combat_time = now;
                bool allocated = false;
                for (int attempt = 0; attempt < projectiles.Length; attempt++)
                {
                    int slot = shot_cursor++ % projectiles.Length;
                    if (projectiles[slot].active) continue;
                    projectiles[slot] = new Shot { active = true, position = positions[i] + Vector3.up, target = target };
                    allocated = true; break;
                }
                if (!allocated) dropped_projectiles++;
                emit_gun_noise(positions[i], now);
            }
        }

        private void emit_gun_noise(Vector3 origin, float now)
        {
            float radius = UnitBalance.human_noise(UnitBalance.human);
            for (int z = CombatSpatialGrid.cell(origin.z - radius); z <= CombatSpatialGrid.cell(origin.z + radius); z++)
                for (int x = CombatSpatialGrid.cell(origin.x - radius); x <= CombatSpatialGrid.cell(origin.x + radius); x++)
                    for (int i = zombie_grid.heads[x + z * CombatSpatialGrid.SIDE]; i >= 0; i = zombie_grid.next[i])
                    {
                        if (health[i] <= 0 || activated[i]) continue;
                        Vector3 offset = positions[i] - origin; offset.y = 0;
                        float distance = offset.magnitude;
                        if (distance > radius) continue;
                        float due = now + distance / UnitBalance.config.noise_propagation_speed;
                        if (due < hearing_due[i]) { hearing_due[i] = due; memories[i] = origin; }
                    }
        }

        private void update_zombies(float now)
        {
            for (int i = SOLDIERS; i < TOTAL; i++)
            {
                if (health[i] <= 0) continue;
                if (fuse[i] < float.PositiveInfinity)
                { if (now >= fuse[i]) damage(i, health[i], now); continue; }
                int target = nearest(soldier_grid, positions[i], UnitBalance.config.zombie_sight);
                if (!activated[i] && (target >= 0 || now >= hearing_due[i]))
                {
                    activated[i] = true; ever_active++;
                    if (target < 0) heard++;
                    needs_path[i] = true;
                }
                if (!activated[i]) continue;
                if (target >= 0)
                {
                    float distance = (positions[i] - positions[target]).magnitude;
                    if (distance <= stats_for(i).attack_range)
                    {
                        if (crowd.agents[i].enabled) crowd.agents[i].isStopped = true;
                        if (exploder[i]) fuse[i] = now + UnitBalance.exploder.fuse_seconds;
                        else if (now >= next_attack[i])
                        { next_attack[i] = now + stats_for(i).attack_interval; bites++; last_combat_time = now; damage(target, stats_for(i).damage, now); }
                        continue;
                    }
                }
                else if (assault) target = lane_targets[lane(positions[i].z)];
                crowd.agents[i].autoBraking = target < 0;
                bool target_moved = playable && target >= 0 && (positions[target] - memories[i]).sqrMagnitude > .04f
                    && now >= zombie_repath_at[i] && (!crowd.agents[i].enabled || !crowd.agents[i].pathPending);
                if (target >= 0 && (path_target[i] != target || target_moved))
                {
                    path_target[i] = target; memories[i] = positions[target]; needs_path[i] = true;
                    zombie_repath_at[i] = now + UnitBalance.config.chase_repath_seconds;
                }
                if (target < 0 && !assault && (positions[i] - memories[i]).sqrMagnitude < .8f * .8f)
                { if (crowd.agents[i].enabled) crowd.agents[i].isStopped = true; }
                else if (crowd.agents[i].enabled && !needs_path[i]) crowd.agents[i].isStopped = false;
            }
        }

        private void process_paths()
        {
            int issued = 0;
            for (int scanned = 0; scanned < ZOMBIES && issued < 64; scanned++)
            {
                int i = path_cursor++;
                if (path_cursor >= TOTAL) path_cursor = SOLDIERS;
                if (!needs_path[i] || health[i] <= 0) continue;
                needs_path[i] = false; issued++;
                var agent = crowd.agents[i]; agent.enabled = true;
                if (!agent.isOnNavMesh || !agent.SetDestination(memories[i])) { path_failures++; continue; }
                agent.stoppingDistance = .75f; agent.isStopped = false;
            }
        }

        private void update_projectiles(float now, float delta)
        {
            for (int i = 0; i < projectiles.Length; i++)
            {
                if (!projectiles[i].active) continue;
                Shot shot = projectiles[i];
                if (health[shot.target] <= 0) { projectiles[i].active = false; continue; }
                Vector3 next = Vector3.MoveTowards(shot.position, positions[shot.target] + Vector3.up, UnitBalance.human.projectile_speed * delta);
                if (!visible(shot.position, next)) { projectiles[i].active = false; continue; }
                if ((next - positions[shot.target] - Vector3.up).sqrMagnitude <= .04f)
                { hits++; last_combat_time = now; damage(shot.target, UnitBalance.human.damage, now); projectiles[i].active = false; }
                else { shot.position = next; projectiles[i] = shot; }
            }
        }

        private void damage(int victim, float amount, float now)
        {
            if (health[victim] <= 0) return;
            health[victim] = Mathf.Max(0, health[victim] - amount);
            if (health[victim] > 0) return;
            crowd.agents[victim].enabled = false;
            if (victim < SOLDIERS) { dead_soldiers++; return; }
            dead_zombies++;
            if (!exploder[victim]) return;
            blasts++; last_combat_time = now;
            flashes[flash_cursor++ % flashes.Length] = new Flash { origin = positions[victim], expires = now + .85f };
            // Explosions NEVER call emit_gun_noise; only friendly victims take AOE damage.
            for (int i = 0; i < SOLDIERS; i++)
                if (health[i] > 0 && (positions[i] - positions[victim]).sqrMagnitude <= UnitBalance.exploder.explosion_radius * UnitBalance.exploder.explosion_radius && visible(positions[victim], positions[i]))
                    damage(i, UnitBalance.exploder.damage, now);
        }

        public UnitStats stats_for(int index) => index < SOLDIERS ? UnitBalance.human : exploder[index] ? UnitBalance.exploder : UnitBalance.runner;

        public void Dispose() { crowd.Dispose(); }
    }
}
