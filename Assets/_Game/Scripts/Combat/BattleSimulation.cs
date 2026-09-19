using System;
using ZombieGame.Balance;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Navigation;
using ZombieGame.Noise;
using ZombieGame.AI;

namespace ZombieGame.Combat
{
    /// <summary>Production combat, orders, perception and native crowd navigation.</summary>
    public partial class BattleSimulation : IDisposable
    {
        public readonly int soldier_count, zombie_count, total_count;
        public Func<int, int> forced_target;
        public readonly Vector3[] positions;
        public readonly float[] health;
        public readonly bool[] activated;
        public readonly bool[] exploder;
        public readonly NativeNavMeshCrowd crowd;
        public readonly bool assault;
        public readonly bool playable;
        public readonly Vector3[] soldier_facing;
        public int shots, hits, bites, blasts, heard, dead_soldiers, dead_zombies, ever_active;
        public int active_now, pending, path_failures, dropped_projectiles, moving_now;
        public int peak_active, peak_moving, geometry_errors;
        public float last_combat_time = -10;
        public readonly Shot[] projectiles = new Shot[2048];
        public readonly Flash[] flashes = new Flash[256];
        public struct Shot { public bool active; public Vector3 position; public int target; }
        public struct Flash { public Vector3 origin; public float expires; }
        private readonly CombatSpatialGrid zombie_grid;
        private readonly CombatSpatialGrid soldier_grid;
        private readonly float[] next_attack, fuse;
        private readonly NoiseTimeline noise;
        private readonly SoundMemory[] sound_memories;
        public int noise_redirects;
        private readonly Vector3[] memories;
        private readonly bool[] needs_path, bad_geometry;
        private readonly int[] path_target;
        private int path_cursor, shot_cursor, flash_cursor;
        private float next_tick;

