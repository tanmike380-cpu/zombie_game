using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ZombieGame.World;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Independent real CC0 green grass layer; no production layer or pipeline changes.</summary>
    public static class ShowcaseGroundImport
    {
        private const string SOURCE_RECORD = "art/environment/showcase_ground_source.json";
        private const string ROOT = "Assets/_Tests/EnvironmentShowcase/Art/GroundGenerated/";
        [Serializable] private sealed class GroundSource { public string id, license; public FileRecord[] files; }
        [Serializable] private sealed class FileRecord { public string path, sha256; }

        [MenuItem("Zombie Game/Environment Showcase/Prepare independent greener ground")]
        public static void prepare_ground()
        {
            if (!File.Exists(SOURCE_RECORD)) throw new FileNotFoundException("Approved showcase ground manifest missing.", SOURCE_RECORD);
            var source = JsonUtility.FromJson<GroundSource>(File.ReadAllText(SOURCE_RECORD));
            if (source == null || source.id != "grass_ground" || source.license != "CC0-1.0" || source.files == null || source.files.Length != 3)
                throw new InvalidOperationException("Independent ground requires approved real grass_ground CC0 diffuse/GL normal/roughness manifest.");
            foreach (var file in source.files) verify_source(file);
            Directory.CreateDirectory(ROOT);
            foreach (var file in source.files)
                File.Copy(file.path, ROOT + Path.GetFileName(file.path), true);
            pack_grass_smoothness();
            configure_texture(ROOT + "grass_ground_albedo_smoothness.png", false, true);
            configure_texture(ROOT + "grass_ground_nor_gl_2k.jpg", true, false);
            configure_texture(ROOT + "grass_ground_rough_2k.jpg", false, false);
            configure_layer();
            configure_material();
            apply_ground_grade();
            AssetDatabase.SaveAssets();
            Debug.Log("[ShowcaseGround] Real 2K CC0 Grass_A override, six layers preserved, independent Built-in PBR macro/neighbour breakup/detail fade; no height blending.");
        }

        private static void verify_source(FileRecord file)
        {
            if (file == null || !file.path.StartsWith("art/environment/polyhaven-src/grass_ground/textures/", StringComparison.Ordinal) ||
                !File.Exists(file.path) || string.IsNullOrEmpty(file.sha256))
                throw new InvalidOperationException("Independent ground source path/checksum invalid.");
            using (var stream = File.OpenRead(file.path))
            using (var hash = SHA256.Create())
            {
                string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != file.sha256.ToLowerInvariant()) throw new InvalidOperationException("Independent ground source checksum mismatch: " + file.path);
            }
        }

        private static void pack_grass_smoothness()
        {
            var color = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            var rough = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!color.LoadImage(File.ReadAllBytes(ROOT + "grass_ground_diff_2k.jpg")) ||
                    !rough.LoadImage(File.ReadAllBytes(ROOT + "grass_ground_rough_2k.jpg")))
                    throw new InvalidOperationException("Cannot decode real showcase ground PBR channels.");
                if (color.width != rough.width || color.height != rough.height)
                    throw new InvalidOperationException("Showcase ground diffuse/roughness source dimensions mismatch.");
                var color_pixels = color.GetPixels32();
                var rough_pixels = rough.GetPixels32();
                for (int index = 0; index < color_pixels.Length; index++)
                    color_pixels[index].a = (byte)Mathf.RoundToInt((255 - rough_pixels[index].r) * .22f);
                color.SetPixels32(color_pixels);
                color.Apply();
                File.WriteAllBytes(ROOT + "grass_ground_albedo_smoothness.png", color.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(color); UnityEngine.Object.DestroyImmediate(rough); }
        }

        private static void configure_texture(string path, bool is_normal, bool is_color)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = is_normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = is_color;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void configure_layer()
        {
            const string path = ROOT + "Grass_A_Showcase.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (!layer)
            {
                var original = Resources.Load<TerrainLayer>(TerrainPresentation.GENERATED_ROOT + "Grass_A");
                if (!original) throw new InvalidOperationException("Prepare original six TerrainLayers first.");
                layer = UnityEngine.Object.Instantiate(original);
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.name = "Grass_A_Showcase_CC0_grass_ground";
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + "grass_ground_albedo_smoothness.png");
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + "grass_ground_nor_gl_2k.jpg");
            layer.tileSize = Vector2.one * 4.5f;
            layer.normalScale = .45f;
            layer.metallic = 0;
            layer.smoothness = 1;
            EditorUtility.SetDirty(layer);
        }

        private static void configure_material()
        {
            const string path = ROOT + "ShowcaseTerrain.mat";
            var shader = Shader.Find("ZombieGame/EnvironmentShowcase/TerrainPbr");
            if (!shader) throw new InvalidOperationException("Independent terrain PBR shader unavailable.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.enableInstancing = true;
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        private static void apply_ground_grade()
        {
            var config = AssetDatabase.LoadAssetAtPath<ZombieGame.Tests.EnvironmentShowcase.EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if (!config) throw new InvalidOperationException("Create showcase config before ground preparation.");
            config.grass_layer_override = AssetDatabase.LoadAssetAtPath<TerrainLayer>(ROOT + "Grass_A_Showcase.terrainlayer");
            config.terrain_material = AssetDatabase.LoadAssetAtPath<Material>(ROOT + "ShowcaseTerrain.mat");
            config.ground_source_record = SOURCE_RECORD + "#Grass_A only; other five layers use original TerrainPresentation sources";
            config.ambient_color = new Color(.34f, .39f, .34f);
            config.sun_color = new Color(1, .94f, .84f);
            config.sun_intensity = 1;
            config.dry_grass_strength = .3f;
            config.grass_tint = new Color(.78f, .95f, .69f);
            config.macro_color_strength = .26f;
            config.neighbor_breakup_strength = .55f;
            EditorUtility.SetDirty(config);
        }
    }
}
