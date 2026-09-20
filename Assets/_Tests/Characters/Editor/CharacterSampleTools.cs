using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ZombieGame.CombatTests;

namespace ZombieGame.EditorTools
{
    public static class CharacterSampleTools
    {
        private const string SCENE = "Assets/_Tests/Characters/Scenes/CharacterSample.unity";

        [MenuItem("Tools/Zombie Game/Characters/Open Playable Model Sample")]
        public static void open_sample()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play before opening model sample"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CharacterBake.ensure_models();
            if (File.Exists(SCENE)) EditorSceneManager.OpenScene(SCENE); else create_sample();
        }

        public static void create_sample()
        {
            CharacterBake.ensure_models();
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 9;
            camera.transform.position = new Vector3(0,25,-20); camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f,.06f,.08f);
            var game = new GameObject("Animated RTS Character Sample").AddComponent<CombatSandbox>();
            game.use_character_models = true;
            game.material_template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Tests/Performance/MovementBenchmark/Runtime/MovementInstanced.mat");
            EditorSceneManager.SaveScene(scene, SCENE);
        }

        public static void build_sample()
        {
            CharacterBake.bake_models(); create_sample();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { SCENE },
                locationPathName = "Builds/CharacterSample.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Character sample build failed");
            Debug.Log("[CharacterSampleBuild] PASS");
        }

        public static void build_players()
        {
            build_sample();
            CombatStressTools.build_player();
        }
    }
}
