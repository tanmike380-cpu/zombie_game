using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using ZombieGame.Presentation;
using ZombieGame.World;

namespace ZombieGame.Tests.V010PresentationReview
{
    /// <summary>Art-only paused snapshot fixture around FrontierGame, never a battle controller.</summary>
    public sealed class PresentationCapture : MonoBehaviour
    {
        [Serializable] public sealed class GroundingPair
        {
            public string fixture_name,source_record;
            public GameObject before,after;
        }
        [Serializable] public sealed class ViewEvidence
        {
            public string id,file,projection = "Orthographic",simulation_state_sha256;
            public float pitch,yaw,orthographic_size,simulation_time;
            public Vector3 focus,position;
            public int width,height,unity_frame,projected_friendly_count,magenta_pixels;
            public bool grounding_after;
        }
        [Serializable] private sealed class CaptureReport
        {
            public string status = "RUNNING",failure,grounding_version;
            public string fixture = "Initial paused production state; global reveal is an art-review fixture only. Unity frames advance for culling, but simulation time/positions/health stay identical.";
            public string balance_sha256,baseline_evidence_path;
            public string lighting_signature,animation_freeze = "Time.time and attack_started_at/facing/observed_velocity are frozen; production sampled animation uses these scaled-time inputs, not Unity frameCount.";
            public int frozen_trees,grounding_pairs;
            public float terrain_pixel_contribution;
            public List<ViewEvidence> views = new List<ViewEvidence>();
            public List<string> checks = new List<string>();
        }

        public bool capture_on_start;
        public string output_folder = "docs/versions/V0.10/evidence/camera-grounding",grounding_version = "Original frozen baseline",balance_sha256;
        public GroundingPair[] grounding_pairs = Array.Empty<GroundingPair>();
        public Vector3 overview_focus = Vector3.zero,closeup_focus = new Vector3(-3,0,-16);
        public int selected_view = 2;
        private static readonly float[] PITCHES = {48,45,40,35,40};
        private static readonly float[] YAWS = {0,30,45,45,60};
        private static readonly string[] IDS = {"A-48-0","B-45-30","C-40-45","D-35-45","E-40-60"};
        private const int WIDTH = 1920,HEIGHT = 1080;
        private const float OVERVIEW_SIZE = 25;
        private FrontierGame game;
        private Camera review_camera;
        private bool fixture_active,is_capturing,previous_reveal,previous_controls;
        private float previous_time_scale,current_pitch,current_yaw,current_size;
        private Vector3 current_focus,previous_position;
        private Quaternion previous_rotation;
        private float previous_size;
        private RenderTexture previous_target;
        private CaptureReport report;
        public string status {get;private set;} = "NOT_STARTED";

        private IEnumerator Start()
        {
            yield return null;
            if(capture_on_start)capture_all();
        }

        public void capture_all()
        {
            if(is_capturing)throw new InvalidOperationException("Presentation capture is already running.");
            StartCoroutine(run_safe_capture());
        }

        private IEnumerator run_safe_capture()
        {
            var sequence = capture_sequence();
            while(true)
            {
                bool has_next;
                try{has_next = sequence.MoveNext();}
                catch(Exception exception){fail(exception);yield break;}
                if(!has_next)yield break;
                yield return sequence.Current;
            }
        }

        private void LateUpdate()
        {
            if(fixture_active)apply_camera();
        }

        private IEnumerator capture_sequence()
        {
            is_capturing = true;status = "RUNNING";
            report = new CaptureReport {grounding_version = grounding_version,balance_sha256 = balance_sha256,
                grounding_pairs = grounding_pairs.Length,baseline_evidence_path = "docs/versions/V0.10/evidence/camera-grounding/baseline-integrity.json"};
            try{begin_fixture();}
            catch(Exception exception){fail(exception);yield break;}
            var target = new RenderTexture(WIDTH,HEIGHT,24,RenderTextureFormat.ARGB32) {antiAliasing = 4,name = "Owned art-review 1920x1080"};
            target.Create();review_camera.targetTexture = target;
            string state_sha = hash_simulation();
            try
            {
                Directory.CreateDirectory(output_folder);
                select_grounding(false);select_camera(48,0,12,closeup_focus);
                yield return null;yield return null;yield return new WaitForEndOfFrame();
                capture_view("ground-before",target,state_sha,false);
                select_grounding(true);select_camera(48,0,12,closeup_focus);
                yield return null;yield return null;yield return new WaitForEndOfFrame();
                capture_view("ground-after",target,state_sha,true);
                for(int index = 0;index < IDS.Length;index++)
                {
                    select_camera(PITCHES[index],YAWS[index],OVERVIEW_SIZE,overview_focus);
                    yield return null;yield return null;yield return new WaitForEndOfFrame();
                    capture_view(IDS[index],target,state_sha,true);
                }
                check_terrain(target);
                report.checks.Add("Seven images share the exact simulation state hash/time; no orders, warps or health overrides.");
                report.checks.Add("Five angles share focus, orthographic size25, lighting/materials, 1920x1080 and the same grounding version.");
                report.checks.Add("Each view waited two complete Unity frames for actor/forest camera culling before capture.");
                report.status = grounding_pairs.Length > 0 ? "TECHNICAL_PASS_NOT_ART_ACCEPTANCE" : "BASELINE_CAMERA_DRAFT_NO_GROUNDING_SOURCE";
                status = report.status;
                File.WriteAllText(Path.Combine(output_folder,"capture-report.json"),JsonUtility.ToJson(report,true));
            }
            finally
            {
                review_camera.targetTexture = previous_target;target.Release();Destroy(target);
                is_capturing = false;end_fixture();
                selected_view = Mathf.Clamp(selected_view,0,4);
                select_camera(PITCHES[selected_view],YAWS[selected_view],OVERVIEW_SIZE,overview_focus);apply_camera();
            }
            Debug.Log("[PresentationReview] "+status+"; "+output_folder);
        }

