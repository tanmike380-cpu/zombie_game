using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieGame.Combat;

namespace ZombieGame.World
{
    [Serializable] public sealed class SiegeConfig
    {
        public bool defense_test;
        public int navigation_iterations_per_frame;
        public float rally_forward_offset;
        public float preparation_seconds,campaign_first_wave_seconds,low_pressure_interval_seconds,high_pressure_interval_seconds;
        public float safe_radius,frontier_radius,territory_cell_size,distance_weight,area_weight;
        public int territory_cells_for_full_pressure;
        public float low_pressure_wave_fraction,high_pressure_wave_fraction;
    }
    /// <summary>Scenario pacing, separate from authored unit balance. No spawning or stat overrides.</summary>
    public sealed class FrontierSiege
    {
        public readonly SiegeConfig config;
        private readonly HashSet<Vector2Int> territory=new HashSet<Vector2Int>();
        private float elapsed,scan_at,progress,furthest;
        public float pressure {get;private set;}
        public int waves {get;private set;}
        public int last_wave_units {get;private set;}
        public float interval=>Mathf.Lerp(config.low_pressure_interval_seconds,config.high_pressure_interval_seconds,pressure);
        public float remaining=>waves==0?Mathf.Max(0,(config.defense_test?config.preparation_seconds:config.campaign_first_wave_seconds)-elapsed):config.defense_test?0:(1-progress)*interval;
        public FrontierSiege(SiegeConfig config)
        {
            if(config==null||config.navigation_iterations_per_frame<=0||config.preparation_seconds<0||config.campaign_first_wave_seconds<0||config.high_pressure_interval_seconds<=0||
                config.low_pressure_interval_seconds<config.high_pressure_interval_seconds||config.frontier_radius<=config.safe_radius||
                config.safe_radius<0||config.territory_cell_size<=0||config.territory_cells_for_full_pressure<=0||
                config.distance_weight<0||config.area_weight<0||Mathf.Abs(config.distance_weight+config.area_weight-1)>.001f||
                config.low_pressure_wave_fraction<=0||config.high_pressure_wave_fraction<config.low_pressure_wave_fraction||config.high_pressure_wave_fraction>1)
                throw new InvalidOperationException("Invalid SiegeConfig pacing/pressure fields");
            this.config=config;
        }
        public void record_expansion(Vector3 point,Vector3 home)
        {
            float distance=Vector2.Distance(new Vector2(point.x,point.z),new Vector2(home.x,home.z));
            if(distance<=config.safe_radius)return;
            furthest=Mathf.Max(furthest,distance);
            territory.Add(new Vector2Int(Mathf.FloorToInt(point.x/config.territory_cell_size),Mathf.FloorToInt(point.z/config.territory_cell_size)));
            pressure=Mathf.Clamp01(config.distance_weight*Mathf.InverseLerp(config.safe_radius,config.frontier_radius,furthest)+
                config.area_weight*Mathf.Clamp01(territory.Count/(float)config.territory_cells_for_full_pressure));
        }
        public void step(float delta,BattleSimulation battle,FrontierConstruction construction,Vector3 home)
        {
            elapsed+=Mathf.Max(0,delta);
            if(elapsed>=scan_at)
            {
                scan_at=elapsed+1;
                for(int i=0;i<battle.soldier_count;i++)if(battle.health[i]>0&&!battle.is_reserve(i))record_expansion(battle.positions[i],home);
                if(construction!=null)foreach(var facility in construction.facilities)if(facility.complete&&!facility.destroyed)record_expansion(facility.region.bounds.center,home);
            }
            if(waves==0)
            {if(remaining>0)return;send_wave(battle,home);return;}
            if(config.defense_test)return;
            progress+=Mathf.Max(0,delta)/interval;
            if(progress>=1){progress=0;send_wave(battle,home);}
        }
        private void send_wave(BattleSimulation battle,Vector3 home)
        {
            int count=config.defense_test?battle.zombie_count:Mathf.CeilToInt(battle.zombie_count*Mathf.Lerp(config.low_pressure_wave_fraction,config.high_pressure_wave_fraction,pressure));
            last_wave_units=battle.order_siege(home+Vector3.forward*config.rally_forward_offset,count);waves++;
            Debug.Log($"[Siege] wave={waves} ordered={last_wave_units} pressure={pressure:P0} defense_test={config.defense_test}");
        }
    }
}
