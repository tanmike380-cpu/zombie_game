using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Two bounded basic water surfaces; depth colours sample the actual native Terrain height field.</summary>
    public sealed class ShowcaseConceptWater : IDisposable
    {
        public readonly Renderer sea, pond;
        private readonly Texture2D height_field;
        private readonly Material sea_material, pond_material;

        public ShowcaseConceptWater(ShowcaseTerrain ground, Transform parent,bool preserve_readable_height=false)
        {
            var layout = ground.concept.layout;
            var data = ground.terrain_data;
            int resolution = data.heightmapResolution;
            height_field = new Texture2D(resolution,resolution,TextureFormat.RFloat,false,true) { name="Actual concept Terrain heights",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
            var heights = data.GetHeights(0,0,resolution,resolution);
            var pixels = new Color[resolution*resolution];
            for (int z=0;z<resolution;z++) for (int x=0;x<resolution;x++) pixels[z*resolution+x]=new Color(heights[z,x],0,0,1);
            height_field.SetPixels(pixels); height_field.Apply(false,!preserve_readable_height);
            sea_material = create_material("Concept sea",ground,layout.sea_level,layout.sea_deep,layout.sea_shallow,false);
            pond_material = create_material("Concept isolated pond",ground,layout.pond_level,layout.pond_deep,layout.pond_shallow,true);
            sea = create_surface(parent,"Concept open sea — basic water",new Vector3(-90,layout.sea_level,0),new Vector2(188,400),sea_material);
            pond = create_surface(parent,"Concept closed pond — independent water level",ground.origin+new Vector3(layout.pond_center.x*data.size.x,layout.pond_level,layout.pond_center.y*data.size.z),
                layout.pond_radius*2+Vector2.one*4,pond_material);
        }

        private Material create_material(string name, ShowcaseTerrain ground, float level, Color deep, Color shallow, bool pool)
        {
            var shader=Shader.Find("ZombieGame/EnvironmentShowcase/ConceptWater");
            if (!shader) throw new InvalidOperationException("Concept water shader unavailable in player.");
            var material=new Material(shader) { name=name };
            material.SetTexture("_HeightField",height_field);
            material.SetVector("_TerrainOrigin",ground.origin);
            material.SetFloat("_TerrainSize",ground.terrain_data.size.x); material.SetFloat("_HeightRange",ground.terrain_data.size.y);
            material.SetFloat("_WaterLevel",level); material.SetColor("_Deep",deep); material.SetColor("_Shallow",shallow);
            material.SetFloat("_IsPond",pool?1:0);
            material.SetFloat("_NaturalPreview",ground.concept.dark?1:0);
            if(ground.concept.dark && ground.concept.dark.vision_surface_regions)
            {
                material.SetFloat("_VisionWater",1);
                material.SetFloat("_VisionVariation",ground.concept.dark.vision_water_variation);
                material.SetFloat("_VisionDepthRange",ground.concept.dark.vision_water_depth_range);
                configure_vision_response(material,ground);
            }
            if(ground.concept.dark) Debug.Log("[DarkWater] "+name+" natural="+material.GetFloat("_NaturalPreview")+" shader="+material.shader.name);
            var layout=ground.concept.layout;
            material.SetFloat("_CoastX",layout.coast_x);
            material.SetVector("_PoolCenter",ground.origin+new Vector3(layout.pond_center.x*ground.terrain_data.size.x,0,layout.pond_center.y*ground.terrain_data.size.z));
            material.SetVector("_PoolRadius",new Vector4(layout.pond_radius.x,layout.pond_radius.y,0,0));
            return material;
        }

        private static void configure_vision_response(Material material,ShowcaseTerrain ground)
        {
            var preset = ground.concept.dark;
            if(preset.vision_water_response_revision < 1) return;
            var layers = ground.terrain_data.terrainLayers;
            if(layers.Length != 6 || !layers[3] || !layers[3].diffuseTexture)
                throw new InvalidOperationException("Vision shallow water requires the existing six-layer Mud diffuse source.");
            var mud = layers[3];
            material.SetTexture("_VisionBed",mud.diffuseTexture);
            material.SetVector("_VisionBedTile",new Vector4(mud.tileSize.x,mud.tileSize.y,mud.tileOffset.x,mud.tileOffset.y));
            material.SetFloat("_VisionResponse",1);
            material.SetFloat("_VisionBedVisibility",preset.vision_water_bed_visibility);
            material.SetFloat("_VisionBedDepth",preset.vision_water_bed_depth_m);
            material.SetFloat("_VisionWaveStrength",preset.vision_water_wave_strength);
            material.SetFloat("_VisionSmoothness",preset.vision_water_smoothness);
            material.SetFloat("_VisionSkyResponse",preset.vision_water_sky_response);
            material.SetColor("_VisionSkyColor",preset.ambient_sky);
        }

        private static Renderer create_surface(Transform parent, string name, Vector3 position, Vector2 extent, Material material)
        {
            var surface=GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name=name; surface.transform.SetParent(parent,false); surface.transform.position=position;
            surface.transform.localScale=new Vector3(extent.x/10,1,extent.y/10);
            if(Application.isPlaying) UnityEngine.Object.Destroy(surface.GetComponent<Collider>());
            else UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
            var renderer=surface.GetComponent<Renderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        public void Dispose()
        {
            foreach (var renderer in new[]{sea,pond}) if(renderer){renderer.gameObject.SetActive(false);UnityEngine.Object.Destroy(renderer.gameObject);}
            UnityEngine.Object.Destroy(height_field); UnityEngine.Object.Destroy(sea_material); UnityEngine.Object.Destroy(pond_material);
        }
    }
}
