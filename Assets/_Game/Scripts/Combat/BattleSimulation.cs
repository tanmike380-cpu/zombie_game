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
        public readonly int soldier_count, total_count;
        public int zombie_count {get;private set;}
        private readonly bool[] zombie_deployed;
        private readonly UnitStats[] human_stats;
        public int infection_spawned {get;private set;}
        public int reserve_soldiers { get; private set; }
        public int living_soldiers => soldier_count-dead_soldiers-reserve_soldiers;
        private readonly bool[] recruited;
        public bool is_reserve(int index) => index<soldier_count?!recruited[index]:!zombie_deployed[index-soldier_count];
        public Func<int, int> forced_target;
        // Optional economy gate. Benchmarks without an economy retain their existing behaviour.
        public Func<int, bool> try_supply_shot;
        public readonly Vector3[] positions;
        public readonly float[] health;
        public readonly bool[] activated;
        public readonly bool[] exploder;
        private readonly UnitStats[] zombie_stats;
        public readonly int[] ammunition;
        public readonly bool[] manual_melee,last_attack_melee;
        public int melee_strikes;
        public bool uses_melee(int index) => manual_melee[index]||ammunition[index]<stats_for(index).ammunition_cost;
        public float human_range(int index) => uses_melee(index)?stats_for(index).melee_range:stats_for(index).attack_range;
        public readonly NativeNavMeshCrowd crowd;
        public readonly bool assault;
        public readonly bool playable;
        public readonly Vector3[] soldier_facing;
        public readonly float[] attack_started_at;
        public int shots, hits, bites, blasts, heard, dead_soldiers, dead_zombies, ever_active;
        public int active_now, pending, path_failures, dropped_projectiles, moving_now;
        public int peak_active, peak_moving, geometry_errors;
        public float last_combat_time = -10;
        public readonly Shot[] projectiles = new Shot[2048];
        public readonly Flash[] flashes = new Flash[256];
        public struct AttackAlert { public Vector3 position; public float expires; }
        public readonly AttackAlert[] attack_alerts=new AttackAlert[32];
        /// <summary>Presentation-only bounded damage notifications. Nearby attacks merge, not noise or perception.</summary>
        private void report_attack(Vector3 point,float now)
        {
            int slot=0;float earliest=float.PositiveInfinity;
            for(int i=0;i<attack_alerts.Length;i++)
            {
                if(attack_alerts[i].expires>now&&(attack_alerts[i].position-point).sqrMagnitude<100)
                {attack_alerts[i]=new AttackAlert{position=point,expires=now+4};return;}
                if(attack_alerts[i].expires<earliest){slot=i;earliest=attack_alerts[i].expires;}
            }
            attack_alerts[slot]=new AttackAlert{position=point,expires=now+4};
        }
        public struct Shot { public bool active; public Vector3 position; public int target,source; }
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
            Bounds[] obstacles, bool global_assault = false, bool player_controlled = true, int initial_humans = -1, string[] unit_ids = null, int infection_reserve = 0)
        {
            if (spawn_positions == null || explosive_units == null || obstacles == null ||
                human_count < 1 || human_count > spawn_positions.Length || explosive_units.Length != spawn_positions.Length)
                throw new ArgumentException("Battle requires valid spawns, human count, explosive flags and obstacles");
            int initial_total=spawn_positions.Length;
            if(unit_ids!=null&&unit_ids.Length!=initial_total)throw new ArgumentException("Spawn unit ids must match positions");
            if(infection_reserve<0)throw new ArgumentOutOfRangeException(nameof(infection_reserve));
            if(infection_reserve>0)
            {
                Array.Resize(ref spawn_positions,initial_total+infection_reserve);Array.Resize(ref explosive_units,initial_total+infection_reserve);
                if(unit_ids!=null)Array.Resize(ref unit_ids,initial_total+infection_reserve);
                for(int i=initial_total;i<spawn_positions.Length;i++){spawn_positions[i]=spawn_positions[0];if(unit_ids!=null)unit_ids[i]="walker";}
            }
            soldier_count = human_count; total_count = spawn_positions.Length; zombie_count = initial_total - soldier_count;
            zombie_deployed=new bool[total_count-soldier_count];
            human_stats=new UnitStats[soldier_count];
            for(int i=0;i<soldier_count;i++)
            {
                string id=unit_ids==null||string.IsNullOrEmpty(unit_ids[i])?"firearm_infantry":unit_ids[i];
                human_stats[i]=UnitBalance.get(id);
                if(!UnitBalance.is_human(id)||!human_stats[i].implemented)throw new ArgumentException("Unsupported human spawn: "+id);
            }
            if(unit_ids!=null&&unit_ids.Length!=total_count)throw new ArgumentException("Spawn unit ids must match positions");
            zombie_stats=new UnitStats[total_count-soldier_count];
            for(int i=human_count;i<total_count;i++)
            {
                string id=i>=initial_total?"walker":unit_ids==null?(explosive_units[i]?"exploder":"runner"):unit_ids[i];
                zombie_deployed[i-human_count]=i<initial_total;
                var stats=UnitBalance.get(id);
                if(!stats.implemented||!UnitBalance.is_zombie(id)||explosive_units[i]!=(id=="exploder"))
                    throw new ArgumentException("Unsupported or inconsistent zombie spawn role: "+id);
                zombie_stats[i-human_count]=stats;
            }
            if(initial_humans<0)initial_humans=human_count;
            if(initial_humans>human_count)throw new ArgumentException("Initial humans exceed reserved capacity");
            recruited=new bool[human_count];reserve_soldiers=human_count-initial_humans;
            path_cursor = soldier_count;
            positions = new Vector3[total_count];
            health = new float[total_count];
            ammunition=new int[soldier_count];manual_melee=new bool[soldier_count];last_attack_melee=new bool[soldier_count];
            poisoned_until=new float[soldier_count];poison_dps=new float[soldier_count];
            activated = new bool[total_count];
            exploder = new bool[total_count];
            soldier_facing = new Vector3[soldier_count];
            attack_started_at = new float[total_count];
            next_attack = new float[total_count];
            noise = new NoiseTimeline(total_count, UnitBalance.max_human_noise);
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
            route_settled = new bool[soldier_count];
            route_progress_goal = new Vector3[soldier_count];
            route_best_distance = new float[soldier_count];
            route_progress_at = new float[soldier_count];
            zombie_repath_at = new float[total_count];
            Array.Copy(spawn_positions, positions, total_count);
            Array.Copy(explosive_units, exploder, total_count);
            assault = global_assault; playable = player_controlled;
            var navigation_stats=new UnitStats[total_count];
            for(int i=0;i<total_count;i++)navigation_stats[i]=stats_for(i);
            crowd = new NativeNavMeshCrowd(positions, obstacles, UnitBalance.runner,navigation_stats);
            for (int i = 0; i < total_count; i++)
            {
                fuse[i] = float.PositiveInfinity;
                attack_started_at[i] = float.NegativeInfinity;
                path_target[i] = -1;
                health[i] = i>=initial_total?0:stats_for(i).health;
                crowd.agents[i].speed = stats_for(i).move_speed;
                crowd.agents[i].acceleration = stats_for(i).acceleration;
                if (i < soldier_count)
                {
                    crowd.agents[i].enabled = true; crowd.agents[i].isStopped = true;
                    crowd.agents[i].angularSpeed = 1440;
                    order_targets[i] = -1; last_soldier_goal[i] = Vector3.positiveInfinity;
                    recruited[i]=i<initial_humans;
                    ammunition[i]=recruited[i]?stats_for(i).ammunition_capacity:0;
                    if(!recruited[i]){health[i]=0;crowd.agents[i].enabled=false;}
                }
            }
            refresh_positions(); rebuild_grids();
        }

        /// <summary>Activate one never-deployed slot on native navigation; false leaves the queue pending.</summary>
        public bool recruit_soldier(Vector3 point,string unit_id="firearm_infantry")
        {
            var recruit_stats=UnitBalance.get(unit_id);
            if(!UnitBalance.is_human(unit_id)||!recruit_stats.implemented)throw new ArgumentException("Unsupported recruitment role: "+unit_id);
            if(reserve_soldiers==0||!NavMesh.SamplePosition(point,out var hit,1,NavMesh.AllAreas))return false;
            for(int i=0;i<total_count;i++)
                if(health[i]>0&&(positions[i]-hit.position).sqrMagnitude<Mathf.Pow(UnitBalance.config.unit_navigation_radius*2+.05f,2))return false;
            for(int i=0;i<soldier_count;i++)
            {
                if(recruited[i])continue;
                var agent=crowd.agents[i];crowd.transforms[i].position=hit.position;agent.enabled=true;
                if(!agent.isOnNavMesh){agent.enabled=false;return false;}
                human_stats[i]=recruit_stats;agent.speed=recruit_stats.move_speed;agent.acceleration=recruit_stats.acceleration;
                agent.isStopped=true;positions[i]=hit.position;health[i]=recruit_stats.health;ammunition[i]=recruit_stats.ammunition_capacity;
                recruited[i]=true;reserve_soldiers--;return true;
            }
            return false;
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
            process_paths(); update_projectiles(now, delta);update_poison(now,delta);update_enemy_projectiles(now,delta);update_building_infection();
            peak_active = Math.Max(peak_active, active_now); peak_moving = Math.Max(peak_moving, moving_now);
        }

        private void refresh_positions()
        {
            active_now = moving_now = pending = 0;
            for (int i = 0; i < total_count; i++)
            {
                if (health[i] <= 0) continue;
                positions[i] = crowd.transforms[i].position;
                // Ignore tiny avoidance corrections when choosing visual facing; firing still aims at its target.
                if (i < soldier_count && crowd.agents[i].velocity.sqrMagnitude > Mathf.Pow(stats_for(i).move_speed*.2f,2))
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
                int target = playable ? update_player_order(i, now) : nearest(zombie_grid, positions[i], human_range(i));
                if (target >= 0) soldier_facing[i] = positions[target] - positions[i];
                if (target < 0 || now < next_attack[i]) continue;
                if(uses_melee(i))
                {
                    next_attack[i]=now+stats_for(i).melee_attack_interval;attack_started_at[i]=now;
                    last_attack_melee[i]=true;melee_strikes++;last_combat_time=now;
                    damage(target,stats_for(i).melee_damage,now);continue;
                }
                if (try_supply_shot != null && !try_supply_shot(i)) continue;
                ammunition[i]-=stats_for(i).ammunition_cost;last_attack_melee[i]=false;
                next_attack[i] = now + stats_for(i).attack_interval; shots++; last_combat_time = now;
                attack_started_at[i] = now;
                bool allocated = false;
                for (int attempt = 0; attempt < projectiles.Length; attempt++)
                {
                    int slot = shot_cursor++ % projectiles.Length;
                    if (projectiles[slot].active) continue;
                    projectiles[slot] = new Shot { active = true, position = positions[i] + Vector3.up, target = target, source=i };
                    allocated = true; break;
                }
                if (!allocated) dropped_projectiles++;
                emit_gun_noise(positions[i], now, stats_for(i));
            }
        }

        public void emit_gun_noise(Vector3 origin, float now,UnitStats shooter=null)
        {
            float radius = UnitBalance.human_noise(shooter??UnitBalance.human);
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
            for (int scanned = 0; scanned < total_count-soldier_count && issued < 64; scanned++)
            {
                int i = path_cursor++;
                if (path_cursor >= total_count) path_cursor = soldier_count;
                if (!needs_path[i] || health[i] <= 0) continue;
                needs_path[i] = false; issued++;
                var agent = crowd.agents[i]; agent.enabled = true;
                if (!agent.isOnNavMesh || !agent.SetDestination(memories[i])) { path_failures++; continue; }
                agent.stoppingDistance = building_targets!=null&&building_targets[i]>=0
                    ? Mathf.Min(.75f,Mathf.Max(.05f,stats_for(i).attack_range-UnitBalance.navigation_radius(stats_for(i))-.3f)) : .75f;
                agent.isStopped = false;
            }
        }

        private void update_projectiles(float now, float delta)
        {
            for (int i = 0; i < projectiles.Length; i++)
            {
                if (!projectiles[i].active) continue;
                Shot shot = projectiles[i];
                if (health[shot.target] <= 0) { projectiles[i].active = false; continue; }
                Vector3 next = Vector3.MoveTowards(shot.position, positions[shot.target] + Vector3.up, stats_for(shot.source).projectile_speed * delta);
                if (!visible(shot.position, next)) { projectiles[i].active = false; continue; }
                if ((next - positions[shot.target] - Vector3.up).sqrMagnitude <= .04f)
                { hits++; last_combat_time = now; apply_human_projectile(shot,now); projectiles[i].active = false; }
                else { shot.position = next; projectiles[i] = shot; }
            }
        }

        private void damage(int victim, float amount, float now)
        {
            if (health[victim] <= 0) return;
            if(victim<soldier_count&&amount>0)report_attack(positions[victim],now);
            health[victim] = Mathf.Max(0, health[victim] - amount);
            if (health[victim] > 0) return;
            crowd.agents[victim].enabled = false;
            if (victim < soldier_count) { dead_soldiers++; return; }
            dead_zombies++;
            if (!exploder[victim]) return;
            blasts++; last_combat_time = now;
            flashes[flash_cursor++ % flashes.Length] = new Flash { origin = positions[victim], expires = now + .85f };
            // Explosions NEVER call emit_gun_noise; only friendly victims take AOE damage.
            damage_buildings_in_radius(positions[victim],UnitBalance.exploder.explosion_radius,UnitBalance.exploder.damage,now);
            for (int i = 0; i < soldier_count; i++)
                if (health[i] > 0 && (positions[i] - positions[victim]).sqrMagnitude <= UnitBalance.exploder.explosion_radius * UnitBalance.exploder.explosion_radius && visible(positions[victim], positions[i]))
                    damage(i, UnitBalance.exploder.damage, now);
        }

        public UnitStats stats_for(int index) => index < soldier_count ? human_stats[index] : zombie_stats[index-soldier_count];

        public void Dispose() { crowd.Dispose(); }
    }
}
