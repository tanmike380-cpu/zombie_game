using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.Combat
{
    public sealed class FlameBurst
    {
        public Vector3 origin,direction;
        public UnitStats stats;
        public float expires;
        public int source;
        public BattleBuilding building;
    }
    public sealed class DefenseProjectile
    {
        public Vector3 position,destination;
        public UnitStats stats;
    }
    public partial class BattleSimulation
    {
        public readonly List<FlameBurst> flame_bursts=new List<FlameBurst>();
        public readonly List<DefenseProjectile> defense_projectiles=new List<DefenseProjectile>();
        public Func<UnitStats,bool> try_supply_defense_shot;
        public int flame_hits,defense_shots,greek_fire_pulses;

        public static bool inside_flame(Vector3 origin,Vector3 direction,Vector3 point,UnitStats stats)
        {
            Vector3 offset=point-origin;offset.y=0;direction.y=0;
            if(offset.sqrMagnitude>stats.attack_range*stats.attack_range)return false;
            return offset.sqrMagnitude<.001f||Vector3.Dot(offset.normalized,direction.normalized)>=Mathf.Cos(stats.cone_angle*.5f*Mathf.Deg2Rad);
        }
        private void fire_flame(Vector3 origin,Vector3 direction,UnitStats stats,float now,int source=-1,BattleBuilding building=null)
        {
            direction.y=0;direction.Normalize();
            if(source>=0)greek_fire_pulses++;
            var burst=flame_bursts.Find(item=>item.source==source&&item.building==building);
            if(burst==null){burst=new FlameBurst();flame_bursts.Add(burst);}
            burst.origin=origin;burst.direction=direction;burst.stats=stats;burst.expires=now+stats.attack_interval+.1f;burst.source=source;burst.building=building;
            for(int i=soldier_count;i<total_count;i++)
                if(health[i]>0&&inside_flame(origin,direction,positions[i],stats)&&ranged_line_clear(origin,positions[i]))
                {damage(i,stats.damage,now);flame_hits++;last_combat_time=now;}
        }
        private void update_defense_weapons(float now)
        {
            foreach(var building in buildings)
            {
                if(building.health<=0||building.infected||string.IsNullOrEmpty(building.weapon_id)||now<building.next_attack)continue;
                var stats=UnitBalance.get(building.weapon_id);
                Vector3 origin=building.bounds.center;origin.y=0;
                int target=nearest(zombie_grid,origin,stats.attack_range,true);
                if(target<0)continue;
                if(try_supply_defense_shot!=null&&!try_supply_defense_shot(stats))continue;
                building.next_attack=now+stats.attack_interval;defense_shots++;
                if(stats.cone_angle>0)fire_flame(origin,positions[target]-origin,stats,now,-1,building);
                else defense_projectiles.Add(new DefenseProjectile{position=origin+Vector3.up,destination=positions[target]+Vector3.up,stats=stats});
                emit_gun_noise(origin,now,stats);
            }
        }
        private void update_defense_projectiles(float now,float delta)
        {
            flame_bursts.RemoveAll(item=>now>=item.expires||item.source>=0&&health[item.source]<=0||item.building!=null&&item.building.health<=0);
            for(int i=defense_projectiles.Count-1;i>=0;i--)
            {
                var shot=defense_projectiles[i];
                Vector3 next=Vector3.MoveTowards(shot.position,shot.destination,shot.stats.projectile_speed*delta);
                if(!ranged_line_clear(shot.position,next)){defense_projectiles.RemoveAt(i);continue;}
                shot.position=next;
                if((next-shot.destination).sqrMagnitude>.001f)continue;
                Vector3 impact=shot.destination;impact.y=0;
                for(int enemy=soldier_count;enemy<total_count;enemy++)
                    if(health[enemy]>0&&(positions[enemy]-impact).sqrMagnitude<=shot.stats.splash_radius*shot.stats.splash_radius&&ranged_line_clear(impact,positions[enemy]))damage(enemy,shot.stats.damage,now);
                add_enemy_impact(impact,shot.stats.splash_radius,now,false);defense_projectiles.RemoveAt(i);
            }
        }
    }
}
