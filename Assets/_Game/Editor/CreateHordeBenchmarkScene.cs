#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieGame.Benchmark;

namespace ZombieGame.EditorTools
{
    public static class CreateHordeBenchmarkScene
    {
        private const string SceneDirectory = "Assets/_Game/Scenes";
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
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            cameraObject.transform.position = new Vector3(0f, 105f, -95f);
            cameraObject.transform.LookAt(Vector3.zero);

            GameObject benchmarkObject = new GameObject("HordeRenderBenchmark");
            benchmarkObject.AddComponent<HordeRenderBenchmark>();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Reference Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(14f, 1f, 14f);

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
