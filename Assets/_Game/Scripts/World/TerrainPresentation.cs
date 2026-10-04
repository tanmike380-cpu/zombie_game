using System;
using UnityEngine;

namespace ZombieGame.World
{
    /// <summary>Authored surface settings only: never movement or combat balance.</summary>
    [Serializable] public sealed class TerrainPresentation
    {
        public int schema_version,alphamap_resolution;
        public float base_map_distance,macro_patch_size,road_half_width,road_edge_width,shore_width;
        public Layer[] layers;
        public const string GENERATED_ROOT="FreeEnvironment/TerrainGenerated/";
        [Serializable] public sealed class Layer
        {
            public string id,source;
            public float tile_size,normal_strength,smoothness_scale;
        }
        public static TerrainPresentation load_config()
        {
            var text=Resources.Load<TextAsset>("TerrainPresentation");
            if(!text)throw new InvalidOperationException("Missing authored TerrainPresentation.json");
            var config=JsonUtility.FromJson<TerrainPresentation>(text.text);
            string[] required={"Grass_A","Grass_Dry","Dirt","Mud","Stone","Road"};
            if(config.schema_version!=1||config.layers==null||config.layers.Length!=required.Length)
                throw new InvalidOperationException("TerrainPresentation needs schema 1 and exactly six named layers");
            for(int i=0;i<required.Length;i++)
                if(config.layers[i].id!=required[i]||config.layers[i].tile_size<=0)
                    throw new InvalidOperationException("Terrain layer order/scale invalid: "+required[i]);
            if(config.alphamap_resolution<128||config.macro_patch_size<=0||config.road_edge_width<=0||config.shore_width<=0)
                throw new InvalidOperationException("Invalid terrain sampling or blend settings");
            return config;
        }
    }
}
