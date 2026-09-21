using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.World
{
    [Serializable] public sealed class ResourceTier
    {
        public int level,sites_per_kind;
        public bool center_on_map;
        public float min_distance,max_distance,deposit_radius,yield_multiplier;
    }
    [Serializable] public sealed class ResourceDistributionConfig
    {
        public bool provisional;
        public int seed;
        public ResourceTier[] tiers;
    }
    /// <summary>Deterministic circular resource tiers; authored economy parameters are separate from combat balance.</summary>
    public static class ResourceDistribution
    {
        public static ResourceDistributionConfig load()
        {
            var asset=Resources.Load<TextAsset>("ResourceDistribution");
            if(asset==null)throw new InvalidOperationException("Missing ResourceDistribution.json");
            var config=JsonUtility.FromJson<ResourceDistributionConfig>(asset.text);validate(config);return config;
        }
        public static void validate(ResourceDistributionConfig config)
        {
            if(config?.tiers==null||config.tiers.Length!=3)throw new InvalidOperationException("Resources require exactly three tiers");
            for(int i=0;i<3;i++)
            {
                var tier=config.tiers[i];
                if(tier==null||tier.level!=i+1||tier.sites_per_kind<1||tier.sites_per_kind>20||tier.min_distance<0||tier.max_distance<=tier.min_distance||tier.deposit_radius<=0||tier.yield_multiplier<=0)
                    throw new InvalidOperationException("Invalid resource tier: "+(i+1));
                foreach(float value in new[]{tier.min_distance,tier.max_distance,tier.deposit_radius,tier.yield_multiplier})
                    if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Non-finite resource tier");
            }
        }
        public static ResourceSite[] populate(FrontierMap map)
        {
            var config=load();var random=new System.Random(config.seed);var sites=new List<ResourceSite>();
            string[] kinds={"Food","Wood","Stone","Iron"};
            Vector3[] starter={new Vector3(-111,0,-112),new Vector3(-116,0,-65),new Vector3(-120,0,-87),new Vector3(-61,0,-116)};
            foreach(var tier in config.tiers)
                for(int kind=0;kind<kinds.Length;kind++)for(int count=0;count<tier.sites_per_kind;count++)
                {
                    Vector3 center=tier.center_on_map?Vector3.zero:map.headquarters_position;
                    Vector3 point=Vector3.zero;bool found=false;
                    for(int attempt=0;attempt<20000;attempt++)
                    {
                        float angle=(float)random.NextDouble()*Mathf.PI*2;
                        float radius=Mathf.Sqrt(Mathf.Lerp(tier.min_distance*tier.min_distance,tier.max_distance*tier.max_distance,(float)random.NextDouble()));
                        point=tier.level==1&&count==0&&attempt==0?starter[kind]:center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                        float distance=Vector3.Distance(center,point);
                        if(distance<tier.min_distance||distance>tier.max_distance||map.blocked(point,tier.deposit_radius+1))continue;
                        // Preserve deployment lanes and the initial compact army's spawn rectangle.
                        if(point.x> -107&&point.x< -80&&point.z> -84&&point.z< -48)continue;
                        bool overlap=false;foreach(var site in sites)
                            if(Vector3.Distance(point,site.position)<tier.deposit_radius+site.radius+3){overlap=true;break;}
                        if(overlap)continue;found=true;break;
                    }
                    if(!found)throw new InvalidOperationException("Cannot place resource tier "+tier.level+" "+kinds[kind]);
                    sites.Add(new ResourceSite(kinds[kind],point.x,point.z,tier.level==1){tier=tier.level,radius=tier.deposit_radius,yield_multiplier=tier.yield_multiplier});
                    if(kind==1)
                        map.regions.Add(new LandscapeRegion(point.x,point.z,tier.deposit_radius*2,tier.deposit_radius*2,4,LandscapeKind.Forest));
                }
            return sites.ToArray();
        }
        public static string label(ResourceSite site)
        {
            string name=site.kind=="Food"?"沃土":site.kind=="Wood"?"林地":site.kind=="Stone"?"石矿":"铁矿";
            return $"{site.tier}级{name} · ×{site.yield_multiplier:0.##}";
        }
    }
}
