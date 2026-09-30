using ZombieGame.Combat;
using UnityEngine;

namespace ZombieGame.Presentation
{
    public static class ZombieAnimation
    {
        /// <summary>Attack beats tiny avoidance movement; alerted pursuit uses a distinct charge clip.</summary>
        public static CharacterPose choose_pose(BattleSimulation battle,int index,float now)
        {
            var stats=battle.stats_for(index);
            float attack_duration=battle.exploder[index]?stats.fuse_seconds:Mathf.Min(.65f,stats.attack_interval);
            if(now-battle.attack_started_at[index]<attack_duration) return CharacterPose.Attack;
            var agent=battle.crowd.agents[index];
            if(agent.enabled && agent.velocity.sqrMagnitude>.04f)
                return battle.activated[index]?CharacterPose.Charge:CharacterPose.Run;
            return CharacterPose.Idle;
        }
    }
}
