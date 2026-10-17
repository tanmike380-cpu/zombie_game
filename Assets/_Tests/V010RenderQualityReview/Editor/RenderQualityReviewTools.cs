using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.Presentation;
using ZombieGame.Tests.V010PresentationReview;
using ZombieGame.Tests.V010PresentationReview.EditorTools;
using ZombieGame.World;

namespace ZombieGame.Tests.V010RenderQualityReview.EditorTools
{
    /// <summary>Opt-in, disposable same-source render comparison. Frozen scenes, settings and imported art stay untouched.</summary>
    [InitializeOnLoad]
    public static class RenderQualityReviewTools
    {
        public const string ROOT="Assets/_Tests/V010RenderQualityReview";
        public const string SCENE_PATH=ROOT+"/Scenes/RenderQualityReview.unity";
        public const string EVIDENCE_ROOT="docs/versions/V0.10/evidence/render-quality";
        private const string QUALITY_KEY="ZombieGame.RenderQualityReview.restore";
        [Serializable] private sealed class FileRecord {public string path,before,after;}
        [Serializable] private sealed class IntegrityReport {public string status="PREPARED";public List<FileRecord> files=new List<FileRecord>();}
        [Serializable] private sealed class QualitySnapshot
        {public float shadow_distance,time_scale;public int cascades,anti_aliasing,vsync,target_frame_rate;public ShadowResolution resolution;public string color_space;}
        [Serializable] private sealed class TextureRecord
        {
            public string path,source_sha256,format,filter,compression,texture_type;
            public int source_width,source_height,effective_width,effective_height,max_import_size,mips,aniso,active_mip_limit;
            public float lod_bias;public bool mipmaps,srgb,streaming;
        }
        [Serializable] private sealed class MaterialRecord
        {
            public string material,shader,shader_path,semantic;
            public float baked_light;public bool actual_normal,actual_roughness,actual_metallic,actual_occlusion;
            public List<TextureRecord> textures=new List<TextureRecord>();
        }
        [Serializable] private sealed class Audit
        {
            public string device,gpu,unity,pipeline,color_space,grounding_handoff_sha256;
            public string interpretation="Baseline keeps Gamma/Built-in and exact same authored meshes/UVs/textures. HQ and wall source exports supply base color only; missing PBR source is not a Unity import loss. Wall pieces reuse original atlas UVs, not one nineteenth of a texture each.";
            public string projection="Orthographic40 pitch,45 yaw;1920x1080 final output. Sampling variants render3840x2160 then reduce; source texture resolution is unchanged. No sharpening/post-processing/DLSS/FSR or dynamic resolution.";
            public string linear_limit="A real Linear comparison requires full import/shader/UI/terrain/actor validation. This opt-in experiment does not change PlayerSettings.colorSpace or the render pipeline.";
            public long active_static_renderer_triangles,instanced_snapshot_triangles_all_lods;
            public int frozen_trees,grounding_pairs,source_texture_count,material_clone_count;
            public Vector3 hq_front_marker_world,hq_center;public float headquarters_front_yaw;
            public List<MaterialRecord> materials=new List<MaterialRecord>();
        }

        static RenderQualityReviewTools(){EditorApplication.playModeStateChanged+=on_play_mode;}

