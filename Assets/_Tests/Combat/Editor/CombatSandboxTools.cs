using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieGame.CombatTests;

namespace ZombieGame.EditorTools
{
    public static class CombatSandboxTools
    {
        private const string SCENE_PATH = "Assets/_Tests/Combat/Scenes/Tech_CombatSandbox.unity";

        [MenuItem("Tools/Zombie Game/Combat/Open Playable RTS Test")]
        public static void open_scene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play before opening combat test"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(SCENE_PATH)) EditorSceneManager.OpenScene(SCENE_PATH); else create_scene();
        }

        public static void create_scene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera_object = new GameObject("Main Camera"); camera_object.tag = "MainCamera";
            var camera = camera_object.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 13;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .045f);
            camera.farClipPlane = 200; camera.transform.position = new Vector3(0, 28, -18); camera.transform.LookAt(Vector3.zero);
            var game = new GameObject("Playable RTS Combat Test").AddComponent<CombatSandbox>();
            game.material_template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Tests/Performance/MovementBenchmark/Runtime/MovementInstanced.mat");
            if (game.material_template == null) throw new InvalidOperationException("Missing combat material template");
            EditorSceneManager.SaveScene(scene, SCENE_PATH); AssetDatabase.SaveAssets();
        }

        public static void build_player()
        {
            create_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { SCENE_PATH },
                locationPathName = "Builds/CombatSandbox.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Combat build failed");
            Debug.Log("[CombatBuild] PASS");
        }
    }
}
