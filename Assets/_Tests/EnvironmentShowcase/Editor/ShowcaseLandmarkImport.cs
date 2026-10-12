using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Only parent-approved neutral source assemblies; preserve all local transforms and UVs.</summary>
    public static class ShowcaseLandmarkImport
    {
        private const string GENERATED_ROOT = "Assets/_Tests/EnvironmentShowcase/Art/SetDressingGenerated/";
        private const string COURTYARD_DEGENERATE_FBX_SHA256 = "a62b35ed830786e8a056e73c1fb0a94cb476deb210da419afca59c307f15a082";
        [Serializable] private sealed class Handoff { public string status, source_blend, source_sha256; public Record[] records; }
        [Serializable] private sealed class Record
        {
            public string id, model_path, model_sha256, source_scene, source_collection, axis_forward, axis_up;
            public float unit_m, placement_scale;
            public int triangles;
            public int source_triangles, effective_triangles, degenerate_triangle_count;
            public string degenerate_triangle_exact_fbx_sha256;
            public float[] dimensions_unity_xyz_m;
            public Piece[] pieces;
            public MaterialRecord[] materials;
        }
        [Serializable] private sealed class Piece { public string name; public string[] materials; }
        [Serializable] private sealed class MaterialRecord
        {
            public string name, alpha_mode;
            public bool double_sided;
            public float metallic, roughness_constant_if_missing, normal_strength;
            public float metallic_multiplier;
            public string normal_convention, normal_preview_assumption;
            public bool normal_visual_check_required;
            public float[] base_color_tint_linear;
            public Channels channels;
        }
        [Serializable] private sealed class Channels { public TextureRecord base_color, normal_gl, normal, roughness, metallic; }
        [Serializable] private sealed class TextureRecord { public string path, sha256, colorspace, component; }

        public static void prepare_route_sources()
        {
            Directory.CreateDirectory(GENERATED_ROOT);
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if (!config) throw new InvalidOperationException("Prepare independent showcase scene/config before source import.");
            config.landmark_sources = import_sources("art/environment/landmark_handoff.json",
                "parent_approved_preview_source_pending_unity_visual_acceptance", "Landmarks", new[] { "EuropeanStoneHouse", "NorthAfricanRuins", "MiddleEasternCourtyard" });
            config.ore_sources = import_sources("art/resources/handoff.json", "parent_approved_preview_source_pending_unity_visual_acceptance",
                "Resources", new[] { "IronOreRock", "GoldOreRock" });
            config.route_layout ??= new EnvironmentShowcaseConfig.RouteLayout();
            config.route_layout.enabled = true;
            config.validate_config();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShowcaseLandmarks] Five approved source assemblies imported; whole scale 1; mixed licenses, user ores unverified/private only. No mining or blocking.");
        }

        private static EnvironmentShowcaseConfig.SourceAsset[] import_sources(string path, string status, string category, string[] expected_ids)
        {
            var handoff = JsonUtility.FromJson<Handoff>(File.ReadAllText(path));
            if (handoff == null || handoff.status != status || handoff.records == null || handoff.records.Length != expected_ids.Length)
                throw new InvalidOperationException("Parent-approved showcase handoff status/count mismatch: " + path);
            verify_file(handoff.source_blend, handoff.source_sha256);
            var sources = new EnvironmentShowcaseConfig.SourceAsset[expected_ids.Length];
            for (int index = 0; index < sources.Length; index++)
            {
                var record = Array.Find(handoff.records, candidate => candidate.id == expected_ids[index]);
                validate_record(record, category);
                var prefab = prepare_prefab(record,out var source_bounds);
                sources[index] = new EnvironmentShowcaseConfig.SourceAsset { id = record.id, prefab = prefab,
                    native_dimensions = new Vector3(record.dimensions_unity_xyz_m[0],record.dimensions_unity_xyz_m[1],record.dimensions_unity_xyz_m[2]),
                    native_bounds_center = source_bounds.center,
                    material_bindings = build_material_bindings(record),
                    source_record = path + "#" + record.id + "; " + handoff.source_blend + "; SHA256=" + handoff.source_sha256 +
                        "; Scene=" + record.source_scene + "; Collection=" + record.source_collection + "; scale=1, once-baked axes" +
                        "; authored_triangles=" + record.triangles + "; effective_triangles=" + get_effective_triangles(record) +
                        "; degenerate_triangles=" + record.degenerate_triangle_count };
            }
            return sources;
        }

        private static void validate_record(Record record, string category)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.id) ||
                !record.model_path.StartsWith("Assets/_Tests/EnvironmentShowcase/Art/" + category + "/",StringComparison.Ordinal) ||
                record.unit_m != 1 || record.placement_scale != 1 || record.axis_forward != "-Z" || record.axis_up != "Y" ||
                record.pieces == null || record.materials == null || record.dimensions_unity_xyz_m == null || record.dimensions_unity_xyz_m.Length != 3 ||
                string.IsNullOrWhiteSpace(record.source_scene) || string.IsNullOrWhiteSpace(record.source_collection))
                throw new InvalidOperationException("Source provenance, dimensions, axis or scope invalid: " + record?.id);
            verify_file(record.model_path,record.model_sha256);
            foreach (var material in record.materials)
            {
                if (material.alpha_mode != "opaque" || (category != "Resources" && (material.metallic != 0 || has_texture(material.channels?.metallic))) ||
                    material.channels == null || !has_texture(material.channels.base_color))
                    throw new InvalidOperationException("Neutral source material invalid: " + record.id + "/" + material.name);
                if (category == "Resources") validate_ore_channels(record.id,material);
                foreach (var texture in get_textures(material))
                    if (has_texture(texture))
                    {
                        if (!texture.path.StartsWith("Assets/_Tests/EnvironmentShowcase/Art/" + category + "/",StringComparison.Ordinal))
                            throw new InvalidOperationException("Source texture outside own art category: " + texture.path);
                        verify_file(texture.path,texture.sha256);
                    }
            }
        }

        private static bool has_texture(TextureRecord record) => record != null && !string.IsNullOrWhiteSpace(record.path);

        private static TextureRecord get_normal(MaterialRecord record) => has_texture(record.channels.normal_gl) ? record.channels.normal_gl : record.channels.normal;

        private static TextureRecord[] get_textures(MaterialRecord record) => new[]
            { record.channels.base_color,get_normal(record),record.channels.roughness,record.channels.metallic };

        private static Texture2D load_texture(TextureRecord record) => has_texture(record) ? AssetDatabase.LoadAssetAtPath<Texture2D>(record.path) : null;

        /// <summary>Keep the unverified source normal encoded; do not label it GL or synthesize missing maps.</summary>
        private static void validate_ore_channels(string id, MaterialRecord record)
        {
            var normal = get_normal(record);
            if (!has_texture(normal) || !has_texture(record.channels.roughness) || !has_texture(record.channels.metallic) ||
                has_texture(record.channels.normal_gl) || record.normal_convention != "unverified" ||
                !record.normal_visual_check_required || record.metallic_multiplier != 1 || record.normal_strength != 1 ||
                record.channels.base_color.colorspace != "sRGB" || normal.colorspace != "linear" || normal.component != "rgb" ||
                record.channels.roughness.colorspace != "linear" || record.channels.roughness.component != "red" ||
                record.channels.metallic.colorspace != "linear" || record.channels.metallic.component != "red")
                throw new InvalidOperationException("New ore PBR channels/convention invalid: " + id + "/" + record.name);
        }

        /// <summary>Only the source-documented two zero-area triangles at this exact hash may be removed by import.</summary>
        private static int get_effective_triangles(Record record)
        {
            if (record.effective_triangles == 0 && record.degenerate_triangle_count == 0) return record.triangles;
            if (record.id != "MiddleEasternCourtyard" || record.model_sha256 != COURTYARD_DEGENERATE_FBX_SHA256 ||
                record.degenerate_triangle_exact_fbx_sha256 != COURTYARD_DEGENERATE_FBX_SHA256 ||
                record.triangles != 10028 || record.source_triangles != 10028 || record.effective_triangles != 10026 ||
                record.degenerate_triangle_count != 2)
                throw new InvalidOperationException("Unapproved effective triangle exception: " + record.id);
            return record.effective_triangles;
        }

        private static EnvironmentShowcaseConfig.SourceMaterialBinding[] build_material_bindings(Record record)
        {
            var bindings = new EnvironmentShowcaseConfig.SourceMaterialBinding[record.materials.Length];
            for (int index = 0; index < bindings.Length; index++)
            {
                var source = record.materials[index];
                var normal = get_normal(source);
                bool imported = true;
                foreach (var texture in get_textures(source))
                    if (has_texture(texture))
                    {
                        var importer = (TextureImporter)AssetImporter.GetAtPath(texture.path);
                        bool is_normal = texture == normal;
                        imported &= importer != null && importer.sRGBTexture == (texture == source.channels.base_color) &&
                            importer.textureType == (is_normal ? TextureImporterType.NormalMap : TextureImporterType.Default) &&
                            (!is_normal || (!importer.flipGreenChannel && !importer.convertToNormalmap));
                    }
                float metallic_max = has_texture(source.channels.metallic) ? measure_metallic_max(source.channels.metallic.path) : 0;
                if (!imported || (has_texture(source.channels.metallic) && metallic_max <= 0))
                    throw new InvalidOperationException("Source channel import or nonzero metallic validation failed: " + record.id + "/" + source.name);
                bindings[index] = new EnvironmentShowcaseConfig.SourceMaterialBinding {
                    material = AssetDatabase.LoadAssetAtPath<Material>(GENERATED_ROOT + record.id + "_" + source.name + ".mat"),
                    base_color = load_texture(source.channels.base_color), normal = load_texture(normal),
                    roughness = load_texture(source.channels.roughness), metallic = load_texture(source.channels.metallic),
                    import_channels_pass = imported, metallic_max_red = metallic_max, metallic_multiplier = source.metallic_multiplier,
                    normal_note = has_texture(source.channels.normal) ? source.normal_convention + "; " + source.normal_preview_assumption +
                        "; visual_check_required=" + source.normal_visual_check_required : "Source-documented normal_gl; no green flip" };
            }
            return bindings;
        }

        private static float measure_metallic_max(string path)
        {
            var decoded = new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if (!ImageConversion.LoadImage(decoded,File.ReadAllBytes(path),false))
                    throw new InvalidOperationException("Cannot decode authored metallic data: " + path);
                byte maximum = 0;
                foreach (var pixel in decoded.GetPixels32()) if (pixel.r > maximum) maximum = pixel.r;
                return maximum / 255f;
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        private static void verify_file(string path, string expected)
        {
            if (!File.Exists(path) || string.IsNullOrWhiteSpace(expected)) throw new InvalidOperationException("Source file/checksum missing: " + path);
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""),expected,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Approved source checksum changed: " + path);
        }

        private static GameObject prepare_prefab(Record record, out Bounds source_bounds)
        {
            AssetDatabase.ImportAsset(record.model_path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(record.model_path);
            importer.globalScale = 1; importer.useFileScale = false;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.addCollider = false; importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.isReadable = true;
            importer.SaveAndReimport();
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(record.model_path));
            try
            {
                instance.name = record.id;
                source_bounds = verify_geometry(instance,record);
                source_bounds.center -= instance.transform.position;
                var materials = new Dictionary<string,Material>();
                foreach (var material in record.materials) materials.Add(material.name,prepare_material(record.id,material));
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    var piece = Array.Find(record.pieces,candidate => candidate.name == filter.name);
                    if (piece == null || piece.materials.Length != filter.sharedMesh.subMeshCount)
                        throw new InvalidOperationException("Exact authored piece/material slot mapping failed: " + record.id + "/" + filter.name);
                    var assigned = new Material[piece.materials.Length];
                    for (int index = 0; index < assigned.Length; index++) assigned[index] = materials[piece.materials[index]];
                    filter.GetComponent<MeshRenderer>().sharedMaterials = assigned;
                }
                return PrefabUtility.SaveAsPrefabAsset(instance,GENERATED_ROOT + record.id + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); importer.isReadable = false; importer.SaveAndReimport(); }
        }

        private static Bounds verify_geometry(GameObject instance, Record record)
        {
            bool has_vertex = false;
            var bounds = new Bounds();
            int triangles = 0;
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh.uv.Length != mesh.vertexCount) throw new InvalidOperationException("Source UV0 incomplete: " + record.id);
                triangles += mesh.triangles.Length / 3;
                foreach (var vertex in mesh.vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if (!has_vertex) { bounds = new Bounds(point,Vector3.zero); has_vertex = true; }
                    else bounds.Encapsulate(point);
                }
            }
            int expected_triangles = get_effective_triangles(record);
            if (!has_vertex || triangles != expected_triangles || Mathf.Abs(bounds.min.y) > .003f)
                throw new InvalidOperationException("Source triangle count/ground root mismatch: " + record.id + "; tris=" + triangles + "; minY=" + bounds.min.y);
            for (int axis = 0; axis < 3; axis++)
                if (Mathf.Abs(bounds.size[axis] - record.dimensions_unity_xyz_m[axis]) > Mathf.Max(.002f,record.dimensions_unity_xyz_m[axis] * .01f))
                    throw new InvalidOperationException("Source bounds/axis mismatch, return to Blender: " + record.id + "; bounds=" + bounds.size);
            return bounds;
        }

        private static Material prepare_material(string id, MaterialRecord record)
        {
            var normal = get_normal(record);
            foreach (var texture in get_textures(record))
                if (has_texture(texture)) prepare_texture(texture.path,texture == normal,texture == record.channels.base_color);
            string path = GENERATED_ROOT + id + "_" + record.name + ".mat";
            var shader = Shader.Find("ZombieGame/EnvironmentShowcase/SourceOpaque");
            if (!shader) throw new InvalidOperationException("Source opaque shader unavailable.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader = shader;
            material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(record.channels.base_color.path));
            bool has_normal = has_texture(normal), has_roughness = has_texture(record.channels.roughness), has_metallic = has_texture(record.channels.metallic);
            material.SetTexture("_BumpMap",load_texture(normal));
            material.SetTexture("_Roughness",load_texture(record.channels.roughness));
            material.SetTexture("_MetallicMap",load_texture(record.channels.metallic));
            material.SetFloat("_HasMetallic",has_metallic ? 1 : 0);
            material.SetFloat("_MetallicConstant",record.metallic);
            material.SetFloat("_MetallicMultiplier",has_metallic ? record.metallic_multiplier : 1);
            material.SetFloat("_HasRoughness",has_roughness ? 1 : 0);
            material.SetFloat("_RoughnessConstant",record.roughness_constant_if_missing);
            material.SetFloat("_BumpScale",has_normal ? record.normal_strength : 0);
            material.SetFloat("_Cull",record.double_sided ? 0 : 2);
            var tint = record.base_color_tint_linear;
            material.SetColor("_Color",tint != null && tint.Length == 4 ? new Color(tint[0],tint[1],tint[2],tint[3]).gamma : Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void prepare_texture(string path, bool normal, bool color)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (normal) { importer.convertToNormalmap = false; importer.flipGreenChannel = false; }
            importer.sRGBTexture = color; importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }
}
