using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Balance;

namespace ZombieGame.Combat
{
    public sealed class BattleBuilding
    {
        public Bounds bounds;
        public string label;
        public bool headquarters,infected;
        public float health,max_health;
        public int infection_remaining,spawned;
    }
    public partial class BattleSimulation
    {
        public readonly List<BattleBuilding> buildings=new List<BattleBuilding>();
        public Action<BattleBuilding> building_infected;
        private int[] building_targets;
        public BattleBuilding add_building_target(Bounds bounds,string label,bool headquarters=false)
        {
            var stats=UnitBalance.config.buildings;
            var building=new BattleBuilding{bounds=bounds,label=label,headquarters=headquarters,
                health=headquarters?stats.headquarters_health:stats.normal_health,max_health=headquarters?stats.headquarters_health:stats.normal_health};
            buildings.Add(building);return building;
        }
        private bool building_visible(Vector3 from,BattleBuilding building,Vector3 to)
        {
            from.y=to.y=1;Vector3 delta=to-from;float length=delta.magnitude;
            if(length<.001f)return true;
            var ray=new Ray(from,delta/length);
            foreach(var wall in crowd.walls)
                if(wall!=building.bounds&&wall.IntersectRay(ray,out float hit)&&hit<length-.05f)return false;
            return true;
        }
        private bool try_attack_building(int index,float now)
        {
            if(buildings.Count==0)return false;
            if(building_targets==null){building_targets=new int[total_count];Array.Fill(building_targets,-1);}
            var stats=stats_for(index);int best=-1;float distance=float.PositiveInfinity;Vector3 point=Vector3.zero;
            for(int b=0;b<buildings.Count;b++)
            {
                var building=buildings[b];if(building.health<=0)continue;
                Vector3 edge=building.bounds.ClosestPoint(positions[index]+Vector3.up);edge.y=0;
                float length=Vector3.Distance(positions[index],edge);
                if(length>=distance||(!has_siege_order(index)&&length>UnitBalance.config.zombie_sight))continue;
                if(!has_siege_order(index)&&!building_visible(positions[index],building,edge))continue;
                best=b;distance=length;point=edge;
            }
            if(best<0){building_targets[index]=-1;return false;}
            if(!activated[index]){activated[index]=true;ever_active++;}
            var agent=crowd.agents[index];var target=buildings[best];
            if(distance<=stats.attack_range&&building_visible(positions[index],target,point))
            {
                if(!agent.enabled)agent.enabled=true;
                if(agent.isOnNavMesh)agent.isStopped=true;
                needs_path[index]=false;attack_started_at[index]=now<next_attack[index]?attack_started_at[index]:now;
                if(exploder[index]){fuse[index]=now+stats.fuse_seconds;return true;}
                if(now>=next_attack[index])
                {
                    next_attack[index]=now+stats.attack_interval;last_combat_time=now;
                    damage_building(target,stats.damage,now);
                    add_enemy_impact(point,Mathf.Max(.5f,stats.splash_radius),now,stats.id=="spitter");
                }
                return true;
            }
            Vector3 outward=positions[index]-point;outward.y=0;
            if(outward.sqrMagnitude<.01f)outward=Vector3.forward;
            Vector3 destination=point+outward.normalized*(UnitBalance.navigation_radius(stats)+.25f);
            if(building_targets[index]!=best||(memories[index]-destination).sqrMagnitude>1||
                (!needs_path[index]&&agent.enabled&&!agent.pathPending&&!agent.hasPath&&now>=zombie_repath_at[index]))
            {
                var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=NavMesh.AllAreas};
                if(NavMesh.SamplePosition(destination,out var hit,3,filter))destination=hit.position;
                building_targets[index]=best;memories[index]=destination;path_target[index]=-1;needs_path[index]=true;
                zombie_repath_at[index]=now+1;
            }
            return true;
        }
        public void damage_building(BattleBuilding building,float amount,float now)
        {
            if(float.IsNaN(amount)||float.IsInfinity(amount))throw new ArgumentException("Building damage must be finite");
            if(building==null||building.health<=0||amount<=0)return;
            report_attack(building.bounds.center,now);
            building.health=Mathf.Max(0,building.health-amount);
            if(building.health>0)return;
            building.infected=true;
            building.infection_remaining=building.headquarters?UnitBalance.config.buildings.headquarters_infection_count:UnitBalance.config.buildings.normal_infection_count;
            building_infected?.Invoke(building);
        }
        private void damage_buildings_in_radius(Vector3 origin,float radius,float amount,float now)
        {
            foreach(var building in buildings)
            {
                Vector3 edge=building.bounds.ClosestPoint(origin+Vector3.up);edge.y=origin.y;
                if(building.health>0&&(edge-origin).sqrMagnitude<=radius*radius&&building_visible(origin,building,edge))damage_building(building,amount,now);
            }
        }
        private void update_building_infection()
        {
            int budget=16;
            foreach(var building in buildings)
            {
                if(building.infection_remaining<=0)continue;
                for(int attempt=0;attempt<80&&building.infection_remaining>0&&budget>0;attempt++)
                {
                    float angle=(building.spawned*2.39996f+attempt*.618f);
                    float radius=Mathf.Max(building.bounds.extents.x,building.bounds.extents.z)+1+attempt/16;
                    Vector3 point=building.bounds.center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);point.y=0;
                    if(!try_spawn_infection(point))continue;
                    building.spawned++;building.infection_remaining--;budget--;
                }
            }
        }
        private bool try_spawn_infection(Vector3 point)
        {
            var filter=new NavMeshQueryFilter{agentTypeID=crowd.agents[0].agentTypeID,areaMask=NavMesh.AllAreas};
            if(!NavMesh.SamplePosition(point,out var hit,.7f,filter))return false;
            for(int i=0;i<total_count;i++)if(health[i]>0&&(positions[i]-hit.position).sqrMagnitude<.8f*.8f)return false;
            for(int i=soldier_count;i<total_count;i++)
            {
                if(zombie_deployed[i-soldier_count])continue;
                var agent=crowd.agents[i];crowd.transforms[i].position=hit.position;agent.enabled=true;
                if(!agent.isOnNavMesh){agent.enabled=false;return false;}
                positions[i]=hit.position;health[i]=stats_for(i).health;activated[i]=true;
                zombie_deployed[i-soldier_count]=true;zombie_count++;infection_spawned++;ever_active++;
                memories[i]=hit.position;needs_path[i]=false;agent.isStopped=true;
                return true;
            }
            return false;
        }
    }
}
