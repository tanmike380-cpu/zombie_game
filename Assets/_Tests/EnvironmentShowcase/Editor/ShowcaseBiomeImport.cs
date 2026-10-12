using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;
using ZombieGame.World;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Verified CC0 biome material integration; independent six-slot overrides, never production TerrainLayer edits.</summary>
    public static class ShowcaseBiomeImport
    {
        private const string MANIFEST = "art/environment/showcase_biome_sources.json";
        private const string ROOT = "Assets/_Tests/EnvironmentShowcase/Art/GroundGenerated/";
        [Serializable] private sealed class Manifest { public int schema_version; public SourceRecord[] assets; }
        [Serializable] private sealed class SourceRecord { public string id, source, license; public FileRecord[] files; }
        [Serializable] private sealed class FileRecord { public string path, sha256, channel; }

        public static void prepare_biomes()
        {
            if (!File.Exists(MANIFEST)) throw new FileNotFoundException("Approved biome manifest missing.", MANIFEST);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(MANIFEST));
            if (manifest == null || manifest.schema_version != 1 || manifest.assets == null)
                throw new InvalidOperationException("Unsupported biome source manifest: " + MANIFEST);
            var layers = new Dictionary<string, TerrainLayer>();
            foreach (var source in manifest.assets)
            {
                validate_source(source);
                layers.Add(source.id, prepare_layer(source));
            }
            assign_profiles(layers);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShowcaseBiomes] Four independent six-slot regions assigned; true CC0 snow/sand/forest soil/natural rock, no pipeline or production changes.");
        }

        private static void validate_source(SourceRecord source)
        {
            if (source == null || source.license != "CC0-1.0" ||
                Array.IndexOf(new[] { "snow_02", "sand_01", "rocky_terrain_02", "forest_ground_04", "rock_boulder_dry", "rock_face_03", "cliff_side", "gravelly_sand" }, source.id) < 0 ||
                source.source != "https://polyhaven.com/a/" + source.id || source.files == null || source.files.Length != 3)
                throw new InvalidOperationException("Unapproved biome source/license: " + source?.id);
            var channels = new HashSet<string>();
            foreach (var file in source.files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.path) ||
                    !file.path.StartsWith("art/environment/polyhaven-src/" + source.id + "/textures/", StringComparison.Ordinal) ||
                    !File.Exists(file.path) || string.IsNullOrWhiteSpace(file.sha256) || !channels.Add(file.channel))
                    throw new InvalidOperationException("Biome source channel/path invalid: " + source.id);
                using (var stream = File.OpenRead(file.path))
                using (var sha = SHA256.Create())
                    if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), file.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Biome source checksum mismatch: " + file.path);
            }
            foreach (string required in new[] { "Diffuse", "nor_gl", "Rough" })
                if (!channels.Contains(required)) throw new InvalidOperationException("Biome PBR channel missing: " + source.id + "/" + required);
        }

        private static TerrainLayer prepare_layer(SourceRecord source)
        {
            Directory.CreateDirectory(ROOT);
            foreach (var file in source.files) File.Copy(file.path, ROOT + Path.GetFileName(file.path), true);
            string normal_path = ROOT + Path.GetFileName(Array.Find(source.files, file => file.channel == "nor_gl").path);
            string albedo_path = ROOT + source.id + "_albedo_smoothness.png";
            pack_smoothness(source, albedo_path);
            configure_texture(normal_path, true);
            configure_texture(albedo_path, false);
            string layer_path = ROOT + source.id + "_Showcase.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layer_path);
            if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layer_path); }
            layer.name = source.id + "_CC0_Showcase";
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo_path);
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normal_path);
            layer.metallic = 0;
            layer.smoothness = 1;
            layer.normalScale = source.id == "rocky_terrain_02" ? .25f : .35f;
            // rocky_terrain_02 is a 90m aerial scan, not a small repeatable brick-sized rock tile.
            layer.tileSize = Vector2.one * (source.id == "rocky_terrain_02" ? 65 : source.id == "sand_01" ? 8 : source.id == "gravelly_sand" ? 2.5f :
                source.id == "rock_face_03" || source.id == "cliff_side" ? 4 : 5);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void pack_smoothness(SourceRecord source, string target_path)
        {
            var color = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            var rough = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!color.LoadImage(File.ReadAllBytes(Array.Find(source.files, file => file.channel == "Diffuse").path)) ||
                    !rough.LoadImage(File.ReadAllBytes(Array.Find(source.files, file => file.channel == "Rough").path)) ||
                    color.width != rough.width || color.height != rough.height)
                    throw new InvalidOperationException("Biome PBR decode or dimensions invalid: " + source.id);
                var pixels = color.GetPixels32();
                var rough_pixels = rough.GetPixels32();
                for (int index = 0; index < pixels.Length; index++) pixels[index].a = (byte)Mathf.RoundToInt((255 - rough_pixels[index].r) * .18f);
                color.SetPixels32(pixels);
                color.Apply();
                File.WriteAllBytes(target_path, color.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(color); UnityEngine.Object.DestroyImmediate(rough); }
        }

        private static void configure_texture(string path, bool is_normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = is_normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !is_normal;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void assign_profiles(Dictionary<string, TerrainLayer> layers)
        {
            foreach (string id in new[] { "snow_02", "sand_01", "rocky_terrain_02", "forest_ground_04", "rock_boulder_dry", "rock_face_03", "cliff_side", "gravelly_sand" })
                if (!layers.ContainsKey(id)) throw new InvalidOperationException("Required biome layer missing: " + id);
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if (!config || !config.grass_layer_override) throw new InvalidOperationException("Prepare showcase grass and config before biome profiles.");
            var europe = create_profile("europe", "Europe", layers);
            europe.ground_tints = new[] { config.grass_tint, config.dry_tint, Color.white, config.mud_tint };
            europe.missing_art = "Medieval architecture and props are not approved for this environment preview.";
            europe.route_missing_art = "Approved neutral European ruins; further period props not supplied.";
            var alpine = create_profile("mountains", "Mountains", layers);
            alpine.layer_overrides[1] = layers["snow_02"];
            alpine.layer_semantics[1] = "Snow_02: upper mountain snow, not dry grass";
            alpine.upper_snow = true;
            alpine.relief_multiplier = 1.25f;
            alpine.rock_start = 12;
            alpine.rock_full = 28;
            alpine.dry_strength = .05f;
            alpine.grass_density = .45f;
            alpine.tree_density = .45f;
            alpine.shrub_density = .5f;
            alpine.rock_density = 1.6f;
            alpine.ground_tints = new[] { new Color(.86f, .95f, .81f), Color.white, Color.white, Color.white };
            alpine.ambient = new Color(.36f, .4f, .45f);
            alpine.fog = new Color(.56f, .63f, .68f);
            alpine.sun = new Color(.96f, .98f, 1);
            alpine.missing_art = "Native broadleaf tree reused only in low valleys; alpine conifers and buildings absent.";
            alpine.route_missing_art = "Broadleaf valley belts and neutral European ruins; alpine conifers absent.";
            alpine.plateau_height = 17;
            var winter = create_profile("winter", "Winter", layers);
            winter.layer_overrides[0] = layers["snow_02"];
            winter.layer_overrides[1] = layers["snow_02"];
            winter.layer_overrides[4] = layers["rock_boulder_dry"];
            winter.layer_semantics[0] = "Snow_02: snow cover, not Grass_A";
            winter.layer_semantics[1] = "Snow_02: drift variation, not Grass_Dry";
            winter.layer_semantics[4] = "Rock_boulder_dry: nonvegetated exposed stone, 5m tile";
            winter.grass_density = winter.tree_density = winter.shrub_density = 0;
            winter.rock_density = 1.25f;
            winter.ground_tints = new[] { Color.white, new Color(.91f, .95f, .98f), Color.white, new Color(.7f, .76f, .8f) };
            winter.ambient = new Color(.4f, .44f, .48f);
            winter.sun = new Color(.91f, .95f, 1);
            winter.sun_intensity = .8f;
            winter.fog = new Color(.62f, .68f, .73f);
            winter.water = new Color(.53f, .64f, .68f);
            winter.missing_art = "No approved snow-covered/leafless trees: green vegetation deliberately hidden; basic ice-coloured surface, not simulated ice.";
            winter.route_missing_art = "European ruins are not snow-authored; leafless/snow trees absent; ice-coloured water only.";
            winter.plateau_height = 13;
            var desert = create_profile("desert", "Desert", layers);
            desert.layer_overrides[0] = layers["sand_01"];
            desert.layer_overrides[1] = layers["sand_01"];
            desert.layer_overrides[2] = Resources.Load<TerrainLayer>(TerrainPresentation.GENERATED_ROOT + "Dirt");
            desert.layer_overrides[4] = layers["rock_boulder_dry"];
            desert.layer_overrides[5] = desert.layer_overrides[2];
            desert.layer_semantics[0] = "Sand_01: sand substrate, not Grass_A";
            desert.layer_semantics[1] = "Sand_01: sand variation, not Grass_Dry";
            desert.layer_semantics[2] = "Dirt: arid exposed soil";
            desert.layer_semantics[4] = "Rock_boulder_dry: nonvegetated exposed stone, 5m tile";
            desert.layer_semantics[5] = "Dirt: unpaved trail, no modern road";
            desert.dry_basin = true;
            desert.relief_multiplier = 1.1f;
            desert.grass_density = desert.tree_density = desert.shrub_density = 0;
            desert.rock_density = 1.7f;
            desert.dry_strength = .5f;
            desert.ground_tints = new[] { new Color(1, .96f, .86f), new Color(.91f, .83f, .69f), new Color(.94f, .85f, .7f), new Color(.84f, .81f, .65f) };
            desert.ambient = new Color(.43f, .4f, .34f);
            desert.sun = new Color(1, .95f, .84f);
            desert.sun_intensity = .95f;
            desert.fog = new Color(.68f, .61f, .48f);
            desert.water = new Color(.18f, .38f, .36f);
            desert.missing_art = "No approved palms, desert shrubs or regional architecture: source rocks only; shallow wet basin preview.";
            desert.route_missing_art = "Approved NorthAfrican/MiddleEastern neutral ruins; palms/desert shrubs absent. Decorative ores only.";
            desert.plateau_height = 15;
            desert.erosion_strength = .08f;
            desert.landmark_source_indices = new[] { 1,1,2 };
            config.biomes = new[] { europe, alpine, winter, desert };
            foreach (var profile in config.biomes) assign_landform_pbr(profile, layers);
            config.validate_config();
            EditorUtility.SetDirty(config);
        }

        private static void assign_landform_pbr(EnvironmentShowcaseConfig.BiomeProfile profile, Dictionary<string, TerrainLayer> layers)
        {
            if (!profile.dry_basin) profile.erosion_strength = .03f;
            string cliff_id = profile.id == "desert" ? "cliff_side" : "rock_face_03";
            var cliff = layers[cliff_id];
            var talus = layers["gravelly_sand"];
            profile.cliff_albedo_smoothness = cliff.diffuseTexture;
            profile.cliff_normal = cliff.normalMapTexture;
            profile.talus_albedo_smoothness = talus.diffuseTexture;
            profile.talus_normal = talus.normalMapTexture;
            profile.cliff_source_record = MANIFEST + "#" + cliff_id + "; steep Stone pixels only, 4m triplanar";
            profile.talus_source_record = MANIFEST + "#gravelly_sand; plateau-foot Stone pixels only, 2.5m";
            profile.layer_semantics[4] += "; local " + cliff_id + " cliff / gravelly_sand talus PBR";
        }

        private static EnvironmentShowcaseConfig.BiomeProfile create_profile(string id, string label, Dictionary<string, TerrainLayer> layers)
        {
            var profile = new EnvironmentShowcaseConfig.BiomeProfile { id = id, label = label, source_record = MANIFEST + "#" + id,
                layer_semantics = new[] { "Grass_A: real green grass", "Grass_Dry: dry grass patches", "Forest_ground_04: leaf litter and forest soil",
                    "Mud: wet shoreline", "Rocky_terrain_02: natural rock, 65m scan scale", "Road: existing rural track source" } };
            profile.layer_overrides[2] = layers["forest_ground_04"];
            profile.layer_overrides[4] = layers["rocky_terrain_02"];
            return profile;
        }
    }
}
