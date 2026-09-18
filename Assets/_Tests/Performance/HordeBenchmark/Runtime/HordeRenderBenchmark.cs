#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ZombieGame.PerformanceTests
{
    /// <summary>
    /// Render-only benchmark for the first technical spike.
    ///
    /// This intentionally does NOT use 10,000 GameObjects.
    /// It renders a shared low-poly mesh with GPU instancing so we can measure
    /// the baseline cost of drawing a large horde before adding movement, AI,
    /// pathfinding, combat, animation, fog of war, or networking.
    /// </summary>
    public sealed class HordeRenderBenchmark : MonoBehaviour
    {
        public const int MAP_TILE_COUNT = 256;
        public const float TILE_WORLD_SIZE = 1f;
        public const float MAP_WORLD_SIZE = MAP_TILE_COUNT * TILE_WORLD_SIZE;

        private const int MaxInstances = 10000;
        private const int SafeBatchSize = 1023;
        private const float AutoWarmupSeconds = 3f;
        private const float AutoSampleSeconds = 10f;
        private static readonly int[] AutoInstanceCounts = { 1000, 5000, 10000 };

        [Header("Benchmark")]
        [SerializeField, Range(1, MaxInstances)]
        private int instanceCount = MaxInstances;

        [SerializeField]
        private Vector3 instanceScale = new Vector3(0.55f, 1.2f, 0.55f);

        [SerializeField]
        private Color instanceColor = new Color(0.75f, 0.06f, 0.04f, 1f);

        [Header("Benchmark Settings")]
        [SerializeField]
        private bool disableVSync = true;

        private Mesh benchmarkMesh;
        private Material benchmarkMaterial;
        private Matrix4x4[] matrices;
        private RenderParams renderParams;
        private int builtForCount = -1;

        private float fpsTimer;
        private int fpsFrames;
        private float measuredFps;

        private readonly List<string> autoResults = new List<string>();
        private bool isAutoRunning;
        private int autoStageIndex;
        private float autoStageElapsed;
        private float autoSampleElapsed;
        private int autoSampleFrames;
        private float autoLowestFps = float.MaxValue;
        private bool hasCapturedScreenshot;
        private double lastDrawMilliseconds;
        private double autoDrawMilliseconds;

        private void Awake()
        {
            Application.runInBackground = true;
            if (disableVSync)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }

            CreateRuntimeResources();
            TryStartAutomatedBenchmark();
            RebuildInstances();
        }

        private void Update()
        {
            if (instanceCount != builtForCount)
            {
                RebuildInstances();
            }

            long drawStartTicks = Stopwatch.GetTimestamp();
            DrawInstances();
            lastDrawMilliseconds = (Stopwatch.GetTimestamp() - drawStartTicks) * 1000.0 / Stopwatch.Frequency;
            UpdateFpsCounter();
            UpdateAutomatedBenchmark();
        }

        private void OnDestroy()
        {
            if (benchmarkMaterial != null)
            {
                Destroy(benchmarkMaterial);
            }

            if (benchmarkMesh != null)
            {
                Destroy(benchmarkMesh);
            }
        }

        private void CreateRuntimeResources()
        {
            benchmarkMesh = CreateLowPolyUnitMesh();

            Shader shader = Shader.Find("ZombieGame/Benchmark/InstancedUnlit");
            if (shader == null)
            {
                Debug.LogError("Horde benchmark could not find its instanced shader.");
                enabled = false;
                return;
            }

            if (!shader.isSupported)
            {
                Debug.LogError($"Horde benchmark shader is not supported by {SystemInfo.graphicsDeviceName}.");
                enabled = false;
                return;
            }

            Debug.Log($"[HordeBenchmarkShader] {shader.name}, Instancing={SystemInfo.supportsInstancing}");

            benchmarkMaterial = new Material(shader)
            {
                name = "Runtime_HordeBenchmark_Material",
                enableInstancing = true
            };

            if (benchmarkMaterial.HasProperty("_BaseColor"))
            {
                benchmarkMaterial.SetColor("_BaseColor", instanceColor);
            }
            else if (benchmarkMaterial.HasProperty("_Color"))
            {
                benchmarkMaterial.SetColor("_Color", instanceColor);
            }

            renderParams = new RenderParams(benchmarkMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(
                    Vector3.zero,
                    new Vector3(MAP_WORLD_SIZE + 10f, 20f, MAP_WORLD_SIZE + 10f))
            };
        }

        private void RebuildInstances()
        {
            instanceCount = Mathf.Clamp(instanceCount, 1, MaxInstances);
            matrices = new Matrix4x4[instanceCount];

            int columns = Mathf.CeilToInt(Mathf.Sqrt(instanceCount));
            int rows = Mathf.CeilToInt(instanceCount / (float)columns);

            float spacingX = MAP_WORLD_SIZE / Mathf.Max(1, columns - 1);
            float spacingZ = MAP_WORLD_SIZE / Mathf.Max(1, rows - 1);
            float startX = -MAP_WORLD_SIZE * 0.5f;
            float startZ = -MAP_WORLD_SIZE * 0.5f;

            for (int i = 0; i < instanceCount; i++)
            {
                int x = i % columns;
                int z = i / columns;

                // A tiny deterministic offset prevents the horde from looking like a perfect spreadsheet.
                float jitterX = Mathf.Sin(i * 12.9898f) * spacingX * 0.18f;
                float jitterZ = Mathf.Sin(i * 78.233f) * spacingZ * 0.18f;

                Vector3 position = new Vector3(
                    startX + x * spacingX + jitterX,
                    instanceScale.y * 0.5f,
                    startZ + z * spacingZ + jitterZ);

                matrices[i] = Matrix4x4.TRS(position, Quaternion.identity, instanceScale);
            }

            builtForCount = instanceCount;

            if (benchmarkMaterial != null)
            {
                renderParams.worldBounds = new Bounds(
                    Vector3.zero,
                    new Vector3(MAP_WORLD_SIZE + 10f, 20f, MAP_WORLD_SIZE + 10f));
            }
        }

        private void DrawInstances()
        {
            if (benchmarkMesh == null || benchmarkMaterial == null || matrices == null)
            {
                return;
            }

            int start = 0;
            int remaining = instanceCount;

            while (remaining > 0)
            {
                int batchCount = Mathf.Min(SafeBatchSize, remaining);

                Graphics.RenderMeshInstanced(
                    renderParams,
                    benchmarkMesh,
                    0,
                    matrices,
                    batchCount,
                    start);

                start += batchCount;
                remaining -= batchCount;
            }
        }

        private void UpdateFpsCounter()
        {
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;

            if (fpsTimer >= 0.5f)
            {
                measuredFps = fpsFrames / fpsTimer;
                fpsFrames = 0;
                fpsTimer = 0f;
            }
        }

        private void TryStartAutomatedBenchmark()
        {
            if (Environment.GetEnvironmentVariable("ZOMBIE_BENCHMARK_AUTORUN") != "1")
            {
                return;
            }

            isAutoRunning = true;
            instanceCount = AutoInstanceCounts[0];
            autoResults.Add("instances,average_fps,lowest_fps,average_draw_submission_ms");
            Debug.Log($"[HordeBenchmarkStart] Map={MAP_TILE_COUNT}x{MAP_TILE_COUNT}, Device={SystemInfo.graphicsDeviceName}");
        }

        private void UpdateAutomatedBenchmark()
        {
            if (!isAutoRunning)
            {
                return;
            }

            float frameDeltaSeconds = Time.unscaledDeltaTime;
            autoStageElapsed += frameDeltaSeconds;

            if (autoStageIndex == AutoInstanceCounts.Length - 1 &&
                !hasCapturedScreenshot &&
                autoStageElapsed >= 1f)
            {
                string screenshotPath = Environment.GetEnvironmentVariable("ZOMBIE_BENCHMARK_SCREENSHOT_PATH");
                if (!string.IsNullOrWhiteSpace(screenshotPath))
                {
                    ScreenCapture.CaptureScreenshot(screenshotPath);
                    Debug.Log($"[HordeBenchmarkScreenshot] Path={screenshotPath}");
                }

                hasCapturedScreenshot = true;
            }

            if (autoStageElapsed <= AutoWarmupSeconds)
            {
                return;
            }

            autoSampleElapsed += frameDeltaSeconds;
            autoSampleFrames++;
            autoDrawMilliseconds += lastDrawMilliseconds;
            autoLowestFps = Mathf.Min(autoLowestFps, 1f / Mathf.Max(frameDeltaSeconds, 0.000001f));

            if (autoSampleElapsed < AutoSampleSeconds)
            {
                return;
            }

            float averageFps = autoSampleFrames / autoSampleElapsed;
            double averageDrawMilliseconds = autoDrawMilliseconds / autoSampleFrames;
            autoResults.Add($"{instanceCount},{averageFps:F1},{autoLowestFps:F1},{averageDrawMilliseconds:F2}");
            Debug.Log($"[HordeBenchmarkResult] Instances={instanceCount}, AverageFPS={averageFps:F1}, LowestFPS={autoLowestFps:F1}, DrawSubmissionMs={averageDrawMilliseconds:F2}");

            autoStageIndex++;
            if (autoStageIndex >= AutoInstanceCounts.Length)
            {
                CompleteAutomatedBenchmark();
                return;
            }

            instanceCount = AutoInstanceCounts[autoStageIndex];
            autoStageElapsed = 0f;
            autoSampleElapsed = 0f;
            autoSampleFrames = 0;
            autoDrawMilliseconds = 0;
            autoLowestFps = float.MaxValue;
        }

        private void CompleteAutomatedBenchmark()
        {
            isAutoRunning = false;
            string resultPath = Environment.GetEnvironmentVariable("ZOMBIE_BENCHMARK_RESULT_PATH");

            if (string.IsNullOrWhiteSpace(resultPath))
            {
                resultPath = Path.Combine(Path.GetTempPath(), "zombie-horde-benchmark.csv");
            }

            File.WriteAllLines(resultPath, autoResults);
            Debug.Log($"[HordeBenchmarkComplete] Results={resultPath}");

            // Keep the completed 10K scene visible for interactive inspection.
        }

        private void OnGUI()
        {
            const int width = 310;
            const int height = 225;

            GUILayout.BeginArea(new Rect(16, 16, width, height), GUI.skin.box);
            GUILayout.Label("Zombie Game - Horde Render Benchmark");
            GUILayout.Label($"Instances: {instanceCount:N0}");
            GUILayout.Label($"Map: {MAP_TILE_COUNT} x {MAP_TILE_COUNT} tiles");
            GUILayout.Label($"Density: {instanceCount / (float)(MAP_TILE_COUNT * MAP_TILE_COUNT):F3} zombies/tile");
            GUILayout.Label($"FPS: {measuredFps:F1}");
            GUILayout.Label($"Draw submit: {lastDrawMilliseconds:F2} ms");
            GUILayout.Label($"Device: {SystemInfo.graphicsDeviceName}");
            GUILayout.Label("Render only: no AI / pathfinding / animation yet");

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("1,000"))
            {
                instanceCount = 1000;
            }

            if (GUILayout.Button("5,000"))
            {
                instanceCount = 5000;
            }

            if (GUILayout.Button("10,000"))
            {
                instanceCount = 10000;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Creates a deliberately cheap 8-vertex box-like mesh.
        /// This is not final zombie art; it exists only to establish a rendering baseline.
        /// </summary>
        private static Mesh CreateLowPolyUnitMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "Runtime_HordeBenchmark_LowPolyUnit"
            };

            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 1f, -0.5f),
                new Vector3(-0.5f, 1f, -0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 1f,  0.5f),
                new Vector3(-0.5f, 1f,  0.5f)
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                5, 6, 4, 6, 7, 4,
                4, 7, 0, 7, 3, 0,
                1, 2, 5, 2, 6, 5,
                3, 7, 2, 7, 6, 2,
                4, 0, 5, 0, 1, 5
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
