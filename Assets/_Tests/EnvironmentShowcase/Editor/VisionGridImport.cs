using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Frozen Blender integer-ground variants only. No geometry edits or changes to formal building balance.</summary>
    public static class VisionGridImport
    {
        public const string HANDOFF_PATH = "art/grid_footprint_handoff.json";
        private const string ART_ROOT = "Assets/_Tests/EnvironmentShowcase/Art/GridFootprints/";
        private const string EXPORT_ROOT = "art/models/grid-footprints/";

        [Serializable] private sealed class Handoff
        {
            public int schema_version;
            public string status,source_blend,master_sha256;
            public GridAsset[] assets;
        }
        [Serializable] private sealed class GridAsset
        {
            public string id,source_id,export_root_name;
            public float[] footprint_m,expected_unity_bounds_center_m,expected_unity_bounds_size_m;
            public float grid_unit_m,ground_anchor_y_m,unity_ground_offset_m;
            public Export export;
            public ComponentRecord[] components;
        }
        [Serializable] private sealed class Export
        {
            public string file,sha256;
            public bool scale_baked;
            public float unity_placement_scale;
            public SourceMaterial[] materials;
            public Roundtrip roundtrip;
        }
        [Serializable] private sealed class Roundtrip
        {
            public int components,uv_loop_samples;
            public float maximum_vertex_error_m,maximum_uv_error;
            public bool passed;
        }
        [Serializable] private sealed class ComponentRecord
        {
            public string name;
            public string[] material_names;
            public float[] expected_unity_bounds_center_m,expected_unity_bounds_size_m;
        }
        [Serializable] private sealed class SourceMaterial
        {
            public string name,source_material,base_color_texture,base_color_sha256,alpha_mode;
            public float[] base_color_multiplier;
            public float metallic,roughness = .9f,normal_strength;
            public bool double_sided;
            public Channels channels;
        }
        [Serializable] private sealed class Channels { public TextureRecord diff,nor_gl,rough; }
        [Serializable] private sealed class TextureRecord
        {
            public string file,sha256,colorspace;
            public int[] resolution;
        }

        /// <summary>Explicit operation after the specialist confirms the final handoff hash; never discovers draft exports.</summary>
        [MenuItem("Zombie Game/Environment Showcase/Vision-6/Import frozen integer building grounds")]
        public static void import_frozen_grid_buildings()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop the owned preview before importing frozen grid sources.");
            var handoff = read_frozen_handoff();
            VisionDenseTools.prepare_dense_config();
            var sources = new List<EnvironmentShowcaseConfig.SourceAsset>();
            foreach(var asset in handoff.assets) sources.Add(import_grid_asset(asset,handoff));
            VisionDenseTools.apply_frozen_grid_sources(sources.ToArray(),HANDOFF_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log("[VisionGrid] Three frozen metre-scale source variants imported only for v6. Preview grid is 1m; formal construction footprints/balance unchanged. Pasture ground, not bounds.minY, is the shared plane.");
        }

        private static Handoff read_frozen_handoff()
        {
            if(!File.Exists(HANDOFF_PATH)) throw new FileNotFoundException("Wait for the final Blender grid handoff.",HANDOFF_PATH);
            var handoff = JsonUtility.FromJson<Handoff>(File.ReadAllText(HANDOFF_PATH));
            if(handoff == null || handoff.schema_version != 1 || handoff.status != "Source frozen; Unity import and actual player visuals pending" ||
                handoff.source_blend != "art/Blender主文件/ZombieGame.blend" || handoff.assets == null || handoff.assets.Length != 3)
                throw new InvalidOperationException("Frozen grid handoff is incomplete/draft or uses an unexpected source/schema.");
            VisionVegetationImport.verify_hash(handoff.source_blend,handoff.master_sha256);
            var expected = new Dictionary<string,float>{{"Pasture",14},{"Headquarters",12},{"Workshop",8}};
            foreach(var asset in handoff.assets)
            {
                if(asset == null || !expected.TryGetValue(asset.source_id,out float size))
                    throw new InvalidOperationException("Grid source is duplicated or outside the three approved building variants.");
                expected.Remove(asset.source_id);
                verify_asset_record(asset,size);
            }
            return handoff;
        }

        private static void verify_asset_record(GridAsset asset,float size)
        {
            if(string.IsNullOrWhiteSpace(asset.id) || string.IsNullOrWhiteSpace(asset.export_root_name) || asset.id.Contains("/") || asset.id.Contains("..") ||
                asset.grid_unit_m != 1 || asset.footprint_m?.Length != 2 || asset.footprint_m[0] != size || asset.footprint_m[1] != size ||
                asset.expected_unity_bounds_center_m?.Length != 3 || asset.expected_unity_bounds_size_m?.Length != 3 ||
                Mathf.Abs(asset.ground_anchor_y_m) > .00001f || Mathf.Abs(asset.unity_ground_offset_m-.025f) > .00001f ||
                asset.components == null || asset.components.Length < 2 || asset.export == null || !asset.export.scale_baked ||
                asset.export.unity_placement_scale != 1 || asset.export.file != EXPORT_ROOT+asset.id+"/"+asset.id+".fbx" ||
                asset.export.materials == null || asset.export.materials.Length < 2 || asset.export.roundtrip == null || !asset.export.roundtrip.passed ||
                asset.export.roundtrip.components != asset.components.Length || asset.export.roundtrip.maximum_uv_error != 0 ||
                asset.export.roundtrip.maximum_vertex_error_m > .00003f || asset.export.roundtrip.uv_loop_samples <= 0)
                throw new InvalidOperationException("Grid variant units, anchor, source dimensions or roundtrip are incomplete: "+asset.id);
            VisionVegetationImport.verify_hash(asset.export.file,asset.export.sha256);
        }

        private static EnvironmentShowcaseConfig.SourceAsset import_grid_asset(GridAsset asset,Handoff handoff)
        {
            string folder = ART_ROOT+asset.id+"-"+asset.export.sha256.Substring(0,12)+"/";
            Directory.CreateDirectory(folder);
            string path = copy_verified_file(asset.export.file,folder+asset.id+".fbx",asset.export.sha256);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!model) throw new InvalidOperationException("Grid FBX unavailable after import: "+path);
            var instance = UnityEngine.Object.Instantiate(model);
            try
            {
                var bindings = prepare_materials(asset.export.materials,folder);
                var bounds = verify_and_bind_components(instance,asset,bindings);
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance,folder+asset.id+".prefab");
                return new EnvironmentShowcaseConfig.SourceAsset
                {
                    id=asset.source_id,prefab=prefab,native_dimensions=bounds.size,native_bounds_center=bounds.center,native_min_y=bounds.min.y,
                    has_authored_ground_anchor=true,authored_ground_anchor_y=asset.ground_anchor_y_m,authored_ground_offset_y=asset.unity_ground_offset_m,
                    preview_grid_unit_m=asset.grid_unit_m,preview_grid_footprint_m=new Vector2(asset.footprint_m[0],asset.footprint_m[1]),
                    source_record=HANDOFF_PATH+"#"+asset.id+"; masterSHA="+handoff.master_sha256+"; FBXSHA="+asset.export.sha256+
                        "; native metre export scale 1; surface anchor="+asset.ground_anchor_y_m+"; ground lift="+asset.unity_ground_offset_m+
                        "; preview grid only, formal balance unchanged; all source child transforms/material slots retained.",
                    material_bindings=new List<EnvironmentShowcaseConfig.SourceMaterialBinding>(bindings.Values).ToArray()
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static Bounds verify_and_bind_components(GameObject instance,GridAsset asset,Dictionary<string,EnvironmentShowcaseConfig.SourceMaterialBinding> bindings)
        {
            bool root_found = instance.name.Replace("(Clone)","").Trim() == asset.export_root_name;
            foreach(var transform in instance.GetComponentsInChildren<Transform>(true)) root_found |= transform.name == asset.export_root_name;
            if(!root_found) throw new InvalidOperationException("Grid authored export root is missing: "+asset.export_root_name);
            var filters = instance.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length != asset.components.Length || instance.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Grid source component count/collider scope mismatch: "+asset.id);
            var names = new HashSet<string>();
            bool first = true;
            var overall = new Bounds();
            foreach(var filter in filters)
            {
                var record = Array.Find(asset.components,item=>item.name == filter.name);
                if(record == null || !names.Add(record.name) || !filter.sharedMesh || record.material_names == null ||
                    record.material_names.Length != filter.sharedMesh.subMeshCount)
                    throw new InvalidOperationException("Grid mesh/UV/material slot identity mismatch: "+filter.name);
                bool first_vertex = true;
                var bounds = new Bounds();
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if(first_vertex) { bounds = new Bounds(point,Vector3.zero); first_vertex = false; } else bounds.Encapsulate(point);
                    if(first) { overall = new Bounds(point,Vector3.zero); first = false; } else overall.Encapsulate(point);
                }
                compare_bounds(bounds,record.expected_unity_bounds_center_m,record.expected_unity_bounds_size_m,filter.name);
                var renderer = filter.GetComponent<MeshRenderer>();
                if(!renderer) throw new InvalidOperationException("Grid source is missing its original mesh renderer: "+filter.name);
                var materials = new Material[record.material_names.Length];
                bool requires_uv = false;
                for(int index=0;index<materials.Length;index++)
                {
                    if(!bindings.TryGetValue(record.material_names[index],out var binding))
                        throw new InvalidOperationException("Grid declared material is absent: "+record.material_names[index]);
                    materials[index] = binding.material;
                    requires_uv |= binding.base_color || binding.normal || binding.roughness;
                }
                int uv_count = filter.sharedMesh.uv.Length;
                if((requires_uv && uv_count != filter.sharedMesh.vertexCount) || (uv_count != 0 && uv_count != filter.sharedMesh.vertexCount))
                    throw new InvalidOperationException("Grid textured source UVs are missing/malformed; constant-colour crop may retain no UV layer: "+filter.name);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            compare_bounds(overall,asset.expected_unity_bounds_center_m,asset.expected_unity_bounds_size_m,asset.id);
            if(Mathf.Abs(overall.size.x-asset.footprint_m[0]) > .003f || Mathf.Abs(overall.size.z-asset.footprint_m[1]) > .003f ||
                Mathf.Abs(overall.center.x) > .003f || Mathf.Abs(overall.center.z) > .003f)
                throw new InvalidOperationException("Grid ground is not centred on its exact integer footprint: "+asset.id);
            return overall;
        }

        private static void compare_bounds(Bounds bounds,float[] center,float[] size,string name)
        {
            if(center?.Length != 3 || size?.Length != 3) throw new InvalidOperationException("Grid source bounds declaration absent: "+name);
            for(int axis=0;axis<3;axis++)
                if(Mathf.Abs(bounds.center[axis]-center[axis]) > .003f || Mathf.Abs(bounds.size[axis]-size[axis]) > .003f)
                    throw new InvalidOperationException("Grid source axis/scale/bounds mismatch; return to Blender, do not refit: "+name+" actual="+bounds);
        }

        private static Dictionary<string,EnvironmentShowcaseConfig.SourceMaterialBinding> prepare_materials(SourceMaterial[] records,string folder)
        {
            var bindings = new Dictionary<string,EnvironmentShowcaseConfig.SourceMaterialBinding>();
            foreach(var record in records)
            {
                if(record == null || string.IsNullOrWhiteSpace(record.name) || record.name.Contains("/") || record.name.Contains("..") ||
                    (!string.IsNullOrEmpty(record.alpha_mode) && record.alpha_mode != "opaque"))
                    throw new InvalidOperationException("Grid material is unnamed or needs an unsupported alpha adapter.");
                var base_record = record.channels?.diff;
                if(base_record == null && !string.IsNullOrWhiteSpace(record.base_color_texture))
                    base_record = new TextureRecord{file=record.base_color_texture,sha256=record.base_color_sha256,colorspace="sRGB"};
                bool has_base_texture = base_record != null && !string.IsNullOrWhiteSpace(base_record.file);
                if(!has_base_texture && record.base_color_multiplier?.Length != 4)
                    throw new InvalidOperationException("Untextured grid source must declare its original four-channel colour: "+record.name);
                var color = has_base_texture ? import_texture(base_record,folder,false,true) : null;
                var normal = record.channels?.nor_gl == null ? null : import_texture(record.channels.nor_gl,folder,true,false);
                var roughness = record.channels?.rough == null ? null : import_texture(record.channels.rough,folder,false,false);
                var shader = Shader.Find("ZombieGame/EnvironmentShowcase/SourceOpaque");
                if(!shader) throw new InvalidOperationException("Existing opaque source adapter unavailable; no new shader invented.");
                string path = folder+record.name+".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
                material.shader = shader;
                // Null retains the shader's built-in white default; authored constant crop colours stay in _Color.
                material.SetTexture("_MainTex",color);
                var multiplier = record.base_color_multiplier;
                if(multiplier != null && multiplier.Length != 4) throw new InvalidOperationException("Grid original base multiplier invalid: "+record.name);
                material.SetColor("_Color",multiplier == null ? Color.white : new Color(multiplier[0],multiplier[1],multiplier[2],multiplier[3]).gamma);
                material.SetTexture("_BumpMap",normal);
                material.SetFloat("_BumpScale",normal ? record.normal_strength : 0);
                material.SetTexture("_Roughness",roughness);
                material.SetFloat("_HasRoughness",roughness ? 1 : 0);
                material.SetFloat("_RoughnessConstant",record.roughness);
                material.SetFloat("_HasMetallic",0);
                material.SetFloat("_MetallicConstant",record.metallic);
                material.SetFloat("_HasOcclusion",0);
                material.SetFloat("_Cull",record.double_sided ? 0 : 2);
                EditorUtility.SetDirty(material);
                bindings.Add(record.name,new EnvironmentShowcaseConfig.SourceMaterialBinding{material=material,base_color=color,normal=normal,roughness=roughness,
                    import_channels_pass=true,normal_note=!has_base_texture ? "Original constant-colour source material; no base texture exists or is fabricated. Authored linear colour is retained via the adapter gamma upload." :
                        normal ? "Declared nor_gl imported as normal; roughness stays linear and existing shader inverts once." : "Original baked colour and source constants only; no fabricated normal/roughness/height maps."});
            }
            return bindings;
        }

        private static Texture2D import_texture(TextureRecord record,string folder,bool normal,bool srgb)
        {
            if(record == null || string.IsNullOrWhiteSpace(record.file) || !record.file.StartsWith(EXPORT_ROOT,StringComparison.Ordinal) ||
                record.sha256?.Length != 64 || (srgb ? record.colorspace != "sRGB" : record.colorspace != "Non-Color"))
                throw new InvalidOperationException("Grid source texture channel/colour space declaration invalid.");
            Directory.CreateDirectory(folder+"Textures/");
            string path = copy_verified_file(record.file,folder+"Textures/"+record.sha256.Substring(0,12)+"-"+Path.GetFileName(record.file),record.sha256);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = record.resolution?.Length == 2 ? Mathf.NextPowerOfTwo(Mathf.Max(record.resolution[0],record.resolution[1])) : 4096;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string copy_verified_file(string source,string target,string checksum)
        {
            if(!target.StartsWith(ART_ROOT,StringComparison.Ordinal) || target.Contains(".."))
                throw new InvalidOperationException("Grid copy outside owned preview art scope: "+target);
            VisionVegetationImport.verify_hash(source,checksum);
            if(!File.Exists(target)) File.Copy(source,target);
            VisionVegetationImport.verify_hash(target,checksum);
            return target;
        }
    }
}
