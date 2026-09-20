using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.World
{
    /// <summary>One-tile resource samples shared by placement previews and final production yields.</summary>
    public sealed class ResourceTerrain
    {
        private readonly FrontierMap map;
        private readonly HashSet<int> forest=new HashSet<int>(),stone=new HashSet<int>(),iron=new HashSet<int>();
        public ResourceTerrain(FrontierMap map)
        {
            this.map=map;
            foreach(var region in map.regions)
                if(region.kind==LandscapeKind.Forest)
                    for(int z=Mathf.CeilToInt(region.bounds.min.z);z<region.bounds.max.z;z++)
                        for(int x=Mathf.CeilToInt(region.bounds.min.x);x<region.bounds.max.x;x++)forest.Add(cell(x,z));
            foreach(var site in map.resource_sites)
            {
                var cells=site.kind=="Stone"?stone:site.kind=="Iron"?iron:null;if(cells==null)continue;
                for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)
                    if(x*x+z*z<=9)cells.Add(cell(Mathf.RoundToInt(site.position.x)+x,Mathf.RoundToInt(site.position.z)+z));
            }
        }
        public static int cell(int x,int z)=>(x+128)+(z+128)*256;
        public static Vector3 position(int cell)=>new Vector3(cell%256-128,0,cell/256-128);
        public float fertility(Vector3 point)
        {
            float wet=0;
            foreach(var region in map.regions)
                if(region.kind==LandscapeKind.River)
                {
                    var ground=region.bounds;ground.center=new Vector3(ground.center.x,0,ground.center.z);
                    if(ground.SqrDistance(point)<100){wet=.3f;break;}
                }
            return Mathf.Clamp(.25f+Mathf.PerlinNoise((point.x+200)*.055f,(point.z+200)*.055f)*.6f+wet,.25f,1);
        }
        public float estimate(HeadquartersRecipe recipe,Vector3 center,HashSet<int> reserved,out int[] cells)
        {
            var eligible=new List<int>();float rate=0;
            int radius=Mathf.CeilToInt(recipe.gather_radius);
            for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++)
            {
                if(x*x+z*z>recipe.gather_radius*recipe.gather_radius)continue;
                int wx=Mathf.RoundToInt(center.x)+x,wz=Mathf.RoundToInt(center.z)+z;
                if(wx< -127||wx>127||wz< -127||wz>127)continue;
                int key=cell(wx,wz);if(reserved.Contains(key))continue;
                var point=new Vector3(wx,0,wz);float quality=1;
                switch(recipe.id)
                {
                    case "food":if(map.blocked(point,0))continue;quality=fertility(point);break;
                    case "wood":if(!forest.Contains(key))continue;break;
                    case "stone":if(!stone.Contains(key))continue;break;
                    case "iron":if(!iron.Contains(key))continue;break;
                    default:continue;
                }
                eligible.Add(key);rate+=quality*recipe.yield_per_cell_minute;
            }
            cells=eligible.ToArray();return rate;
        }
    }
}
