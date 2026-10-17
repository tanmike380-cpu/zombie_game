using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.Presentation;
using ZombieGame.World;

namespace ZombieGame.Tests.V010RenderQualityReview
{
    /// <summary>Disposable same-source image experiment, not a replacement game/controller or production quality preset.</summary>
    public sealed class RenderQualityCapture : MonoBehaviour
    {
        [Serializable] public sealed class MaterialBinding {public Material material;public float original_baked_light;}
        [Serializable] private sealed class TextureSnapshot {public Texture texture;public int aniso;public FilterMode filter;public float bias;}
        [Serializable] private sealed class ImageEvidence
        {
            public string view,variant,file,simulation_sha256,sampling,lighting;
            public int render_width,render_height,output_width=1920,output_height=1080,msaa=4,unity_frame,projected_friendlies;
            public Vector3 focus,camera_position;public float pitch=40,yaw,orthographic_size,simulation_time;
            public Color ambient_sky,ambient_equator,ambient_ground,sun_color;public Vector3 sun_euler;
            public float sun_intensity,shadow_bias,shadow_normal_bias,shadow_distance,reflection_intensity;
            public string shadow_resolution,reflection_mode,anisotropic_filtering;public bool render_texture_srgb,dynamic_resolution;
            public double median_camera_submit_ms,median_gpu_readback_ms,median_total_render_sync_ms;
            public long latest_frame_submitted_triangles;
            public Vector2 hq_projected_pixels,wall_projected_pixels;
        }
        [Serializable] private sealed class Report
        {
            public string status="RUNNING",failure,device,gpu,pipeline,color_space,source_audit="render-source-audit.json";
            public string interpretation="Synchronous offscreen Camera.Render and GPU readback timings in the Editor; not battle FPS or a pure GPU benchmark. No sharpening, invented normal/roughness/AO, geometry edits, or source texture upscaling.";
            public string triangle_counter_limit="latest_frame_submitted_triangles is the Editor whole-frame Profiler counter. It includes this fixture's repeated Camera.Render warmups/timing renders and other Editor cameras in that frame. It is NOT a single PNG's draw count or a normal gameplay frame. Source geometry totals are listed separately in render-source-audit.json; instanced snapshot totals include all LODs and are not simultaneous visibility.";
            public string reflection_note;
            public List<ImageEvidence> images=new List<ImageEvidence>();
            public bool settings_restored,source_sampling_restored,material_constants_restored;
        }
        private sealed class LightingSnapshot
        {
            public Color sky,equator,ground,sun_color;public float ambient_intensity,sun_intensity,bias,normal_bias,strength,shadow_distance,reflection_intensity;
            public int cascades;public ShadowResolution resolution;public DefaultReflectionMode reflection_mode;public Texture reflection;
        }

        public bool capture_on_start;
        public bool first_pair_only;
        public string output_folder="docs/versions/V0.10/evidence/render-quality";
        public MaterialBinding[] material_bindings=Array.Empty<MaterialBinding>();
        public float headquarters_front_yaw=225;
        private static readonly string[] VIEW_IDS={"overview","hq-side","wall-detail","hq-front"};
        private static readonly Vector3[] FOCUSES={Vector3.zero,new Vector3(4,3.5f,-16),new Vector3(-8,3,-2),new Vector3(4,3.5f,-16)};
        private static readonly float[] SIZES={25,10,8,10};
        private static readonly string[] VARIANTS={"baseline","sampling","lighting","combined"};
        private const int WIDTH=1920,HEIGHT=1080;
        private FrontierGame game;private Camera review_camera;private FrozenEnvironmentView environment;
        private LightingSnapshot lighting_snapshot;private readonly List<TextureSnapshot> texture_snapshots=new List<TextureSnapshot>();
        private bool fixture_active,previous_reveal,previous_controls;private float previous_time_scale,previous_size;
        private Vector3 previous_position,focus;private Quaternion previous_rotation;private RenderTexture previous_target;
        private float current_size,current_yaw;private Report report;private ReflectionProbe reflection_probe;private Texture captured_reflection;
        private ProfilerRecorder triangles;
        public string status {get;private set;}="NOT_STARTED";

