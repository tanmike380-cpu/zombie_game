using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;
using ModelRecord = ZombieGame.Tests.EditorTools.VegetationShowcaseImport.ModelRecord;
using TextureRecord = ZombieGame.Tests.EditorTools.VegetationShowcaseImport.TextureRecord;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Explicit local-preview handoff for three original CC0 clumps; no tree, mesh edits or automatic candidate discovery.</summary>
    public static class VisionVegetationImport
    {
        public const string HANDOFF_PATH = "art/environment/vision-vegetation-handoff.json";
        private const string SOURCE_ROOT = "art/environment/vision-sources/";
        private const string ART_ROOT = "Assets/_Tests/EnvironmentShowcase/Art/VisionVegetation/";
        private const string GENERATED_ROOT = ART_ROOT+"Generated/";

        [Serializable] private sealed class Handoff
        {
            public int schema_version;
            public string status,source_blend,source_sha256;
            public ModelRecord[] records;
        }

        [MenuItem("Zombie Game/Environment Showcase/Vision-1/Import approved grass and fern handoff")]
        public static void import_approved_clumps()
        {
            var handoff = read_verified_handoff();
            Directory.CreateDirectory(GENERATED_ROOT);
            var materials = new Dictionary<string,Material>();
            var prefabs = new Dictionary<string,GameObject>();
            foreach(var source in handoff.records)
            {
                var record = copy_source_record(source);
                prefabs.Add(record.id,VegetationShowcaseImport.prepare_prefab(record,materials,GENERATED_ROOT,true,true,true));
            }
            assign_vision_clumps(prefabs,handoff);
            AssetDatabase.SaveAssets();
            Debug.Log("[VisionVegetation] Three source clumps verified: original triangles/UV/metres, source alpha R, GL normals and once-inverted roughness. No tree imported; art review pending.");
        }

        private static Handoff read_verified_handoff()
        {
            if(!File.Exists(HANDOFF_PATH)) throw new FileNotFoundException("Complete approved Vision handoff required.",HANDOFF_PATH);
            var handoff = JsonUtility.FromJson<Handoff>(File.ReadAllText(HANDOFF_PATH));
            if(handoff == null || handoff.schema_version != 1 || handoff.status != "approved_for_local_preview" || handoff.records == null || handoff.records.Length != 3)
                throw new InvalidOperationException("Vision handoff schema/status/count mismatch: "+HANDOFF_PATH);
            verify_hash(handoff.source_blend,handoff.source_sha256);
            var expected_triangles = new Dictionary<string,int>{{"Fern02A",784},{"Fern02B",2384},{"GrassMedium02E",1664}};
            foreach(var record in handoff.records)
            {
                if(record == null || !expected_triangles.TryGetValue(record.id,out var triangles))
                    throw new InvalidOperationException("Unapproved Vision clump ID; tree candidates are not authorized by this importer.");
                expected_triangles.Remove(record.id);
                verify_record(record,triangles);
            }
            if(expected_triangles.Count != 0) throw new InvalidOperationException("Vision handoff has duplicate or missing clumps.");
            return handoff;
        }

        private static void verify_record(ModelRecord record,int triangles)
        {
            string expected_path = SOURCE_ROOT+record.id+"/"+record.id+".fbx";
            if(record.model_path != expected_path || record.triangles != triangles || record.license != "CC0-1.0" ||
                record.axis_forward != "-Z" || record.axis_up != "Y" || record.unit_m != 1 || record.placement_scale != 1 || record.lod_index != 0 ||
                record.dimensions_unity_xyz_m == null || record.dimensions_unity_xyz_m.Length != 3 ||
                record.pivot_bounds_min == null || record.pivot_bounds_min.Length != 3 || record.pivot_bounds_min[1] != 0 ||
                string.IsNullOrEmpty(record.source_collection) || string.IsNullOrEmpty(record.source_object) ||
                record.materials == null || record.materials.Length != 1 || record.material_slots == null || record.material_slots.Length != 1)
                throw new InvalidOperationException("Vision original source provenance/geometry mismatch: "+record.id);
            verify_hash(record.model_path,record.model_sha256);
            var material = record.materials[0];
            if(material == null || material.name != record.material_slots[0] || material.alpha_mode != "cutout" || material.alpha_cutoff != .5f ||
                !material.double_sided || material.metallic != 0 || material.normal_convention != "OpenGL official nor_gl" || material.channels == null)
                throw new InvalidOperationException("Vision source PBR/alpha declaration mismatch: "+record.id);
            verify_channel(record.id,material.channels.base_color,"sRGB","rgb");
            verify_channel(record.id,material.channels.normal_gl,"linear","rgb");
            verify_channel(record.id,material.channels.roughness,"linear","red");
            verify_channel(record.id,material.channels.alpha,"linear","red");
        }

        private static void verify_channel(string id,TextureRecord texture,string colorspace,string component)
        {
            if(texture == null || string.IsNullOrEmpty(texture.path) || !texture.path.StartsWith(SOURCE_ROOT+id+"/Textures/",StringComparison.Ordinal) ||
                texture.colorspace != colorspace || texture.component != component || texture.resolution == null || texture.resolution.Length != 2 ||
                texture.resolution[0] != 1024 || texture.resolution[1] != 1024)
                throw new InvalidOperationException("Vision literal source channel metadata mismatch: "+id+"/"+component+"/"+colorspace);
            verify_hash(texture.path,texture.sha256);
            var image = new Texture2D(2,2,TextureFormat.RGBA32,false,colorspace == "linear");
            try
            {
                if(!image.LoadImage(File.ReadAllBytes(texture.path)) || image.width != texture.resolution[0] || image.height != texture.resolution[1])
                    throw new InvalidOperationException("Vision decoded channel resolution mismatch: "+texture.path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        internal static void verify_hash(string path,string expected)
        {
            if(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(expected))
                throw new InvalidOperationException("Vision source file/checksum unavailable: "+path);
            using(var stream = File.OpenRead(path))
            using(var hash = SHA256.Create())
            {
                string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                if(!string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Vision approved source checksum mismatch: "+path);
            }
        }

        private static ModelRecord copy_source_record(ModelRecord original)
        {
            var record = JsonUtility.FromJson<ModelRecord>(JsonUtility.ToJson(original));
            string root = ART_ROOT+record.id+"/";
            Directory.CreateDirectory(root+"Textures/");
            record.model_path = copy_verified_file(original.model_path,root+record.id+".fbx",original.model_sha256);
            var channels = record.materials[0].channels;
            foreach(var texture in new[]{channels.base_color,channels.normal_gl,channels.roughness,channels.alpha})
                texture.path = copy_verified_file(texture.path,root+"Textures/"+Path.GetFileName(texture.path),texture.sha256);
            return record;
        }

        internal static string copy_verified_file(string source,string target,string checksum)
        {
            if(!target.StartsWith(ART_ROOT,StringComparison.Ordinal)) throw new InvalidOperationException("Vision copy outside owned Art scope: "+target);
            if(!File.Exists(target)) File.Copy(source,target);
            verify_hash(target,checksum);
            return target;
        }

        private static void assign_vision_clumps(Dictionary<string,GameObject> prefabs,Handoff handoff)
        {
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(VisionWildernessTools.CONFIG_PATH);
            if(!config || !config.vision_layout) throw new InvalidOperationException("Prepare independent Vision config before importing approved clumps.");
            var original = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if(!original || original.grass_variants.Length != 2 || original.shrub_prefabs.Length != 2)
                throw new InvalidOperationException("Frozen baseline two grass/two shrub sources must remain available.");
            config.grass_variants = new[]{original.grass_variants[0],original.grass_variants[1],prefabs["GrassMedium02E"]};
            config.shrub_prefabs = new[]{original.shrub_prefabs[0],original.shrub_prefabs[1],prefabs["Fern02A"],prefabs["Fern02B"]};
            config.grass_source_record = original.grass_source_record+"; "+HANDOFF_PATH+"#GrassMedium02E; sourceSHA="+handoff.source_sha256;
            config.shrub_source_record = original.shrub_source_record+"; "+HANDOFF_PATH+"#Fern02A,Fern02B; sourceSHA="+handoff.source_sha256;
            config.vision_layout.vision_grass_budget = 6000;
            configure_source_beds(config.vision_layout);
            config.vision_layout.version = "vision-1-original-clump-beds-v4";
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(config.vision_layout);
        }

        private static void configure_source_beds(DarkWildernessConfig preset)
        {
            if(preset.vision_plant_patches != null && preset.vision_plant_patches.Length > 0)
            {
                redistribute_existing_grass(preset);
                return;
            }
            var outer_centers = preset.vegetation_centers;
            var outer_radii = preset.vegetation_radii;
            var bed_centers = new[]{new Vector2(.42f,.49f),new Vector2(.60f,.68f),new Vector2(.84f,.63f)};
            var bed_radii = new[]{new Vector2(5.6f,3.8f),new Vector2(5,3.5f),new Vector2(4.2f,3.2f)};
            preset.vision_plant_patches = new[]{
                bed("GrassBermuda","grass",2400,bed_centers,bed_radii,preset.bermuda_scale_range,true),
                bed("GrassMedium","grass",2800,bed_centers,bed_radii,preset.medium_scale_range,true),
                bed("GrassMedium02E","grass",800,outer_centers,outer_radii,new Vector2(1,1.3f),false),
                bed("ShrubA","shrub",10,outer_centers,outer_radii,new Vector2(.85f,1.1f),false),
                bed("ShrubB","shrub",10,outer_centers,outer_radii,new Vector2(.85f,1.1f),false),
                bed("Fern02A","shrub",40,
                    new[]{new Vector2(.405f,.515f),new Vector2(.615f,.675f),new Vector2(.855f,.64f)},
                    new[]{new Vector2(3,2),new Vector2(3,2.2f),new Vector2(2.4f,1.8f)},new Vector2(.85f,1.1f),true),
                bed("Fern02B","shrub",80,
                    new[]{new Vector2(.41f,.47f),new Vector2(.59f,.71f),new Vector2(.835f,.635f)},
                    new[]{new Vector2(3.4f,2.2f),new Vector2(3.6f,2.4f),new Vector2(2.8f,2.3f)},new Vector2(.85f,1.1f),true)
            };
            preset.validate_vision_regions();
        }

        private static void redistribute_existing_grass(DarkWildernessConfig preset)
        {
            var bermuda = VisionPlantLayout.find_patch(preset,"GrassBermuda");
            var medium = VisionPlantLayout.find_patch(preset,"GrassMedium");
            var new_grass = VisionPlantLayout.find_patch(preset,"GrassMedium02E");
            // Only migrate this tool's exact preceding distribution; leave an independently edited preset intact.
            if(bermuda.count != 2000 || medium.count != 600 || new_grass.count != 3400) return;
            bermuda.count = 2400;
            medium.count = 2800;
            bermuda.centers = new_grass.centers;
            bermuda.radii = new_grass.radii;
            medium.centers = new_grass.centers;
            medium.radii = new_grass.radii;
            bermuda.core_bed = true;
            medium.core_bed = true;
            new_grass.count = 800;
            new_grass.centers = preset.vegetation_centers;
            new_grass.radii = preset.vegetation_radii;
            new_grass.core_bed = false;
            preset.validate_vision_regions();
        }

        private static DarkWildernessConfig.PlantPatch bed(string id,string category,int count,Vector2[] centers,Vector2[] radii,Vector2 scale,bool core)
        {
            return new DarkWildernessConfig.PlantPatch{source_id=id,category=category,count=count,centers=centers,radii=radii,scale_range=scale,core_bed=core};
        }
    }
}
