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
        [Serializable] public sealed class SurfaceRegion
        {
            public Vector2 center,radius;
            public float strength;
        }
        public SurfaceRegion[] vision_soil_regions = Array.Empty<SurfaceRegion>();
        public SurfaceRegion[] vision_dry_regions = Array.Empty<SurfaceRegion>();
        public SurfaceRegion[] vision_meadow_regions = Array.Empty<SurfaceRegion>();
        public int vision_surface_revision;
        public TerrainLayer[] vision_layer_overrides = new TerrainLayer[6];
        public bool vision_surface_regions;
        public int vision_grass_budget = 12000;
        public int vision_shrub_budget = 140;
        public int vision_rock_budget = 80;
        public float vision_base_soil = .18f;
        public float vision_macro_scale = 30;
        public float vision_water_variation = .10f;
        public float vision_water_depth_range = 1.8f;
        public float vision_shadow_distance = 150;
        public int vision_shadow_cascades = 2;
        public ShadowResolution vision_shadow_resolution = ShadowResolution.High;
        [Serializable] public sealed class PlantPatch
        {
            public string source_id,category;
            public int count;
            public Vector2[] centers,radii;
            public Vector2 scale_range;
            public bool core_bed;
        }
        public PlantPatch[] vision_plant_patches = Array.Empty<PlantPatch>();
        public Vector2[] vision_tree_centers = Array.Empty<Vector2>();
        public Vector2[] vision_tree_radii = Array.Empty<Vector2>();
        public float vision_tree_spacing = 5.8f;
        public float vision_tree_chunk_size = 64;

        public void validate_vision_regions()
        {
            if(vision_layer_overrides == null || vision_layer_overrides.Length != 6)
                throw new InvalidOperationException("Vision requires exactly six layer override slots.");
            if(vision_base_soil < 0 || vision_base_soil > 1 || vision_macro_scale <= 0 || vision_water_depth_range <= 0)
                throw new InvalidOperationException("Vision surface scale or base soil weight is invalid.");
            if(vision_shadow_distance <= 0 || (vision_shadow_cascades != 1 && vision_shadow_cascades != 2 && vision_shadow_cascades != 4))
                throw new InvalidOperationException("Vision camera-relative shadow budget is invalid.");
            validate_regions(vision_soil_regions,"soil");
            validate_regions(vision_dry_regions,"dry");
            validate_regions(vision_meadow_regions,"meadow");
            VisionPlantLayout.validate_patches(this);
            if(vision_tree_centers == null || vision_tree_radii == null || vision_tree_centers.Length != vision_tree_radii.Length || vision_tree_spacing <= 0 || vision_tree_chunk_size <= 0)
                throw new InvalidOperationException("Vision original-tree group layout is invalid.");
            foreach(var radius in vision_tree_radii)
                if(radius.x <= 0 || radius.y <= 0) throw new InvalidOperationException("Vision tree group radii must be positive.");
        }

        private static void validate_regions(SurfaceRegion[] regions,string semantic)
        {
            if(regions == null) throw new InvalidOperationException("Vision region array missing: "+semantic);
            foreach(var region in regions)
            {
                if(region == null || region.radius.x <= 0 || region.radius.y <= 0 || region.strength < 0 || region.strength > 1 ||
                    region.center.x < 0 || region.center.x > 1 || region.center.y < 0 || region.center.y > 1)
                    throw new InvalidOperationException("Vision natural region is invalid: "+semantic);
            }
        }

        public EnvironmentShowcaseConfig.SourceAsset find_source(EnvironmentShowcaseConfig config,string id)
        {
            foreach(var sources in new[]{frozen_sources,config.landmark_sources,config.ore_sources})
                foreach(var source in sources) if(source.id==id) return source;
            if(stone_candidate!=null && stone_candidate.id==id && stone_candidate.prefab) return stone_candidate;
            throw new InvalidOperationException("Dark preview source not approved/imported: "+id);
        }
    }
}