        private IEnumerator Start(){yield return null;if(capture_on_start)capture_all();}
        public void capture_all(){if(fixture_active)throw new InvalidOperationException("Render-quality fixture already running.");StartCoroutine(run_safe());}
        private void LateUpdate(){if(fixture_active)apply_camera();}

        private IEnumerator run_safe()
        {
            var sequence=capture_sequence();
            while(true)
            {
                bool more;
                try{more=sequence.MoveNext();}catch(Exception exception){fail(exception);yield break;}
                if(!more)yield break;yield return sequence.Current;
            }
        }

        private IEnumerator capture_sequence()
        {
            status="RUNNING";report=new Report {device=SystemInfo.deviceModel+"; RAMMB="+SystemInfo.systemMemorySize,gpu=SystemInfo.graphicsDeviceName,
                pipeline=GraphicsSettings.currentRenderPipeline?GraphicsSettings.currentRenderPipeline.name:"Built-in",color_space=QualitySettings.activeColorSpace.ToString()};
            begin_fixture();Directory.CreateDirectory(output_folder);
            string state=hash_simulation();
            // A real scene probe supplies reflection only to the two lighting experiments, never a made-up HDR map.
            apply_lighting(true);yield return capture_scene_reflection();apply_lighting(false);
            try
            {
                for(int view_index=0;view_index<VIEW_IDS.Length;view_index++)
                {
                    if(first_pair_only && view_index!=3)continue;
                    focus=FOCUSES[view_index];current_size=SIZES[view_index];current_yaw=view_index==3?headquarters_front_yaw:45;
                    for(int variant_index=0;variant_index<VARIANTS.Length;variant_index++)
                    {
                        if(first_pair_only && variant_index!=0 && variant_index!=3)continue;
                        bool high_sampling=variant_index==1 || variant_index==3;
                        bool shaped_lighting=variant_index>=2;
                        apply_sampling(high_sampling);apply_lighting(shaped_lighting);apply_materials(shaped_lighting);
                        int scale=high_sampling?2:1;
                        var target=new RenderTexture(WIDTH*scale,HEIGHT*scale,24,RenderTextureFormat.ARGB32){antiAliasing=4,filterMode=FilterMode.Bilinear,name="Owned render-quality review"};
                        target.Create();review_camera.targetTexture=target;apply_camera();
                        try
                        {
                            yield return null;yield return null;yield return new WaitForEndOfFrame();
                            require(hash_simulation()==state && game.is_paused && Time.timeScale==0,"Simulation moved during render-quality comparison.");
                            capture_image(view_index,variant_index,target,state);
                        }
                        finally{review_camera.targetTexture=previous_target;target.Release();Destroy(target);}
                    }
                }
                report.status="CAPTURED_TECHNICAL_REVIEW_NOT_ART_ACCEPTANCE";status=report.status;
            }
            finally{restore_fixture();save_report();}
            UnityEngine.Debug.Log("[RenderQuality] "+status+"; "+output_folder);
        }

        private void begin_fixture()
        {
            game=FindAnyObjectByType<FrontierGame>();review_camera=Camera.main;
            require(game && game.current!=null && game.is_paused && review_camera,"Run only the isolated paused production review scene.");
            environment=game.GetComponentInChildren<FrozenEnvironmentView>();require(environment,"Frozen source environment missing.");
            previous_reveal=game.reveal_map;previous_controls=game.controls.enabled;previous_time_scale=Time.timeScale;
            previous_position=review_camera.transform.position;previous_rotation=review_camera.transform.rotation;previous_size=review_camera.orthographicSize;previous_target=review_camera.targetTexture;
            lighting_snapshot=snapshot_lighting();snapshot_textures();
            fixture_active=true;Time.timeScale=0;set_reveal(true);game.controls.enabled=false;
            triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
        }

