using UnityEngine;

namespace ZombieGame.Combat
{
    public partial class BattleSimulation
    {
        public struct EnemyShot {public bool active;public int source;public Vector3 position,destination;}
        public struct EnemyImpact {public Vector3 origin;public float radius,expires;public bool acid;}
        public readonly EnemyShot[] enemy_projectiles=new EnemyShot[256];
        public readonly EnemyImpact[] enemy_impacts=new EnemyImpact[128];
        private readonly float[] poisoned_until,poison_dps;
        private int enemy_shot_cursor,enemy_impact_cursor;
        public int acid_shots,slam_attacks;

        /// <summary>All eight roles share perception/navigation; only their authored attack differs.
        /// Spit aims at a position so players can dodge. Giant/boss slams are silent area attacks.</summary>
        private void execute_zombie_attack(int source,int target,float now)
        {
            var stats=stats_for(source);
            if(stats.id=="spitter")
            {
                for(int attempt=0;attempt<enemy_projectiles.Length;attempt++)
                {
                    int slot=enemy_shot_cursor++%enemy_projectiles.Length;if(enemy_projectiles[slot].active)continue;
                    enemy_projectiles[slot]=new EnemyShot{active=true,source=source,position=positions[source]+Vector3.up,destination=positions[target]+Vector3.up*.2f};
                    acid_shots++;return;
                }
                dropped_projectiles++;return;
            }
            damage(target,stats.damage,now);
            if(stats.splash_radius<=0)return;
            slam_attacks++;
            add_enemy_impact(positions[source],stats.splash_radius,now,false);
            for(int i=0;i<soldier_count;i++)
                if(i!=target&&health[i]>0&&(positions[i]-positions[source]).sqrMagnitude<=stats.splash_radius*stats.splash_radius&&visible(positions[source],positions[i]))
                    damage(i,stats.damage,now);
        }
        private void update_enemy_projectiles(float now,float delta)
        {
            for(int i=0;i<enemy_projectiles.Length;i++)
            {
                var shot=enemy_projectiles[i];if(!shot.active)continue;
                var stats=stats_for(shot.source);
                Vector3 next=Vector3.MoveTowards(shot.position,shot.destination,stats.projectile_speed*delta);
                if(!visible(shot.position,next)){enemy_projectiles[i].active=false;continue;}
                if((next-shot.destination).sqrMagnitude>.0001f){shot.position=next;enemy_projectiles[i]=shot;continue;}
                enemy_projectiles[i].active=false;Vector3 origin=shot.destination;origin.y=0;
                add_enemy_impact(origin,stats.splash_radius,now,true);
                for(int target=0;target<soldier_count;target++)
                {
                    if(health[target]<=0||(positions[target]-origin).sqrMagnitude>stats.splash_radius*stats.splash_radius||!visible(origin,positions[target]))continue;
                    damage(target,stats.damage,now);
                    poisoned_until[target]=Mathf.Max(poisoned_until[target],now+stats.poison_duration);
                    poison_dps[target]=Mathf.Max(poison_dps[target],stats.poison_damage_per_second);
                }
            }
        }
        private void update_poison(float now,float delta)
        {
            for(int i=0;i<soldier_count;i++)
            {
                float duration=Mathf.Min(delta,Mathf.Max(0,poisoned_until[i]-(now-delta)));
                if(duration>0&&health[i]>0)damage(i,poison_dps[i]*duration,now);
                if(now>=poisoned_until[i])poison_dps[i]=0;
            }
        }
        private void add_enemy_impact(Vector3 origin,float radius,float now,bool acid)
        {enemy_impacts[enemy_impact_cursor++%enemy_impacts.Length]=new EnemyImpact{origin=origin,radius=radius,expires=now+.8f,acid=acid};}
    }
}