        public BattleSimulation(Vector3[] spawn_positions, int human_count, bool[] explosive_units,
            Bounds[] obstacles, bool global_assault = false, bool player_controlled = true)
        {
            if (spawn_positions == null || explosive_units == null || obstacles == null ||
                human_count < 1 || human_count > spawn_positions.Length || explosive_units.Length != spawn_positions.Length)
                throw new ArgumentException("Battle requires valid spawns, human count, explosive flags and obstacles");
            soldier_count = human_count; total_count = spawn_positions.Length; zombie_count = total_count - soldier_count;
            path_cursor = soldier_count;
            positions = new Vector3[total_count];
            health = new float[total_count];
            activated = new bool[total_count];
            exploder = new bool[total_count];
            soldier_facing = new Vector3[soldier_count];
            next_attack = new float[total_count];
            noise = new NoiseTimeline(total_count, UnitBalance.human_noise(UnitBalance.human));
            sound_memories = new SoundMemory[total_count];
            fuse = new float[total_count];
            memories = new Vector3[total_count];
            needs_path = new bool[total_count];
            bad_geometry = new bool[total_count];
            path_target = new int[total_count];
            zombie_grid = new CombatSpatialGrid(total_count);
            soldier_grid = new CombatSpatialGrid(total_count);
            selected = new bool[soldier_count];
            orders = new SoldierOrder[soldier_count];
            order_targets = new int[soldier_count];
            order_goals = new Vector3[soldier_count];
            patrol_starts = new Vector3[soldier_count];
            patrol_ends = new Vector3[soldier_count];
            last_soldier_goal = new Vector3[soldier_count];
            soldier_repath_at = new float[soldier_count];
            zombie_repath_at = new float[total_count];
            Array.Copy(spawn_positions, positions, total_count);
            Array.Copy(explosive_units, exploder, total_count);
            assault = global_assault; playable = player_controlled;
            crowd = new NativeNavMeshCrowd(positions, obstacles, UnitBalance.runner);
            for (int i = 0; i < total_count; i++)
            {
                fuse[i] = float.PositiveInfinity;
                path_target[i] = -1;
                health[i] = stats_for(i).health;
                crowd.agents[i].speed = stats_for(i).move_speed;
                crowd.agents[i].acceleration = stats_for(i).acceleration;
                if (i < soldier_count)
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
            int target = forced_target?.Invoke(index) ?? -1;
            if (target < 0 || target >= soldier_count) return false;
            path_target[index] = target;
            bool ready = crowd.investigate_position(index, positions[target]);
            crowd.agents[index].isStopped = true;
            if (!ready) path_failures++;
            return ready;
        }

        public void release_assault()
        {
            for (int i = soldier_count; i < total_count; i++)
            { activated[i] = true; crowd.agents[i].isStopped = false; }
            ever_active = zombie_count;
        }

        public void step(float now, float delta)
        {
            refresh_positions();
            if (now >= next_tick)
            {
                next_tick = now + .1f; // No unlimited catch-up loop after a slow frame.
                rebuild_grids(); update_soldiers(now); noise.advance(now); update_zombies(now);
            }
            process_paths(); update_projectiles(now, delta);
            peak_active = Math.Max(peak_active, active_now); peak_moving = Math.Max(peak_moving, moving_now);
        }

        private void refresh_positions()
        {
            active_now = moving_now = pending = 0;
            for (int i = 0; i < total_count; i++)
            {
                if (health[i] <= 0) continue;
                positions[i] = crowd.transforms[i].position;
                if (i < soldier_count && crowd.agents[i].velocity.sqrMagnitude > .01f)
                    soldier_facing[i] = crowd.agents[i].velocity;
                if (i < soldier_count || !activated[i]) continue;
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

        private void rebuild_grids()
        {
            zombie_grid.clear(); soldier_grid.clear();
            for (int i = 0; i < total_count; i++)
                if (health[i] > 0) (i < soldier_count ? soldier_grid : zombie_grid).insert(i, positions[i]);
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
            for (int i = 0; i < soldier_count; i++)
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

        public void emit_gun_noise(Vector3 origin, float now)
        {
            float radius = UnitBalance.human_noise(UnitBalance.human);
            NoiseSignal signal = noise.create_signal(origin, radius, now);
            for (int z = CombatSpatialGrid.cell(origin.z - radius); z <= CombatSpatialGrid.cell(origin.z + radius); z++)
                for (int x = CombatSpatialGrid.cell(origin.x - radius); x <= CombatSpatialGrid.cell(origin.x + radius); x++)
                    for (int i = zombie_grid.heads[x + z * CombatSpatialGrid.SIDE]; i >= 0; i = zombie_grid.next[i])
                    {
                        if (health[i] <= 0) continue;
                        noise.queue_listener(i, signal, positions[i]);
                    }
        }


        private void process_paths()
        {
            int issued = 0;
            for (int scanned = 0; scanned < zombie_count && issued < 64; scanned++)
            {
                int i = path_cursor++;
                if (path_cursor >= total_count) path_cursor = soldier_count;
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
            if (victim < soldier_count) { dead_soldiers++; return; }
            dead_zombies++;
            if (!exploder[victim]) return;
            blasts++; last_combat_time = now;
            flashes[flash_cursor++ % flashes.Length] = new Flash { origin = positions[victim], expires = now + .85f };
            // Explosions NEVER call emit_gun_noise; only friendly victims take AOE damage.
            for (int i = 0; i < soldier_count; i++)
                if (health[i] > 0 && (positions[i] - positions[victim]).sqrMagnitude <= UnitBalance.exploder.explosion_radius * UnitBalance.exploder.explosion_radius && visible(positions[victim], positions[i]))
                    damage(i, UnitBalance.exploder.damage, now);
        }

        public UnitStats stats_for(int index) => index < soldier_count ? UnitBalance.human : exploder[index] ? UnitBalance.exploder : UnitBalance.runner;

        public void Dispose() { crowd.Dispose(); }
    }
}