        private LightingSnapshot snapshot_lighting()
        {
            var sun=RenderSettings.sun;require(sun,"Review source directional light missing.");
            return new LightingSnapshot {sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,
                ambient_intensity=RenderSettings.ambientIntensity,sun_color=sun.color,sun_intensity=sun.intensity,bias=sun.shadowBias,normal_bias=sun.shadowNormalBias,
                strength=sun.shadowStrength,shadow_distance=QualitySettings.shadowDistance,cascades=QualitySettings.shadowCascades,resolution=QualitySettings.shadowResolution,
                reflection_mode=RenderSettings.defaultReflectionMode,reflection=RenderSettings.customReflectionTexture,reflection_intensity=RenderSettings.reflectionIntensity};
        }

        private void snapshot_textures()
        {
            var unique=new HashSet<Texture>();
            foreach(var renderer in environment.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)add_textures(material,unique);
            foreach(var batch in environment.batches)add_textures(batch.material,unique);
            foreach(var entry in game.imported_roster.units)add_textures(entry.frames.material,unique);
            var terrain=environment.GetComponentInChildren<Terrain>();
            foreach(var layer in terrain.terrainData.terrainLayers){unique.Add(layer.diffuseTexture);unique.Add(layer.normalMapTexture);unique.Add(layer.maskMapTexture);}
            foreach(var texture in unique)if(texture)texture_snapshots.Add(new TextureSnapshot{texture=texture,aniso=texture.anisoLevel,filter=texture.filterMode,bias=texture.mipMapBias});
        }

        private static void add_textures(Material material,HashSet<Texture> textures)
        {if(material)foreach(string name in material.GetTexturePropertyNames())textures.Add(material.GetTexture(name));}

        private void apply_sampling(bool improved)
        {
            foreach(var snapshot in texture_snapshots)
            {
                snapshot.texture.anisoLevel=improved?16:snapshot.aniso;
                snapshot.texture.filterMode=improved?FilterMode.Trilinear:snapshot.filter;
                snapshot.texture.mipMapBias=snapshot.bias; // No negative LOD bias or disabling mips to fake sharpness.
            }
        }

        private void apply_materials(bool improved)
        {foreach(var binding in material_bindings)binding.material.SetFloat("_BakedLight",improved?Mathf.Min(.2f,binding.original_baked_light):binding.original_baked_light);}

        private void apply_lighting(bool improved)
        {
            var sun=RenderSettings.sun;var source=lighting_snapshot;
            RenderSettings.ambientSkyColor=improved?new Color(.38f,.405f,.425f):source.sky;
            RenderSettings.ambientEquatorColor=improved?new Color(.25f,.27f,.26f):source.equator;
            RenderSettings.ambientGroundColor=improved?new Color(.17f,.18f,.16f):source.ground;
            RenderSettings.ambientIntensity=source.ambient_intensity;sun.color=source.sun_color;
            sun.intensity=improved?1.18f:source.sun_intensity;sun.shadowBias=improved?.02f:source.bias;
            sun.shadowNormalBias=improved?.15f:source.normal_bias;sun.shadowStrength=source.strength;
            QualitySettings.shadowDistance=source.shadow_distance;QualitySettings.shadowCascades=source.cascades;
            QualitySettings.shadowResolution=improved?ShadowResolution.VeryHigh:source.resolution;
            RenderSettings.defaultReflectionMode=improved && captured_reflection?DefaultReflectionMode.Custom:source.reflection_mode;
            RenderSettings.customReflectionTexture=improved && captured_reflection?captured_reflection:source.reflection;
            RenderSettings.reflectionIntensity=(improved && captured_reflection) ? .7f : source.reflection_intensity;
        }

        private IEnumerator capture_scene_reflection()
        {
            var holder=new GameObject("Disposable actual-source reflection capture, not world art");
            holder.transform.position=new Vector3(4,6,-16);reflection_probe=holder.AddComponent<ReflectionProbe>();
            reflection_probe.mode=ReflectionProbeMode.Realtime;reflection_probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
            reflection_probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;reflection_probe.resolution=128;
            reflection_probe.size=new Vector3(128,64,128);reflection_probe.boxProjection=true;reflection_probe.cullingMask=review_camera.cullingMask;
            reflection_probe.clearFlags=ReflectionProbeClearFlags.Skybox;reflection_probe.shadowDistance=40;
            int request=reflection_probe.RenderProbe();float started=Time.realtimeSinceStartup;
            while(!reflection_probe.IsFinishedRendering(request) && Time.realtimeSinceStartup-started<30)yield return null;
            if(reflection_probe.IsFinishedRendering(request))
            {captured_reflection=reflection_probe.texture;report.reflection_note="Actual frozen scene realtime cubemap128 at(4,6,-16); one-time probe capture; not generated environment art. Texture="+(captured_reflection?captured_reflection.name:"missing");}
            else report.reflection_note="Probe capture unavailable; no custom reflection substituted.";
            reflection_probe.enabled=false;
        }

