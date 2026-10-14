using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Explicit approved source handoff import, confined to independent showcase art.</summary>
    public static class VegetationShowcaseImport
    {
        private const string HANDOFF_PATH = "art/environment/vegetation_handoff.json";
        private const string GENERATED_ROOT = "Assets/_Tests/EnvironmentShowcase/Art/VegetationGenerated/";
        [Serializable] private sealed class Handoff { public string status, source_blend, source_sha256; public ModelRecord[] records; }
        [Serializable] internal sealed class ModelRecord
        {
            public string id, model_path, model_sha256, source_scene, source_collection, source_object, axis_forward, axis_up;
            public string[] material_slots;
            public float[] dimensions_unity_xyz_m;
            public float[] pivot_bounds_min;
            public string license;
            public MaterialRecord[] materials;
            public int triangles, lod_index;
            public float lod_screen_relative_transition, placement_scale, unit_m;
        }
        [Serializable] internal sealed class MaterialRecord
        {
            public string name, alpha_mode;
            public string normal_convention;
            public float alpha_cutoff, metallic;
            public bool double_sided;
            public ChannelRecord channels;
        }
        [Serializable] internal sealed class ChannelRecord { public TextureRecord base_color, normal_gl, roughness, alpha; }
        [Serializable] internal sealed class TextureRecord { public string path, sha256, colorspace, component; public int[] resolution; }

        [MenuItem("Zombie Game/Environment Showcase/Import approved vegetation handoff")]
        public static void import_approved_handoff()
        {
            import_handoff(HANDOFF_PATH, false);
        }

        /// <summary>User-approved environment category source; usage remains confined to this independent test.</summary>
        public static void import_root_approved_test_candidate()
        {
            import_handoff("art/environment/vegetation_handoff_candidate.json", true);
        }

        private static void import_handoff(string handoff_path, bool is_test_candidate)
        {
            if (!File.Exists(handoff_path)) throw new FileNotFoundException("Root-approved vegetation handoff is required.", handoff_path);
            var handoff = JsonUtility.FromJson<Handoff>(File.ReadAllText(handoff_path));
            bool accepted_status = handoff != null && (is_test_candidate ?
                handoff.status == "preview_approved_not_formal_source" || handoff.status == "formal_category_source_test_only" : handoff.status == "approved");
            if (handoff == null || handoff.records == null || !accepted_status)
                throw new InvalidOperationException("Vegetation handoff status mismatch: " + handoff_path);
            verify_file(handoff.source_blend, handoff.source_sha256);
            foreach (var record in handoff.records) validate_record(record);
            var prefabs = new Dictionary<string, GameObject>();
            var material_cache = new Dictionary<string, Material>();
            Directory.CreateDirectory(GENERATED_ROOT);
            foreach (var record in handoff.records)
            {
                prefabs.Add(record.id, prepare_prefab(record, material_cache));
            }
            assign_config(prefabs, handoff.records, handoff_path, is_test_candidate, handoff.source_blend);
            AssetDatabase.SaveAssets();
            Debug.Log("[EnvironmentShowcaseImport] Root-approved " + (is_test_candidate ? "independent TEST CANDIDATE" : "handoff") +
                ", source meshes retained, alpha/GL normals/linear roughness packed once; config assigned " + handoff_path);
        }

        /// <summary>Refresh traceable formal-category provenance without repacking or modifying any source mesh.</summary>
        public static void refresh_root_test_source_provenance()
        {
            const string path = "art/environment/vegetation_handoff_candidate.json";
            if (!File.Exists(path)) throw new FileNotFoundException("Environment category source handoff missing.", path);
            var handoff = JsonUtility.FromJson<Handoff>(File.ReadAllText(path));
            if (handoff == null || handoff.status != "formal_category_source_test_only" || handoff.records == null)
                throw new InvalidOperationException("Prepared player requires the user's formal category source, test-only status: " + path);
            verify_file(handoff.source_blend, handoff.source_sha256);
            foreach (var record in handoff.records) validate_record(record);
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if (!config) throw new InvalidOperationException("Prepared showcase config missing.");
            assign_source_records(config, path, true, handoff.source_blend);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static void validate_record(ModelRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.id) || record.id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                record.id.Contains("/") || record.id.Contains("\\")) throw new InvalidOperationException("Vegetation source ID invalid.");
            if (string.IsNullOrWhiteSpace(record.model_path) || !record.model_path.StartsWith("Assets/_Tests/EnvironmentShowcase/Art/Vegetation/", StringComparison.Ordinal) ||
                record.axis_forward != "-Z" || record.axis_up != "Y" || record.unit_m != 1 || record.placement_scale != 1)
                throw new InvalidOperationException("Vegetation source requires scoped FBX, once-baked Y-up metres, scale 1: " + record.id);
            if (record.materials == null || record.material_slots == null || string.IsNullOrWhiteSpace(record.source_object) ||
                record.dimensions_unity_xyz_m == null || record.dimensions_unity_xyz_m.Length != 3)
                throw new InvalidOperationException("Vegetation source provenance/materials missing: " + record.id);
            verify_file(record.model_path, record.model_sha256);
            foreach (var material in record.materials)
            {
                if (material == null || string.IsNullOrWhiteSpace(material.name) || material.channels == null ||
                    !has_texture(material.channels.base_color) || !has_texture(material.channels.normal_gl) || !has_texture(material.channels.roughness))
                    throw new InvalidOperationException("Vegetation PBR channels missing: " + record.id + "/" + material?.name);
                verify_texture(material.channels.base_color);
                verify_texture(material.channels.normal_gl);
                verify_texture(material.channels.roughness);
                // JsonUtility may hydrate an absent/null reference as an empty nested record.
                // Empty opaque alpha is absence, never a fabricated image or a mask inference.
                if (!has_texture(material.channels.alpha))
                {
                    if (material.channels.alpha != null && !string.IsNullOrWhiteSpace(material.channels.alpha.sha256))
                        throw new InvalidOperationException("Alpha metadata has checksum but no actual path: " + record.id + "/" + material.name);
                    material.channels.alpha = null;
                }
                else verify_texture(material.channels.alpha);
                if (material.alpha_mode != "opaque" && material.alpha_mode != "cutout")
                    throw new InvalidOperationException("Unsupported source alpha mode: " + record.id + "/" + material.alpha_mode);
                if (material.alpha_mode == "cutout" && !has_texture(material.channels.alpha))
                    throw new InvalidOperationException("Vegetation alpha cutout requires actual source alpha: " + record.id + "/" + material.name);
                if (material.metallic != 0) throw new InvalidOperationException("Nonmetal vegetation metallic must be zero: " + material.name);
            }
        }

        private static bool has_texture(TextureRecord texture)
        {
            return texture != null && !string.IsNullOrWhiteSpace(texture.path);
        }

        private static void verify_texture(TextureRecord texture)
        {
            if (!has_texture(texture) || !texture.path.StartsWith("Assets/_Tests/EnvironmentShowcase/Art/", StringComparison.Ordinal))
                throw new InvalidOperationException("Vegetation texture outside approved showcase art scope.");
            verify_file(texture.path, texture.sha256);
        }

        private static void verify_file(string path, string expected_sha256)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("Approved handoff source missing.", path);
            if (string.IsNullOrWhiteSpace(expected_sha256)) throw new InvalidOperationException("Approved handoff has no source checksum: " + path);
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
            {
                string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, expected_sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Approved vegetation source checksum mismatch: " + path);
            }
        }

        internal static GameObject prepare_prefab(ModelRecord record, Dictionary<string, Material> material_cache,string generated_root=GENERATED_ROOT,bool strict_geometry=false,bool use_native_file_units=false,bool keep_readable=false)
        {
            AssetDatabase.ImportAsset(record.model_path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(record.model_path);
            importer.globalScale = 1;
            importer.useFileScale = use_native_file_units;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            if(strict_geometry) importer.importNormals = ModelImporterNormals.Import;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var materials = new Dictionary<string, Material>();
            foreach (var material in record.materials)
            {
                string key = material.channels.base_color.sha256 + material.channels.normal_gl.sha256 + material.channels.roughness.sha256 +
                    (material.channels.alpha == null ? "opaque" : material.channels.alpha.sha256) + material.alpha_cutoff + material.double_sided;
                if (!material_cache.TryGetValue(key, out var prepared))
                {
                    prepared = prepare_material(record.id, material,generated_root,strict_geometry ? 1 : .35f);
                    material_cache.Add(key, prepared);
                }
                materials.Add(material.name, prepared);
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(record.model_path);
            if (!source) throw new InvalidOperationException("Cannot load approved FBX: " + record.model_path);
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                instance.name = record.id;
                verify_imported_bounds(instance, record,strict_geometry);
                if(strict_geometry) verify_source_geometry(instance,record);
                if(use_native_file_units)
                    Debug.Log("[VisionSourceUnits] "+record.id+": ModelImporter.globalScale=1; useFileScale=true; FBX declared unit conversion applied once. Exact source dimensions verified, no bounds refit or vertex edits.");
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || !filter.sharedMesh) continue;
                    if (record.material_slots.Length != filter.sharedMesh.subMeshCount)
                        throw new InvalidOperationException("Source submesh/material slot count mismatch: " + record.id + "/" + filter.name);
                    var assigned = new Material[record.material_slots.Length];
                    for (int index = 0; index < assigned.Length; index++)
                    {
                        if (!materials.TryGetValue(record.material_slots[index], out assigned[index]))
                            throw new InvalidOperationException("Source material slot unmapped: " + record.id + "/" + record.material_slots[index]);
                    }
                    renderer.sharedMaterials = assigned;
                }
                return PrefabUtility.SaveAsPrefabAsset(instance, generated_root + record.id + ".prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                importer.isReadable = keep_readable;
                importer.SaveAndReimport();
            }
        }

        private static void verify_source_geometry(GameObject instance,ModelRecord record)
        {
            int triangles = 0;
            foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh || filter.sharedMesh.uv.Length != filter.sharedMesh.vertexCount)
                    throw new InvalidOperationException("Vision source mesh/UV missing: "+record.id);
                triangles += filter.sharedMesh.triangles.Length/3;
            }
            if(triangles != record.triangles || instance.GetComponentsInChildren<LODGroup>(true).Length != 0)
                throw new InvalidOperationException("Vision single-level original source geometry mismatch: "+record.id+" triangles="+triangles+" expected="+record.triangles);
        }

        private static void verify_imported_bounds(GameObject instance, ModelRecord record,bool strict_geometry=false)
        {
            bool has_vertex = false;
            var bounds = new Bounds();
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                if (!filter.sharedMesh.isReadable) throw new InvalidOperationException("Exact source unit/axis validation requires temporary readable mesh: " + record.id);
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    Vector3 world_vertex = filter.transform.TransformPoint(vertex);
                    if (!has_vertex) { bounds = new Bounds(world_vertex, Vector3.zero); has_vertex = true; }
                    else bounds.Encapsulate(world_vertex);
                }
            }
            if (!has_vertex) throw new InvalidOperationException("Imported source has no vertices: " + record.id);
            var expected = new Vector3(record.dimensions_unity_xyz_m[0], record.dimensions_unity_xyz_m[1], record.dimensions_unity_xyz_m[2]);
            for (int axis = 0; axis < 3; axis++)
                if (Mathf.Abs(bounds.size[axis] - expected[axis]) > Mathf.Max(.001f, expected[axis] * (strict_geometry ? .002f : .02f)))
                    throw new InvalidOperationException("Approved source Unity axis/unit size mismatch; do not refit mesh: " + record.id +
                        ", actual=" + bounds.size + ", expected=" + expected);
            if (Mathf.Abs(bounds.min.y) > .003f)
                throw new InvalidOperationException("Approved source root does not rest at ground; return source to Blender: " + record.id + ", bounds min.y=" + bounds.min.y);
        }

        private static Material prepare_material(string source_id, MaterialRecord record,string generated_root=GENERATED_ROOT,float smoothness_multiplier=.35f)
        {
            string safe_name = record.name.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
            string stem = generated_root + source_id + "_" + safe_name;
            pack_pbr_channels(record.channels, stem, record.alpha_cutoff,smoothness_multiplier);
            prepare_texture(record.channels.normal_gl.path, true, false);
            if(smoothness_multiplier == 1)
            {
                var normal_importer = (TextureImporter)AssetImporter.GetAtPath(record.channels.normal_gl.path);
                normal_importer.flipGreenChannel = false;
                normal_importer.SaveAndReimport();
            }
            prepare_texture(record.channels.roughness.path, false, false);
            if (record.channels.alpha != null) prepare_texture(record.channels.alpha.path, false, false);
            prepare_texture(stem + "_albedo_alpha.png", false, true, record.alpha_mode == "cutout", record.alpha_cutoff,generated_root);
            prepare_texture(stem + "_metallic_smoothness.png", false, false);
            string material_path = stem + ".mat";
            var shader = Shader.Find("ZombieGame/EnvironmentShowcase/VegetationPbr");
            if (!shader) throw new InvalidOperationException("Showcase Built-in vegetation PBR shader unavailable.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(material_path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, material_path); }
            material.shader = shader;
            material.enableInstancing = true;
            material.SetColor("_Color", Color.white);
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_albedo_alpha.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(record.channels.normal_gl.path));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_metallic_smoothness.png"));
            material.SetFloat("_Cutoff", record.alpha_mode == "opaque" ? 0 : record.alpha_cutoff);
            material.SetFloat("_Cull", record.double_sided ? 0 : 2);
            material.SetFloat("_BumpScale", record.alpha_mode == "cutout" ? .2f : .3f);
            material.SetFloat("_SpecularStrength", record.alpha_mode == "cutout" ? .008f : .018f);
            material.SetFloat("_SmoothnessCap", record.alpha_mode == "cutout" ? .08f : .15f);
            material.SetFloat("_NormalFadeStart", 20);
            material.SetFloat("_NormalFadeEnd", 65);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void prepare_texture(string path, bool is_normal, bool is_color, bool is_cutout = false, float cutoff = .5f,string generated_root=GENERATED_ROOT)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = is_normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = is_color;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = is_color;
            if (path.StartsWith(generated_root, StringComparison.Ordinal) && path.EndsWith("_albedo_alpha.png", StringComparison.Ordinal))
            {
                importer.mipMapsPreserveCoverage = is_cutout;
                importer.alphaTestReferenceValue = cutoff;
            }
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        [MenuItem("Zombie Game/Environment Showcase/Apply generated cutout mip coverage only")]
        public static void apply_existing_coverage()
        {
            int changed_count = 0;
            var texture_paths = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { GENERATED_ROOT.TrimEnd('/') }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material || !material.HasProperty("_Cutoff") || material.GetFloat("_Cutoff") <= 0 || !material.HasProperty("_MainTex")) continue;
                var texture = material.GetTexture("_MainTex");
                string path = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(GENERATED_ROOT, StringComparison.Ordinal) ||
                    !path.EndsWith("_albedo_alpha.png", StringComparison.Ordinal) || !texture_paths.Add(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new InvalidOperationException("Generated cutout texture importer missing: " + path);
                float cutoff = material.GetFloat("_Cutoff");
                if (importer.mipMapsPreserveCoverage && Mathf.Abs(importer.alphaTestReferenceValue - cutoff) < .0001f) continue;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = cutoff;
                importer.SaveAndReimport();
                changed_count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[EnvironmentShowcaseImport] Generated cutout mip coverage preserved at source cutoff; changed textures=" + changed_count + "; no source/global texture importers changed.");
        }

        private static Texture2D decode_texture(TextureRecord record, bool linear)
        {
            if (!has_texture(record)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            if (!texture.LoadImage(File.ReadAllBytes(record.path)))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException("Cannot decode source vegetation channel: " + record.path);
            }
            return texture;
        }

        private static void pack_pbr_channels(ChannelRecord channels, string stem, float cutoff,float smoothness_multiplier=.35f)
        {
            var color = decode_texture(channels.base_color, false);
            var rough = decode_texture(channels.roughness, true);
            var alpha = decode_texture(channels.alpha, true);
            Texture2D metallic = null;
            try
            {
                if (color.width != rough.width || color.height != rough.height ||
                    (alpha && (color.width != alpha.width || color.height != alpha.height)))
                    throw new InvalidOperationException("Vegetation source PBR channel dimensions mismatch: " + stem);
                var pixels = color.GetPixels32();
                var rough_pixels = rough.GetPixels32();
                var alpha_pixels = alpha ? alpha.GetPixels32() : null;
                var packed_pixels = new Color32[pixels.Length];
                for (int index = 0; index < pixels.Length; index++)
                {
                    pixels[index].a = alpha_pixels == null ? (byte)255 : alpha_pixels[index].r;
                    packed_pixels[index] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt((255 - rough_pixels[index].r) * smoothness_multiplier));
                }
                if (alpha) pad_cutout_colors(pixels, color.width, color.height, cutoff);
                color.SetPixels32(pixels);
                color.Apply();
                metallic = new Texture2D(color.width, color.height, TextureFormat.RGBA32, false, true);
                metallic.SetPixels32(packed_pixels);
                metallic.Apply();
                File.WriteAllBytes(stem + "_albedo_alpha.png", color.EncodeToPNG());
                File.WriteAllBytes(stem + "_metallic_smoothness.png", metallic.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(color);
                UnityEngine.Object.DestroyImmediate(rough);
                if (alpha) UnityEngine.Object.DestroyImmediate(alpha);
                if (metallic) UnityEngine.Object.DestroyImmediate(metallic);
            }
        }

        /// <summary>Bleed nearest visible source RGB into discarded texels only. Visible RGB and every alpha byte stay unchanged.</summary>
        private static void pad_cutout_colors(Color32[] pixels, int width, int height, float cutoff)
        {
            int threshold = Mathf.CeilToInt(cutoff * 255);
            var assigned = new bool[pixels.Length];
            var queue = new Queue<int>();
            for (int index = 0; index < pixels.Length; index++)
                if (pixels[index].a >= threshold) { assigned[index] = true; queue.Enqueue(index); }
            if (queue.Count == 0) throw new InvalidOperationException("Source cutout has no visible texels at cutoff " + cutoff);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % width, y = current / width;
                if (x > 0) bleed_neighbor(current, current - 1, pixels, assigned, queue);
                if (x + 1 < width) bleed_neighbor(current, current + 1, pixels, assigned, queue);
                if (y > 0) bleed_neighbor(current, current - width, pixels, assigned, queue);
                if (y + 1 < height) bleed_neighbor(current, current + width, pixels, assigned, queue);
            }
        }

        private static void bleed_neighbor(int source, int neighbor, Color32[] pixels, bool[] assigned, Queue<int> queue)
        {
            if (assigned[neighbor]) return;
            assigned[neighbor] = true;
            byte alpha = pixels[neighbor].a;
            pixels[neighbor] = new Color32(pixels[source].r, pixels[source].g, pixels[source].b, alpha);
            queue.Enqueue(neighbor);
        }

        private static void assign_config(Dictionary<string, GameObject> prefabs, ModelRecord[] records, string handoff_path, bool is_test_candidate, string source_blend)
        {
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if (!config) throw new InvalidOperationException("Create independent showcase scene/config before vegetation import.");
            config.grass_variants = new[] { require_prefab(prefabs, "GrassBermuda"), require_prefab(prefabs, "GrassMedium") };
            config.shrub_prefabs = new[] { require_prefab(prefabs, "ShrubA"), require_prefab(prefabs, "ShrubB") };
            config.tree_prefabs = new[] { create_tree_lod(prefabs, records) };
            if (prefabs.TryGetValue("RockNatural", out var rock))
            {
                config.rock_prefabs = new[] { rock };
                config.rock_count = 180;
                config.rock_scale_range = new Vector2(2, 4);
                config.rock_source_record = handoff_path + " (source=" + source_blend + "; independent test only)#RockNatural; uniform placement scale 2–4, no mesh edits";
            }
            config.grass_count = 24000;
            assign_source_records(config, handoff_path, is_test_candidate, source_blend);
            config.validate_config();
            EditorUtility.SetDirty(config);
        }

        private static void assign_source_records(EnvironmentShowcaseConfig config, string handoff_path, bool is_test_candidate, string source_blend)
        {
            string source_prefix = handoff_path + " (source=" + source_blend + (is_test_candidate ? "; independent test only)" : ")");
            config.grass_source_record = source_prefix + "#GrassBermuda,GrassMedium";
            config.shrub_source_record = source_prefix + "#ShrubA,ShrubB";
            config.tree_source_record = source_prefix + "#TreeSmall_LOD0,TreeSmall_LOD1";
        }

        private static GameObject require_prefab(Dictionary<string, GameObject> prefabs, string id)
        {
            if (!prefabs.TryGetValue(id, out var prefab) || !prefab) throw new InvalidOperationException("Approved handoff required source absent: " + id);
            return prefab;
        }

        private static GameObject create_tree_lod(Dictionary<string, GameObject> prefabs, ModelRecord[] records)
        {
            var root = new GameObject("TreeSmall approved source LOD assembly");
            try
            {
                var lods = new LOD[2];
                for (int index = 0; index < lods.Length; index++)
                {
                    string id = "TreeSmall_LOD" + index;
                    var source = require_prefab(prefabs, id);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    instance.transform.SetParent(root.transform, false);
                    ModelRecord record = Array.Find(records, item => item.id == id);
                    if (record.lod_index != index || record.lod_screen_relative_transition <= 0)
                        throw new InvalidOperationException("Approved source LOD metadata invalid: " + id);
                    lods[index] = new LOD(record.lod_screen_relative_transition, instance.GetComponentsInChildren<Renderer>(true));
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(root, GENERATED_ROOT + "TreeSmall.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
