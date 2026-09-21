using System;
using System.Collections;
using System.IO;
using UnityEngine;
using ZombieGame.Presentation;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    /// <summary>Disposable player fixture: validates visual-only switching and renders actual
    /// scene/model assets into offscreen targets, without substituting concept illustrations.</summary>
    public sealed class ArtStyleSmoke:MonoBehaviour
    {
        private const string OUTPUT="/tmp/zombie-art-preview";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void install_fixture()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-artSmoke")<0)return;
            Application.logMessageReceived+=fail_on_error;
            var game=FindFirstObjectByType<FrontierGame>();
            if(game!=null&&game.GetComponent<ArtStyleSmoke>()==null)game.gameObject.AddComponent<ArtStyleSmoke>();
        }
        private IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-artSmoke")<0)yield break;
            yield return null;
            var game=GetComponent<FrontierGame>();var simulation=game.current;
            var original_agents=simulation.crowd.agents;
            float powder=game.economy.gunpowder;int blockers=game.map.regions.Count;
            Time.timeScale=0;Array.Clear(simulation.selected,0,simulation.selected.Length);
            Directory.CreateDirectory(OUTPUT);
            for(int index=0;index<3;index++)
            {
                game.switch_visual_style(index);Camera.main.orthographicSize=25;game.focus_camera(new Vector3(-98,0,-90));
                yield return null;
                if(game.current!=simulation||game.current.crowd.agents!=original_agents||game.economy.gunpowder!=powder||game.map.regions.Count!=blockers)
                    throw new InvalidOperationException("Art switching changed gameplay state");
                string id=VisualStyles.current.id;
                render_camera(Camera.main,OUTPUT+"/"+id+"-world.png",2048,1152);
                render_models(id);
                Debug.Log("[ArtStyleSmoke] PASS "+id+" gameplay unchanged; world/model render targets saved");
            }
            Time.timeScale=1;Debug.Log("[ArtStyleSmoke] COMPLETE "+OUTPUT);Application.Quit(0);
        }
        private static void fail_on_error(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){Time.timeScale=1;Application.Quit(1);}}
        private void OnDestroy(){Application.logMessageReceived-=fail_on_error;}
        private static void render_camera(Camera camera,string path,int width,int height)
        {
            var target=new RenderTexture(width,height,24){antiAliasing=4};target.Create();
            var old_target=camera.targetTexture;var old_active=RenderTexture.active;float old_aspect=camera.aspect;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.aspect=(float)width/height;camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{camera.targetTexture=old_target;camera.aspect=old_aspect;RenderTexture.active=old_active;target.Release();Destroy(target);Destroy(texture);}
        }
        private static void render_models(string id)
        {
            var root=new GameObject("Disposable art gallery");root.transform.position=new Vector3(350,0,350);
            var camera_object=new GameObject("Offscreen model camera");var camera=camera_object.AddComponent<Camera>();
            camera.enabled=false;camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=1.35f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.15f);
            camera.transform.position=root.transform.position+new Vector3(.3f,2.1f,5.5f);camera.transform.LookAt(root.transform.position+Vector3.up*.75f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=31;floor.transform.SetParent(root.transform,false);
            floor.transform.localPosition=new Vector3(0,-.07f,0);floor.transform.localScale=new Vector3(8,.1f,6);
            var material=new Material(Shader.Find("ZombieGame/FrontierSurface")){color=new Color(.3f,.31f,.27f)};
            floor.GetComponent<Renderer>().sharedMaterial=material;
            string[] names={"Human","Zombie","Exploder"};
            for(int i=0;i<3;i++)
            {
                var asset=Resources.Load<CharacterFrames>("CharacterGenerated/"+id+"/"+names[i]);
                if(asset==null||asset.poses.Length!=7||!asset.material.shader.isSupported)throw new InvalidOperationException("Invalid style model: "+id+" "+names[i]);
                foreach(var pose in asset.poses)
                {
                    if(pose.frames.Length!=12)throw new InvalidOperationException("Style animation frame mismatch");
                    foreach(var mesh in pose.frames)
                        if(mesh==null||mesh.vertexCount<100||!(mesh.bounds.size.sqrMagnitude>0&&mesh.bounds.size.sqrMagnitude<40))
                            throw new InvalidOperationException("Invalid style animation mesh: "+id+" "+names[i]);
                }
                var unit=new GameObject(names[i]);unit.layer=31;unit.transform.SetParent(root.transform,false);unit.transform.localPosition=new Vector3((i-1)*1.4f,0,0);
                unit.AddComponent<MeshFilter>().sharedMesh=asset.poses[0].frames[0];unit.AddComponent<MeshRenderer>().sharedMaterial=asset.material;
            }
            float previous_shadow_distance=QualitySettings.shadowDistance;
            try{QualitySettings.shadowDistance=20;render_camera(camera,OUTPUT+"/"+id+"-models.png",1920,1080);}
            finally{QualitySettings.shadowDistance=previous_shadow_distance;}
            root.SetActive(false);Destroy(root);Destroy(camera_object);Destroy(material);
        }
    }
}
