using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    [Serializable] public sealed class ZombieRoleShare { public string id;public float percent; }
    [Serializable] public sealed class ZombieDistanceBand
    {
        public string name;
        public float max_distance_tiles,population_percent;
        public ZombieRoleShare[] roles;
    }
    [Serializable] public sealed class ZombieDistributionConfig
    {
        public int version,seed;
        public ZombieDistanceBand[] bands;
    }

    /// <summary>Initial population policy only; combat numbers belong to UnitBalance.</summary>
    public static class ZombieDistribution
    {
        public static ZombieDistributionConfig load()
        {
            var asset=Resources.Load<TextAsset>("ZombieDistribution");
            if(asset==null)throw new InvalidOperationException("Missing Resources/ZombieDistribution.json");
            var config=JsonUtility.FromJson<ZombieDistributionConfig>(asset.text);validate(config);return config;
        }
        public static void validate(ZombieDistributionConfig config)
        {
            if(config==null||config.version!=2||config.bands==null||config.bands.Length==0)
                throw new InvalidOperationException("Invalid zombie distribution schema");
            float last_distance=0,total=0;
            foreach(var band in config.bands)
            {
                if(band==null||band.roles==null||band.roles.Length==0)throw new InvalidOperationException("Missing zombie band/roles");
                if(!finite_nonnegative(band.max_distance_tiles)||band.max_distance_tiles<=last_distance||
                   !finite_nonnegative(band.population_percent)||band.population_percent>100)
                    throw new InvalidOperationException("Invalid distance/population in "+band.name);
                float role_total=0;var ids=new HashSet<string>();
                foreach(var role in band.roles)
                {
                    if(role==null||!UnitBalance.is_zombie(role.id)||!ids.Add(role.id)||!finite_nonnegative(role.percent)||role.percent>100||
                       !UnitBalance.get(role.id).implemented)throw new InvalidOperationException("Invalid active role share in "+band.name);
                    role_total+=role.percent;
                }
                if(Mathf.Abs(role_total-100)>.001f)throw new InvalidOperationException("Role percentages must total 100: "+band.name);
                total+=band.population_percent;last_distance=band.max_distance_tiles;
            }
            if(Mathf.Abs(total-100)>.001f)throw new InvalidOperationException("Zombie population percentages must total 100");
        }
        private static bool finite_nonnegative(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=0;
        public static int find_band(ZombieDistributionConfig config,float distance)
        {
            for(int i=0;i<config.bands.Length;i++)if(distance<config.bands[i].max_distance_tiles)return i;
            return -1;
        }
        public static int[] role_counts(ZombieDistanceBand band,int count)
        {
            var result=new int[band.roles.Length];float cumulative=0;int assigned=0;
            for(int i=0;i<result.Length;i++)
            {
                cumulative+=band.roles[i].percent;
                int end=i==result.Length-1?count:Mathf.RoundToInt(count*cumulative/100);
                result[i]=end-assigned;assigned=end;
            }
            return result;
        }
        public static void populate(FrontierMap map)
        {
            var config=load();var random=new System.Random(config.seed);
            var cells=new List<Vector3>[config.bands.Length];
            for(int i=0;i<cells.Length;i++)cells[i]=new List<Vector3>();
            // All zombie roles now share one fixed footprint, so one global hex lattice guarantees spacing.
            float spacing=UnitBalance.config.zombie_navigation_radius*2+.05f;int row=0;
            for(float z=-122;z<122;z+=spacing*.8660254f,row++)for(float x=-122+(row%2)*spacing*.5f;x<122;x+=spacing)
            {
                var point=new Vector3(x+.5f,0,z+.5f);
                if((point.x<-40&&point.z<-48)||map.blocked(point,1))continue;
                int band=find_band(config,Vector3.Distance(point,map.headquarters_position));
                if(band<0)throw new InvalidOperationException("Last zombie band does not cover map");
                cells[band].Add(point);
            }
            var occupied=new HashSet<Vector3>();
            int filled=FrontierMap.HUMAN_CAPACITY,allocated=0;float cumulative=0;
            for(int band_index=0;band_index<cells.Length;band_index++)
            {
                var band=config.bands[band_index];var candidates=cells[band_index];
                for(int i=0;i<candidates.Count;i++){int chosen=random.Next(i,candidates.Count);var p=candidates[i];candidates[i]=candidates[chosen];candidates[chosen]=p;}
                cumulative+=band.population_percent;
                int end=band_index==cells.Length-1?FrontierMap.ZOMBIES:Mathf.RoundToInt(FrontierMap.ZOMBIES*cumulative/100);
                int count=end-allocated;allocated=end;
                var quotas=role_counts(band,count);var role_order=new List<int>();
                for(int i=0;i<quotas.Length;i++)role_order.Add(i);
                role_order.Sort((a,b)=>UnitBalance.navigation_radius(UnitBalance.get(band.roles[b].id)).CompareTo(UnitBalance.navigation_radius(UnitBalance.get(band.roles[a].id))));
                foreach(int role in role_order)
                {
                    string id=band.roles[role].id;float radius=UnitBalance.navigation_radius(UnitBalance.get(id));int spawned=0;
                    foreach(var point in candidates)
                    {
                        if(spawned==quotas[role])break;
                        if(map.blocked(point,radius+.4f)||occupied.Contains(point))continue;
                        map.spawns[filled]=point;map.unit_ids[filled]=id;map.explosive[filled]=id=="exploder";filled++;spawned++;
                        occupied.Add(point);
                    }
                    if(spawned!=quotas[role])throw new InvalidOperationException("Insufficient cleared spawn cells: "+band.name+" "+id);
                    Debug.Log($"[ZombieDistribution] {band.name} {id} count={spawned}");
                }
            }
        }
    }
}
