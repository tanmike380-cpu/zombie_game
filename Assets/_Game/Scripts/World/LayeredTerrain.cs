using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.World
{
    /// <summary>Six-layer presentation terrain. Flat at the existing ground; no gameplay collider or navigation source.</summary>
    public sealed class LayeredTerrain:IDisposable
    {
        private readonly GameObject root;
        private readonly TerrainData data;
        private readonly TerrainPresentation config;
        private readonly List<Vector4> roads=new List<Vector4>();
        public readonly Terrain terrain;
        public const float GROUND_Y=-.005f;
        private const float MAP_MIN=-128,MAP_SIZE=256;

        public LayeredTerrain(FrontierMap map,Transform parent)
        {
            config=TerrainPresentation.load_config();
            var layers=new TerrainLayer[config.layers.Length];
            for(int i=0;i<layers.Length;i++)
            {
                layers[i]=Resources.Load<TerrainLayer>(TerrainPresentation.GENERATED_ROOT+config.layers[i].id);
                if(!layers[i])throw new InvalidOperationException("Missing TerrainLayer "+config.layers[i].id+"; run TerrainLayerImport.prepare_layers before building");
            }
            var material=Resources.Load<Material>(TerrainPresentation.GENERATED_ROOT+"TerrainStandard");
            if(!material)throw new InvalidOperationException("Missing generated TerrainStandard material");
            data=new TerrainData{name="Six-layer ground — presentation only",heightmapResolution=33,
                alphamapResolution=config.alphamap_resolution,baseMapResolution=1024,size=new Vector3(MAP_SIZE,1,MAP_SIZE)};
            data.SetHeights(0,0,new float[33,33]);data.terrainLayers=layers;
            collect_roads(map);
            data.SetAlphamaps(0,0,build_weights(map));
            root=new GameObject("Ground — Grass_A / Grass_Dry / Dirt / Mud / Stone / Road");
            root.transform.SetParent(parent,false);root.transform.position=new Vector3(MAP_MIN,GROUND_Y,MAP_MIN);
            terrain=root.AddComponent<Terrain>();terrain.terrainData=data;terrain.materialTemplate=material;
            terrain.drawInstanced=true;terrain.heightmapPixelError=5;terrain.basemapDistance=config.base_map_distance;
            terrain.shadowCastingMode=ShadowCastingMode.Off;terrain.allowAutoConnect=false;
            Debug.Log("[TerrainArt] Six-layer flat Terrain active; existing navigation, ground height and CombatFog retained; macro layer variation, scanned normals and smoothness; no geometric displacement");
        }

        private void collect_roads(FrontierMap map)
        {
            Vector3 headquarters=Vector3.zero;
            foreach(var region in map.regions)if(region.label=="COMMAND HALL")headquarters=region.bounds.center;
            foreach(var region in map.regions)
            {
                if(region.kind!=LandscapeKind.Building||is_wall(region.label)||region.label=="COMMAND HALL"||region.label=="OUTPOST")continue;
                Vector3 destination=region.bounds.center;
                roads.Add(new Vector4(headquarters.x,headquarters.z,destination.x,destination.z));
                if(ZombieGame.Combat.BattleSimulation.is_friendly_gate(region.label))
                {
                    var outward=destination-headquarters;outward.y=0;outward.Normalize();
                    roads.Add(new Vector4(destination.x,destination.z,destination.x+outward.x*18,destination.z+outward.z*18));
                }
            }
        }
        private static bool is_wall(string label)
        {return label.Contains("WALL")||label.Contains("PIER");}

        private float[,,] build_weights(FrontierMap map)
        {
            int resolution=config.alphamap_resolution;
            var weights=new float[resolution,resolution,6];
            var sample=new float[6];
            for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
            {
                float px=MAP_MIN+x/(float)(resolution-1)*MAP_SIZE,pz=MAP_MIN+z/(float)(resolution-1)*MAP_SIZE;
                evaluate_weights(map,px,pz,sample);
                float total=0;for(int i=0;i<6;i++)total+=sample[i];
                if(total<=0||float.IsNaN(total))throw new InvalidOperationException("Terrain material weights invalid at "+px+","+pz);
                for(int i=0;i<6;i++)weights[z,x,i]=sample[i]/total;
            }
            return weights;
        }
        private void evaluate_weights(FrontierMap map,float x,float z,float[] sample)
        {
            float macro=Mathf.PerlinNoise((x+347)/config.macro_patch_size,(z+711)/config.macro_patch_size);
            float medium=Mathf.PerlinNoise((x+57)/9,(z+43)/9);
            float dry=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.73f,macro));
            float dirt=Mathf.SmoothStep(0,.38f,Mathf.InverseLerp(.5f,.8f,medium));
            float mud=0,stone=0;
            Vector3 point=new Vector3(x,0,z);
            foreach(var region in map.regions)
            {
                float distance=distance_to_region(point,region.bounds);
                if(region.kind==LandscapeKind.River)
                    mud=Mathf.Max(mud,1-Mathf.SmoothStep(0,1,distance/config.shore_width));
                if(region.kind==LandscapeKind.Cliff)
                    stone=Mathf.Max(stone,1-Mathf.SmoothStep(0,1,distance/7));
                if(region.kind==LandscapeKind.Building&&is_wall(region.label))
                    stone=Mathf.Max(stone,(1-Mathf.SmoothStep(0,1,distance/2.5f))*.72f);
                if(region.kind==LandscapeKind.Building&&!is_wall(region.label))
                    dirt=Mathf.Max(dirt,(1-Mathf.SmoothStep(0,1,distance/3.5f))*.85f);
            }
            float road=0;
            foreach(var segment in roads)
            {
                float distance=distance_to_segment(new Vector2(x,z),segment);
                road=Mathf.Max(road,1-Mathf.SmoothStep(0,1,(distance-config.road_half_width)/config.road_edge_width));
            }
            // Terrain is intentionally flat in this phase; slope use becomes meaningful only after gameplay-safe terrain authoring.
            float slope=data.GetSteepness((x-MAP_MIN)/MAP_SIZE,(z-MAP_MIN)/MAP_SIZE);
            stone=Mathf.Max(stone,Mathf.InverseLerp(25,50,slope));
            sample[5]=road;
            sample[3]=mud*(1-road);
            sample[4]=stone*(1-road)*(1-mud);
            float remainder=(1-road)*(1-mud)*(1-stone);
            sample[2]=dirt*remainder;
            sample[1]=dry*(1-dirt)*remainder;
            sample[0]=(1-dry)*(1-dirt)*remainder;
        }
        private static float distance_to_region(Vector3 point,Bounds bounds)
        {
            float dx=Mathf.Max(0,Mathf.Max(bounds.min.x-point.x,point.x-bounds.max.x));
            float dz=Mathf.Max(0,Mathf.Max(bounds.min.z-point.z,point.z-bounds.max.z));
            return Mathf.Sqrt(dx*dx+dz*dz);
        }
        private static float distance_to_segment(Vector2 point,Vector4 segment)
        {
            var start=new Vector2(segment.x,segment.y);var end=new Vector2(segment.z,segment.w);
            var delta=end-start;float t=Mathf.Clamp01(Vector2.Dot(point-start,delta)/Mathf.Max(.0001f,delta.sqrMagnitude));
            return Vector2.Distance(point,start+delta*t);
        }
        public void Dispose()
        {if(root)UnityEngine.Object.Destroy(root);if(data)UnityEngine.Object.Destroy(data);}
    }
}
