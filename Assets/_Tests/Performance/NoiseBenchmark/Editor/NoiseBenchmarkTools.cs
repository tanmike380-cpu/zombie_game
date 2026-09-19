using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieGame.PerformanceTests;

namespace ZombieGame.EditorTools
{
    public static class NoiseBenchmarkTools
    {
        private const string SCENE_PATH = "Assets/_Tests/Performance/NoiseBenchmark/Scenes/Tech_NoiseBenchmark.unity";

        [MenuItem("Tools/Zombie Game/Noise/Open 10 Tile Test")]
        public static void open_scene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play before opening noise test"); return; }
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
            camera.backgroundColor = new Color(.045f, .06f, .075f);
            camera.orthographic = true;
            camera.orthographicSize = 14;
            camera.farClipPlane = 500;
            camera.transform.position = new Vector3(-2, 200, 0);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            var benchmark = new GameObject("10 Tile Noise Trigger Test").AddComponent<NoiseBenchmark>();
            benchmark.instance_template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Tests/Performance/MovementBenchmark/Runtime/MovementInstanced.mat");
            if (benchmark.instance_template == null) throw new InvalidOperationException("Missing benchmark material");
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Zombie Game/Noise/Validate Hearing Rules")]
        public static void validate_rules()
        {
            require(NoisePulseField.distance_percent(0) == 100, "Source is 100%");
            for (int tile = 1; tile <= 10; tile++)
                require(NoisePulseField.distance_percent(tile) == (11 - tile) * 10, "Wrong tile band: " + tile);
            require(NoisePulseField.distance_percent(10.001f) == 0, "Outside boundary heard");
            require(NoisePulseField.distance_percent(float.NaN) == 0, "NaN audible");
            var pulse = new NoisePulseField();
            pulse.emit(Vector3.zero, 0);
            require(pulse.sample_percent(new Vector3(10, 0, 0), 1.99) == 0, "Far unit heard before wave");
            require(pulse.sample_percent(new Vector3(10, 0, 0), 2) == 10, "Exact ten-tile boundary missing");
            require(pulse.sample_percent(new Vector3(6, 0, 8), 2) == 10, "Diagonal boundary missing");
            require(pulse.sample_percent(new Vector3(10.001f, 0, 0), 3) == 0, "Outside circle audible");
            require(pulse.sample_percent(new Vector3(10, 0, 0), 3) == 0, "Pulse never expired");
            var memory = new NoiseInvestigation();
            Vector3 old_source = new Vector3(3, 0, 0);
            require(memory.hear(old_source, 10), "Weak valid sound rejected");
            require(!memory.hear(Vector3.zero, 0) && memory.remembered_position == old_source, "Silence erased source memory");
            require(!memory.hear(old_source, 10), "Same source reissued path unnecessarily");
            require(memory.hear(Vector3.zero, 100), "Stronger new source ignored");
            require(memory.hear(old_source, 10), "New weak source cannot update expired source memory");
            memory.has_direct_target = true;
            require(!memory.hear(old_source, 100), "Noise overrode direct target");
            Debug.Log("[NoiseRules] PASS: 10 bands, exact/diagonal/outside boundary, delayed wave, expiry, weak-sound memory, source update, direct-target priority");
        }

        private static void require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Noise test failed: " + message);
        }

        public static void build_player()
        {
            validate_rules();
            create_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { SCENE_PATH },
                locationPathName = "Builds/NoiseBenchmark.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Noise Player build failed");
            Debug.Log("[NoiseBuild] PASS");
        }
    }
}
