using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.Presentation;

namespace ZombieGame.CharacterTests
{
    /// <summary>Rendering-only comparison. No navigation/combat workload; never present this as full battle FPS.</summary>
    public sealed class CharacterRenderBenchmark : MonoBehaviour
    {
        public Material template;
        private CharacterCrowdRenderer renderer;
        private Mesh capsule;
        private Material material;
        private readonly Matrix4x4[] matrices = new Matrix4x4[10400];
        private int population;
        private bool real_models, sampling;
        private readonly List<double> frames = new List<double>(5000);
        private double last_frame;

        private IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule = primitive.GetComponent<MeshFilter>().sharedMesh; Destroy(primitive);
            material = new Material(template) { enableInstancing = true }; material.color = new Color(.3f,.7f,.2f);
            renderer = new CharacterCrowdRenderer(10400);
            Camera.main.orthographicSize = 85; Camera.main.transform.position = new Vector3(0,180,0); Camera.main.transform.rotation = Quaternion.Euler(90,0,0);
            foreach (int zombies in new[] { 5000,10000 })
                foreach (bool models in new[] { false,true })
                {
                    sampling = false; population = 400 + zombies; real_models = models;
                    yield return new WaitForSecondsRealtime(3);
                    frames.Clear(); last_frame = Time.realtimeSinceStartupAsDouble; sampling = true;
                    yield return new WaitForSecondsRealtime(6); sampling = false;
                    frames.Sort(); double sum=0; foreach(double value in frames) sum+=value;
                    double mean=sum/frames.Count, p95=frames[(int)((frames.Count-1)*.95)];
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,"[CharacterRenderBench] PASS render_only humans=400 zombies={0} models={1} resolution={2}x{3} frames={4} avg_ms={5:F3} p95_ms={6:F3} avg_fps={7:F1}",zombies,models,Screen.width,Screen.height,frames.Count,mean,p95,1000/mean));
                }
            Debug.Log("[CharacterRenderBench] COMPLETE rendering-only, no AI/NavMesh/combat included");
            Application.Quit();
        }

        private void Update()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(sampling) frames.Add((now-last_frame)*1000); last_frame=now;
            if(renderer==null)return;
            if(real_models) renderer.begin_frame();
            for(int i=0;i<population;i++)
            {
                Vector3 position = i<400 ? new Vector3(-70+i%4*1.3f,0,-74+i/4*1.5f)
                    : new Vector3(-60+(i-400)%100*1.2f,0,-74+(i-400)/100*1.5f);
                if(real_models) renderer.add(i<400,CharacterPose.Run,Time.time+i*.137f,position,Quaternion.identity,i>=400 && i%10==0);
                else matrices[i]=Matrix4x4.TRS(position+Vector3.up*.7f,Quaternion.identity,new Vector3(.6f,.7f,.6f));
            }
            if(real_models)renderer.draw();
            else
            {
                var parameters = new RenderParams(material) { worldBounds=new Bounds(Vector3.zero,new Vector3(260,20,260)),shadowCastingMode=ShadowCastingMode.Off };
                for(int start=0;start<population;start+=1023)Graphics.RenderMeshInstanced(parameters,capsule,0,matrices,Math.Min(1023,population-start),start);
            }
        }
        private void OnDestroy() { if(material!=null)Destroy(material); }
    }
}
