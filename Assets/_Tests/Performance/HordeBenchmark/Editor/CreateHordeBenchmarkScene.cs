#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieGame.PerformanceTests;

namespace ZombieGame.EditorTools
{
    public static class CreateHordeBenchmarkScene
    {
        private const string SceneDirectory = "Assets/_Tests/Performance/HordeBenchmark/Scenes";
        private const string ScenePath = SceneDirectory + "/Tech_HordeBenchmark.unity";

        [MenuItem("Tools/Zombie Game/Create 10K Horde Benchmark Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(SceneDirectory);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Tech_HordeBenchmark";

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = HordeRenderBenchmark.MAP_WORLD_SIZE * 0.56f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            cameraObject.transform.position = new Vector3(0f, 200f, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject benchmarkObject = new GameObject("HordeRenderBenchmark");
            benchmarkObject.AddComponent<HordeRenderBenchmark>();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Reference Ground";
            ground.transform.position = Vector3.zero;
            float groundScale = HordeRenderBenchmark.MAP_WORLD_SIZE / 10f;
            ground.transform.localScale = new Vector3(groundScale, 1f, groundScale);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = benchmarkObject;
            Debug.Log($"Created benchmark scene at {ScenePath}. Open it and press Play.");
        }
    }
}
#endif
