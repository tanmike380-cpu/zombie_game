using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.Combat
{
    public partial class BattleSimulation
    {
        private bool[] queue_held;
        private float[] queue_until,queue_stalled_since;
        private float next_queue_probe;
        public int queued_zombies {get;private set;}
        public int queue_releases {get;private set;}
        public bool is_zombie_queued(int index)=>queue_held!=null&&queue_held[index];
        private bool has_local_attack(int index)
        {
            int target=path_target[index];
            if(target>=0&&health[target]>0&&Vector3.Distance(positions[index],positions[target])<=zombie_attack_range(index,target))return true;
            int building=building_targets==null?-1:building_targets[index];
            if(building<0||buildings[building].health<=0)return false;
            Vector3 edge=buildings[building].bounds.ClosestPoint(positions[index]+Vector3.up);edge.y=0;
            return Vector3.Distance(positions[index],edge)<=building_attack_range(index);
        }
        /// <summary>Pause only stalled rear ranks; keep native routes and recheck vacancies, never merge units.</summary>
        private void update_zombie_queues(float now)
        {
            queue_held??=new bool[total_count];queue_until??=new float[total_count];queue_stalled_since??=new float[total_count];
            bool probe=now>=next_queue_probe;if(probe)next_queue_probe=now+UnitBalance.config.zombie_queue_probe_seconds;
            queued_zombies=0;
            for(int i=soldier_count;i<total_count;i++)
            {
                var agent=crowd.agents[i];
                if(health[i]<=0||!agent.enabled||!agent.isOnNavMesh||!activated[i]){queue_held[i]=false;continue;}
                if(has_local_attack(i)){queue_held[i]=false;queue_stalled_since[i]=0;continue;}
                if(probe)
                {
                    Vector3 direction=agent.hasPath?agent.steeringTarget-positions[i]:memories[i]-positions[i];direction.y=0;direction.Normalize();
                    bool blocked=front_is_queued(i,direction,queue_held[i]);
                    if(queue_held[i]&&now>=queue_until[i]&&!blocked)
                    {queue_held[i]=false;queue_stalled_since[i]=0;agent.isStopped=false;queue_releases++;}
                    else if(!queue_held[i]&&!agent.isStopped&&agent.hasPath&&blocked&&Vector3.Dot(observed_velocity[i],direction)<stats_for(i).move_speed*.12f)
                    {
                        if(queue_stalled_since[i]==0)queue_stalled_since[i]=now;
                        if(now-queue_stalled_since[i]>=UnitBalance.config.zombie_queue_hold_seconds)
                        {queue_held[i]=true;queue_until[i]=now+UnitBalance.config.zombie_queue_hold_seconds;}
                    }
                    else if(!queue_held[i])queue_stalled_since[i]=0;
                }
                if(queue_held[i]){agent.isStopped=true;agent.velocity=Vector3.zero;queued_zombies++;}
            }
        }
        private bool front_is_queued(int index,Vector3 direction,bool holding)
        {
            float radius=UnitBalance.config.zombie_navigation_radius*2+UnitBalance.config.zombie_queue_clearance*(holding?1:.4f);
            Vector3 origin=positions[index];
            for(int z=CombatSpatialGrid.cell(origin.z-radius);z<=CombatSpatialGrid.cell(origin.z+radius);z++)
            for(int x=CombatSpatialGrid.cell(origin.x-radius);x<=CombatSpatialGrid.cell(origin.x+radius);x++)
            for(int other=zombie_grid.heads[x+z*CombatSpatialGrid.SIDE];other>=0;other=zombie_grid.next[other])
            {
                if(other==index||health[other]<=0)continue;
                Vector3 offset=positions[other]-origin;offset.y=0;
                if(offset.sqrMagnitude>radius*radius||Vector3.Dot(offset.normalized,direction)<.55f)continue;
                if(holding||observed_velocity[other].magnitude<stats_for(other).move_speed*.2f)return true;
            }
            return false;
        }
    }
}
