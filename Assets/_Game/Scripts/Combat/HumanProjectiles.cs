using UnityEngine;

namespace ZombieGame.Combat
{
    public partial class BattleSimulation
    {
        private void apply_human_projectile(Shot shot,float now)
        {
            var stats=stats_for(shot.source);
            float multiplier=stats_for(shot.target).model_scale>=1.5f?Mathf.Max(1,stats.large_target_multiplier):1;
            damage(shot.target,stats.damage*multiplier,now);
            if(stats.splash_radius<=0)return;
            add_enemy_impact(positions[shot.target],stats.splash_radius,now,false);
            for(int i=soldier_count;i<total_count;i++)
                if(i!=shot.target&&health[i]>0&&(positions[i]-positions[shot.target]).sqrMagnitude<=stats.splash_radius*stats.splash_radius&&visible(positions[shot.target],positions[i]))damage(i,stats.damage,now);
        }
    }
}