        [MenuItem("Zombie Game/V0.10/Render quality review/Prepare independent comparison")]
        public static void prepare_render_review()
        {
            guard_editor();Directory.CreateDirectory(ROOT+"/Scenes");Directory.CreateDirectory(ROOT+"/Art");Directory.CreateDirectory(EVIDENCE_ROOT);
            var integrity=capture_integrity();
            var scene=EditorSceneManager.OpenScene(PresentationReviewTools.SCENE_PATH);
            var game=UnityEngine.Object.FindAnyObjectByType<FrontierGame>();
            require(game && game.defense_scenario,"Approved production presentation scene is unavailable.");
            foreach(var old in UnityEngine.Object.FindObjectsByType<PresentationCapture>())UnityEngine.Object.DestroyImmediate(old);
            game.imported_roster=create_owned_roster(game.imported_roster,out var bindings);
            var capture=game.gameObject.AddComponent<RenderQualityCapture>();capture.capture_on_start=false;capture.material_bindings=bindings;
            capture.output_folder=EVIDENCE_ROOT;
            var audit=audit_source(game,bindings.Length);
            capture.headquarters_front_yaw=audit.headquarters_front_yaw;
            var camera=Camera.main;var rotation=Quaternion.Euler(40,45,0);
            camera.orthographic=true;camera.orthographicSize=25;camera.transform.SetPositionAndRotation(-(rotation*Vector3.forward)*57.5f,rotation);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,SCENE_PATH);
            File.WriteAllText(EVIDENCE_ROOT+"/render-source-audit.json",JsonUtility.ToJson(audit,true));
            verify_integrity(integrity);File.WriteAllText(EVIDENCE_ROOT+"/baseline-integrity.json",JsonUtility.ToJson(integrity,true));
            AssetDatabase.Refresh();Debug.Log("[RenderQuality] Prepared independent scene; original render/source assets and ProjectSettings unchanged.");
        }

        [MenuItem("Zombie Game/V0.10/Render quality review/Capture fixed-state comparisons")]
        public static void capture_render_review()
        {start_capture(false);}

        public static void capture_first_pair()
        {start_capture(true);}

        private static void start_capture(bool first_pair_only)
        {
            guard_editor();EditorSceneManager.OpenScene(SCENE_PATH);
            var capture=UnityEngine.Object.FindAnyObjectByType<RenderQualityCapture>();require(capture,"Prepare review first.");
            capture.first_pair_only=first_pair_only;capture.output_folder=EVIDENCE_ROOT+(first_pair_only?"/first-pair":"");
            snapshot_editor_quality();capture.capture_on_start=true;EditorApplication.EnterPlaymode();
        }

        [MenuItem("Zombie Game/V0.10/Render quality review/Open independent comparison")]
        public static void open_render_review()
        {guard_editor();EditorSceneManager.OpenScene(SCENE_PATH);EditorApplication.ExecuteMenuItem("Window/General/Game");}

