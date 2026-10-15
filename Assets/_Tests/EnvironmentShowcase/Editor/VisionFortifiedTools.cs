using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieGame.Tests.EnvironmentShowcase;
using ShowcaseRuntime = ZombieGame.Tests.EnvironmentShowcase.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Independent v4 city composition, reusing the existing Terrain, water and source-preview controller.</summary>
    public static class VisionFortifiedTools
    {
        private const string ROOT = "Assets/_Tests/EnvironmentShowcase/";
        public const string CONFIG_PATH = ROOT+"Settings/VisionFortifiedShowcaseV4.asset";
        public const string CITY_PATH = ROOT+"Settings/VisionFortifiedCityV4.asset";
        public const string SCENE_PATH = ROOT+"Scenes/EnvironmentFortifiedVision.unity";
        private const string PRESET_PATH = ROOT+"Settings/VisionWildernessV4.asset";
        private const string GEOMETRY_PATH = ROOT+"Settings/VisionConceptV4.asset";
        private const string FOREST_PATH = ROOT+"Settings/VisionConnectedForestV4.asset";

        public static EnvironmentShowcaseConfig prepare_fortified_config()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            if(existing) return existing;
            var previous = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(VisionWildernessTools.CONFIG_PATH);
            if(!previous || !previous.vision_layout || previous.vision_layout.version != "vision-connected-v3" || previous.map_size != 128)
                throw new InvalidOperationException("Fortified v4 requires the saved 128m connected-forest v3 baseline. Preserve other presets.");
            var config = UnityEngine.Object.Instantiate(previous);
            config.name = "VisionFortifiedShowcaseV4";
            config.vision_layout = prepare_preset(previous.vision_layout);
            AssetDatabase.CreateAsset(config,CONFIG_PATH);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static DarkWildernessConfig prepare_preset(DarkWildernessConfig previous)
        {
            var preset = UnityEngine.Object.Instantiate(previous);
            preset.name = "VisionWildernessV4";
            preset.version = "vision-fortified-v4";
            preset.source_record += "; v4 organized walled-city preview, original source roots only; two local non-walkable Terrain cliffs; v3 retained.";
            preset.fortified_city = prepare_city();
            preset.placements = build_city_placements(previous.placements);
            preset.layout = prepare_geometry(previous.layout);
            preset.vision_forest = prepare_forest(previous.vision_forest);
            preset.vision_soil_regions = new[]{soil_region(68,46,7,17,.74f),soil_region(77,47,7,7,.78f)};
            preset.vision_meadow_regions = new[]{soil_region(98,43,9,18,.72f),soil_region(54,84,13,14,.65f)};
            preset.vision_dry_regions = new[]{soil_region(93,120,16,8,.65f),soil_region(116,45,8,22,.70f)};
            preset.layout.missing_art += " "+preset.fortified_city.missing_art;
            preset.validate_vision_regions();
            AssetDatabase.CreateAsset(preset,PRESET_PATH);
            return preset;
        }

        private static VisionFortifiedConfig prepare_city()
        {
            var city = ScriptableObject.CreateInstance<VisionFortifiedConfig>();
            city.name = "VisionFortifiedCityV4";
            city.source_record = "Pending frozen general-category Blender city-wall handoff; no Unity-created wall geometry.";
            city.fixture_ids = new[]{"city-headquarters","city-workshop","city-pasture","housing-west-north","neutral-north-african-ruin",
                "neutral-middle-eastern-courtyard","housing-east-north","iron-0","iron-1","iron-2","gold-0","gold-1","gold-2",
                "stone-0","stone-1","stone-2","housing-west-south","housing-east-south"};
            city.fixture_source_ids = new[]{"Headquarters","Workshop","Pasture","EuropeanStoneHouse","NorthAfricanRuins",
                "MiddleEasternCourtyard","EuropeanStoneHouse","IronOreRock","IronOreRock","IronOreRock","GoldOreRock","GoldOreRock","GoldOreRock",
                "BoulderNative","BoulderNative","BoulderNative","EuropeanStoneHouse","EuropeanStoneHouse"};
            city.human_city_fixtures = new[]{true,true,true,true,false,false,true,false,false,false,false,false,false,false,false,false,true,true};
            city.courtyard_soil = new[]{soil_region(68,41,6,12,.68f),soil_region(77,47,7,7,.82f),soil_region(57,24,5,13,.55f),soil_region(79,24,5,13,.55f)};
            city.cliffs = new[]{
                new VisionFortifiedConfig.CliffRegion{name="Eastern dry escarpment",center_m=new Vector2(119,42),radius_m=new Vector2(6.2f,17),top_height_m=11.5f},
                new VisionFortifiedConfig.CliffRegion{name="Northern forest escarpment",center_m=new Vector2(82,121),radius_m=new Vector2(16,4.8f),top_height_m=10.5f}
            };
            city.interior_clearance_points_m = new[]{new Vector2(52.5f,14),new Vector2(83.5f,14),new Vector2(52.5f,56.5f),new Vector2(83.5f,56.5f),
                new Vector2(68,58),new Vector2(68,62),new Vector2(68,66),new Vector2(69.5f,42),new Vector2(69.5f,51)};
            city.protected_flat_points_m = new[]{new Vector2(68,35),new Vector2(68,59),new Vector2(79,74),new Vector2(45,108),
                new Vector2(47,76),new Vector2(118,76),new Vector2(97,21),new Vector2(98,42)};
            city.detail_focus = new[]{new Vector2(68,36)/128,new Vector2(68,61)/128,new Vector2(118,42)/128,new Vector2(82,118)/128};
            city.validate(false);
            AssetDatabase.CreateAsset(city,CITY_PATH);
            return city;
        }

        private static DarkWildernessConfig.Placement[] build_city_placements(DarkWildernessConfig.Placement[] previous)
        {
            if(previous.Length != 16) throw new InvalidOperationException("Fortified fixture migration requires the original sixteen-source baseline.");
            var centers = new[]{new Vector2(68,25),new Vector2(77,47),new Vector2(60,44),new Vector2(57,31),new Vector2(58,91),new Vector2(68,105),new Vector2(79,31),
                new Vector2(105,14),new Vector2(109,15),new Vector2(107,20),new Vector2(52,113),new Vector2(56,115),new Vector2(58,111),
                new Vector2(110,114),new Vector2(116,114),new Vector2(113,108),new Vector2(57,18.5f),new Vector2(79,18.5f)};
            var result = new DarkWildernessConfig.Placement[18];
            for(int index=0;index<result.Length;index++)
            {
                var original = previous[index < 16 ? index : 3];
                // Whole-root orientation is layout, not a mesh refit. Buildings face the inner court instead of random yaw.
                float yaw = index == 0 ? 180 : index == 1 || index == 6 || index == 17 ? 90 : index == 3 || index == 16 ? -90 : index < 7 ? 0 : original.yaw;
                result[index] = new DarkWildernessConfig.Placement{source_id=original.source_id,center=centers[index]/128,yaw=yaw,
                    uniform_scale=original.uniform_scale,ore=original.ore};
            }
            return result;
        }

        private static ConceptLayoutConfig prepare_geometry(ConceptLayoutConfig previous)
        {
            var layout = UnityEngine.Object.Instantiate(previous);
            layout.name = "VisionConceptV4";
            layout.views = new[]{new Vector2(68,39)/128,previous.views[1],previous.views[2],new Vector2(79,71)/128};
            layout.camera_sizes = new[]{11f,25f,43f};
            AssetDatabase.CreateAsset(layout,GEOMETRY_PATH);
            return layout;
        }

        private static VisionForestConfig prepare_forest(VisionForestConfig previous)
        {
            if(!previous) throw new InvalidOperationException("Fortified city requires the connected-forest source preset.");
            var forest = UnityEngine.Object.Instantiate(previous);
            forest.name = "VisionConnectedForestV4";
            forest.version = "vision-connected-forest-v4-city-approach";
            var hub = new Vector2(79,74);
            forest.corridors = new[]{
                corridor("North forest opening",4,hub,new Vector2(58,76),new Vector2(45,86),new Vector2(45,108)),
                corridor("Coastal forest opening",4.5f,hub,new Vector2(58,76),new Vector2(47,76)),
                corridor("Pool-side forest opening",4,hub,new Vector2(98,66),new Vector2(118,76)),
                corridor("South exterior opening",4.5f,hub,new Vector2(98,66),new Vector2(98,41),new Vector2(97,21)),
                corridor("Open north city approach",3,hub,new Vector2(68,71),new Vector2(68,59))
            };
            forest.review_focus = new[]{new Vector2(79,90)/128,new Vector2(68,62)/128,new Vector2(108,79)/128,new Vector2(98,42)/128};
            forest.validate();
            AssetDatabase.CreateAsset(forest,FOREST_PATH);
            return forest;
        }

        private static VisionForestConfig.Corridor corridor(string name,float half_width,params Vector2[] points_m)
        {
            var points = new Vector2[points_m.Length];
            for(int index=0;index<points.Length;index++) points[index] = points_m[index]/128;
            return new VisionForestConfig.Corridor{name=name,half_width=half_width,points=points};
        }

        private static DarkWildernessConfig.SurfaceRegion soil_region(float x,float z,float radius_x,float radius_z,float strength)
        {
            return new DarkWildernessConfig.SurfaceRegion{center=new Vector2(x,z)/128,radius=new Vector2(radius_x,radius_z),strength=strength};
        }

        [MenuItem("Zombie Game/Environment Showcase/Vision-4/Prepare and open fortified city")]
        public static void prepare_fortified_scene()
        {
            guard_saved_scenes();
            var settings = prepare_fortified_config();
            settings.vision_layout.fortified_city.validate(true);
            AssetDatabase.SaveAssets();
            if(File.Exists(SCENE_PATH)) EditorSceneManager.OpenScene(SCENE_PATH);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var runtime = UnityEngine.Object.FindFirstObjectByType<ShowcaseRuntime>();
            if(runtime && (runtime.default_layout != "vision" || AssetDatabase.GetAssetPath(runtime.config) != CONFIG_PATH))
                throw new InvalidOperationException("Fortified scene contains an unowned runtime controller; preserve it.");
            if(!runtime) runtime = new GameObject("Vision v4 — existing showcase controller").AddComponent<ShowcaseRuntime>();
            if(runtime.editor_reference_root)
            {
                var marker = runtime.editor_reference_root.GetComponent<VisionEditorReference>();
                if(!marker || AssetDatabase.GetAssetPath(marker.config) != CONFIG_PATH)
                    throw new InvalidOperationException("Refusing to replace an unowned fortified Editor reference.");
                UnityEngine.Object.DestroyImmediate(runtime.editor_reference_root);
            }
            // Scene switching may unload persistent wrappers, so load the current instance afterward.
            runtime.config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            runtime.default_layout = "vision";
            runtime.show_hud = false;
            runtime.editor_reference_root = VisionWildernessTools.create_independent_reference(runtime.config,ROOT+"Settings/VisionFortifiedEditorPreview/");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),SCENE_PATH);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Zombie Game/Environment Showcase/Vision-4/Open saved fortified city and Play")]
        public static void open_fortified_review_play()
        {
            guard_saved_scenes();
            if(!File.Exists(SCENE_PATH)) throw new FileNotFoundException("Prepare the approved fortified scene first.",SCENE_PATH);
            EditorSceneManager.OpenScene(SCENE_PATH);
            var runtime = UnityEngine.Object.FindFirstObjectByType<ShowcaseRuntime>();
            if(!runtime || AssetDatabase.GetAssetPath(runtime.config) != CONFIG_PATH)
                throw new InvalidOperationException("Saved scene is not the independent fortified review.");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }

        public static void prepare_and_build_fortified_preview()
        {
            prepare_fortified_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SCENE_PATH},locationPathName="Builds/EnvironmentFortifiedVision.app",
                target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
            if(report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Fortified environment player build failed: "+report.summary.result);
        }

        private static void guard_saved_scenes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Preserve the current Play session before preparing fortified city assets.");
            for(int index=0;index<SceneManager.sceneCount;index++)
                if(SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Preserve unsaved scene before fortified review: "+SceneManager.GetSceneAt(index).path);
        }
    }
}
