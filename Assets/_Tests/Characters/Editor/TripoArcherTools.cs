using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieGame.CharacterTests;
using ZombieGame.EditorTools;
using ZombieGame.Presentation;

namespace ZombieGame.CharacterTests.Editor
{
    public static class TripoArcherTools
    {
        private static string scene_path => "Assets/_Tests/Characters/Scenes/" + (original_only ? "TripoArcherCombat" : "TripoCrossbowCombat") + ".unity";
        private static bool original_only = true;

        [MenuItem("Tools/Zombie Game/Characters/Build Original Tripo 10K Baseline")]
        public static void build_original_review()
        {
            original_only = true;
            build_review();
        }

        [MenuItem("Tools/Zombie Game/Characters/Build Tripo Crossbow Battle")]
        public static void build_crossbow_review()
        {
            original_only = false;
            build_review();
        }

        public static void build_review()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CharacterBake.ensure_models();
            if (original_only) { TripoArcherImport.bake_original_rest(); CharacterVisualChecks.run_tripo_original(); }
            else { TripoArcherImport.import_and_bake(); TripoArcherImport.import_exploder(); CharacterVisualChecks.run_tripo(); CharacterVisualChecks.run_explosion_feedback(); }
            create_scene();
            Directory.CreateDirectory("Builds/TripoArcher");
            string app_path = original_only ? "Builds/TripoArcher/TripoArcher.app" : "Builds/TripoArcher/TripoCrossbow.app";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene_path },
                locationPathName = app_path, target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Tripo build failed");
            Debug.Log("[TripoArcher] BUILD PASS");
        }

        private static void create_scene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(.65f, .67f, .7f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = false;
            QualitySettings.antiAliasing = 2;
            var light = new GameObject("Review sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            light.shadows = LightShadows.None;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.transform.position = new Vector3(0, 100, -90);
            camera.transform.LookAt(Vector3.zero);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 400;
            camera.backgroundColor = new Color(.17f, .19f, .19f);
            camera.allowHDR = false;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "256 x 256 test ground";
            floor.transform.position = new Vector3(0, -.1f, 0);
            floor.transform.localScale = new Vector3(256, .2f, 256);
            var floor_material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Tests/Characters/TripoGround.mat");
            if (floor_material == null)
            {
                floor_material = new Material(Shader.Find("Standard")) { color = new Color(.25f, .29f, .21f) };
                floor_material.SetFloat("_Glossiness", 0);
                AssetDatabase.CreateAsset(floor_material, "Assets/_Tests/Characters/TripoGround.mat");
            }
            floor.GetComponent<Renderer>().sharedMaterial = floor_material;
            var fixture = new GameObject("Tripo archer - production combat fixture").AddComponent<TripoArcherBattle>();
            fixture.original_rest_only = original_only;
            fixture.human_unit_id = original_only ? "archer" : "heavy_crossbowman";
            fixture.sample_humans = 3; fixture.sample_zombies = 18;
            fixture.status = original_only ? "Original Tripo model (no animation)" : "Tripo crossbow: original skeleton / shooting and reloading review";
            fixture.archer_frames = AssetDatabase.LoadAssetAtPath<CharacterFrames>(original_only ? TripoArcherImport.REST_PATH : TripoArcherImport.FRAME_PATH);
            if (!original_only) fixture.exploder_frames = AssetDatabase.LoadAssetAtPath<CharacterFrames>(TripoArcherImport.EXPLODER_PATH);
            fixture.fog_template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Resources/FrontierFog.mat");
            Directory.CreateDirectory(Path.GetDirectoryName(scene_path));
            EditorSceneManager.SaveScene(scene, scene_path);
            AssetDatabase.SaveAssets();
        }
    }
}