        /// <summary>Explicit CLI teardown also covers Editor delay callbacks that do not pump after a remote Play stop.</summary>
        public static void finish_render_review()
        {
            require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play and wait before final teardown.");
            if(SessionState.GetString(QUALITY_KEY,"")!="")restore_editor_quality();
            var capture=UnityEngine.Object.FindAnyObjectByType<RenderQualityCapture>();
            if(capture){capture.capture_on_start=false;capture.first_pair_only=false;capture.output_folder=EVIDENCE_ROOT;EditorUtility.SetDirty(capture);}
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path==SCENE_PATH)EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            verify_frozen_after_capture();
        }

        /// <summary>Repeatable technical checks only; image/style acceptance still requires looking at the PNGs.</summary>
        public static void verify_render_review()
        {
            guard_editor();verify_frozen_after_capture();
            var report=JObject.Parse(File.ReadAllText(EVIDENCE_ROOT+"/render-capture-report.json"));
            const string counter_limit="latest_frame_submitted_triangles is the Editor whole-frame Profiler counter, including this fixture's repeated Camera.Render warmups/timing renders and other Editor cameras in that frame. It is NOT a single PNG's draw count or a normal gameplay frame. Source geometry totals in render-source-audit.json are separate; instanced totals include all LODs, not simultaneous visibility.";
            report["triangle_counter_limit"]=counter_limit;File.WriteAllText(EVIDENCE_ROOT+"/render-capture-report.json",report.ToString());
            string first_path=EVIDENCE_ROOT+"/first-pair/render-capture-report.json";
            if(File.Exists(first_path)){var first=JObject.Parse(File.ReadAllText(first_path));first["triangle_counter_limit"]=counter_limit;File.WriteAllText(first_path,first.ToString());}
            var images=(JArray)report["images"];require(images.Count==16,"Expected four fixed views with four isolated variants.");
            var state=(string)images[0]["simulation_sha256"];
            foreach(var image in images)
            {
                require((string)image["simulation_sha256"]==state && (float)image["simulation_time"]==0,"Comparison did not preserve the paused state.");
                var bytes=File.ReadAllBytes((string)image["file"]);
                int width=(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19],height=(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23];
                require(width==1920 && height==1080,"Output dimensions differ.");
            }
            var audit=JObject.Parse(File.ReadAllText(EVIDENCE_ROOT+"/render-source-audit.json"));int textures_checked=0,meshes_checked=0;
            foreach(var material in audit["materials"])foreach(var item in material["textures"])
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>((string)item["path"]);
                require(texture && texture.anisoLevel==(int)item["aniso"] && texture.filterMode.ToString()==(string)item["filter"] && Mathf.Abs(texture.mipMapBias-(float)item["lod_bias"])<.00001f,"Texture sampling was not restored.");textures_checked++;
            }
            var original=AssetDatabase.LoadAssetAtPath<ImportedRoster>("Assets/_Game/Resources/CoastalUnits/Roster.asset");
            var owned=AssetDatabase.LoadAssetAtPath<ImportedRoster>(ROOT+"/Art/ReviewRoster.asset");
            for(int unit=0;unit<original.units.Length;unit++)
            {
                var a=original.units[unit].frames;var b=owned.units[unit].frames;require(original.units[unit].unit_id==owned.units[unit].unit_id,"Roster order changed.");
                if(!a){require(!b,"Missing art was substituted.");continue;}
                require(a.poses.Length==b.poses.Length && a.material.shader==b.material.shader,"Approved pose/shader changed.");
                if(a.material.HasProperty("_BakedLight"))require(a.material.GetFloat("_BakedLight")==b.material.GetFloat("_BakedLight"),"Owned material constant not restored.");
                for(int pose=0;pose<a.poses.Length;pose++)
                {
                    require(a.poses[pose].frames.Length==b.poses[pose].frames.Length,"Pose frame count changed.");
                    for(int frame=0;frame<a.poses[pose].frames.Length;frame++){require(a.poses[pose].frames[frame]==b.poses[pose].frames[frame],"Authored frame mesh was replaced.");meshes_checked++;}
                }
            }
            var capture=UnityEngine.Object.FindAnyObjectByType<RenderQualityCapture>();
            require(capture && !capture.capture_on_start && Time.timeScale==1 && !EditorApplication.isPlaying,"Review fixture leaked into normal Editor state.");
            var method=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            object[] counts={0,0,0};method.Invoke(null,counts);require((int)counts[0]==0 && !EditorUtility.scriptCompilationFailed,"Current Console has errors.");
            var result=new JObject{["status"]="TECHNICAL_PASS_NOT_ART_ACCEPTANCE",["images"]=images.Count,["simulation_sha256"]=state,["reused_original_animation_mesh_references"]=meshes_checked,
                ["restored_textures_checked"]=textures_checked,["current_console_errors"]=(int)counts[0],["current_console_warnings"]=(int)counts[1],
                ["scene"]=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,["scene_dirty"]=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,["play"]=EditorApplication.isPlaying,
                ["time_scale"]=Time.timeScale,["capture_on_start"]=capture.capture_on_start,["color_space"]=PlayerSettings.colorSpace.ToString(),
                ["timing_limit"]="Warm-up2 and median5 synchronous Camera.Render plus blocking GPU readback per PNG, Editor only; not representative foreground game FPS. Profiler triangle field includes other Editor/frame submissions, not a pure offscreen draw count.",
                ["visual_limit"]="Sampling reduces high-frequency aliasing but does not straighten source geometry or create missing PBR detail; lighting experiment is not selected as a production replacement."};
            File.WriteAllText(EVIDENCE_ROOT+"/editor-verification.json",result.ToString());Debug.Log("[RenderQuality] Technical checks PASS; style still pending.");
        }

        private static ImportedRoster create_owned_roster(ImportedRoster original,out RenderQualityCapture.MaterialBinding[] bindings)
        {
            require(original && original.units!=null,"Approved unit roster is missing.");
            string path=ROOT+"/Art/ReviewRoster.asset";var result=AssetDatabase.LoadAssetAtPath<ImportedRoster>(path);
            if(!result){result=ScriptableObject.CreateInstance<ImportedRoster>();AssetDatabase.CreateAsset(result,path);}
            var entries=new List<ImportedUnitArt>();var owned_bindings=new List<RenderQualityCapture.MaterialBinding>();
            foreach(var unit in original.units)
            {
                if(!unit.frames || !unit.frames.material || !unit.frames.material.HasProperty("_BakedLight"))
                {entries.Add(new ImportedUnitArt{unit_id=unit.unit_id,frames=unit.frames});continue;}
                string material_path=ROOT+"/Art/"+unit.unit_id+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(material_path);
                if(!material){material=new Material(unit.frames.material);AssetDatabase.CreateAsset(material,material_path);}
                else EditorUtility.CopySerialized(unit.frames.material,material);
                material.name="Owned review "+unit.unit_id;
                string frames_path=ROOT+"/Art/"+unit.unit_id+"-frames.asset";
                var frames=AssetDatabase.LoadAssetAtPath<CharacterFrames>(frames_path);
                if(!frames){frames=ScriptableObject.CreateInstance<CharacterFrames>();AssetDatabase.CreateAsset(frames,frames_path);}
                // Meshes/poses remain persistent references to approved animation assets, never recreated geometry.
                EditorUtility.CopySerialized(unit.frames,frames);frames.material=material;EditorUtility.SetDirty(material);EditorUtility.SetDirty(frames);
                entries.Add(new ImportedUnitArt{unit_id=unit.unit_id,frames=frames});
                owned_bindings.Add(new RenderQualityCapture.MaterialBinding{material=material,original_baked_light=unit.frames.material.GetFloat("_BakedLight")});
            }
            result.units=entries.ToArray();EditorUtility.SetDirty(result);bindings=owned_bindings.ToArray();return result;
        }

        private static Audit audit_source(FrontierGame game,int material_clones)
        {
            var prefab=game.defense_scenario.environment_prefab;var environment=prefab.GetComponent<FrozenEnvironmentView>();
            require(environment,"Approved frozen environment missing.");
            var audit=new Audit{device=SystemInfo.deviceModel+"; RAMMB="+SystemInfo.systemMemorySize,gpu=SystemInfo.graphicsDeviceName,
                unity=Application.unityVersion,pipeline=GraphicsSettings.currentRenderPipeline?GraphicsSettings.currentRenderPipeline.name:"Built-in",
                color_space=PlayerSettings.colorSpace.ToString(),grounding_handoff_sha256=sha("art/grounding_review_handoff.json"),
                frozen_trees=environment.frozen_tree_count,material_clone_count=material_clones};
            var unique_materials=new HashSet<Material>();var unique_textures=new HashSet<Texture>();
            foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>())
            {
                var filter=renderer.GetComponent<MeshFilter>();if(filter && filter.sharedMesh)audit.active_static_renderer_triangles+=triangle_count(filter.sharedMesh);
                string semantic=renderer.name.StartsWith("Headquarters",StringComparison.Ordinal)?"HQ":renderer.name.StartsWith("CityWall",StringComparison.Ordinal)?"Wall":null;
                if(semantic!=null)foreach(var material in renderer.sharedMaterials)if(material && unique_materials.Add(material))audit.materials.Add(audit_material(material,semantic,unique_textures));
                if(renderer.name=="HeadquartersGroundedReview_OriginalArchitecture")find_front_marker(renderer,game.defense_scenario.environment_translation,audit);
            }
            foreach(var batch in environment.batches)if(batch.mesh)audit.instanced_snapshot_triangles_all_lods+=(long)(batch.mesh.GetIndexCount(batch.submesh)/3)*batch.matrices.Length;
            foreach(var unit in game.imported_roster.units)if(unit.unit_id=="archer" && unit.frames && unit.frames.material)
                audit.materials.Add(audit_material(unit.frames.material,"Archer; shader samples base color only",unique_textures));
            var groundings=prefab.GetComponent<GroundingReviewBindings>();audit.grounding_pairs=groundings?groundings.pairs.Length:0;
            audit.source_texture_count=unique_textures.Count;require(audit.hq_front_marker_world!=Vector3.zero,"HQ unique source UV marker not located; cannot assume front direction.");
            return audit;
        }

        private static void find_front_marker(Renderer renderer,Vector3 environment_translation,Audit audit)
        {
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var uv=mesh.uv;var vertices=mesh.vertices;
            var target=new Vector2(.08500086516f,.53480535746f);float minimum=float.PositiveInfinity;int closest=-1;
            for(int index=0;index<uv.Length;index++){float d=(uv[index]-target).sqrMagnitude;if(d<minimum){minimum=d;closest=index;}}
            require(minimum<1e-10f && closest>=0,"HQ source front-marker UV not found.");
            audit.hq_front_marker_world=renderer.transform.TransformPoint(vertices[closest])+environment_translation;
            audit.hq_center=new Vector3(4,0,-16);
            var direction=audit.hq_front_marker_world-audit.hq_center;direction.y=0;
            // Camera forward points toward the source front, opposite the front's outward vector.
            audit.headquarters_front_yaw=Mathf.Repeat(Mathf.Atan2(-direction.x,-direction.z)*Mathf.Rad2Deg+30,360);
        }

        private static MaterialRecord audit_material(Material material,string semantic,HashSet<Texture> textures)
        {
            var record=new MaterialRecord{material=AssetDatabase.GetAssetPath(material),shader=material.shader.name,shader_path=AssetDatabase.GetAssetPath(material.shader),semantic=semantic,
                baked_light=material.HasProperty("_BakedLight")?material.GetFloat("_BakedLight"):0};
            record.actual_normal=has_texture(material,"_BumpMap") && (!material.HasProperty("_BumpScale") || material.GetFloat("_BumpScale")>0);
            record.actual_roughness=has_texture(material,"_Roughness") && (!material.HasProperty("_HasRoughness") || material.GetFloat("_HasRoughness")>0);
            record.actual_metallic=has_texture(material,"_MetallicMap") && (!material.HasProperty("_HasMetallic") || material.GetFloat("_HasMetallic")>0);
            record.actual_occlusion=has_texture(material,"_OcclusionMap") && (!material.HasProperty("_HasOcclusion") || material.GetFloat("_HasOcclusion")>0);
            foreach(string property in material.GetTexturePropertyNames())if(material.GetTexture(property) is Texture2D texture && textures.Add(texture))record.textures.Add(audit_texture(texture));
            return record;
        }

        private static bool has_texture(Material material,string name){return material.HasProperty(name) && material.GetTexture(name);}
        private static TextureRecord audit_texture(Texture2D texture)
        {
            string path=AssetDatabase.GetAssetPath(texture);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            var record=new TextureRecord{path=path,source_sha256=File.Exists(path)?sha(path):"embedded",format=texture.format.ToString(),filter=texture.filterMode.ToString(),
                effective_width=texture.width,effective_height=texture.height,mips=texture.mipmapCount,aniso=texture.anisoLevel,lod_bias=texture.mipMapBias,active_mip_limit=texture.activeMipmapLimit};
            if(importer){importer.GetSourceTextureWidthAndHeight(out record.source_width,out record.source_height);record.max_import_size=importer.maxTextureSize;
                record.compression=importer.textureCompression.ToString();record.texture_type=importer.textureType.ToString();record.mipmaps=importer.mipmapEnabled;record.srgb=importer.sRGBTexture;record.streaming=importer.streamingMipmaps;}
            return record;
        }

        private static long triangle_count(Mesh mesh){long total=0;for(int index=0;index<mesh.subMeshCount;index++)total+=(long)mesh.GetIndexCount(index)/3;return total;}
        private static IntegrityReport capture_integrity()
        {
            var report=new IntegrityReport();var paths=new HashSet<string>{
                "Assets/_Tests/V010Defense/Scenes/V010Defense.unity","Assets/_Game/Art/V010Defense/DefenseScenario.asset","Assets/_Game/Art/V010Defense/FrozenEnvironment.prefab",
                PresentationReviewTools.SCENE_PATH,PresentationReviewTools.SCENARIO_PATH,PresentationReviewTools.PREFAB_PATH,
                "balance/unit_balance.json","art/models/fortified-city/FortifiedCityWalls.fbx","art/models/grid-footprints/Pasture14_GoldenCorner/Pasture14_GoldenCorner.fbx"};
            foreach(string folder in new[]{"ProjectSettings","Assets/_Tests/V010PresentationReview","docs/versions/V0.10/evidence/camera-grounding"})
                foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))paths.Add(file.Replace('\\','/'));
            foreach(string path in paths)report.files.Add(new FileRecord{path=path,before=sha(path)});return report;
        }
        private static void verify_integrity(IntegrityReport report)
        {foreach(var file in report.files){file.after=sha(file.path);require(file.before==file.after,"Protected file changed: "+file.path);}report.status="PASS";}
        public static void verify_frozen_after_capture()
        {
            string path=EVIDENCE_ROOT+"/baseline-integrity.json";var report=JsonUtility.FromJson<IntegrityReport>(File.ReadAllText(path));
            verify_integrity(report);File.WriteAllText(path,JsonUtility.ToJson(report,true));
        }

        private static void snapshot_editor_quality()
        {
            SessionState.SetString(QUALITY_KEY,JsonUtility.ToJson(new QualitySnapshot{shadow_distance=QualitySettings.shadowDistance,cascades=QualitySettings.shadowCascades,
                resolution=QualitySettings.shadowResolution,anti_aliasing=QualitySettings.antiAliasing,vsync=QualitySettings.vSyncCount,target_frame_rate=Application.targetFrameRate,
                time_scale=Time.timeScale,color_space=PlayerSettings.colorSpace.ToString()}));
        }
        private static void on_play_mode(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetString(QUALITY_KEY,"")!="")EditorApplication.delayCall+=restore_editor_quality;}
        private static void restore_editor_quality()
        {
            var saved=JsonUtility.FromJson<QualitySnapshot>(SessionState.GetString(QUALITY_KEY,""));
            if(saved==null)return;QualitySettings.shadowDistance=saved.shadow_distance;QualitySettings.shadowCascades=saved.cascades;QualitySettings.shadowResolution=saved.resolution;
            QualitySettings.antiAliasing=saved.anti_aliasing;QualitySettings.vSyncCount=saved.vsync;Application.targetFrameRate=saved.target_frame_rate;Time.timeScale=saved.time_scale;
            require(PlayerSettings.colorSpace.ToString()==saved.color_space,"Project color space changed unexpectedly.");
            SessionState.EraseString(QUALITY_KEY);var capture=UnityEngine.Object.FindAnyObjectByType<RenderQualityCapture>();if(capture)capture.capture_on_start=false;
            verify_frozen_after_capture();Debug.Log("[RenderQuality] Editor quality restored; frozen baseline integrity PASS.");
        }
        private static string sha(string path){using(var stream=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static void guard_editor()
        {require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop owned comparison before preparing.");for(int index=0;index<UnityEngine.SceneManagement.SceneManager.sceneCount;index++)require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty,"Unsaved scene: preserve user edits first.");}
        private static void require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
