using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    [Serializable]
    public sealed class ZombieDistanceBand
    {
        public string name;
        public float max_distance_tiles, population_percent, walker_percent, runner_percent, exploder_percent;
    }

    [Serializable]
    public sealed class ZombieDistributionConfig
    {
        public int version, seed;
        public ZombieDistanceBand[] bands;
    }

    /// <summary>Authored population shares by straight-line distance from the starting HQ.
    /// Only initial world generation uses this policy; it never changes live combat stats.</summary>
    public static class ZombieDistribution
    {
        public static ZombieDistributionConfig load()
        {
            var asset=Resources.Load<TextAsset>("ZombieDistribution");
            if(asset==null)throw new InvalidOperationException("Missing Resources/ZombieDistribution.json");
            var config=JsonUtility.FromJson<ZombieDistributionConfig>(asset.text);
            validate(config);
            return config;
        }

        public static void validate(ZombieDistributionConfig config)
        {
            if(config==null||config.version!=1||config.bands==null||config.bands.Length==0)
                throw new InvalidOperationException("Invalid zombie distribution schema");
            float last_distance=0,total=0;
            foreach(var band in config.bands)
            {
                if(band==null)throw new InvalidOperationException("Missing zombie distance band");
                foreach(float value in new[]{band.max_distance_tiles,band.population_percent,band.walker_percent,band.runner_percent,band.exploder_percent})
                    if(float.IsNaN(value)||float.IsInfinity(value)||value<0)throw new InvalidOperationException("Invalid zombie band number: "+band.name);
                if(band.max_distance_tiles<=last_distance||band.population_percent>100||
                    Mathf.Abs(band.walker_percent+band.runner_percent+band.exploder_percent-100)>.001f)
                    throw new InvalidOperationException("Zombie distances must increase and role percentages must total 100: "+band.name);
                total+=band.population_percent;last_distance=band.max_distance_tiles;
            }
            if(Mathf.Abs(total-100)>.001f)throw new InvalidOperationException("Zombie population percentages must total 100");
            foreach(string id in new[]{"walker","runner","exploder"})
                if(!UnitBalance.get(id).implemented)throw new InvalidOperationException("Distribution requires active shared balance: "+id);
        }

        public static int find_band(ZombieDistributionConfig config,float distance)
        {
            for(int i=0;i<config.bands.Length;i++)if(distance<config.bands[i].max_distance_tiles)return i;
            return -1;
        }

        public static void populate(FrontierMap map)
        {
            var config=load();var random=new System.Random(config.seed);
            var cells=new List<Vector3>[config.bands.Length];
            for(int i=0;i<cells.Length;i++)cells[i]=new List<Vector3>();
            for(int z=-122;z<122;z++)for(int x=-122;x<122;x++)
            {
                var point=new Vector3(x+.5f,0,z+.5f);
                // Existing established settlement remains spawn-free.
                if((point.x<-40&&point.z<-48)||map.blocked(point,1))continue;
                int band=find_band(config,Vector3.Distance(point,map.headquarters_position));
                if(band<0)throw new InvalidOperationException("Last zombie distance band does not cover the map");
                cells[band].Add(point);
            }
            int filled=FrontierMap.HUMAN_CAPACITY,allocated=0;
            float cumulative=0;
            for(int band_index=0;band_index<cells.Length;band_index++)
            {
                var band=config.bands[band_index];var candidates=cells[band_index];
                cumulative+=band.population_percent;
                int end=band_index==cells.Length-1?FrontierMap.ZOMBIES:Mathf.RoundToInt(FrontierMap.ZOMBIES*cumulative/100);
                int count=end-allocated;allocated=end;
                if(count>candidates.Count)throw new InvalidOperationException("Insufficient free zombie cells in "+band.name+"; reduce its population percentage");
                int walkers=Mathf.RoundToInt(count*band.walker_percent/100);
                int runners=Mathf.RoundToInt(count*(band.walker_percent+band.runner_percent)/100)-walkers;
                for(int i=0;i<count;i++)
                {
                    int chosen=random.Next(i,candidates.Count);
                    Vector3 point=candidates[chosen];candidates[chosen]=candidates[i];candidates[i]=point;
                    string id=i<walkers?"walker":i<walkers+runners?"runner":"exploder";
                    map.spawns[filled]=point;map.unit_ids[filled]=id;map.explosive[filled]=id=="exploder";filled++;
                }
                Debug.Log($"[ZombieDistribution] {band.name} count={count} free_cells={candidates.Count} density={(float)count/Math.Max(1,candidates.Count):P1} walker={walkers} runner={runners} exploder={count-walkers-runners}");
            }
        }
    }
}