        private void begin_fixture()
        {
            game = FindFirstObjectByType<FrontierGame>();review_camera = Camera.main;
            require(game && game.current != null && game.defense_scenario && review_camera,"Production scenario has not initialized.");
            require(game.is_paused,"Capture must start from the existing paused briefing, never freeze an active battle silently.");
            require(FindObjectsByType<FrontierGame>(FindObjectsSortMode.None).Length == 1,"Expected exactly one production controller.");
            previous_time_scale = Time.timeScale;previous_reveal = game.reveal_map;
            previous_controls = game.controls.enabled;previous_position = review_camera.transform.position;
            previous_rotation = review_camera.transform.rotation;previous_size = review_camera.orthographicSize;previous_target = review_camera.targetTexture;
            Time.timeScale = 0;set_reveal(true);game.controls.enabled = false;fixture_active = true;
            var environment = game.GetComponentInChildren<FrozenEnvironmentView>();
            require(environment && environment.frozen_tree_count == 81,"Frozen tree layout changed.");
            var bindings = environment.GetComponent<GroundingReviewBindings>();
            if(bindings){grounding_pairs = bindings.pairs;report.grounding_pairs = grounding_pairs.Length;}
            report.frozen_trees = environment.frozen_tree_count;
            report.lighting_signature = lighting_signature();
            foreach(var renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
                foreach(var material in renderer.sharedMaterials)
                    require(material && material.shader && material.shader.isSupported,"Unsupported/missing source material: "+renderer.name);
        }

        private void end_fixture()
        {
            if(!fixture_active)return;
            fixture_active = false;
            if(game){set_reveal(previous_reveal);if(game.controls)game.controls.enabled = previous_controls;}
            Time.timeScale = previous_time_scale;
            if(review_camera)
            {
                review_camera.targetTexture = previous_target;review_camera.orthographicSize = previous_size;
                review_camera.transform.SetPositionAndRotation(previous_position,previous_rotation);
            }
        }

        private void set_reveal(bool value)
        {
            // Private production setter remains private; this disposable fixture never changes Fog logic.
            typeof(FrontierGame).GetProperty("reveal_map",BindingFlags.Instance|BindingFlags.Public).SetValue(game,value);
        }

        private void select_grounding(bool after)
        {
            foreach(var pair in grounding_pairs)
            {
                require(pair.before && pair.after,"Grounding pair is incomplete: "+pair.fixture_name);
                pair.before.SetActive(!after);pair.after.SetActive(after);
            }
        }

        private void select_camera(float pitch,float yaw,float size,Vector3 focus)
        {current_pitch = pitch;current_yaw = yaw;current_size = size;current_focus = focus;apply_camera();}

        private void apply_camera()
        {
            if(!review_camera)return;
            var rotation = Quaternion.Euler(current_pitch,current_yaw,0);
            review_camera.orthographic = true;review_camera.orthographicSize = current_size;
            review_camera.transform.SetPositionAndRotation(current_focus-rotation*Vector3.forward*Mathf.Max(50,current_size*2.3f),rotation);
        }

        private void capture_view(string id,RenderTexture target,string state_sha,bool after)
        {
            require(game.is_paused && Time.timeScale == 0 && hash_simulation() == state_sha,"Simulation state changed during angle comparison: "+id);
            require(lighting_signature() == report.lighting_signature,"Lighting/settings changed during angle comparison: "+id);
            var image = read_camera(target);
            try
            {
                int magenta = 0;foreach(var pixel in image.GetPixels32())if(pixel.r > 240 && pixel.g < 30 && pixel.b > 240)magenta++;
                require(magenta < 1000,"Potential missing/pink shader in "+id+": "+magenta);
                string path = Path.Combine(output_folder,id+".png");File.WriteAllBytes(path,image.EncodeToPNG());
                report.views.Add(new ViewEvidence {id = id,file = path,pitch = current_pitch,yaw = current_yaw,orthographic_size = current_size,
                    focus = current_focus,position = review_camera.transform.position,width = WIDTH,height = HEIGHT,unity_frame = Time.frameCount,
                    simulation_time = Time.time,simulation_state_sha256 = state_sha,grounding_after = after,
                    projected_friendly_count = count_projected_friendlies(),magenta_pixels = magenta});
            }
            finally{Destroy(image);}
        }

        private void check_terrain(RenderTexture target)
        {
            var terrain = game.GetComponentInChildren<FrozenEnvironmentView>().GetComponentInChildren<Terrain>();
            require(terrain && terrain.enabled && terrain.terrainData.terrainLayers.Length == 6,"Six-layer Terrain is absent.");
            Texture2D shown = null,hidden = null;
            try
            {
                shown = read_camera(target);terrain.enabled = false;hidden = read_camera(target);
                var on_pixels = shown.GetPixels32();var off_pixels = hidden.GetPixels32();int changed = 0;
                for(int index=0;index<on_pixels.Length;index++)
                    if(Mathf.Abs(on_pixels[index].r-off_pixels[index].r)+Mathf.Abs(on_pixels[index].g-off_pixels[index].g)+Mathf.Abs(on_pixels[index].b-off_pixels[index].b) > 12)changed++;
                report.terrain_pixel_contribution = changed/(float)on_pixels.Length;
                require(report.terrain_pixel_contribution > .0003f,"Terrain contributes no visible pixels.");
                report.checks.Add("Six-layer Terrain contributes real image pixels; no hidden fallback floor mistaken for terrain.");
            }
            finally{terrain.enabled = true;if(shown)Destroy(shown);if(hidden)Destroy(hidden);}
        }

        private Texture2D read_camera(RenderTexture target)
        {
            var previous_active = RenderTexture.active;
            var resolved = RenderTexture.GetTemporary(WIDTH,HEIGHT,0,RenderTextureFormat.ARGB32);
            try
            {
                review_camera.Render();Graphics.Blit(target,resolved);RenderTexture.active = resolved;
                var image = new Texture2D(WIDTH,HEIGHT,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,WIDTH,HEIGHT),0,0);image.Apply();return image;
            }
            finally{RenderTexture.active = previous_active;RenderTexture.ReleaseTemporary(resolved);}
        }

