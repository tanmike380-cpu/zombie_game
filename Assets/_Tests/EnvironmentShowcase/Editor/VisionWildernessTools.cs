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
    /// <summary>Vision-1 uses the existing controller/helpers and frozen assets; no live model authoring or automatic source import.</summary>
    public static class VisionWildernessTools
    {
        private const string ROOT = "Assets/_Tests/EnvironmentShowcase/";
        public const string SCENE_PATH = ROOT+"Scenes/EnvironmentVision.unity";
        public const string CONFIG_PATH = ROOT+"Settings/VisionShowcaseConfig.asset";
        private const string PREVIEW_ROOT = ROOT+"Settings/VisionEditorPreview/";

        [MenuItem("Zombie Game/Environment Showcase/Vision-1/Prepare and open Editor scene")]
        public static void prepare_vision_scene()
        {
            guard_saved_scenes();
            prepare_vision_config();
            AssetDatabase.SaveAssets();
            if(File.Exists(SCENE_PATH))
            {
                EditorSceneManager.OpenScene(SCENE_PATH);
                refresh_saved_reference();
                AssetDatabase.SaveAssets();
                align_editor_view();
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            // NewScene may unload asset wrappers. Reload only after the scene switch.
            var config = load_saved_vision_config();
            var runtime = new GameObject("Vision-1 — same environment controller").AddComponent<ShowcaseRuntime>();
            runtime.config = config;
            runtime.default_layout = "vision";
            runtime.editor_reference_root = create_editor_reference(config);
            EditorSceneManager.SaveScene(scene,SCENE_PATH);
            AssetDatabase.SaveAssets();
            align_editor_view();
            Debug.Log("[Vision-1] Saved Editor-visible reference and Play entry: "+SCENE_PATH+". New assets were not imported; art review pending.");
        }

        public static void prepare_and_build_vision_preview()
        {
            prepare_vision_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[]{SCENE_PATH},
                locationPathName = "Builds/EnvironmentVision.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            if(report.summary.result!=BuildResult.Succeeded)
                throw new InvalidOperationException("Vision packaged preview build failed: "+report.summary.result);
        }

        public static void import_clumps_and_build_vision_preview()
        {
            guard_saved_scenes();
            prepare_vision_config();
            AssetDatabase.SaveAssets();
            VisionVegetationImport.import_approved_clumps();
            prepare_and_build_vision_preview();
        }

        public static void import_oak_and_build_vision_preview()
        {
            guard_saved_scenes();
            prepare_vision_config();
            AssetDatabase.SaveAssets();
            VisionVegetationImport.import_approved_clumps();
            VisionOakImport.import_approved_oak();
            prepare_and_build_vision_preview();
        }

        [MenuItem("Zombie Game/Environment Showcase/Vision-1/Open saved Vision review and Play")]
        public static void open_vision_review_play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Do not interrupt an existing Play session to open Vision.");
            guard_saved_scenes();
            if(!File.Exists(SCENE_PATH))
                throw new FileNotFoundException("Prepare and validate the saved Vision scene before playing it.",SCENE_PATH);
            EditorSceneManager.OpenScene(SCENE_PATH);
            var runtime = UnityEngine.Object.FindFirstObjectByType<ShowcaseRuntime>();
            if(!runtime || !runtime.config || runtime.default_layout != "vision" || AssetDatabase.GetAssetPath(runtime.config) != CONFIG_PATH)
                throw new InvalidOperationException("Saved scene is not the independent Vision preview: "+SCENE_PATH);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
            EditorApplication.delayCall += focus_game_view;
        }

        private static void focus_game_view()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        private static EnvironmentShowcaseConfig prepare_vision_config()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            if(existing)
            {
                if(!existing.vision_layout) throw new InvalidOperationException("Existing Vision config has no independent preset: "+CONFIG_PATH);
                move_vision_house(existing.vision_layout);
                configure_vision_ground_zones(existing.vision_layout);
                existing.vision_layout.validate_vision_regions();
                return existing;
            }
            var original = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if(!original || !original.flat_layout || !original.flat_layout.layout)
                throw new InvalidOperationException("Prepare and validate flat-v3 frozen sources before Vision-1. No substitute assets permitted.");
            var config = UnityEngine.Object.Instantiate(original);
            config.name = "VisionShowcaseConfig";
            config.vision_layout = prepare_vision_preset(original.flat_layout);
            AssetDatabase.CreateAsset(config,CONFIG_PATH);
            return config;
        }

        private static EnvironmentShowcaseConfig load_saved_vision_config()
        {
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            if(!config || !EditorUtility.IsPersistent(config))
                throw new InvalidOperationException("Vision config could not be reloaded after scene switch: "+CONFIG_PATH);
            if(!config.vision_layout || !config.vision_layout.layout)
                throw new InvalidOperationException("Saved Vision config is missing its independent layout reference: "+CONFIG_PATH);
            config.vision_layout.validate_vision_regions();
            return config;
        }

        private static DarkWildernessConfig prepare_vision_preset(DarkWildernessConfig previous)
        {
            var preset = UnityEngine.Object.Instantiate(previous);
            preset.name = "VisionWildernessConfig";
            preset.version = "vision-1";
            preset.source_record = "Vision-1: flat-v3 frozen source refs; 05/07/09 wilderness-dark-v3 directional references, not art approval";
            preset.vision_surface_regions = true;
            preset.layout = prepare_vision_geometry(previous.layout);
            preset.vision_layer_overrides[2] = load_existing_layer("forest_ground_04");
            preset.vision_layer_overrides[4] = load_existing_layer("rock_boulder_dry");
            preset.grass_tint = new Color(.82f,.88f,.74f);
            preset.dry_tint = new Color(.82f,.78f,.64f);
            preset.soil_tint = new Color(.86f,.79f,.69f);
            preset.vision_soil_regions = new[]{region(.59f,.49f,18,15,.68f),region(.43f,.62f,19,12,.65f),region(.77f,.72f,19,13,.70f)};
            preset.vision_dry_regions = new[]{region(.58f,.77f,26,16,.82f),region(.88f,.46f,15,25,.75f),region(.47f,.25f,20,12,.72f)};
            preset.vegetation_centers = new[]{new Vector2(.42f,.49f),new Vector2(.59f,.68f),new Vector2(.85f,.53f),new Vector2(.49f,.28f)};
            preset.vegetation_radii = new[]{new Vector2(12,10),new Vector2(15,9),new Vector2(10,13),new Vector2(14,10)};
            move_vision_house(preset);
            configure_vision_ground_zones(preset);
            // Source transforms, scale ranges and 16 fixture assembly references are copied unchanged from flat-v3.
            preset.validate_vision_regions();
            AssetDatabase.CreateAsset(preset,ROOT+"Settings/VisionWildernessConfig.asset");
            return preset;
        }

        private static void configure_vision_ground_zones(DarkWildernessConfig preset)
        {
            if(preset.vision_surface_revision >= 2) return;
            preset.vision_base_soil = .10f;
            preset.grass_tint = new Color(.64f,1.05f,.62f);
            preset.dry_tint = new Color(.92f,.85f,.66f);
            preset.soil_tint = new Color(.84f,.69f,.52f);
            preset.vision_meadow_regions = new[]{region(.47f,.58f,24,20,.85f),region(.75f,.42f,18,22,.75f),region(.85f,.69f,15,17,.70f)};
            preset.vision_soil_regions = new[]{region(.59f,.49f,10,6,.45f),region(.43f,.62f,7,5,.38f),region(.77f,.72f,8,5,.40f)};
            // Initial composition changes only. Fixed source/acceptance near cameras remain unchanged.
            preset.layout.views[0] = new Vector2(.56f,.57f);
            preset.layout.camera_sizes[1] = 23;
            preset.vision_surface_revision = 2;
            EditorUtility.SetDirty(preset.layout);
            EditorUtility.SetDirty(preset);
        }

        private static void move_vision_house(DarkWildernessConfig preset)
        {
            if(preset.placements.Length != 16 || preset.placements[3].source_id != "EuropeanStoneHouse")
                throw new InvalidOperationException("Vision house migration requires the original frozen 16-fixture layout.");
            // Only the whole source instance moves; flat-v3 and all source scales/rotations remain unchanged.
            if(preset.placements[3].center == new Vector2(.70f,.60f))
            {
                preset.placements[3].center = new Vector2(.70f,.58f);
                EditorUtility.SetDirty(preset);
            }
        }

        private static void refresh_saved_reference()
        {
            var runtime = UnityEngine.Object.FindFirstObjectByType<ShowcaseRuntime>();
            if(!runtime || runtime.default_layout != "vision")
                throw new InvalidOperationException("Existing Vision scene has no owned preview controller: "+SCENE_PATH);
            var config = load_saved_vision_config();
            if(runtime.editor_reference_root)
            {
                var marker = runtime.editor_reference_root.GetComponent<VisionEditorReference>();
                if(!marker || AssetDatabase.GetAssetPath(marker.config) != CONFIG_PATH)
                    throw new InvalidOperationException("Refusing to replace an unowned Editor reference group.");
                UnityEngine.Object.DestroyImmediate(runtime.editor_reference_root);
            }
            runtime.config = config;
            runtime.editor_reference_root = create_editor_reference(config);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),SCENE_PATH);
        }

        private static DarkWildernessConfig.SurfaceRegion region(float x,float z,float radius_x,float radius_z,float strength)
        {
            return new DarkWildernessConfig.SurfaceRegion{center=new Vector2(x,z),radius=new Vector2(radius_x,radius_z),strength=strength};
        }

        private static TerrainLayer load_existing_layer(string id)
        {
            string path = ROOT+"Art/GroundGenerated/"+id+"_Showcase.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if(!layer || !layer.diffuseTexture || !layer.normalMapTexture)
                throw new InvalidOperationException("Vision source PBR layer unavailable; do not silently import or substitute: "+path);
            return layer;
        }

        private static ConceptLayoutConfig prepare_vision_geometry(ConceptLayoutConfig previous)
        {
            var layout = UnityEngine.Object.Instantiate(previous);
            layout.name = "VisionConceptLayout";
            layout.pond_center = new Vector2(.73f,.72f);
            layout.camera_pitch = 38;
            layout.camera_yaw = -18;
            layout.camera_sizes = new[]{11f,20f,30f};
            layout.views = new[]{new Vector2(.55f,.50f),new Vector2(.35f,.50f),layout.pond_center,new Vector2(.56f,.55f)};
            layout.sea_deep = new Color(.06f,.16f,.23f);
            layout.sea_shallow = new Color(.20f,.31f,.33f);
            layout.pond_deep = new Color(.07f,.16f,.17f);
            layout.pond_shallow = new Color(.22f,.30f,.25f);
            layout.missing_art = "Dense whole crowns, wide natural grass clumps, fuller shrubs, reeds/cattails and long authored cliff modules await approved source handoff. Not a reference-species forest.";
            layout.validate_config(128,18);
            AssetDatabase.CreateAsset(layout,ROOT+"Settings/VisionConceptLayout.asset");
            return layout;
        }

        private static GameObject create_editor_reference(EnvironmentShowcaseConfig settings)
        {
            if(!settings || !EditorUtility.IsPersistent(settings))
                throw new InvalidOperationException("Editor reference requires the saved Vision config, not an unloaded wrapper: "+CONFIG_PATH);
            Directory.CreateDirectory(PREVIEW_ROOT);
            var config = UnityEngine.Object.Instantiate(settings);
            config.hideFlags = HideFlags.HideAndDontSave;
            select_vision(config);
            var root = new GameObject("Vision-1 Editor reference — hidden by the runtime controller");
            var ground = new ShowcaseTerrain(config,root.transform,config.biomes[0]);
            _ = new ShowcaseLandmarks(config,ground,root.transform);
            var water = new ShowcaseConceptWater(ground,root.transform,true);
            save_reference_assets(ground,water);
            var saved_ground = new ShowcaseTerrain(config,ground.terrain,config.biomes[0]);
            configure_reference_lighting(config,root.transform);
            var camera = create_reference_camera(config,saved_ground,root.transform);
            var marker = root.AddComponent<VisionEditorReference>();
            marker.config = settings;
            marker.terrain = ground.terrain;
            marker.comparison_camera = camera;
            if(!EditorUtility.IsPersistent(config)) UnityEngine.Object.DestroyImmediate(config);
            return root;
        }

        private static void save_reference_assets(ShowcaseTerrain ground,ShowcaseConceptWater water)
        {
            // Capture every transient reference before any CreateAsset can trigger native reimport.
            var data = ground.terrain_data;
            var layers = data.terrainLayers;
            var terrain_material = ground.terrain.materialTemplate;
            var sea_material = water.sea.sharedMaterial;
            var pond_material = water.pond.sharedMaterial;
            var height_field = sea_material.GetTexture("_HeightField") as Texture2D;
            var objects = new List<UnityEngine.Object>{data,terrain_material,sea_material,pond_material,height_field};
            objects.AddRange(layers);
            var original_flags = protect_reference_objects(objects);
            AssetDatabase.StartAssetEditing();
            try
            {
                var saved_layers = save_layer_assets(layers);
                data.terrainLayers = saved_layers;
                save_generated_asset(data,PREVIEW_ROOT+"Terrain.asset");
                save_generated_asset(terrain_material,PREVIEW_ROOT+"Terrain.mat");
                var saved_height = save_generated_asset(height_field,PREVIEW_ROOT+"HeightField.asset");
                sea_material.SetTexture("_HeightField",saved_height);
                pond_material.SetTexture("_HeightField",saved_height);
                save_generated_asset(sea_material,PREVIEW_ROOT+"Sea.mat");
                save_generated_asset(pond_material,PREVIEW_ROOT+"Pond.mat");
            }
            finally
            {
                restore_reference_flags(objects,original_flags);
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            bind_saved_reference_assets(ground.terrain,water);
        }

        private static HideFlags[] protect_reference_objects(List<UnityEngine.Object> objects)
        {
            var flags = new HideFlags[objects.Count];
            for(int index=0;index<objects.Count;index++)
            {
                if(!objects[index]) throw new InvalidOperationException("Vision generated reference missing before serialization, index="+index);
                flags[index] = objects[index].hideFlags;
            }
            for(int index=0;index<objects.Count;index++)
                if(!EditorUtility.IsPersistent(objects[index])) objects[index].hideFlags |= HideFlags.DontUnloadUnusedAsset;
            return flags;
        }

        private static void restore_reference_flags(List<UnityEngine.Object> objects,HideFlags[] flags)
        {
            for(int index=0;index<objects.Count;index++)
                if(objects[index]) objects[index].hideFlags = flags[index];
        }

        private static TerrainLayer[] save_layer_assets(TerrainLayer[] layers)
        {
            if(layers.Length != 6) throw new InvalidOperationException("Vision Editor reference requires six prepared Terrain layers.");
            var saved_layers = new TerrainLayer[6];
            for(int index=0;index<layers.Length;index++)
                saved_layers[index] = save_generated_asset(layers[index],PREVIEW_ROOT+"Layer-"+index+".terrainlayer");
            return saved_layers;
        }

        /// <summary>Upsert only this tool's generated preview assets; never delete or mutate authored source assets.</summary>
        private static T save_generated_asset<T>(T source,string path) where T : UnityEngine.Object
        {
            if(!source || !path.StartsWith(PREVIEW_ROOT,StringComparison.Ordinal))
                throw new InvalidOperationException("Vision generated asset save outside owned scope or null source: "+path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing)
            {
                EditorUtility.CopySerialized(source,existing);
                existing.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            if(File.Exists(path)) throw new InvalidOperationException("Vision generated asset exists with unexpected type: "+path);
            AssetDatabase.CreateAsset(source,path);
            source.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
            EditorUtility.SetDirty(source);
            return source;
        }

        private static T load_saved_reference<T>(string filename) where T : UnityEngine.Object
        {
            string path = PREVIEW_ROOT+filename;
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if(!asset || !EditorUtility.IsPersistent(asset))
                throw new InvalidOperationException("Vision reference did not survive serialization: "+path);
            return asset;
        }

        private static void bind_saved_reference_assets(Terrain terrain,ShowcaseConceptWater water)
        {
            var data = load_saved_reference<TerrainData>("Terrain.asset");
            var layers = new TerrainLayer[6];
            for(int index=0;index<layers.Length;index++)
                layers[index] = load_saved_reference<TerrainLayer>("Layer-"+index+".terrainlayer");
            data.terrainLayers = layers;
            terrain.terrainData = data;
            terrain.GetComponent<TerrainCollider>().terrainData = data;
            terrain.materialTemplate = load_saved_reference<Material>("Terrain.mat");
            water.sea.sharedMaterial = load_saved_reference<Material>("Sea.mat");
            water.pond.sharedMaterial = load_saved_reference<Material>("Pond.mat");
            var height_field = load_saved_reference<Texture2D>("HeightField.asset");
            water.sea.sharedMaterial.SetTexture("_HeightField",height_field);
            water.pond.sharedMaterial.SetTexture("_HeightField",height_field);
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(water.sea.sharedMaterial);
            EditorUtility.SetDirty(water.pond.sharedMaterial);
            terrain.Flush();
        }

        private static void configure_reference_lighting(EnvironmentShowcaseConfig config,Transform parent)
        {
            var preset = config.active_dark_layout;
            RenderSettings.ambientMode = preset.ambient_mode;
            RenderSettings.ambientSkyColor = preset.ambient_sky;
            RenderSettings.ambientEquatorColor = preset.ambient_equator;
            RenderSettings.ambientGroundColor = preset.ambient_ground;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = preset.fog;
            RenderSettings.fogStartDistance = config.fog_start;
            RenderSettings.fogEndDistance = config.fog_end;
            var sun = new GameObject("Vision neutral directional light").AddComponent<Light>();
            sun.transform.SetParent(parent,false);
            sun.transform.rotation = Quaternion.Euler(preset.layout.sun_rotation);
            sun.type = LightType.Directional;
            sun.color = preset.sun;
            sun.intensity = preset.sun_intensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = .025f;
            sun.shadowNormalBias = .35f;
            RenderSettings.sun = sun;
        }

        private static Camera create_reference_camera(EnvironmentShowcaseConfig config,ShowcaseTerrain ground,Transform parent)
        {
            var camera = new GameObject("Vision Editor and Game view reference").AddComponent<Camera>();
            camera.transform.SetParent(parent,false);
            camera.orthographic = true;
            camera.orthographicSize = config.active_concept_layout.camera_sizes[1];
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 500;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.active_dark_layout.fog;
            camera.allowMSAA = true;
            var rotation = Quaternion.Euler(config.active_concept_layout.camera_pitch,config.active_concept_layout.camera_yaw,0);
            var point = config.active_concept_layout.views[0]*config.map_size;
            var focus = ground.sample_ground(point.x,point.y);
            camera.transform.SetPositionAndRotation(focus-rotation*Vector3.forward*camera.orthographicSize*2.3f,rotation);
            return camera;
        }

        public static void select_vision(EnvironmentShowcaseConfig config)
        {
            config.vision_enabled = true;
            config.flat_enabled = true;
            config.dark_enabled = true;
            config.concept_enabled = true;
        }

        private static void align_editor_view()
        {
            var marker = UnityEngine.Object.FindFirstObjectByType<VisionEditorReference>();
            var scene_view = SceneView.lastActiveSceneView;
            if(!marker || !marker.comparison_camera || !scene_view) return;
            var camera = marker.comparison_camera;
            scene_view.rotation = camera.transform.rotation;
            scene_view.pivot = camera.transform.position+camera.transform.forward*camera.orthographicSize*2.3f;
            scene_view.size = camera.orthographicSize;
            scene_view.orthographic = true;
            scene_view.Repaint();
        }

        private static void guard_saved_scenes()
        {
            for(int index=0;index<SceneManager.sceneCount;index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if(scene.isDirty) throw new InvalidOperationException("Preserve unsaved scene before Vision preparation: "+scene.path);
            }
        }
    }
}
