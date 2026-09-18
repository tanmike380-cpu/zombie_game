using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieGame.PerformanceTests;
using Debug = UnityEngine.Debug;

namespace ZombieGame.EditorTools
{
    public static class MovementBenchmarkTools
    {
        private const string SCENE_PATH = "Assets/_Tests/Performance/MovementBenchmark/Scenes/Tech_MovementBenchmark.unity";

        [MenuItem("Tools/Zombie Game/Movement/Open Movement Benchmark")]
        public static void open_scene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play before opening the movement scene."); return; }
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
            Camera camera = camera_object.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .07f, .085f);
            camera.orthographic = true;
            camera.orthographicSize = 143;
            camera.farClipPlane = 500;
            camera.transform.position = new Vector3(0, 200, 0);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            var benchmark = new GameObject("Isolated Movement Benchmark").AddComponent<MovementBenchmark>();
            benchmark.instance_shader = Shader.Find("ZombieGame/Benchmark/InstancedUnlit");
            if (benchmark.instance_shader == null) throw new InvalidOperationException("Missing benchmark shader");
            const string material_path = "Assets/_Tests/Performance/MovementBenchmark/Runtime/MovementInstanced.mat";
            var template = AssetDatabase.LoadAssetAtPath<Material>(material_path);
            if (template == null)
            {
                template = new Material(benchmark.instance_shader) { enableInstancing = true };
                AssetDatabase.CreateAsset(template, material_path);
            }
            benchmark.instance_template = template;
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Zombie Game/Movement/Validate Navigation")]
        public static void validate_navigation()
        {
            var report = new System.Text.StringBuilder("scenario,count,ticks,arrived,invalid,cpu_ms\n");
            foreach (bool obstacles in new[] { false, true })
            {
                var field = new HordeNavigation(obstacles);
                int previous_goal = field.target_cell;
                require(!field.build_field(-1) && field.target_cell == previous_goal, "Invalid goal changed field");
                if (obstacles) require(!field.build_field(80), "Wall goal accepted");
                var units = new HordeMovement(10000, field);
                var timer = Stopwatch.StartNew();
                int ticks = 0;
                while (units.arrived_count < 10000 && ticks < 2000)
                {
                    units.step(.05f);
                    ticks++;
                    for (int i = 0; i < units.positions.Length; i++)
                    {
                        Vector3 position = units.positions[i];
                        require(!float.IsNaN(position.x) && !float.IsNaN(position.z), "Non-finite position");
                        int x = Mathf.FloorToInt(position.x + 128);
                        int z = Mathf.FloorToInt(position.z + 128);
                        require(x >= 0 && x < 256 && z >= 0 && z < 256, "Unit outside map");
                        require(!field.blocked[x + z * 256], "Unit entered wall");
                    }
                }
                timer.Stop();
                require(units.arrived_count == 10000 && units.invalid_count == 0, "Horde failed to arrive");
                report.AppendLine($"{obstacles},10000,{ticks},{units.arrived_count},{units.invalid_count},{timer.ElapsedMilliseconds}");
                require(field.build_field(10 + 128 * 256), "Retarget failed");
                units.retarget();
                for (int i = 0; i < 2000 && units.arrived_count < 10000; i++) units.step(.05f);
                require(units.arrived_count == 10000, "Return journey failed");
            }
            validate_unreachable();
            validate_tick_rates();
            Directory.CreateDirectory("Logs/MovementBenchmark");
            File.WriteAllText("Logs/MovementBenchmark/correctness.csv", report.ToString());
            Debug.Log("[MovementValidation] PASS: 10K open/walls, return journeys, invalid/unreachable goals, tick-rate equivalence.\n" + report);
        }

        private static void validate_unreachable()
        {
            var field = new HordeNavigation(false);
            for (int z = 0; z < 256; z++) field.blocked[100 + z * 256] = true;
            field.build_field(field.target_cell);
            require(field.distance[10 + 128 * 256] == -1, "Disconnected region considered reachable");
        }

        private static void validate_tick_rates()
        {
            var field = new HordeNavigation(true);
            var slow = new HordeMovement(1000, field);
            var fast = new HordeMovement(1000, field);
            for (int i = 0; i < 100; i++) slow.step(.1f);
            for (int i = 0; i < 300; i++) fast.step(1f / 30);
            for (int i = 0; i < 1000; i++)
                require(Vector3.Distance(slow.positions[i], fast.positions[i]) < .02f, "Tick-rate-dependent movement");
        }

        private static void require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Movement validation: " + message);
        }

        public static void build_test_player()
        {
            validate_navigation();
            create_scene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = "Builds/MovementBenchmark.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Movement Player build failed: " + report.summary.result);
            Debug.Log("[MovementBuild] PASS");
        }
    }
}
