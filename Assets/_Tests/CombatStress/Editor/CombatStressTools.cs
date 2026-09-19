using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieGame.CombatStressTests;

namespace ZombieGame.EditorTools
{
    public static class CombatStressTools
    {
        private const string SCENE_PATH = "Assets/_Tests/CombatStress/Scenes/Tech_CombatStress.unity";
        [MenuItem("Tools/Zombie Game/Combat/Open 400 vs 10000 Stress Test")]
        public static void open_scene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play before opening stress test"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(SCENE_PATH)) EditorSceneManager.OpenScene(SCENE_PATH); else create_scene();
        }
        public static void create_scene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera_object = new GameObject("Main Camera"); camera_object.tag = "MainCamera";
            var camera = camera_object.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 143;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.06f,.075f);
            camera.farClipPlane = 500; camera.transform.position = new Vector3(0,200,0); camera.transform.rotation = Quaternion.Euler(90,0,0);
            var benchmark = new GameObject("400 vs 10000 Combat Stress").AddComponent<CombatStressBenchmark>();
            benchmark.instance_template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Tests/Performance/MovementBenchmark/Runtime/MovementInstanced.mat");
            if (benchmark.instance_template == null) throw new InvalidOperationException("Missing instanced material");
            const string fog_path = "Assets/_Tests/CombatStress/Runtime/StressFog.mat";
            benchmark.fog_template = AssetDatabase.LoadAssetAtPath<Material>(fog_path);
            if (benchmark.fog_template == null)
            {
                Shader shader = Shader.Find("ZombieGame/StressFog");
                if (shader == null) throw new InvalidOperationException("Missing stress fog shader");
                benchmark.fog_template = new Material(shader);
                AssetDatabase.CreateAsset(benchmark.fog_template, fog_path);
            }
            EditorSceneManager.SaveScene(scene, SCENE_PATH); AssetDatabase.SaveAssets();
        }
        public static void build_player()
        {
            create_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { SCENE_PATH },
                locationPathName = "Builds/CombatStress.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Combat stress build failed");
            Debug.Log("[CombatStressBuild] PASS");
        }

        public static void build_both_players()
        {
            build_player();
            CombatSandboxTools.build_player();
        }
    }
}
