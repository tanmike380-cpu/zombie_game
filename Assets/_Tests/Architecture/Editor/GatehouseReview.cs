using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.EditorTools.Architecture;

namespace ZombieGame.EditorTools
{
    /// <summary>Isolated visual review; never modifies a gameplay scene or balance record.</summary>
    public static class GatehouseReview
    {
        private const string SCENE_PATH = "Assets/_Tests/Architecture/Scenes/GreatWallGatehouse.unity";
        private const string REVIEW_PATH = "Builds/ArtReview/GreatWallGatehouse";

        [MenuItem("Tools/Zombie Game/Architecture/Create Gatehouse Review")]
        public static void create_review()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before creating the gatehouse review.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
            Directory.CreateDirectory(REVIEW_PATH);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GreatWallGatehouse().build_model();
            UnityEngine.Object.DestroyImmediate(root);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GreatWallGatehouse.ASSET_ROOT + "/GreatWallGatehouse.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            create_stage();
            var camera = create_camera();
            validate_model(instance);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            render_view(camera, "front-quarter", new Vector3(24, 20, -31), 13);
            render_view(camera, "rts", new Vector3(24, 32, -28), 15);
            render_view(camera, "detail", new Vector3(15, 13, -24), 10.5f);
            camera.transform.position = new Vector3(24, 20, -31);
            camera.transform.LookAt(new Vector3(0, 5, 0));
            camera.orthographicSize = 13;
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            Selection.activeGameObject = instance;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 5, 0), camera.transform.rotation, 22);
            Debug.Log("[GatehouseReview] PASS: saved reusable prefab, review scene and three rendered views.");
        }

        [MenuItem("Tools/Zombie Game/Architecture/Open Gatehouse Review")]
        public static void open_review()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(SCENE_PATH);
            EditorApplication.delayCall += () =>
            {
                var scene_view = EditorWindow.GetWindow<SceneView>();
                scene_view.sceneLighting = true;
                scene_view.LookAt(new Vector3(0, 5, 0), Quaternion.LookRotation(new Vector3(-24, -15, 31)), 20, true, true);
                scene_view.Focus();
                Debug.Log("[GatehouseReview] Opened review in Unity Scene view.");
            };
        }

        private static void create_stage()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.58f, .65f, .72f);
            RenderSettings.ambientEquatorColor = new Color(.46f, .44f, .37f);
            RenderSettings.ambientGroundColor = new Color(.23f, .21f, .17f);
            RenderSettings.fog = false;
            var sun = new GameObject("Warm afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = new Color(1, .88f, .70f);
            sun.transform.rotation = Quaternion.Euler(43, -32, 0);
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = .065f;
            sun.shadowNormalBias = .4f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Review ground - not game terrain";
            ground.transform.position = new Vector3(0, -.18f, 0);
            ground.transform.localScale = new Vector3(160, .25f, 160);
            string path = GreatWallGatehouse.ASSET_ROOT + "/ReviewGround.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.color = new Color(.32f, .30f, .24f);
                material.SetFloat("_Glossiness", .05f);
                AssetDatabase.CreateAsset(material, path);
            }
            ground.GetComponent<Renderer>().sharedMaterial = material;
            AssetDatabase.SaveAssets();
        }

        private static Camera create_camera()
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.32f, .30f, .24f);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            return camera;
        }

        private static void render_view(Camera camera, string name, Vector3 position, float size)
        {
            camera.transform.position = position;
            camera.transform.LookAt(new Vector3(0, 5, 0));
            camera.orthographicSize = size;
            var target = new RenderTexture(1920, 1440, 24) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            var pixels = new Texture2D(1920, 1440, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1440), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(REVIEW_PATH + "/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void validate_model(GameObject root)
        {
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                    throw new InvalidOperationException("Gatehouse mesh missing: " + filter.name);
                if (filter.GetComponent<MeshRenderer>().sharedMaterial == null)
                    throw new InvalidOperationException("Gatehouse material missing: " + filter.name);
                foreach (var vertex in filter.sharedMesh.vertices)
                    if (!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z))
                        throw new InvalidOperationException("Gatehouse contains non-finite geometry: " + filter.name);
                triangles += filter.sharedMesh.triangles.Length / 3;
            }
            Debug.Log($"[GatehouseReview] geometry triangles={triangles}, renderers={root.GetComponentsInChildren<MeshRenderer>().Length}");
        }
    }
}
