using System.Collections.Generic;
using UnityEngine;
using ZombieGame.Balance;
using ZombieGame.Combat;

namespace ZombieGame.World
{
    /// <summary>Transfer shared inventory to carried rounds only inside completed depot coverage.</summary>
    public sealed class AmmunitionSupply
    {
        public readonly List<Vector3> depots=new List<Vector3>();
        private readonly FrontierEconomy economy;
        public AmmunitionSupply(FrontierEconomy economy){this.economy=economy;}
        public bool covers(Vector3 point)
        {
            float squared=UnitBalance.config.ammunition_depot_radius*UnitBalance.config.ammunition_depot_radius;
            foreach(var depot in depots){var delta=point-depot;delta.y=0;if(delta.sqrMagnitude<=squared)return true;}
            return false;
        }
        public void step(BattleSimulation battle)
        {
            for(int i=0;i<battle.soldier_count;i++)
            {
                if(battle.health[i]<=0||!covers(battle.positions[i]))continue;
                var stats=battle.stats_for(i);bool arrows=stats.ammunition_type=="arrows";
                int transfer=Mathf.Min(stats.ammunition_capacity-battle.ammunition[i],Mathf.FloorToInt(arrows?economy.arrows:economy.gunpowder));
                if(transfer<=0)continue;
                battle.ammunition[i]+=transfer;if(arrows)economy.arrows-=transfer;else economy.gunpowder-=transfer;
            }
        }
    }
}
