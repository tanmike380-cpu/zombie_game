using System;
using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    [Serializable]
    public sealed class FrontierEconomyConfig
    {
        public bool provisional;
        public float food, wood, stone, iron, arrows, gunpowder;
        public float food_per_second, wood_per_second, stone_per_second, iron_per_second;
        public float arrow_batches_per_second, powder_batches_per_second;
        public float wood_per_arrow_batch, arrows_per_batch, iron_per_powder_batch, powder_per_batch;
    }

    /// <summary>Infinite deposits, limited throughput; one shallow recipe for each ammunition type.</summary>
    public sealed class FrontierEconomy
    {
        public readonly FrontierEconomyConfig config;
        public float food,wood,stone,iron,arrows,gunpowder;
        public bool arrow_works=true,powder_works=true;
        public int arrow_workshops=1,powder_workshops=1;
        public float food_bonus_minute,wood_bonus_minute,stone_bonus_minute,iron_bonus_minute;
        public int supplied_shots;
        public float food_sites=1,wood_sites=1,stone_sites=1,iron_sites=1;
        public FrontierEconomy(FrontierEconomyConfig config)
        {
            this.config=config ?? throw new ArgumentNullException(nameof(config));
            foreach(float value in new[]{config.food,config.wood,config.stone,config.iron,config.arrows,config.gunpowder,
                config.food_per_second,config.wood_per_second,config.stone_per_second,config.iron_per_second,
                config.arrow_batches_per_second,config.powder_batches_per_second,config.wood_per_arrow_batch,
                config.arrows_per_batch,config.iron_per_powder_batch,config.powder_per_batch})
                if(float.IsNaN(value)||float.IsInfinity(value)||value<0) throw new ArgumentException("Invalid frontier economy configuration");
            if(config.wood_per_arrow_batch<=0 || config.iron_per_powder_batch<=0) throw new ArgumentException("Recipe inputs must be positive");
            food=config.food;wood=config.wood;stone=config.stone;iron=config.iron;arrows=config.arrows;gunpowder=config.gunpowder;
        }
        public void step(float seconds)
        {
            food+=config.food_per_second*food_sites*seconds; wood+=config.wood_per_second*wood_sites*seconds;
            stone+=config.stone_per_second*stone_sites*seconds; iron+=config.iron_per_second*iron_sites*seconds;
            food+=food_bonus_minute/60*seconds;wood+=wood_bonus_minute/60*seconds;
            stone+=stone_bonus_minute/60*seconds;iron+=iron_bonus_minute/60*seconds;
            if(arrow_works)
            {
                float batches=Mathf.Min(config.arrow_batches_per_second*arrow_workshops*seconds,wood/config.wood_per_arrow_batch);
                wood-=batches*config.wood_per_arrow_batch; arrows+=batches*config.arrows_per_batch;
            }
            if(powder_works)
            {
                float batches=Mathf.Min(config.powder_batches_per_second*powder_workshops*seconds,iron/config.iron_per_powder_batch);
                iron-=batches*config.iron_per_powder_batch; gunpowder+=batches*config.powder_per_batch;
            }
        }
        public bool try_supply(int unit_index)
        {
            int cost=UnitBalance.human.ammunition_cost;
            if(gunpowder<cost) return false;
            gunpowder-=cost; supplied_shots++; return true;
        }
    }
}
