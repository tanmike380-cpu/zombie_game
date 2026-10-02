using ZombieGame.Combat;
using UnityEngine;

namespace ZombieGame.Presentation
{
    public static class ZombieAnimation
    {
        public static float attack_duration(ZombieGame.Balance.UnitStats stats,bool explosive)
            =>explosive?stats.fuse_seconds:Mathf.Min(stats.id=="giant"||stats.id=="boss"?1.65f:.85f,stats.attack_interval);
        /// <summary>Attack beats tiny avoidance movement; alerted pursuit uses a distinct charge clip.</summary>
        public static CharacterPose choose_pose(BattleSimulation battle,int index,float now)
        {
            var stats=battle.stats_for(index);
            if(now-battle.attack_started_at[index]<attack_duration(stats,battle.exploder[index])) return CharacterPose.Attack;
            var agent=battle.crowd.agents[index];
            if(agent.enabled && !battle.is_zombie_queued(index) && battle.observed_velocity[index].sqrMagnitude>.04f)
                return battle.activated[index]?CharacterPose.Charge:CharacterPose.Run;
            return CharacterPose.Idle;
        }
    }
}
