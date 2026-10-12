using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieGame.EditorTools;
using ZombieGame.Tests.EnvironmentShowcase;
using ZombieGame.World;
using ShowcaseRuntime = ZombieGame.Tests.EnvironmentShowcase.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Explicit independent scene/preset/build entry points; never overwrites battle scenes or build settings.</summary>
    public static class EnvironmentShowcaseTools
    {
        public const string SCENE_PATH = "Assets/_Tests/EnvironmentShowcase/Scenes/EnvironmentShowcase.unity";
        public const string CONFIG_PATH = "Assets/_Tests/EnvironmentShowcase/Settings/EnvironmentShowcaseConfig.asset";

        [MenuItem("Zombie Game/Environment Showcase/Create or open independent scene")]
        public static void create_scene()
        {
            guard_unsaved_scene();
            ensure_layers();
            var config = ensure_config();
            if (File.Exists(SCENE_PATH))
            {
                EditorSceneManager.OpenScene(SCENE_PATH);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Environment showcase — isolated art lab");
            root.AddComponent<ShowcaseRuntime>().config = config;
            TerrainLayerImport.ensure_scene_placeholder();
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log("[EnvironmentShowcase] Saved independent scene " + SCENE_PATH + "; config " + CONFIG_PATH);
        }

        private static void guard_unsaved_scene()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty) throw new InvalidOperationException("Preserve unsaved scene before showcase scene/build operation: " + scene.path);
            }
        }

        private static void ensure_layers()
        {
            var profile = TerrainPresentation.load_config();
            bool all_ready = Resources.Load<Material>(TerrainPresentation.GENERATED_ROOT + "TerrainStandard");
            foreach (var layer in profile.layers)
                all_ready &= Resources.Load<TerrainLayer>(TerrainPresentation.GENERATED_ROOT + layer.id) != null;
            if (!all_ready) TerrainLayerImport.prepare_layers();
        }

        private static EnvironmentShowcaseConfig ensure_config()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CONFIG_PATH));
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<EnvironmentShowcaseConfig>();
                AssetDatabase.CreateAsset(config, CONFIG_PATH);
            }
            config.validate_config();
            return config;
        }

        [MenuItem("Zombie Game/Environment Showcase/Build independent macOS player")]
        public static void build_player()
        {
            create_scene();
            var keeper = UnityEngine.Object.FindAnyObjectByType<Terrain>(FindObjectsInactive.Include);
            if (!keeper) throw new InvalidOperationException("Showcase scene has no serialized Terrain resource keeper; build refused.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { SCENE_PATH },
                locationPathName = "Builds/EnvironmentShowcase.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Environment showcase player build failed: " + report.summary.result);
            Debug.Log("[EnvironmentShowcase] Built Builds/EnvironmentShowcase.app. Run -showcaseCheck -showcaseOutput <directory> for player evidence.");
        }

        [MenuItem("Zombie Game/Environment Showcase/Prepare root-approved independent preview")]
        public static void prepare_root_approved_preview()
        {
            create_scene();
            ShowcaseGroundImport.prepare_ground();
            ShowcaseBiomeImport.prepare_biomes();
            VegetationShowcaseImport.import_root_approved_test_candidate();
            Debug.Log("[EnvironmentShowcase] Independent preview ready; traceable user-approved environment category art is not a production-world replacement.");
        }

        [MenuItem("Zombie Game/Environment Showcase/Prepare and build root-approved preview")]
        public static void prepare_and_build_root_approved_preview()
        {
            prepare_root_approved_preview();
            build_player();
        }

        [MenuItem("Zombie Game/Environment Showcase/Apply mip coverage and build prepared preview")]
        public static void build_prepared_preview_player()
        {
            guard_unsaved_scene();
            VegetationShowcaseImport.refresh_root_test_source_provenance();
            VegetationShowcaseImport.apply_existing_coverage();
            build_player();
        }

        /// <summary>Refresh approved region materials/config without reimporting already verified source model geometry.</summary>
        [MenuItem("Zombie Game/Environment Showcase/Refresh biome materials and build prepared preview")]
        public static void build_refreshed_biome_preview()
        {
            guard_unsaved_scene();
            ShowcaseBiomeImport.prepare_biomes();
            VegetationShowcaseImport.refresh_root_test_source_provenance();
            VegetationShowcaseImport.apply_existing_coverage();
            build_player();
        }

        /// <summary>Parent runs one Unity session after source handoff, never concurrent with source authoring.</summary>
        public static void prepare_and_build_route_preview()
        {
            guard_unsaved_scene();
            ShowcaseBiomeImport.prepare_biomes();
            VegetationShowcaseImport.refresh_root_test_source_provenance();
            ShowcaseLandmarkImport.prepare_route_sources();
            VegetationShowcaseImport.apply_existing_coverage();
            build_player();
        }
    }
}