        private void apply_camera()
        {
            if(!review_camera)return;var rotation=Quaternion.Euler(40,current_yaw,0);
            review_camera.orthographic=true;review_camera.orthographicSize=current_size;
            review_camera.transform.SetPositionAndRotation(focus-rotation*Vector3.forward*Mathf.Max(50,current_size*2.3f),rotation);
        }

        private void capture_image(int view_index,int variant_index,RenderTexture target,string state)
        {
            var submit=new List<double>();var readbacks=new List<double>();var total=new List<double>();Texture2D image=null;
            try
            {
                // Warm this view/RT before timing; the first shader/texture upload is not steady-state rendering cost.
                for(int warmup=0;warmup<2;warmup++){var warmed=read_camera(target,out _,out _);Destroy(warmed);}
                for(int index=0;index<5;index++)
                {
                    if(image)Destroy(image);image=read_camera(target,out double submit_ms,out double readback_ms);
                    submit.Add(submit_ms);readbacks.Add(readback_ms);total.Add(submit_ms+readback_ms);
                }
                submit.Sort();readbacks.Sort();total.Sort();
                string file=Path.Combine(output_folder,VIEW_IDS[view_index]+"-"+VARIANTS[variant_index]+".png");File.WriteAllBytes(file,image.EncodeToPNG());
                report.images.Add(new ImageEvidence {view=VIEW_IDS[view_index],variant=VARIANTS[variant_index],file=file,simulation_sha256=state,
                    sampling=variant_index==1 || variant_index==3?"3840x2160 with4xMSAA, source textures unchanged, AF16, bilinear reduction to1920x1080; no sharpening":"1920x1080 with4xMSAA, original source sampling",
                    lighting=variant_index>=2?"Review-only neutral direct/fill balance, smaller shadow normal bias,4096-class shadow setting, real scene reflection; owned actor material baked emission <=0.2":"Original frozen daylight/material constants",
                    render_width=target.width,render_height=target.height,focus=focus,camera_position=review_camera.transform.position,yaw=current_yaw,orthographic_size=current_size,
                    simulation_time=Time.time,unity_frame=Time.frameCount,projected_friendlies=count_friendlies(),median_camera_submit_ms=submit[2],median_gpu_readback_ms=readbacks[2],median_total_render_sync_ms=total[2],
                    ambient_sky=RenderSettings.ambientSkyColor,ambient_equator=RenderSettings.ambientEquatorColor,ambient_ground=RenderSettings.ambientGroundColor,
                    sun_color=RenderSettings.sun.color,sun_euler=RenderSettings.sun.transform.eulerAngles,sun_intensity=RenderSettings.sun.intensity,
                    shadow_bias=RenderSettings.sun.shadowBias,shadow_normal_bias=RenderSettings.sun.shadowNormalBias,shadow_distance=QualitySettings.shadowDistance,
                    shadow_resolution=QualitySettings.shadowResolution.ToString(),reflection_intensity=RenderSettings.reflectionIntensity,reflection_mode=RenderSettings.defaultReflectionMode.ToString(),
                    anisotropic_filtering=QualitySettings.anisotropicFiltering.ToString(),render_texture_srgb=target.sRGB,dynamic_resolution=review_camera.allowDynamicResolution,
                    latest_frame_submitted_triangles=triangles.Valid?triangles.LastValue:-1,hq_projected_pixels=projected_bounds_pixels("HeadquartersGroundedReview_OriginalArchitecture"),wall_projected_pixels=projected_bounds_pixels("CityWall_North_Body_00")});
            }
            finally{if(image)Destroy(image);}
        }

