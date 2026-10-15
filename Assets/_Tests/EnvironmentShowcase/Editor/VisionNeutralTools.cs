using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ZombieGame.Tests.EnvironmentShowcase;
using ShowcaseRuntime = ZombieGame.Tests.EnvironmentShowcase.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Independent flat, bright-neutral v5 comparison. Sources and v3/v4 presets remain unchanged.</summary>
    public static class VisionNeutralTools
    {
        private const string ROOT = "Assets/_Tests/EnvironmentShowcase/";
        public const string CONFIG_PATH = ROOT+"Settings/VisionNeutralShowcaseV5.asset";
        public const string SCENE_PATH = ROOT+"Scenes/EnvironmentNeutralVision.unity";

        public static EnvironmentShowcaseConfig prepare_neutral_config()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            if(existing) return existing;
            var previous = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(VisionFortifiedTools.CONFIG_PATH);
            var forest_source = AssetDatabase.LoadAssetAtPath<VisionForestConfig>(ROOT+"Settings/VisionConnectedForestV3.asset");
            if(!previous || !previous.vision_layout || !previous.vision_layout.fortified_city || !forest_source || previous.map_size != 128)
                throw new InvalidOperationException("Neutral v5 needs the frozen v4 wall import and preserved v3 forest source.");
            previous.vision_layout.fortified_city.validate(true);
            var config = UnityEngine.Object.Instantiate(previous);
            config.name = "VisionNeutralShowcaseV5";
            var preset = UnityEngine.Object.Instantiate(previous.vision_layout);
            preset.name = "VisionWildernessV5";
            preset.version = "vision-neutral-flat-v5";
            preset.source_record += "; v5 full inland activity plane and bright neutral-white light comparison; oak, not pine; no new world geometry.";
            preset.neutral_light_baseline = previous.vision_layout;
            preset.flat_ground = true;
            preset.flat_height = 4.2f;
            preset.fortified_city = prepare_city(previous.vision_layout.fortified_city);
            preset.placements = prepare_placements(previous.vision_layout.placements);
            preset.layout = prepare_geometry(previous.vision_layout.layout);
            preset.vision_forest = prepare_forest(forest_source,preset.fortified_city);
            configure_daylight(preset);
            preset.vision_soil_regions = new[]{region(68,54,8,10,.58f),region(78,27,7,7,.72f)};
            preset.vision_meadow_regions = new[]{region(62,80,14,10,.58f),region(91,58,8,19,.62f)};
            preset.vision_dry_regions = new[]{region(108,38,7,19,.55f),region(51,115,10,10,.42f)};
            preset.validate_vision_regions();
            AssetDatabase.CreateAsset(preset,ROOT+"Settings/VisionWildernessV5.asset");
            config.vision_layout = preset;
            AssetDatabase.CreateAsset(config,CONFIG_PATH);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static VisionFortifiedConfig prepare_city(VisionFortifiedConfig previous)
        {
            var city = UnityEngine.Object.Instantiate(previous);
            city.name = "VisionFortifiedCityV5";
            city.version = "vision-neutral-flat-v5";
            city.cliffs = Array.Empty<VisionFortifiedConfig.CliffRegion>();
            city.expected_city_fixture_count = 3;
            city.composition_only = true;
            city.fixture_ids = new[]{"city-headquarters","city-workshop","city-pasture","neutral-damaged-house-1","neutral-north-african-ruin",
                "neutral-middle-eastern-courtyard","neutral-damaged-house-2","iron-0","iron-1","iron-2","gold-0","gold-1","gold-2","stone-0","stone-1","stone-2"};
            city.fixture_source_ids = new[]{"Headquarters","Workshop","Pasture","EuropeanStoneHouse","NorthAfricanRuins","MiddleEasternCourtyard","EuropeanStoneHouse",
                "IronOreRock","IronOreRock","IronOreRock","GoldOreRock","GoldOreRock","GoldOreRock","BoulderNative","BoulderNative","BoulderNative"};
            city.human_city_fixtures = new[]{true,true,true,false,false,false,false,false,false,false,false,false,false,false,false,false};
            city.courtyard_soil = new[]{region(68,54,8,9,.60f),region(78,27,7,7,.76f)};
            city.interior_clearance_points_m = new[]{new Vector2(52.5f,14),new Vector2(83,14),new Vector2(52.5f,56),new Vector2(83,56),
                city.portal_center_m-new Vector2(0,4),city.portal_center_m,city.portal_center_m+new Vector2(0,4),new Vector2(68,54),new Vector2(70,29)};
            city.protected_flat_points_m = new[]{new Vector2(68,43),new Vector2(60,25),new Vector2(78,27),new Vector2(86,70),
                new Vector2(70,113),new Vector2(47,74),new Vector2(118,76),new Vector2(97,21),new Vector2(119,42),new Vector2(82,121)};
            city.detail_focus = new[]{new Vector2(72,63)/128,city.portal_center_m/128,new Vector2(56,104)/128,new Vector2(97,83)/128};
            city.missing_art = "Original source wall remains visibly crooked; its geometry is not repaired in Unity. No intact civilian housing/barracks/warehouse or gate leaf source. Damaged houses remain outside as ruins. Oak is not the reference pine forest; no long rear rock/cliff module or thick shoreline plants were invented.";
            city.validate(true);
            AssetDatabase.CreateAsset(city,ROOT+"Settings/VisionFortifiedCityV5.asset");
            return city;
        }

        private static DarkWildernessConfig.Placement[] prepare_placements(DarkWildernessConfig.Placement[] previous)
        {
            if(previous.Length < 16) throw new InvalidOperationException("Neutral v5 requires the original sixteen source fixtures.");
            var centers = new[]{new Vector2(68,43),new Vector2(78,27),new Vector2(60,25),new Vector2(112,34),new Vector2(113,61),new Vector2(117,20),new Vector2(112,46),
                new Vector2(105,14),new Vector2(109,15),new Vector2(107,20),new Vector2(111,112),new Vector2(116,116),new Vector2(120,111),
                new Vector2(110,8),new Vector2(115,8),new Vector2(120,8)};
            var placements = new DarkWildernessConfig.Placement[16];
            for(int index=0;index<placements.Length;index++)
            {
                var original = previous[index];
                placements[index] = new DarkWildernessConfig.Placement{source_id=original.source_id,center=centers[index]/128,
                    yaw=original.yaw,uniform_scale=original.uniform_scale,ore=original.ore};
            }
            return placements;
        }

        private static ConceptLayoutConfig prepare_geometry(ConceptLayoutConfig previous)
        {
            var geometry = UnityEngine.Object.Instantiate(previous);
            geometry.name = "VisionConceptV5";
            geometry.views = new[]{new Vector2(72,63)/128,previous.views[1],previous.views[2],new Vector2(78,80)/128};
            geometry.camera_sizes = new[]{11f,23f,43f};
            geometry.missing_art = "Flat neutral daylight comparison, not a reconstruction of protected reference assets; sparse source oak differs from pine. No artificial rear rock wall, no new houses, no inland slope.";
            AssetDatabase.CreateAsset(geometry,ROOT+"Settings/VisionConceptV5.asset");
            return geometry;
        }

        private static VisionForestConfig prepare_forest(VisionForestConfig source,VisionFortifiedConfig city)
        {
            var forest = UnityEngine.Object.Instantiate(source);
            forest.name = "VisionConnectedForestV5";
            forest.version = "vision-flat-forest-v5";
            var hub = new Vector2(86,70);
            forest.corridors = new[]{
                corridor("North forest opening",4,hub,new Vector2(73,74),new Vector2(70,88),new Vector2(70,113)),
                corridor("Coastal forest opening",4.5f,hub,new Vector2(70,72),new Vector2(47,74)),
                corridor("Pool-side forest opening",4,hub,new Vector2(100,68),new Vector2(118,76)),
                corridor("South exterior opening",4.5f,hub,new Vector2(98,68),new Vector2(98,41),new Vector2(97,21)),
                corridor("Open north city approach",3,hub,new Vector2(city.portal_center_m.x,68),city.portal_center_m-new Vector2(0,2))
            };
            forest.review_focus = new[]{new Vector2(56,104)/128,new Vector2(72,70)/128,new Vector2(106,89)/128,new Vector2(98,42)/128};
            forest.validate();
            AssetDatabase.CreateAsset(forest,ROOT+"Settings/VisionConnectedForestV5.asset");
            return forest;
        }

        private static void configure_daylight(DarkWildernessConfig preset)
        {
            preset.sun = new Color(.98f,.99f,1);
            preset.sun_intensity = 1.08f;
            preset.ambient_mode = AmbientMode.Trilight;
            preset.ambient = new Color(.36f,.38f,.39f);
            preset.ambient_sky = new Color(.45f,.48f,.50f);
            preset.ambient_equator = new Color(.36f,.38f,.37f);
            preset.ambient_ground = new Color(.28f,.29f,.27f);
            preset.fog = new Color(.56f,.59f,.60f);
            preset.grass_tint = new Color(.78f,.88f,.75f);
            preset.dry_tint = new Color(.93f,.89f,.77f);
            preset.soil_tint = new Color(.92f,.88f,.79f);
        }

        private static VisionForestConfig.Corridor corridor(string name,float half_width,params Vector2[] points_m)
        {
            var points = new Vector2[points_m.Length];
            for(int index=0;index<points.Length;index++) points[index] = points_m[index]/128;
            return new VisionForestConfig.Corridor{name=name,half_width=half_width,points=points};
        }

        private static DarkWildernessConfig.SurfaceRegion region(float x,float z,float radius_x,float radius_z,float strength)
        {
            return new DarkWildernessConfig.SurfaceRegion{center=new Vector2(x,z)/128,radius=new Vector2(radius_x,radius_z),strength=strength};
        }

        [MenuItem("Zombie Game/Environment Showcase/Vision-5/Prepare flat neutral comparison")]
        public static void prepare_neutral_scene()
        {
            guard_saved_scenes();
            prepare_neutral_config();
            if(File.Exists(SCENE_PATH)) EditorSceneManager.OpenScene(SCENE_PATH);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var runtime = UnityEngine.Object.FindFirstObjectByType<ShowcaseRuntime>();
            if(runtime && AssetDatabase.GetAssetPath(runtime.config) != CONFIG_PATH)
                throw new InvalidOperationException("Refusing to overwrite an unowned v5 scene controller.");
            if(!runtime) runtime = new GameObject("Vision v5 — existing flat environment controller").AddComponent<ShowcaseRuntime>();
            if(runtime.editor_reference_root)
            {
                var marker = runtime.editor_reference_root.GetComponent<VisionEditorReference>();
                if(!marker || AssetDatabase.GetAssetPath(marker.config) != CONFIG_PATH)
                    throw new InvalidOperationException("Refusing to replace an unowned v5 reference root.");
                UnityEngine.Object.DestroyImmediate(runtime.editor_reference_root);
            }
            runtime.config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            runtime.default_layout = "vision";
            runtime.show_hud = false;
            runtime.editor_reference_root = VisionWildernessTools.create_independent_reference(runtime.config,ROOT+"Settings/VisionNeutralEditorPreview/");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),SCENE_PATH);
            AssetDatabase.SaveAssets();
        }

        public static void prepare_and_build_neutral_preview()
        {
            prepare_neutral_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SCENE_PATH},locationPathName="Builds/EnvironmentNeutralVision.app",
                target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
            if(report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Neutral environment comparison build failed: "+report.summary.result);
        }

        [MenuItem("Zombie Game/Environment Showcase/Vision-5/Open saved flat neutral comparison and Play")]
        public static void open_neutral_review_play()
        {
            guard_saved_scenes();
            if(!File.Exists(SCENE_PATH)) throw new FileNotFoundException("Prepare neutral v5 first.",SCENE_PATH);
            EditorSceneManager.OpenScene(SCENE_PATH);
            var runtime = UnityEngine.Object.FindFirstObjectByType<ShowcaseRuntime>();
            if(!runtime || AssetDatabase.GetAssetPath(runtime.config) != CONFIG_PATH) throw new InvalidOperationException("Saved scene is not the independent v5 preview.");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }

        private static void guard_saved_scenes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Preserve the running review before preparing v5.");
            for(int index=0;index<SceneManager.sceneCount;index++)
                if(SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Preserve unsaved scene before v5: "+SceneManager.GetSceneAt(index).path);
        }
    }
}
