using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using ZombieGame.World;

namespace ZombieGame.EditorTools
{
    public static class FrontierTools
    {
        private const string SCENE="Assets/_Tests/Frontier/Scenes/FrontierMap.unity";
        [MenuItem("Tools/Zombie Game/World/Open Frontier Map")]
        public static void open_scene()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play before opening Frontier");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            CharacterBake.ensure_models();
            if(File.Exists(SCENE))EditorSceneManager.OpenScene(SCENE);else create_scene();
        }
        public static void create_scene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE));
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera_object=new GameObject("Main Camera");camera_object.tag="MainCamera";
            var camera=camera_object.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=27;
            camera.farClipPlane=500;camera.backgroundColor=new Color(.12f,.17f,.20f);camera.allowHDR=false;
            var sun=new GameObject("Afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;
            sun.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.ambientLight=new Color(.55f,.58f,.62f);
            var game=new GameObject("Frontier settlement").AddComponent<FrontierGame>();
            game.gameObject.AddComponent<ZombieGame.FrontierTests.FrontierSmokeChecks>();
            game.landscape_shader=Shader.Find("ZombieGame/FrontierSurface");
            const string material_path="Assets/_Game/Resources/FrontierFog.mat";
            game.fog_template=AssetDatabase.LoadAssetAtPath<Material>(material_path);
            if(game.fog_template==null){game.fog_template=new Material(Shader.Find("ZombieGame/WorldFog"));AssetDatabase.CreateAsset(game.fog_template,material_path);}
            EditorSceneManager.SaveScene(scene,SCENE);AssetDatabase.SaveAssets();
        }
        public static void build_player()
        {
            CharacterBake.bake_models();create_scene();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SCENE},locationPathName="Builds/Frontier.app",target=BuildTarget.StandaloneOSX});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Frontier build failed");
            Debug.Log("[FrontierBuild] PASS");
        }
    }
}
