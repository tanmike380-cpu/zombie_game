using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.Benchmark
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
        private const int MaxInstances = 10000;
        private const int SafeBatchSize = 1023;

        [Header("Benchmark")]
        [SerializeField, Range(1, MaxInstances)]
        private int instanceCount = MaxInstances;

        [SerializeField, Min(10f)]
        private float fieldWidth = 120f;

        [SerializeField, Min(10f)]
        private float fieldDepth = 120f;

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

        private void Awake()
        {
            if (disableVSync)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }

            CreateRuntimeResources();
            RebuildInstances();
        }

        private void Update()
        {
            if (instanceCount != builtForCount)
            {
                RebuildInstances();
            }

            DrawInstances();
            UpdateFpsCounter();
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

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                Debug.LogError("Horde benchmark could not find an Unlit shader. Create the project with the Universal 3D / URP template.");
                enabled = false;
                return;
            }

            benchmarkMaterial = new Material(shader)
            {
                name = "Runtime_HordeBenchmark_Material",
                enableInstancing = true,
                color = instanceColor
            };

            renderParams = new RenderParams(benchmarkMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(
                    Vector3.zero,
                    new Vector3(fieldWidth + 10f, 20f, fieldDepth + 10f))
            };
        }

        private void RebuildInstances()
        {
            instanceCount = Mathf.Clamp(instanceCount, 1, MaxInstances);
            matrices = new Matrix4x4[instanceCount];

            int columns = Mathf.CeilToInt(Mathf.Sqrt(instanceCount));
            int rows = Mathf.CeilToInt(instanceCount / (float)columns);

            float spacingX = fieldWidth / Mathf.Max(1, columns - 1);
            float spacingZ = fieldDepth / Mathf.Max(1, rows - 1);
            float startX = -fieldWidth * 0.5f;
            float startZ = -fieldDepth * 0.5f;

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
                    new Vector3(fieldWidth + 10f, 20f, fieldDepth + 10f));
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
                    ref renderParams,
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

        private void OnGUI()
        {
            const int width = 310;
            const int height = 190;

            GUILayout.BeginArea(new Rect(16, 16, width, height), GUI.skin.box);
            GUILayout.Label("Zombie Game - Horde Render Benchmark");
            GUILayout.Label($"Instances: {instanceCount:N0}");
            GUILayout.Label($"FPS: {measuredFps:F1}");
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
