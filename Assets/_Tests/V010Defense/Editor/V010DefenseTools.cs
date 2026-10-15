using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieGame.Balance;
using ZombieGame.Presentation;
using ZombieGame.Tests.EnvironmentShowcase;
using ZombieGame.World;

namespace ZombieGame.Tests.V010Defense.EditorTools
{
    /// <summary>Compile the frozen dense art layout into an independent production-data scenario; never starts the review controller.</summary>
    public static class V010DefenseTools
    {
        public const string SCENE_PATH = "Assets/_Tests/V010Defense/Scenes/V010Defense.unity";
        public const string SCENARIO_PATH = "Assets/_Game/Art/V010Defense/DefenseScenario.asset";
        public const string PREFAB_PATH = "Assets/_Game/Art/V010Defense/FrozenEnvironment.prefab";
        public const string BUILD_PATH = "Builds/V010Defense.app";
        private const string CONFIG_PATH = "Assets/_Tests/EnvironmentShowcase/Settings/VisionDenseShowcaseV6.asset";
        private const string ART_ROOT = "Assets/_Game/Art/V010Defense";
        private static readonly Vector3 GROUND_TRANSLATION = new Vector3(0,-4.2f,0);

        [MenuItem("Zombie Game/V0.10/Prepare frozen defense scene")]
        public static void prepare_defense_scene()
        {
            guard_editor();
            // Scene switching may unload assets that have no serialized reference in the
            // previous scenario. Load the source config only after the old scene is gone.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(CONFIG_PATH);
            if(!config || config.map_size != 128 || !config.vision_layout || !config.vision_layout.fortified_city)
                throw new InvalidOperationException("V0.10 requires the frozen 128m dense v6 map; no substitute layout is generated.");
            Directory.CreateDirectory(ART_ROOT);Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
            var working = UnityEngine.Object.Instantiate(config);
            working.hideFlags = HideFlags.HideAndDontSave;
            ZombieGame.Tests.EditorTools.VisionWildernessTools.select_vision(working);
            working.validate_config();
            var root = new GameObject("FrozenDenseEnvironment");
            var ground = new ShowcaseTerrain(working,root.transform,working.biomes[0]);
            var landmarks = new ShowcaseLandmarks(working,ground,root.transform);
            var walls = new ShowcaseFortifications(working,ground,root.transform);
            _ = new ShowcaseConceptWater(ground,root.transform,true);
            using(var foliage = new ShowcaseFoliage(working,ground))
            {
                var view = root.AddComponent<FrozenEnvironmentView>();
                view.batches = foliage.snapshot_batches();view.frozen_tree_count = foliage.tree_instances;view.frozen_grass_count = foliage.grass_instances;
                view.tree_shadow_distance = working.tree_shadow_draw_distance;
                if(view.frozen_tree_count != 81)throw new InvalidOperationException("Frozen v6 tree placements changed: expected 81, found "+view.frozen_tree_count);
                var scenario = prepare_scenario(working,root,ground,landmarks,walls,foliage.tree_positions);
                var snapshot = new V010AssetSnapshot(ART_ROOT+"/RenderData");
                snapshot.freeze_root(root);
                scenario.environment_prefab = PrefabUtility.SaveAsPrefabAsset(root,PREFAB_PATH);
                scenario.source_record += "\n"+string.Join("\n",snapshot.records);
                scenario.validate();EditorUtility.SetDirty(scenario);
                UnityEngine.Object.DestroyImmediate(root);
                create_game_entry(working,scenario);
            }
            UnityEngine.Object.DestroyImmediate(working);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,SCENE_PATH);
            Debug.Log("[V0.10] Prepared frozen 128m defense: 64 humans, 240 zombies, three 80-unit waves after 60s; original production battle/controller and source transforms retained.");
        }

