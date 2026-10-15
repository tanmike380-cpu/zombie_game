using System;
using System.Collections.Generic;
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
    /// <summary>User-authorized dense repetition of existing whole source models; v3/v4/v5 stay intact.</summary>
    public static class VisionDenseTools
    {
        private const string ROOT = "Assets/_Tests/EnvironmentShowcase/";
        public const string CONFIG_PATH = ROOT+"Settings/VisionDenseShowcaseV6.asset";
        public const string SCENE_PATH = ROOT+"Scenes/EnvironmentDenseVision.unity";
        private const float MAP_SIZE_M = 128;

        private sealed class DenseLayout
        {
            public readonly List<DarkWildernessConfig.Placement> placements = new List<DarkWildernessConfig.Placement>();
            public readonly List<string> ids = new List<string>();
            public readonly List<string> source_ids = new List<string>();
            // This is an inside-wall placement guard, not a gameplay allegiance or building-function claim.
            public readonly List<bool> inside_wall = new List<bool>();
        }

        public static EnvironmentShowcaseConfig prepare_dense_config()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            if(existing) return existing;
            var previous = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(VisionNeutralTools.CONFIG_PATH);
            if(!previous || !previous.vision_layout || !previous.vision_layout.fortified_city || previous.map_size != MAP_SIZE_M)
                throw new InvalidOperationException("Dense v6 requires the saved v5 neutral-light 128m source baseline.");
            previous.vision_layout.fortified_city.validate(true);
            var layout = build_dense_layout(previous.vision_layout.placements);
            var config = UnityEngine.Object.Instantiate(previous);
            config.name = "VisionDenseShowcaseV6";
            config.vision_layout = prepare_preset(previous.vision_layout,layout);
            validate_dense_layout(config,layout);
            AssetDatabase.CreateAsset(config,CONFIG_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log("[VisionDense] Prepared 30 real building roots, including 12 whole pastures, plus 9 mineral roots; bright v5 daylight retained. Actual forest count requires the rendered scene.");
            return config;
        }

        /// <summary>Explicitly refresh only owned v6 layout assets; older presets and source imports are untouched.</summary>
        public static void refresh_owned_dense_layout()
        {
            guard_saved_scenes();
            var config = load_owned_dense_config();
            var preset = config.vision_layout;
            var layout = build_dense_layout(preset.placements);
            preset.placements = layout.placements.ToArray();
            var city = preset.fortified_city;
            city.fixture_ids = layout.ids.ToArray();
            city.fixture_source_ids = layout.source_ids.ToArray();
            city.human_city_fixtures = layout.inside_wall.ToArray();
            configure_dense_views(preset.layout);
            configure_dense_pond(preset.layout);
            configure_dense_overview(city);
            city.detail_focus[0] = preset.layout.views[0];
            configure_dense_clearance_points(city);
            preset.vision_forest.corridors = build_dense_corridors();
            validate_dense_layout(config,layout);
            foreach(var asset in new UnityEngine.Object[]{config,preset,preset.layout,city,preset.vision_forest}) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Persist only the proven v6 screenshot pose; no model, lighting or other view changes.</summary>
        public static void refresh_owned_dense_overview()
        {
            guard_saved_scenes();
            var config = load_owned_dense_config();
            configure_dense_overview(config.vision_layout.fortified_city);
            config.vision_layout.fortified_city.validate(true);
            EditorUtility.SetDirty(config.vision_layout.fortified_city);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Only the three frozen grid variants replace matching v6 lookup entries, at authored metre scale.</summary>
        public static void apply_frozen_grid_sources(EnvironmentShowcaseConfig.SourceAsset[] grid_sources,string handoff_record)
        {
            guard_saved_scenes();
            var config = load_owned_dense_config();
            var preset = config.vision_layout;
            if(grid_sources == null || grid_sources.Length != 3 || string.IsNullOrWhiteSpace(handoff_record))
                throw new InvalidOperationException("Dense v6 grid update needs exactly the three frozen building source records.");
            var sources = new List<EnvironmentShowcaseConfig.SourceAsset>(preset.frozen_sources);
            var approved = new HashSet<string>{"Headquarters","Workshop","Pasture"};
            foreach(var source in grid_sources)
            {
                if(source == null || !approved.Remove(source.id) || !source.prefab || source.material_bindings == null ||
                    !source.source_record.Contains(handoff_record))
                    throw new InvalidOperationException("Grid update source is absent, duplicated or outside the approved three-source handoff.");
                int index = sources.FindIndex(candidate=>candidate.id == source.id);
                if(index < 0) sources.Add(source); else sources[index] = source;
            }
            preset.frozen_sources = sources.ToArray();
            foreach(var placement in preset.placements)
                if(placement.source_id == "Headquarters" || placement.source_id == "Workshop" || placement.source_id == "Pasture") placement.uniform_scale = 1;
            preset.source_record += "; integer-grid variants: "+handoff_record+"; authored metre exports at whole-root scale 1; original v3-v5 sources retained.";
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            refresh_owned_dense_layout();
        }

        private static EnvironmentShowcaseConfig load_owned_dense_config()
        {
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            if(!config || !config.vision_layout) throw new InvalidOperationException("Prepare the independent v6 assets before updating them.");
            var preset = config.vision_layout;
            if(AssetDatabase.GetAssetPath(preset) != ROOT+"Settings/VisionWildernessV6.asset" ||
                AssetDatabase.GetAssetPath(preset.layout) != ROOT+"Settings/VisionConceptV6.asset" ||
                AssetDatabase.GetAssetPath(preset.fortified_city) != ROOT+"Settings/VisionFortifiedCityV6.asset" ||
                AssetDatabase.GetAssetPath(preset.vision_forest) != ROOT+"Settings/VisionConnectedForestV6.asset")
                throw new InvalidOperationException("Dense update refused: a v6 reference points at an older or unowned preset.");
            return config;
        }

        private static DarkWildernessConfig prepare_preset(DarkWildernessConfig previous,DenseLayout layout)
        {
            var preset = UnityEngine.Object.Instantiate(previous);
            preset.name = "VisionWildernessV6";
            preset.version = "vision-dense-repetition-v6";
            preset.source_record += "; v6 user-authorized repetition of existing whole roots: 30 buildings/12 pastures, dense native oak curtain, connected Terrain edge cap. Not a new model or functioning economy. V5 white daylight and water retained.";
            preset.neutral_light_baseline = null;
            preset.flat_ground = true;
            preset.flat_height = 4.2f;
            preset.placements = layout.placements.ToArray();
            preset.fortified_city = prepare_city(previous.fortified_city,layout);
            preset.layout = prepare_geometry(previous.layout);
            preset.vision_forest = prepare_forest(previous.vision_forest);
            preset.vision_soil_regions = new[]{region(68,50,15,11,.70f),region(108,39,17,31,.70f)};
            preset.vision_meadow_regions = new[]{region(89,72,38,9,.65f),region(47.5f,87,8,25,.64f)};
            preset.vision_dry_regions = new[]{region(112,10,14,5,.58f),region(80,117,35,5,.38f)};
            preset.validate_vision_regions();
            AssetDatabase.CreateAsset(preset,ROOT+"Settings/VisionWildernessV6.asset");
            return preset;
        }

        private static DenseLayout build_dense_layout(DarkWildernessConfig.Placement[] templates)
        {
            var layout = new DenseLayout();
            add_source(layout,templates,"Headquarters","inner-headquarters",68,48,180,true);
            add_source(layout,templates,"Workshop","inner-workshop-west",56,49,90,true);
            add_source(layout,templates,"Workshop","inner-workshop-east",80,49,-90,true);
            foreach(float z in new[]{21f,35f})
                foreach(float x in new[]{61f,75f}) add_source(layout,templates,"Pasture","inner-field-"+x+"-"+z,x,z,0,true);
            // The broad crop belt lies below the closed pond; the short western return stops before the cliff cap.
            foreach(float x in new[]{61f,75f,89f,103f,117f})
                add_source(layout,templates,"Pasture","farm-belt-"+x,x,74,0,false);
            foreach(float z in new[]{74f,88f,102f})
                add_source(layout,templates,"Pasture","farm-west-"+z,47,z,0,false);
            foreach(float z in new[]{17f,59f})
                foreach(float x in new[]{97f,107f,117f})
                    add_source(layout,templates,"EuropeanStoneHouse","east-house-"+x+"-"+z,x,z,z == 17 ? 0 : 180,false);
            foreach(float x in new[]{97f,108f,119f})
            {
                add_source(layout,templates,"Workshop","east-workshop-"+x,x,28,90,false);
                add_source(layout,templates,"MiddleEasternCourtyard","east-courtyard-"+x,x,40,180,false);
                add_source(layout,templates,"NorthAfricanRuins","east-ruin-"+x,x,51,0,false);
            }
            for(int index=0;index<3;index++)
            {
                add_source(layout,templates,"IronOreRock","iron-"+index,97+index*5,6,0,false);
                add_source(layout,templates,"GoldOreRock","gold-"+index,112+index*5,6,0,false);
                add_source(layout,templates,"BoulderNative","stone-"+index,42,16+index*13,0,false);
            }
            return layout;
        }

        private static void add_source(DenseLayout layout,DarkWildernessConfig.Placement[] templates,string source_id,string id,float x,float z,float yaw,bool inside_wall)
        {
            DarkWildernessConfig.Placement template = null;
            foreach(var candidate in templates)
                if(candidate.source_id == source_id) { template = candidate; break; }
            if(template == null) throw new InvalidOperationException("Dense layout has no approved whole-source placement template: "+source_id);
            layout.placements.Add(new DarkWildernessConfig.Placement{source_id=source_id,center=new Vector2(x,z)/MAP_SIZE_M,
                yaw=yaw,uniform_scale=template.uniform_scale,ore=template.ore});
            layout.ids.Add(id);
            layout.source_ids.Add(source_id);
            layout.inside_wall.Add(inside_wall);
        }

        private static VisionFortifiedConfig prepare_city(VisionFortifiedConfig previous,DenseLayout layout)
        {
            var city = UnityEngine.Object.Instantiate(previous);
            city.name = "VisionFortifiedCityV6";
            city.version = "vision-dense-repetition-v6";
            city.source_record += "; whole frozen ring unchanged; dense repeated source district extends east/north outside the ring by user instruction.";
            city.dense_repetition = true;
            configure_dense_overview(city);
            city.composition_only = true;
            city.expected_city_fixture_count = 7;
            city.patrol_clearance_m = 1.5f;
            city.fixture_ids = layout.ids.ToArray();
            city.fixture_source_ids = layout.source_ids.ToArray();
            city.human_city_fixtures = layout.inside_wall.ToArray();
            city.cliffs = Array.Empty<VisionFortifiedConfig.CliffRegion>();
            city.boundary_cliff_enabled = true;
            city.boundary_north_start_m = 122;
            city.boundary_side_start_m = 96;
            city.boundary_west_x_m = 38;
            city.boundary_east_x_m = 125.5f;
            city.boundary_rise_width_m = 2;
            city.boundary_top_m = 13.2f;
            city.courtyard_soil = new[]{region(68,48,16,10,.68f),region(108,39,17,30,.70f)};
            configure_dense_clearance_points(city);
            city.detail_focus = new[]{new Vector2(80,81)/MAP_SIZE_M,city.portal_center_m/MAP_SIZE_M,new Vector2(80,113)/MAP_SIZE_M,new Vector2(91,71)/MAP_SIZE_M};
            city.missing_art = "Dense v6 uses repeated real HQ/workshop/pasture and existing damaged house/courtyard/ruin sources, not newly invented housing. Whole source wall remains crooked. Open portal has no gate leaf. Terrain boundary cap is not an overhanging cliff model. Native oak is not reference pine. Layout only: no economy/terrain navigation claimed.";
            city.validate(true);
            AssetDatabase.CreateAsset(city,ROOT+"Settings/VisionFortifiedCityV6.asset");
            return city;
        }

        private static ConceptLayoutConfig prepare_geometry(ConceptLayoutConfig previous)
        {
            var geometry = UnityEngine.Object.Instantiate(previous);
            geometry.name = "VisionConceptV6";
            configure_dense_views(geometry);
            configure_dense_pond(geometry);
            geometry.missing_art = "Dense source-repetition composition: cliff cap, native oak curtain, twelve pastures and town blocks. Not a one-to-one reference reconstruction or a functioning economy. No source model refit.";
            AssetDatabase.CreateAsset(geometry,ROOT+"Settings/VisionConceptV6.asset");
            return geometry;
        }

        private static VisionForestConfig prepare_forest(VisionForestConfig previous)
        {
            if(!previous) throw new InvalidOperationException("Dense v6 requires the approved native oak/LOD forest configuration.");
            var forest = UnityEngine.Object.Instantiate(previous);
            forest.name = "VisionConnectedForestV6";
            forest.version = "vision-dense-forest-v6";
            forest.dense_repetition = true;
            forest.tree_budget = 220;
            forest.tree_spacing = 4.8f;
            forest.use_native_source_clearance = true;
            forest.forest_masses = new[]{mass(81,109,51,22),mass(63,94,25,31),mass(119,103,16,27)};
            forest.clearings = Array.Empty<VisionForestConfig.Clearing>();
            forest.corridors = build_dense_corridors();
            forest.review_focus = new[]{new Vector2(80,108)/MAP_SIZE_M,new Vector2(80,84)/MAP_SIZE_M,new Vector2(91,71)/MAP_SIZE_M,new Vector2(108,40)/MAP_SIZE_M};
            forest.validate();
            AssetDatabase.CreateAsset(forest,ROOT+"Settings/VisionConnectedForestV6.asset");
            return forest;
        }

        private static void configure_dense_views(ConceptLayoutConfig geometry)
        {
            geometry.views = new[]{new Vector2(80,81)/MAP_SIZE_M,geometry.views[1],geometry.views[2],new Vector2(81,71)/MAP_SIZE_M};
            geometry.camera_sizes = new[]{20f,36f,47f};
            geometry.camera_pitch = 48;
        }

        private static void configure_dense_pond(ConceptLayoutConfig geometry)
        {
            var baseline = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(VisionNeutralTools.CONFIG_PATH);
            if(!baseline || !baseline.vision_layout || !baseline.vision_layout.layout)
                throw new InvalidOperationException("V6 clearance update needs its preserved v5 pond reference; refusing an incremental shift.");
            // A single absolute +3m shift keeps the moved crop belt off the pond bank. Repeated refresh never accumulates it.
            geometry.pond_center = baseline.vision_layout.layout.pond_center+Vector2.up*(3/MAP_SIZE_M);
        }

        private static void configure_dense_clearance_points(VisionFortifiedConfig city)
        {
            city.interior_clearance_points_m = new[]{new Vector2(53,14),new Vector2(83,14),new Vector2(53,55),new Vector2(83,55),
                city.portal_center_m-new Vector2(0,2),city.portal_center_m,city.portal_center_m+new Vector2(0,3),new Vector2(68,59)};
            city.protected_flat_points_m = new[]{new Vector2(68,48),new Vector2(61,21),new Vector2(75,35),new Vector2(47,88),
                new Vector2(68,63),new Vector2(92,61),new Vector2(92,12),new Vector2(80,117),new Vector2(120,110)};
        }

        private static void configure_dense_overview(VisionFortifiedConfig city)
        {
            city.composition_overview_enabled = true;
            city.composition_overview_focus_m = new Vector2(70,81);
            city.composition_overview_focus_height_m = 8;
            city.composition_overview_size_m = 36;
            city.composition_overview_pitch_deg = 48;
            city.composition_overview_yaw_deg = 0;
        }

        private static VisionForestConfig.Corridor[] build_dense_corridors()
        {
            // Touching 14m crop tiles intentionally have no gap on their shared edge. Do not call that seam a path.
            return new[]{corridor("Open portal forecourt",new Vector2(68,58.543f),new Vector2(68,63)),
                corridor("East district seam",new Vector2(92,61),new Vector2(92,6))};
        }

        private static VisionForestConfig.Clearing mass(float x,float z,float radius_x,float radius_z)
        {
            return new VisionForestConfig.Clearing{center=new Vector2(x,z)/MAP_SIZE_M,radius=new Vector2(radius_x,radius_z)};
        }

        private static VisionForestConfig.Corridor corridor(string name,params Vector2[] points_m)
        {
            var points = new Vector2[points_m.Length];
            for(int index=0;index<points.Length;index++) points[index] = points_m[index]/MAP_SIZE_M;
            return new VisionForestConfig.Corridor{name=name,half_width=.8f,points=points};
        }

        private static DarkWildernessConfig.SurfaceRegion region(float x,float z,float radius_x,float radius_z,float strength)
        {
            return new DarkWildernessConfig.SurfaceRegion{center=new Vector2(x,z)/MAP_SIZE_M,radius=new Vector2(radius_x,radius_z),strength=strength};
        }

        /// <summary>Fast source-manifest and activity-plane guard before the first real render, not art acceptance.</summary>
        private static void validate_dense_layout(EnvironmentShowcaseConfig config,DenseLayout layout)
        {
            var preset = config.vision_layout;
            int buildings = 0,fields = 0;
            var unique_ids = new HashSet<string>(layout.ids);
            if(unique_ids.Count != layout.ids.Count) throw new InvalidOperationException("Dense fixture identifiers must be unique.");
            foreach(var placement in preset.placements)
            {
                if(!placement.ore) buildings++;
                if(placement.source_id == "Pasture") fields++;
                var source = preset.find_source(config,placement.source_id);
                if(!source.prefab || placement.uniform_scale <= 0 || string.IsNullOrEmpty(source.source_record))
                    throw new InvalidOperationException("Dense fixture has no traceable original whole root: "+placement.source_id);
            }
            if(buildings < 24 || fields < 12 || preset.placements.Length != 39)
                throw new InvalidOperationException("Dense v6 fixture count does not meet the explicit 30 buildings / 12 pasture plan.");
            var concept = new ShowcaseConceptLayout(preset.layout,MAP_SIZE_M,preset,config);
            foreach(var point in preset.fortified_city.protected_flat_points_m)
                if(Mathf.Abs(concept.sample_height(point.x,point.y)-preset.flat_height) > .003f)
                    throw new InvalidOperationException("Dense v6 protected activity plane changed at "+point);
        }

        [MenuItem("Zombie Game/Environment Showcase/Vision-6/Prepare dense source repetition")]
        public static void prepare_dense_scene()
        {
            guard_saved_scenes();
            prepare_dense_config();
            if(File.Exists(SCENE_PATH)) EditorSceneManager.OpenScene(SCENE_PATH);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            // Scene replacement may unload the previous ScriptableObject wrapper.
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            var runtime = UnityEngine.Object.FindFirstObjectByType<ShowcaseRuntime>();
            if(runtime && AssetDatabase.GetAssetPath(runtime.config) != CONFIG_PATH)
                throw new InvalidOperationException("Refusing to overwrite an unowned dense v6 scene controller.");
            if(!runtime) runtime = new GameObject("Vision v6 — dense approved source composition").AddComponent<ShowcaseRuntime>();
            if(runtime.editor_reference_root)
            {
                var marker = runtime.editor_reference_root.GetComponent<VisionEditorReference>();
                if(!marker || AssetDatabase.GetAssetPath(marker.config) != CONFIG_PATH)
                    throw new InvalidOperationException("Refusing to replace an unowned dense v6 reference root.");
                UnityEngine.Object.DestroyImmediate(runtime.editor_reference_root);
            }
            runtime.config = config;
            runtime.default_layout = "vision";
            runtime.show_hud = false;
            runtime.editor_reference_root = VisionWildernessTools.create_independent_reference(config,ROOT+"Settings/VisionDenseEditorPreview/");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),SCENE_PATH);
            AssetDatabase.SaveAssets();
        }

        public static void prepare_and_build_dense_preview()
        {
            prepare_dense_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SCENE_PATH},locationPathName="Builds/EnvironmentDenseVision.app",
                target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
            if(report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Dense environment v6 build failed: "+report.summary.result);
        }

        [MenuItem("Zombie Game/Environment Showcase/Vision-6/Open dense repetition and Play")]
        public static void open_dense_review_play()
        {
            guard_saved_scenes();
            if(!File.Exists(SCENE_PATH)) throw new FileNotFoundException("Prepare dense v6 first.",SCENE_PATH);
            EditorSceneManager.OpenScene(SCENE_PATH);
            var runtime = UnityEngine.Object.FindFirstObjectByType<ShowcaseRuntime>();
            if(!runtime || AssetDatabase.GetAssetPath(runtime.config) != CONFIG_PATH) throw new InvalidOperationException("Saved scene is not the independent dense v6 review.");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }

        private static void guard_saved_scenes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop the running review before preparing dense v6.");
            for(int index=0;index<SceneManager.sceneCount;index++)
                if(SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Preserve unsaved scene before dense v6: "+SceneManager.GetSceneAt(index).path);
        }
    }
}
