using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Independent dark art preset. No economy, navigation or production asset mutation.</summary>
    public sealed class DarkWildernessConfig : ScriptableObject
    {
        [Serializable] public sealed class Placement
        {
            public string source_id;
            public Vector2 center;
            public float yaw, uniform_scale=1;
            public bool ore;
        }
        public string version="dark-v1", source_record="Assets/_Tests/EnvironmentShowcase/Settings/dark-handoff.json";
        public ConceptLayoutConfig layout;
        public bool props_enabled=true;
        public Placement[] placements=Array.Empty<Placement>();
        public EnvironmentShowcaseConfig.SourceAsset[] frozen_sources=Array.Empty<EnvironmentShowcaseConfig.SourceAsset>();
        public GameObject[] tree_prefabs=Array.Empty<GameObject>();
        public EnvironmentShowcaseConfig.SourceAsset stone_candidate;
        public bool stone_visual_accepted;
        public Vector2 bermuda_scale_range=new Vector2(3,3.8f),medium_scale_range=new Vector2(1,1.3f);
        public string tree_source="No accepted dense canopy source; TreeSmall and 1.6M triangle candidate deliberately not used.";
        public Color ambient=new Color(.29f,.31f,.33f), sun=new Color(.95f,.97f,1), fog=new Color(.35f,.39f,.42f);
        public float sun_intensity=.95f;
        public Color grass_tint=new Color(.66f,.77f,.67f), dry_tint=new Color(.70f,.68f,.61f), soil_tint=new Color(.78f,.76f,.70f);
        public Vector2[] exposed_centers={new Vector2(.52f,.37f),new Vector2(.69f,.47f),new Vector2(.85f,.78f)};
        public Vector2[] exposed_radii={new Vector2(10,6),new Vector2(12,7),new Vector2(8,5)};
        public bool flat_ground;
        public float flat_height = 4.2f;
        public AmbientMode ambient_mode = AmbientMode.Flat;
        public Color ambient_sky = new Color(.30f,.34f,.38f);
        public Color ambient_equator = new Color(.25f,.27f,.25f);
        public Color ambient_ground = new Color(.16f,.17f,.15f);
        public Vector2[] vegetation_centers = Array.Empty<Vector2>();
        public Vector2[] vegetation_radii = Array.Empty<Vector2>();

        public EnvironmentShowcaseConfig.SourceAsset find_source(EnvironmentShowcaseConfig config,string id)
        {
            foreach(var sources in new[]{frozen_sources,config.landmark_sources,config.ore_sources})
                foreach(var source in sources) if(source.id==id) return source;
            if(stone_candidate!=null && stone_candidate.id==id && stone_candidate.prefab) return stone_candidate;
            throw new InvalidOperationException("Dark preview source not approved/imported: "+id);
        }
    }
}