        private int count_projected_friendlies()
        {
            int count = 0;
            for(int index=0;index<game.current.soldier_count;index++)
            {
                if(game.current.health[index] <= 0 || game.current.is_reserve(index))continue;
                var point = review_camera.WorldToViewportPoint(game.current.positions[index]+Vector3.up);
                if(point.z > review_camera.nearClipPlane && point.z < review_camera.farClipPlane && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1)count++;
            }
            return count;
        }

        private string hash_simulation()
        {
            using(var stream = new MemoryStream())
            using(var writer = new BinaryWriter(stream))
            {
                writer.Write(Time.time);writer.Write(game.current.shots);writer.Write(game.current.hits);
                for(int index=0;index<game.current.total_count;index++)
                {
                    var point = game.current.positions[index];writer.Write(point.x);writer.Write(point.y);writer.Write(point.z);
                    writer.Write(game.current.health[index]);
                    writer.Write(game.current.attack_started_at[index]);
                    var velocity = game.current.observed_velocity[index];writer.Write(velocity.x);writer.Write(velocity.y);writer.Write(velocity.z);
                    if(index < game.current.soldier_count)
                    {
                        var facing = game.current.soldier_facing[index];writer.Write(facing.x);writer.Write(facing.y);writer.Write(facing.z);
                        writer.Write(game.current.ammunition[index]);
                    }
                }
                writer.Flush();using(var hash = SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }

        private static string lighting_signature()
        {
            var sun = RenderSettings.sun;
            return "sun="+(sun ? sun.color.ToString()+"/"+sun.intensity+"/"+sun.transform.rotation.ToString() : "none")+
                ";ambient="+RenderSettings.ambientSkyColor+"/"+RenderSettings.ambientEquatorColor+"/"+RenderSettings.ambientGroundColor+
                ";fog="+RenderSettings.fog+"/"+RenderSettings.fogColor+"/"+RenderSettings.fogStartDistance+"/"+RenderSettings.fogEndDistance+
                ";AA="+QualitySettings.antiAliasing+";shadows="+QualitySettings.shadowDistance+"/"+QualitySettings.shadowCascades+"/"+QualitySettings.shadowResolution;
        }

        private void fail(Exception exception)
        {
            status = "FAIL";report.status = status;report.failure = exception.ToString();is_capturing = false;end_fixture();
            Directory.CreateDirectory(output_folder);File.WriteAllText(Path.Combine(output_folder,"capture-report.json"),JsonUtility.ToJson(report,true));
            Debug.LogException(exception);
        }

        private static void require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);}

        private void OnDisable(){end_fixture();}
    }
}