        public static void build_defense_player()
        {
            prepare_defense_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes = new[]{SCENE_PATH},locationPathName = BUILD_PATH,
                target = BuildTarget.StandaloneOSX,options = BuildOptions.None});
            if(report.summary.result != BuildResult.Succeeded)throw new InvalidOperationException("V0.10 defense build failed: "+report.summary.result);
        }

        [MenuItem("Zombie Game/V0.10/Open playable V0.10")]
        public static void open_defense_play()
        {
            guard_editor();
            if(!File.Exists(SCENE_PATH))throw new FileNotFoundException("Prepare the frozen defense scene before playing.",SCENE_PATH);
            EditorSceneManager.OpenScene(SCENE_PATH);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.EnterPlaymode();
        }

        public static void open_defense_checks()
        {
            guard_editor();EditorSceneManager.OpenScene(SCENE_PATH);
            var checks = UnityEngine.Object.FindFirstObjectByType<V010DefenseChecks>();
            if(!checks)throw new InvalidOperationException("V0.10 scene has no thin integration checks.");
            checks.run_on_start = true;EditorApplication.EnterPlaymode();
        }

        private static DefenseScenario prepare_scenario(EnvironmentShowcaseConfig config,GameObject root,ShowcaseTerrain ground,
            ShowcaseLandmarks landmarks,ShowcaseFortifications walls,List<Vector2> trees)
        {
            var scenario = AssetDatabase.LoadAssetAtPath<DefenseScenario>(SCENARIO_PATH);
            if(!scenario){scenario = ScriptableObject.CreateInstance<DefenseScenario>();AssetDatabase.CreateAsset(scenario,SCENARIO_PATH);}
            scenario.version = "V0.10 defense integration preview 1";
            scenario.source_record = "Frozen baseline v0.10.0-baseline / 63946b1; "+CONFIG_PATH+"; art/grid_footprint_handoff.json; art/fortified_city_handoff.json; exact v6 placement snapshot, whole environment Y=-4.2m. Crops are walkable; original hut source AABB blocks. Rounded navigation experiment OFF after full-wave failure. Scenario-only crop renderers CAST shadows OFF, RECEIVE shadows ON; houses/walls/actors and source assets unchanged. No art acceptance implied.";
            scenario.environment_translation = GROUND_TRANSLATION;scenario.world_bounds = new Rect(-64,-64,128,128);
            scenario.headquarters_position = ground.sample_ground(68,48)+GROUND_TRANSLATION;
            scenario.base_center = ground.sample_ground(68,35)+GROUND_TRANSLATION;
            scenario.initial_depot = ground.sample_ground(68,56)+GROUND_TRANSLATION;
            scenario.initial_camera_focus = ground.sample_ground(68,51)+GROUND_TRANSLATION;
            scenario.camera_pitch = 48;scenario.camera_yaw = 0;scenario.initial_camera_size = 25;
            scenario.infection_reserve = 256;scenario.allow_new_buildings = false;
            scenario.crop_cast_shadows = false;
            var lighting = config.active_dark_layout;
            scenario.shadow_distance = lighting.vision_shadow_distance;
            scenario.shadow_cascades = lighting.vision_shadow_cascades == 1 ? 0 : lighting.vision_shadow_cascades;
            scenario.shadow_resolution = lighting.vision_shadow_resolution;
            scenario.available_unit_recipes = new[]{"archer","greek_fire"};
            scenario.session = new CoastalSessionConfig {initial_humans = 64,zombies = 240,preparation_seconds = 60,wave_interval_seconds = 75,wave_counts = new[]{80,80,80}};
            var regions = new List<DefenseScenario.Region>();
            add_outer_boundary(regions);
            add_terrain_obstacles(ground,regions);
            for(int index = 0; index < trees.Count; index++)
                add_region(regions,new Bounds(new Vector3(trees[index].x-64,1,trees[index].y-64),new Vector3(1.8f,2,1.8f)),LandscapeKind.Forest,"FROZEN TREE "+index,null);
            for(int index = 0; index < landmarks.instances.Count; index++)
            {
                var model = landmarks.instances[index];var source = landmarks.placements[index].source_id;
                model.name = "FrozenFixture-"+index.ToString("D2")+"-"+source;
                if(source == "Headquarters" || source == "Workshop")
                    add_region(regions,translated_bounds(model),LandscapeKind.Building,source == "Headquarters" ? "COMMAND HALL" : "ARROW WORKS",relative_path(root.transform,model.transform));
                else if(source == "Pasture")add_crop_hut_obstacle(model,regions,index);
                else add_region(regions,translated_bounds(model),LandscapeKind.Cliff,"NEUTRAL SOURCE "+source,null);
            }
            walls.instance.name = "FrozenFortifiedCity";
            foreach(var filter in walls.instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var bounds = transform_bounds(filter.sharedMesh.bounds,filter.transform.localToWorldMatrix);bounds.center += GROUND_TRANSLATION;
                add_region(regions,bounds,LandscapeKind.Building,"STONE WALL "+filter.name,relative_path(root.transform,filter.transform));
            }
            scenario.regions = regions.ToArray();
            scenario.humans = create_human_spawns(regions);
            scenario.zombies = create_zombie_spawns(regions);
            return scenario;
        }

        private static void add_outer_boundary(List<DefenseScenario.Region> regions)
        {
            add_region(regions,new Bounds(new Vector3(-96,1,0),new Vector3(64,4,256)),LandscapeKind.Cliff,"SCENARIO OUTSIDE WEST",null);
            add_region(regions,new Bounds(new Vector3(96,1,0),new Vector3(64,4,256)),LandscapeKind.Cliff,"SCENARIO OUTSIDE EAST",null);
            add_region(regions,new Bounds(new Vector3(0,1,-96),new Vector3(128,4,64)),LandscapeKind.Cliff,"SCENARIO OUTSIDE SOUTH",null);
            add_region(regions,new Bounds(new Vector3(0,1,96),new Vector3(128,4,64)),LandscapeKind.Cliff,"SCENARIO OUTSIDE NORTH",null);
        }

        private static void add_terrain_obstacles(ShowcaseTerrain ground,List<DefenseScenario.Region> regions)
        {
            // Merge consecutive one-metre non-flat samples into row boxes; never permit units on water, banks or boundary caps.
            for(int z = 0; z < 128; z++)
            {
                int start = -1;
                for(int x = 0; x <= 128; x++)
                {
                    bool blocked = x < 128 && !has_flat_cell(ground,x,z);
                    if(blocked && start < 0)start = x;
                    if(blocked || start < 0)continue;
                    var bounds = new Bounds(new Vector3((start+x)*.5f-64,1,z+.5f-64),new Vector3(x-start,4,1));
                    add_region(regions,bounds,LandscapeKind.River,"NON-ACTIVITY HEIGHTFIELD",null);start = -1;
                }
            }
        }

        private static bool has_flat_cell(ShowcaseTerrain ground,int x,int z)
        {
            foreach(float dx in new[]{0f,.5f,1f})foreach(float dz in new[]{0f,.5f,1f})
                if(Mathf.Abs(ground.sample_authored_height(x+dx,z+dz)-4.2f) > .008f)return false;
            return true;
        }

        private static void add_crop_hut_obstacle(GameObject model,List<DefenseScenario.Region> regions,int index)
        {
            const float NAV_ROUNDING_M = 0;
            Bounds field_bounds = default;bool has_field = false;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.name.EndsWith("_ground",StringComparison.OrdinalIgnoreCase))continue;
                field_bounds = transform_bounds(filter.sharedMesh.bounds,filter.transform.localToWorldMatrix);field_bounds.center += GROUND_TRANSLATION;
                has_field = true;break;
            }
            if(!has_field || Mathf.Abs(field_bounds.size.x-14) > .0001f || Mathf.Abs(field_bounds.size.z-14) > .0001f)
                throw new InvalidOperationException("Rounded hut requires the existing exact 14m source ground component: "+model.name);
            bool found = false;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.name.IndexOf("architecture",StringComparison.OrdinalIgnoreCase) < 0)continue;
                var bounds = transform_bounds(filter.sharedMesh.bounds,filter.transform.localToWorldMatrix);bounds.center += GROUND_TRANSLATION;
                if(bounds.min.x-NAV_ROUNDING_M < field_bounds.min.x-.0001f || bounds.max.x+NAV_ROUNDING_M > field_bounds.max.x+.0001f ||
                    bounds.min.z-NAV_ROUNDING_M < field_bounds.min.z-.0001f || bounds.max.z+NAV_ROUNDING_M > field_bounds.max.z+.0001f)
                    throw new InvalidOperationException("Rounded hut navigation exceeds its unchanged 14m field: "+model.name);
                regions.Add(new DefenseScenario.Region {bounds = bounds,kind = LandscapeKind.Cliff,label = "FARM HUT ONLY "+index,
                    model_path = model.name+"/"+relative_path(model.transform,filter.transform),nav_rounding_radius = NAV_ROUNDING_M});
                found = true;
            }
            if(!found)throw new InvalidOperationException("Frozen 14m crop field has no traceable original architecture component: "+model.name);
        }

        private static DefenseScenario.Spawn[] create_human_spawns(List<DefenseScenario.Region> regions)
        {
            var spawns = new List<DefenseScenario.Spawn>();
            foreach(var position in new[]{new Vector3(-1,0,-7.5f),new Vector3(9,0,-7.5f)})
            {
                if(!is_clear(position,UnitBalance.navigation_radius(UnitBalance.get("greek_fire"))+.05f,regions))continue;
                spawns.Add(new DefenseScenario.Spawn {position = position,unit_id = "greek_fire"});
            }
            for(float z = -6; z >= -48 && spawns.Count < 64; z -= 1.5f)
                for(float x = -10; x <= 18 && spawns.Count < 64; x += 1.5f)
                {
                    const string id = "archer";
                    var point = new Vector3(x,0,z);
                    if(is_spawn_clear(point,id,regions,spawns))spawns.Add(new DefenseScenario.Spawn {position = point,unit_id = id});
                }
            if(spawns.Count != 64)throw new InvalidOperationException("Frozen city cannot fit 64 source-model defenders without overlaps.");
            return spawns.ToArray();
        }

        private static DefenseScenario.Spawn[] create_zombie_spawns(List<DefenseScenario.Region> regions)
        {
            var spawns = new List<DefenseScenario.Spawn>();
            for(float z = 21; z < 51 && spawns.Count < 240; z += 1.35f)
                for(float x = -23; x < 58 && spawns.Count < 240; x += 1.35f)
                {
                    int index = spawns.Count;
                    string id = index%20 == 0 ? "zombie_hound" : index%5 == 0 ? "exploder" : "walker";
                    var point = new Vector3(x,0,z);
                    if(is_spawn_clear(point,id,regions,spawns))spawns.Add(new DefenseScenario.Spawn {position = point,unit_id = id});
                }
            if(spawns.Count != 240)throw new InvalidOperationException("Frozen north approach cannot fit 240 zombies without touching source obstacles.");
            return spawns.ToArray();
        }

        private static bool is_spawn_clear(Vector3 point,string id,List<DefenseScenario.Region> regions,List<DefenseScenario.Spawn> spawns)
        {
            float radius = UnitBalance.navigation_radius(UnitBalance.get(id));
            // Spawn fixtures leave two voxels beyond the actual baked profile clearance.
            // This changes placement only, never the unit's authored navigation/contact radius.
            float bake_clearance = Mathf.Max(radius,UnitBalance.config.unit_navigation_radius)+.2f;
            if(!is_clear(point,bake_clearance,regions))return false;
            foreach(var spawn in spawns)
                if(Vector3.Distance(point,spawn.position) < radius+UnitBalance.navigation_radius(UnitBalance.get(spawn.unit_id))+.05f)return false;
            return true;
        }

        private static bool is_clear(Vector3 point,float radius,List<DefenseScenario.Region> regions)
        {
            foreach(var region in regions)
            {
                float margin = radius+region.nav_rounding_radius;
                if(point.x > region.bounds.min.x-margin && point.x < region.bounds.max.x+margin && point.z > region.bounds.min.z-margin && point.z < region.bounds.max.z+margin)return false;
            }
            return true;
        }

        private static void add_region(List<DefenseScenario.Region> regions,Bounds bounds,LandscapeKind kind,string label,string path)
        {regions.Add(new DefenseScenario.Region {bounds = bounds,kind = kind,label = label,model_path = path});}

        private static Bounds translated_bounds(GameObject model)
        {
            Bounds bounds = default;bool first = true;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {if(first){bounds = renderer.bounds;first = false;}else bounds.Encapsulate(renderer.bounds);}
            if(first)throw new InvalidOperationException("Frozen source has no geometry: "+model.name);
            bounds.center += GROUND_TRANSLATION;return bounds;
        }

        private static Bounds transform_bounds(Bounds bounds,Matrix4x4 matrix)
        {
            var x = matrix.MultiplyVector(Vector3.right*bounds.extents.x);var y = matrix.MultiplyVector(Vector3.up*bounds.extents.y);var z = matrix.MultiplyVector(Vector3.forward*bounds.extents.z);
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center),new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*2);
        }

        private static string relative_path(Transform root,Transform model)
        {
            string path = model.name;
            while(model.parent != root){model = model.parent;if(!model)throw new InvalidOperationException("Combat model is outside its frozen root.");path = model.name+"/"+path;}
            return path;
        }

        private static void create_game_entry(EnvironmentShowcaseConfig config,DefenseScenario scenario)
        {
            var preset = config.active_dark_layout;
            RenderSettings.ambientMode = preset.ambient_mode;RenderSettings.ambientSkyColor = preset.ambient_sky;
            RenderSettings.ambientEquatorColor = preset.ambient_equator;RenderSettings.ambientGroundColor = preset.ambient_ground;
            RenderSettings.fog = true;RenderSettings.fogMode = FogMode.Linear;RenderSettings.fogColor = preset.fog;
            RenderSettings.fogStartDistance = config.fog_start;RenderSettings.fogEndDistance = config.fog_end;
            var sun = new GameObject("Frozen v6 neutral daylight").AddComponent<Light>();sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(preset.layout.sun_rotation);sun.color = preset.sun;sun.intensity = preset.sun_intensity;
            sun.shadows = LightShadows.Soft;sun.shadowBias = .025f;sun.shadowNormalBias = .35f;RenderSettings.sun = sun;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();camera.tag = "MainCamera";camera.orthographic = true;
            camera.orthographicSize = scenario.initial_camera_size;camera.nearClipPlane = .1f;camera.farClipPlane = 500;camera.backgroundColor = preset.fog;
            camera.allowHDR = false;camera.allowMSAA = true;camera.clearFlags = CameraClearFlags.SolidColor;
            var rotation = Quaternion.Euler(scenario.camera_pitch,scenario.camera_yaw,0);
            camera.transform.SetPositionAndRotation(scenario.initial_camera_focus-rotation*Vector3.forward*Mathf.Max(50,scenario.initial_camera_size*2.3f),rotation);
            var game = new GameObject("V0.10 — production FrontierGame").AddComponent<FrontierGame>();
            game.defense_scenario = scenario;game.coastal_defense = true;game.coastal_skirmish = true;
            game.imported_roster = AssetDatabase.LoadAssetAtPath<ImportedRoster>("Assets/_Game/Resources/CoastalUnits/Roster.asset");
            game.fog_template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Resources/FrontierFog.mat");game.landscape_shader = Shader.Find("ZombieGame/FrontierSurface");
            if(!game.imported_roster || !game.fog_template || !game.landscape_shader)throw new InvalidOperationException("V0.10 requires the existing imported roster, fog and production surface shader.");
            game.gameObject.AddComponent<V010DefenseChecks>();
            // A serialized real Terrain retains Unity terrain resources in the player; it is hidden when FrontierGame instantiates the frozen root.
            var placeholder = UnityEngine.Object.Instantiate(scenario.environment_prefab);
            placeholder.name = "Serialized frozen environment reference — disabled during gameplay";placeholder.transform.position = GROUND_TRANSLATION;
            placeholder.GetComponent<FrozenEnvironmentView>().set_crop_shadows(scenario.crop_cast_shadows);
            var marker = game.gameObject.AddComponent<V010SceneReference>();marker.reference = placeholder;
        }

        private static void guard_editor()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before preparing or opening V0.10.");
            for(int index = 0; index < SceneManager.sceneCount; index++)
                if(SceneManager.GetSceneAt(index).isDirty)throw new InvalidOperationException("Save the current Editor scene before switching to V0.10.");
        }
    }
}
