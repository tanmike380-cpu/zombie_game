using UnityEngine;
using ZombieGame.Balance;
namespace ZombieGame.Combat
{
    public partial class BattleSimulation
    {
        /// <summary>Current navigation intent for regression diagnostics.</summary>
        public Vector3 zombie_navigation_goal(int index)=>memories[index];
        private void update_zombies(float now)
        {
            for (int i = soldier_count; i < total_count; i++)
            {
                if (health[i] <= 0) continue;
                if (fuse[i] < float.PositiveInfinity)
                { if (now >= fuse[i]) damage(i, health[i], now); continue; }
                int target = nearest(soldier_grid, positions[i], UnitBalance.config.zombie_sight);
                bool new_sound = noise.try_hear(i, out var signal) && sound_memories[i].accept(signal);
                if(target<0&&sound_memories[i].pending&&(positions[i]-sound_memories[i].origin).sqrMagnitude<.64f)
                    sound_memories[i].finish_investigation();
                bool investigating_sound=sound_memories[i].pending;
                bool siege=has_siege_order(i);
                if(siege&&target<0&&siege_headquarters!=null&&siege_headquarters.health<=0)
                {
                    needs_path[i]=false;path_target[i]=-1;
                    if(crowd.agents[i].enabled&&crowd.agents[i].isOnNavMesh)crowd.agents[i].isStopped=true;
                    continue;
                }
                if(target<0&&(siege||(!new_sound&&!investigating_sound))&&try_attack_building(i,now))continue;
                if(building_targets!=null&&(target>=0||new_sound&&!siege))building_targets[i]=-1;
                if (investigating_sound && target < 0 && !assault && !siege)
                {
                    Vector3 sound_origin=sound_memories[i].origin;
                    bool changed_goal = !activated[i] || path_target[i] >= 0 || (memories[i] - sound_origin).sqrMagnitude > .01f;
                    memories[i] = sound_origin; path_target[i] = -1;
                    if (changed_goal)
                    {
                        noise_redirects++; needs_path[i] = true;
                        var agent = crowd.agents[i];
                        if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
                    }
                }
                if (!activated[i] && (target >= 0 || new_sound))
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
                        memories[i]=positions[target];path_target[i]=target;
                        needs_path[i]=false;
                        if (crowd.agents[i].enabled) crowd.agents[i].isStopped = true;
                        if (exploder[i]) fuse[i] = now + UnitBalance.exploder.fuse_seconds;
                        else if (now >= next_attack[i])
                        { attack_started_at[i] = now; next_attack[i] = now + stats_for(i).attack_interval; bites++; last_combat_time = now; execute_zombie_attack(i,target,now); }
                        continue;
                    }
                }
                else if (assault) target = forced_target?.Invoke(i) ?? -1;
                else if(path_target[i]>=0)
                {path_target[i]=-1;needs_path[i]=true;if(siege)memories[i]=siege_goal;}
                if(target<0&&siege&&(memories[i]-siege_goal).sqrMagnitude>.01f)
                {memories[i]=siege_goal;needs_path[i]=true;}
                crowd.agents[i].autoBraking = target < 0;
                bool target_moved = playable && target >= 0 && (positions[target] - memories[i]).sqrMagnitude > .04f
                    && now >= zombie_repath_at[i] && (!crowd.agents[i].enabled || !crowd.agents[i].pathPending);
                if (target >= 0 && (path_target[i] != target || target_moved))
                {
                    path_target[i] = target; memories[i] = positions[target]; needs_path[i] = true;
                    zombie_repath_at[i] = now + UnitBalance.config.chase_repath_seconds;
                }
                if (target < 0 && !assault && (positions[i] - memories[i]).sqrMagnitude < .8f * .8f)
                { needs_path[i]=false;if (crowd.agents[i].enabled) crowd.agents[i].isStopped = true; }
                else if (crowd.agents[i].enabled && !needs_path[i]) crowd.agents[i].isStopped = false;
            }
        }

    }
}
