using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;
using TextureRecord = ZombieGame.Tests.EditorTools.VegetationShowcaseImport.TextureRecord;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Two untouched author oak LOD assemblies; CC BY-SA attribution, source atlas and constant roughness only.</summary>
    public static class VisionOakImport
    {
        private const string HANDOFF_PATH = "art/environment/vision-oak-handoff.json";
        private const string SOURCE_ROOT = "art/environment/vision-sources/OakLauris71/";
        private const string ART_ROOT = "Assets/_Tests/EnvironmentShowcase/Art/VisionVegetation/OakLauris71/";
        [Serializable] private sealed class Handoff
        {
            public int schema_version;
            public string status,source_blend,source_sha256,license,attribution;
            public Record[] records;
        }
        [Serializable] private sealed class Record
        {
            public string id,model_path,sha256,axis_forward,axis_up,source_collection,source_root;
            public int triangles,piece_count,lod_index;
            public float unit_m,placement_scale,lod_screen_relative_transition,unity_global_scale,fbx_unit_scale_factor;
            public bool unity_use_file_scale;
            public float[] dimensions_unity_xyz_m,pivot_bounds_min;
            public string[] source_objects,material_slots;
            public Piece[] pieces;
            public SourceMaterial[] materials;
        }
        [Serializable] private sealed class Piece { public string name; public int triangles; }
        [Serializable] private sealed class SourceMaterial
        {
            public string name,alpha_mode;
            public float alpha_cutoff,roughness_constant,smoothness_constant,metallic;
            public bool double_sided;
            public Channels channels;
        }
        [Serializable] private sealed class Channels { public TextureRecord base_color,alpha; }

        public static void import_approved_oak()
        {
            var handoff = read_handoff();
            Directory.CreateDirectory(ART_ROOT+"Textures/");
            var prefabs = new GameObject[2];
            for(int index=0;index<2;index++) prefabs[index] = prepare_author_lod(handoff.records[index]);
            string attribution_target = ART_ROOT+"ATTRIBUTION.txt";
            if(!File.Exists(attribution_target)) File.Copy(handoff.attribution,attribution_target);
            var oak = create_lod_wrapper(prefabs,handoff.records);
            assign_vision_oak(oak,handoff);
            AssetDatabase.SaveAssets();
            Debug.Log("[VisionOak] 5303/842 triangles, 36 original pieces per LOD; native metre units, unchanged shared author basis. CC BY-SA 3.0, normal/roughness/AO textures unavailable. Sparse oak preview, not dense forest acceptance.");
        }

        private static Handoff read_handoff()
        {
            if(!File.Exists(HANDOFF_PATH)) throw new FileNotFoundException("Complete approved oak source required.",HANDOFF_PATH);
            var handoff = JsonUtility.FromJson<Handoff>(File.ReadAllText(HANDOFF_PATH));
            if(handoff == null || handoff.schema_version != 1 || handoff.status != "approved_sparse_oak_local_preview_not_dense_forest_acceptance" ||
                handoff.license != "CC-BY-SA-3.0" || handoff.records == null || handoff.records.Length != 2 || !File.Exists(handoff.attribution))
                throw new InvalidOperationException("Oak source status/license/attribution mismatch: "+HANDOFF_PATH);
            VisionVegetationImport.verify_hash(handoff.source_blend,handoff.source_sha256);
            for(int index=0;index<2;index++) verify_record(handoff.records[index],index);
            return handoff;
        }

        private static void verify_record(Record record,int index)
        {
            string id = "OakLauris71_LOD"+index;
            if(record == null || record.id != id || record.model_path != SOURCE_ROOT+id+".fbx" || record.triangles != (index == 0 ? 5303 : 842) ||
                record.piece_count != 36 || record.source_objects == null || record.source_objects.Length != 36 || record.pieces == null || record.pieces.Length != 36 ||
                record.axis_forward != "-Z" || record.axis_up != "Y" || record.unit_m != 1 || record.placement_scale != 1 || !record.unity_use_file_scale ||
                record.unity_global_scale != 1 || record.fbx_unit_scale_factor != 100 || record.lod_index != index || record.lod_screen_relative_transition <= 0 ||
                record.dimensions_unity_xyz_m == null || record.dimensions_unity_xyz_m.Length != 3 || record.pivot_bounds_min == null || record.pivot_bounds_min.Length != 3 ||
                record.materials == null || record.materials.Length != 1 || record.material_slots == null || record.material_slots.Length != 1)
                throw new InvalidOperationException("Oak original assembly/units/LOD metadata mismatch: "+id);
            VisionVegetationImport.verify_hash(record.model_path,record.sha256);
            var material = record.materials[0];
            if(material == null || material.name != record.material_slots[0] || material.alpha_mode != "cutout" || material.alpha_cutoff != .5f || !material.double_sided ||
                material.metallic != 0 || material.roughness_constant != .8f || material.smoothness_constant != .2f || material.channels == null ||
                material.channels.base_color == null || material.channels.alpha == null || material.channels.base_color.resolution == null ||
                material.channels.base_color.resolution.Length != 2 || !material.channels.base_color.path.StartsWith(SOURCE_ROOT+"Textures/",StringComparison.Ordinal) ||
                material.channels.alpha.path != material.channels.base_color.path || material.channels.alpha.component != "alpha" || material.channels.base_color.colorspace != "sRGB")
                throw new InvalidOperationException("Oak original RGBA/constant roughness declaration mismatch: "+id);
            VisionVegetationImport.verify_hash(material.channels.base_color.path,material.channels.base_color.sha256);
        }

        private static GameObject prepare_author_lod(Record record)
        {
            string path = VisionVegetationImport.copy_verified_file(record.model_path,ART_ROOT+record.id+".fbx",record.sha256);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var material = prepare_atlas_material(record);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!source) throw new InvalidOperationException("Oak original FBX unavailable after import: "+path);
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                // Never reset, center or rescale the imported root or its 36 authored children.
                verify_imported_assembly(instance,record);
                foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterials = new[]{material};
                return PrefabUtility.SaveAsPrefabAsset(instance,ART_ROOT+record.id+".prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static void verify_imported_assembly(GameObject instance,Record record)
        {
            var filters = instance.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length != 36 || instance.GetComponentsInChildren<LODGroup>(true).Length != 0)
                throw new InvalidOperationException("Oak must retain 36 source meshes and no added LOD: "+record.id);
            var parts = new Dictionary<string,int>();
            foreach(var piece in record.pieces) parts.Add(piece.name,piece.triangles);
            int triangles = 0;
            bool first = true;
            var bounds = new Bounds();
            foreach(var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if(!mesh || !parts.TryGetValue(filter.name,out var expected) || mesh.subMeshCount != 1 || mesh.triangles.Length/3 != expected || mesh.uv.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Oak original piece/UV/submesh mismatch: "+record.id+"/"+filter.name);
                triangles += mesh.triangles.Length/3;
                foreach(var vertex in mesh.vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if(first) { bounds = new Bounds(point,Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            if(triangles != record.triangles || Mathf.Abs(bounds.min.y-record.pivot_bounds_min[1]) > .001f)
                throw new InvalidOperationException("Oak exact source triangles/shared grounding mismatch: "+record.id);
            for(int axis=0;axis<3;axis++)
                if(Mathf.Abs(bounds.size[axis]-record.dimensions_unity_xyz_m[axis]) > .002f)
                    throw new InvalidOperationException("Oak exact transformed vertex dimensions mismatch: "+record.id+" actual="+bounds.size);
        }

        private static Material prepare_atlas_material(Record record)
        {
            var channel = record.materials[0].channels.base_color;
            string path = VisionVegetationImport.copy_verified_file(channel.path,ART_ROOT+"Textures/"+Path.GetFileName(channel.path),channel.sha256);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = channel.resolution[0];
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = .5f;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            string material_path = ART_ROOT+record.material_slots[0]+".mat";
            var shader = Shader.Find("ZombieGame/EnvironmentShowcase/VegetationPbr");
            if(!shader) throw new InvalidOperationException("Built-in vegetation shader missing for source atlas.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(material_path);
            if(!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,material_path); }
            material.shader = shader;
            material.enableInstancing = true;
            material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetColor("_Color",Color.white);
            material.SetFloat("_Cull",0);
            material.SetFloat("_Cutoff",.5f);
            material.SetFloat("_UseConstantSmoothness",1);
            material.SetFloat("_ConstantSmoothness",.2f);
            material.SetFloat("_SmoothnessCap",.2f);
            material.SetFloat("_HasNormal",0);
            material.SetFloat("_SpecularStrength",.008f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject create_lod_wrapper(GameObject[] prefabs,Record[] records)
        {
            var root = new GameObject("OakLauris71");
            try
            {
                var lods = new LOD[2];
                for(int index=0;index<2;index++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[index]);
                    instance.transform.SetParent(root.transform,false);
                    lods[index] = new LOD(records[index].lod_screen_relative_transition,instance.GetComponentsInChildren<Renderer>(true));
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(root,ART_ROOT+"OakLauris71.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void assign_vision_oak(GameObject prefab,Handoff handoff)
        {
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(VisionWildernessTools.CONFIG_PATH);
            if(!config || !config.vision_layout) throw new InvalidOperationException("Independent Vision preset required for sparse oak preview.");
            var preset = config.vision_layout;
            preset.tree_prefabs = new[]{prefab};
            preset.tree_source = HANDOFF_PATH+"; Lauris Kaplinski, CC BY-SA 3.0; sparse original oak crown, not dense-canopy art acceptance.";
            preset.layout.tree_budget = 32;
            preset.vision_tree_spacing = 5.8f;
            preset.vision_tree_centers = new[]{new Vector2(.43f,.78f),new Vector2(.83f,.36f),new Vector2(.89f,.73f)};
            preset.vision_tree_radii = new[]{new Vector2(15,15),new Vector2(16,15),new Vector2(12,16)};
            config.tree_scale_range = new Vector2(.65f,.95f);
            config.tree_source_record = HANDOFF_PATH+"; sourceSHA="+handoff.source_sha256+"; native original LOD0/1 5303/842 triangles, CC BY-SA 3.0, source atlas/constant roughness only";
            preset.validate_vision_regions();
            EditorUtility.SetDirty(preset.layout);
            EditorUtility.SetDirty(preset);
            EditorUtility.SetDirty(config);
        }
    }
}
