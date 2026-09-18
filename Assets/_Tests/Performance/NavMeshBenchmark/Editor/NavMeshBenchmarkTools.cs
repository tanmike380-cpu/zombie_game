using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieGame.PerformanceTests;

namespace ZombieGame.EditorTools
{
    public static class NavMeshBenchmarkTools
    {
        private const string SCENE_PATH = "Assets/_Tests/Performance/NavMeshBenchmark/Scenes/Tech_NavMeshBenchmark.unity";

        [MenuItem("Tools/Zombie Game/NavMesh/Open 5K 10K Test")]
        public static void open_scene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play before opening NavMesh test"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(SCENE_PATH)) EditorSceneManager.OpenScene(SCENE_PATH);
            else create_scene();
        }

        public static void create_scene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera_object = new GameObject("Main Camera");
            camera_object.tag = "MainCamera";
            var camera = camera_object.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .07f, .085f);
            camera.orthographic = true;
            camera.orthographicSize = 143;
            camera.farClipPlane = 500;
            camera.transform.position = new Vector3(0, 200, 0);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            var benchmark = new GameObject("Official NavMesh Benchmark").AddComponent<NavMeshBenchmark>();
            benchmark.instance_template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Tests/Performance/MovementBenchmark/Runtime/MovementInstanced.mat");
            if (benchmark.instance_template == null) throw new InvalidOperationException("Missing benchmark material");
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
        }

        public static void build_player()
        {
            create_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { SCENE_PATH },
                locationPathName = "Builds/NavMeshBenchmark.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("NavMesh test build failed");
            Debug.Log("[NavMeshBuild] PASS");
        }
    }
}