        private Texture2D read_camera(RenderTexture target,out double submit_ms,out double readback_ms)
        {
            var old_active=RenderTexture.active;var resolved=RenderTexture.GetTemporary(WIDTH,HEIGHT,0,RenderTextureFormat.ARGB32);
            var clock=Stopwatch.StartNew();review_camera.Render();submit_ms=clock.Elapsed.TotalMilliseconds;clock.Restart();
            try
            {
                Graphics.Blit(target,resolved);RenderTexture.active=resolved;var image=new Texture2D(WIDTH,HEIGHT,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,WIDTH,HEIGHT),0,0);image.Apply();readback_ms=clock.Elapsed.TotalMilliseconds;return image;
            }
            finally{RenderTexture.active=old_active;RenderTexture.ReleaseTemporary(resolved);}
        }

        private Vector2 projected_bounds_pixels(string name)
        {
            foreach(var renderer in environment.GetComponentsInChildren<Renderer>())if(renderer.name==name)
            {
                var b=renderer.bounds;Vector2 minimum=Vector2.one*float.PositiveInfinity,maximum=Vector2.one*float.NegativeInfinity;
                for(int index=0;index<8;index++)
                {
                    var p=b.center+Vector3.Scale(b.extents,new Vector3((index&1)==0?-1:1,(index&2)==0?-1:1,(index&4)==0?-1:1));
                    var v=review_camera.WorldToViewportPoint(p);minimum=Vector2.Min(minimum,new Vector2(v.x,v.y));maximum=Vector2.Max(maximum,new Vector2(v.x,v.y));
                }
                return Vector2.Scale(maximum-minimum,new Vector2(WIDTH,HEIGHT));
            }
            return Vector2.zero;
        }

        private int count_friendlies()
        {
            int count=0;for(int index=0;index<game.current.soldier_count;index++)
            {
                if(game.current.health[index]<=0 || game.current.is_reserve(index))continue;var v=review_camera.WorldToViewportPoint(game.current.positions[index]+Vector3.up);
                if(v.z>0 && v.x>0 && v.x<1 && v.y>0 && v.y<1)count++;
            }
            return count;
        }

        private string hash_simulation()
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(Time.time);writer.Write(game.current.shots);writer.Write(game.current.hits);
                for(int index=0;index<game.current.total_count;index++)
                {
                    var point=game.current.positions[index];writer.Write(point.x);writer.Write(point.y);writer.Write(point.z);writer.Write(game.current.health[index]);writer.Write(game.current.attack_started_at[index]);
                    var v=game.current.observed_velocity[index];writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);
                    if(index<game.current.soldier_count){var f=game.current.soldier_facing[index];writer.Write(f.x);writer.Write(f.y);writer.Write(f.z);writer.Write(game.current.ammunition[index]);}
                }
                writer.Flush();using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }

        private void restore_fixture()
        {
            if(!fixture_active)return;
            apply_sampling(false);apply_materials(false);apply_lighting(false);fixture_active=false;
            if(reflection_probe)Destroy(reflection_probe.gameObject);
            if(triangles.Valid)triangles.Dispose();set_reveal(previous_reveal);game.controls.enabled=previous_controls;Time.timeScale=previous_time_scale;
            review_camera.targetTexture=previous_target;review_camera.orthographicSize=previous_size;review_camera.transform.SetPositionAndRotation(previous_position,previous_rotation);
            report.source_sampling_restored=true;report.material_constants_restored=true;report.settings_restored=true;
        }
        private void set_reveal(bool value){typeof(FrontierGame).GetProperty("reveal_map",BindingFlags.Instance|BindingFlags.Public).SetValue(game,value);}
        private void save_report(){Directory.CreateDirectory(output_folder);File.WriteAllText(Path.Combine(output_folder,"render-capture-report.json"),JsonUtility.ToJson(report,true));}
        private void fail(Exception exception){status="FAIL";report=report??new Report();report.status=status;report.failure=exception.ToString();restore_fixture();save_report();UnityEngine.Debug.LogException(exception);}
        private static void require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private void OnDisable(){if(fixture_active){restore_fixture();save_report();}}
    }
}
