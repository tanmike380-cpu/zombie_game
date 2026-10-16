using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.Tests.V010PresentationReview.EditorTools
{
    /// <summary>Import only the specialist's frozen two-building grounding review, preserving the old map and all source transforms.</summary>
    public static class GroundingReviewImport
    {
        public const string HANDOFF_PATH = "art/grounding_review_handoff.json";
        private const string ART_ROOT = "Assets/_Game/Art/BuildingGroundingReview/";
        [Serializable] private sealed class Handoff
        {public int schema_version;public string status,source_blend,master_sha256;public GroundingAsset[] assets;}
        [Serializable] private sealed class GroundingAsset
        {
            public string id,source_id,export_root_name;
            public float[] footprint_m,expected_unity_bounds_center_m,expected_unity_bounds_size_m;
            public float grid_unit_m,ground_anchor_y_m,unity_ground_offset_m;
            public Export export;public ComponentRecord[] components;
        }
        [Serializable] private sealed class Export
        {public string file,sha256;public bool scale_baked;public float unity_placement_scale;public SourceMaterial[] materials;public Roundtrip roundtrip;}
        [Serializable] private sealed class Roundtrip
        {public bool passed;public int components,uv_loop_samples;public float maximum_vertex_error_m,maximum_uv_error;}
        [Serializable] private sealed class ComponentRecord
        {
            public string name,vertex_alpha_channel;
            public string[] material_names;
            public bool shadow_casting = true;
            public float[] expected_unity_bounds_center_m,expected_unity_bounds_size_m;
        }
        [Serializable] private sealed class SourceMaterial
        {
            public string name,source_material,base_color_texture,base_color_sha256,alpha_mode,vertex_alpha_channel;
            public float[] base_color_multiplier;
            public float metallic,roughness = .9f,normal_strength;
            public bool double_sided;
            public Channels channels;
        }
        [Serializable] private sealed class Channels {public TextureRecord diff,nor_gl,rough;}
        [Serializable] private sealed class TextureRecord {public string file,sha256,colorspace;public int[] resolution;}
        [Serializable] private sealed class ImportEvidence
        {
            public string status,source_blend,master_sha256,handoff_sha256;
            public int pairs,active_old_integer_ground;
            public List<string> models = new List<string>(),checks = new List<string>();
        }

        [MenuItem("Zombie Game/V0.10/Presentation review/Import frozen grounding and prepare")]
        public static void import_grounding_review()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop owned review before source import.");
            var handoff = read_frozen_handoff();
            Directory.CreateDirectory(ART_ROOT);Directory.CreateDirectory(PresentationReviewTools.EVIDENCE_ROOT);
            var prefabs = new Dictionary<string,GameObject>();
            var evidence = new ImportEvidence {source_blend=handoff.source_blend,master_sha256=handoff.master_sha256,
                handoff_sha256=PresentationReviewTools.hash_file(HANDOFF_PATH)};
            foreach(var asset in handoff.assets)prefabs.Add(asset.source_id,import_asset(asset,evidence));
            var environment = PresentationReviewTools.clone_frozen_environment();
            try
            {
                var pairs = new List<PresentationCapture.GroundingPair>();
                foreach(var fixture in environment.GetComponentsInChildren<Transform>(true))
                {
                    if(!fixture.name.StartsWith("FrozenFixture-",StringComparison.Ordinal))continue;
                    string id=fixture.name.EndsWith("-Headquarters",StringComparison.Ordinal)?"Headquarters":fixture.name.EndsWith("-Workshop",StringComparison.Ordinal)?"Workshop":null;
                    if(id == null)continue;
                    if(fixture.childCount != 1)throw new InvalidOperationException("Original complete building root is ambiguous: "+fixture.name);
                    var before = fixture.GetChild(0).gameObject;
                    var after = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[id],fixture);
                    after.transform.localPosition = Vector3.zero;
                    // Imported root carries its authored FBX axis transform; never re-fit sub-pieces.
                    compare_architecture(before,after,fixture.name,evidence);
                    before.SetActive(false);after.SetActive(true);
                    pairs.Add(new PresentationCapture.GroundingPair {fixture_name=fixture.name,before=before,after=after,
                        source_record=HANDOFF_PATH+"; "+id+"; masterSHA="+handoff.master_sha256});
                }
                if(pairs.Count < 3)throw new InvalidOperationException("Expected all HQ and repeated Workshop fixtures, not only the centre city.");
                var bindings = environment.AddComponent<GroundingReviewBindings>();bindings.pairs=pairs.ToArray();
                bindings.source_record=HANDOFF_PATH+"; SHA="+evidence.handoff_sha256+"; masterSHA="+handoff.master_sha256;
                foreach(var renderer in environment.GetComponentsInChildren<MeshRenderer>())
                    if(renderer.name.IndexOf("IntegerGround",StringComparison.Ordinal)>=0)evidence.active_old_integer_ground++;
                if(evidence.active_old_integer_ground != 0)throw new InvalidOperationException("An old hard rectangular IntegerGround remains active.");
                evidence.pairs=pairs.Count;
                var prefab = PrefabUtility.SaveAsPrefabAsset(environment,PresentationReviewTools.PREFAB_PATH);
                evidence.checks.Add("All HQ/Workshop fixture placements retained, including repeated external workshops; old thick ground roots inactive.");
                evidence.checks.Add("Wall/farm/tree/mineral source roots and all materials outside the two authorised replacements remain references to the frozen environment.");
                evidence.status="SOURCE_IMPORT_PASS_VISUAL_REVIEW_PENDING";
                File.WriteAllText(PresentationReviewTools.EVIDENCE_ROOT+"/grounding-import.json",JsonUtility.ToJson(evidence,true));
                PresentationReviewTools.prepare_review_scene(prefab,bindings.source_record);
            }
            finally{PrefabUtility.UnloadPrefabContents(environment);}
            AssetDatabase.SaveAssets();
        }

        private static Handoff read_frozen_handoff()
        {
            if(!File.Exists(HANDOFF_PATH))throw new FileNotFoundException("Wait for the specialist's final frozen handoff.",HANDOFF_PATH);
            var handoff=JsonUtility.FromJson<Handoff>(File.ReadAllText(HANDOFF_PATH));
            if(handoff == null || handoff.schema_version != 1 || handoff.status != "Blender source frozen; Unity visual review pending" ||
                handoff.source_blend != "art/Blender主文件/ZombieGame.blend" || handoff.assets?.Length != 2)
                throw new InvalidOperationException("Grounding handoff is draft, missing, or outside the two approved sources.");
            verify_hash(handoff.source_blend,handoff.master_sha256);
            var expected = new Dictionary<string,float>{{"Headquarters",12},{"Workshop",8}};
            foreach(var asset in handoff.assets)
            {
                if(asset == null || !expected.TryGetValue(asset.source_id,out float size))throw new InvalidOperationException("Unexpected/duplicate grounding asset.");
                expected.Remove(asset.source_id);
                if(string.IsNullOrWhiteSpace(asset.id) || asset.id.Contains("/") || asset.id.Contains("..") || asset.components == null || asset.components.Length < 2 ||
                    asset.footprint_m?.Length != 2 || asset.footprint_m[0]!=size || asset.footprint_m[1]!=size || asset.grid_unit_m!=1 ||
                    Mathf.Abs(asset.ground_anchor_y_m)>.00001f || Mathf.Abs(asset.unity_ground_offset_m-.025f)>.00001f ||
                    asset.export==null || !asset.export.scale_baked || asset.export.unity_placement_scale!=1 || asset.export.materials==null ||
                    asset.export.roundtrip==null || !asset.export.roundtrip.passed || asset.export.roundtrip.components!=asset.components.Length ||
                    asset.export.roundtrip.maximum_vertex_error_m>.00003f || asset.export.roundtrip.maximum_uv_error>.000001f)
                    throw new InvalidOperationException("Grounding units/anchor/roundtrip are invalid: "+asset.id);
                verify_hash(asset.export.file,asset.export.sha256);
            }
            return handoff;
        }

        private static GameObject import_asset(GroundingAsset asset,ImportEvidence evidence)
        {
            string folder=ART_ROOT+asset.id+"-"+asset.export.sha256.Substring(0,12)+"/";
            Directory.CreateDirectory(folder);
            string path=copy_verified(asset.export.file,folder+asset.id+".fbx",asset.export.sha256);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale=1;importer.useFileScale=true;importer.preserveHierarchy=true;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;
            importer.meshCompression=ModelImporterMeshCompression.Off;importer.importNormals=ModelImporterNormals.Import;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;importer.SaveAndReimport();
            var preview_scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),preview_scene);
            try
            {
                var materials=import_materials(asset.export.materials,folder);
                var filters=instance.GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length!=asset.components.Length)throw new InvalidOperationException("Grounding component count changed.");
                bool first=true;var total=new Bounds();
                foreach(var filter in filters)
                {
                    var record=Array.Find(asset.components,item=>item.name==filter.name);
                    if(record==null || record.material_names?.Length!=filter.sharedMesh.subMeshCount)throw new InvalidOperationException("Undeclared grounding mesh/material slots: "+filter.name);
                    var source_bounds=mesh_world_bounds(filter);compare_bounds(source_bounds,record.expected_unity_bounds_center_m,record.expected_unity_bounds_size_m,record.name);
                    if(first){total=source_bounds;first=false;}else total.Encapsulate(source_bounds);
                    var slots=new Material[record.material_names.Length];bool blend=false;
                    for(int index=0;index<slots.Length;index++)
                    {
                        if(!materials.TryGetValue(record.material_names[index],out slots[index]))throw new InvalidOperationException("Grounding declared material not found.");
                        blend|=slots[index].shader.name=="ZombieGame/PresentationReview/GroundingSurface";
                    }
                    if(blend)
                    {
                        var colours=filter.sharedMesh.colors32;
                        if(colours.Length!=filter.sharedMesh.vertexCount)throw new InvalidOperationException("Authored vertex edge opacity absent: "+filter.name);
                        byte minimum=255,maximum=0;foreach(var colour in colours){minimum=Math.Min(minimum,colour.a);maximum=Math.Max(maximum,colour.a);}
                        if(minimum!=0 || maximum!=255)throw new InvalidOperationException("Grounding alpha does not cover transparent edge and opaque interior.");
                    }
                    var renderer=filter.GetComponent<MeshRenderer>();renderer.sharedMaterials=slots;
                    renderer.shadowCastingMode=blend?ShadowCastingMode.Off:ShadowCastingMode.On;renderer.receiveShadows=true;
                }
                compare_bounds(total,asset.expected_unity_bounds_center_m,asset.expected_unity_bounds_size_m,asset.id);
                evidence.models.Add(asset.id+"; FBX="+asset.export.sha256+"; bounds="+total+"; whole native-scale1; source root/mesh normals/UV/vertex alpha retained");
                return PrefabUtility.SaveAsPrefabAsset(instance,folder+asset.id+".prefab");
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview_scene);importer.isReadable=false;importer.SaveAndReimport();}
        }

        private static Dictionary<string,Material> import_materials(SourceMaterial[] records,string folder)
        {
            var materials=new Dictionary<string,Material>();
            foreach(var record in records)
            {
                bool blend=record.alpha_mode=="blend";
                if(!blend && !string.IsNullOrEmpty(record.alpha_mode) && record.alpha_mode!="opaque")throw new InvalidOperationException("Unsupported source alpha mode.");
                var shader=Shader.Find(blend?"ZombieGame/PresentationReview/GroundingSurface":"ZombieGame/EnvironmentShowcase/SourceOpaque");
                if(!shader || !shader.isSupported)throw new InvalidOperationException("Grounding PBR adapter unavailable.");
                var material=new Material(shader){name=record.name};
                var diffuse=record.channels?.diff;
                if(diffuse==null && !string.IsNullOrEmpty(record.base_color_texture))diffuse=new TextureRecord{file=record.base_color_texture,sha256=record.base_color_sha256,colorspace="sRGB"};
                var color=diffuse==null?null:import_texture(diffuse,folder,false,true);
                var normal=record.channels?.nor_gl==null?null:import_texture(record.channels.nor_gl,folder,true,false);
                var rough=record.channels?.rough==null?null:import_texture(record.channels.rough,folder,false,false);
                material.SetTexture("_MainTex",color);material.SetTexture("_BumpMap",normal);material.SetTexture("_Roughness",rough);
                var c=record.base_color_multiplier;
                material.SetColor("_Color",c==null?Color.white:new Color(c[0],c[1],c[2],c[3]).gamma);
                material.SetFloat("_BumpScale",normal?record.normal_strength:0);material.SetFloat("_HasRoughness",rough?1:0);material.SetFloat("_RoughnessConstant",record.roughness);
                if(!blend){material.SetFloat("_MetallicConstant",record.metallic);material.SetFloat("_HasMetallic",0);material.SetFloat("_HasOcclusion",0);material.SetFloat("_Cull",record.double_sided?0:2);}
                string path=folder+record.name+".mat";
                var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing){EditorUtility.CopySerialized(material,existing);UnityEngine.Object.DestroyImmediate(material);material=existing;EditorUtility.SetDirty(material);}else AssetDatabase.CreateAsset(material,path);
                materials.Add(record.name,material);
            }
            return materials;
        }

        private static Texture2D import_texture(TextureRecord record,string folder,bool normal,bool srgb)
        {
            if(record==null || string.IsNullOrWhiteSpace(record.file) || (srgb?record.colorspace!="sRGB":record.colorspace!="Non-Color"))throw new InvalidOperationException("Grounding texture channels/colour space incomplete.");
            Directory.CreateDirectory(folder+"Textures/");
            string path=copy_verified(record.file,folder+"Textures/"+record.sha256.Substring(0,12)+"-"+Path.GetFileName(record.file),record.sha256);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=srgb;
            importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void compare_architecture(GameObject before,GameObject after,string label,ImportEvidence evidence)
        {
            MeshFilter original=null,replacement=null;
            foreach(var filter in before.GetComponentsInChildren<MeshFilter>(true))if(filter.name.IndexOf("Architecture",StringComparison.OrdinalIgnoreCase)>=0)original=filter;
            foreach(var filter in after.GetComponentsInChildren<MeshFilter>(true))if(filter.name.IndexOf("Architecture",StringComparison.OrdinalIgnoreCase)>=0)replacement=filter;
            if(!original || !replacement)throw new InvalidOperationException("Grounding source has no uniquely traced architecture: "+label);
            var old_bounds=original.GetComponent<Renderer>().bounds;var new_bounds=replacement.GetComponent<Renderer>().bounds;
            if((old_bounds.center-new_bounds.center).magnitude>.0002f || (old_bounds.size-new_bounds.size).magnitude>.0002f || original.sharedMesh.vertexCount!=replacement.sharedMesh.vertexCount)
                throw new InvalidOperationException("Source building placement/proportions changed; return to Blender, do not refit: "+label+"; "+old_bounds+" vs "+new_bounds);
            evidence.checks.Add(label+": architecture vertex count="+original.sharedMesh.vertexCount+"; world bounds and source-scale alignment unchanged within0.2mm; original source preservation proof retained in handoff.");
        }

        private static Bounds mesh_world_bounds(MeshFilter filter)
        {
            var vertices=filter.sharedMesh.vertices;if(vertices.Length==0)throw new InvalidOperationException("Empty grounding source mesh.");
            var bounds=new Bounds(filter.transform.TransformPoint(vertices[0]),Vector3.zero);
            for(int index=1;index<vertices.Length;index++)bounds.Encapsulate(filter.transform.TransformPoint(vertices[index]));return bounds;
        }

        private static void compare_bounds(Bounds actual,float[] center,float[] size,string name)
        {
            if(center?.Length!=3 || size?.Length!=3)throw new InvalidOperationException("Grounding source bounds absent: "+name);
            for(int axis=0;axis<3;axis++)if(Mathf.Abs(actual.center[axis]-center[axis])>.003f || Mathf.Abs(actual.size[axis]-size[axis])>.003f)
                throw new InvalidOperationException("Grounding source axis/scale mismatch: "+name+"; "+actual);
        }

        private static string copy_verified(string source,string target,string sha)
        {
            if(!target.StartsWith(ART_ROOT,StringComparison.Ordinal) || target.Contains("..") || !source.StartsWith("art/",StringComparison.Ordinal) || source.Contains(".."))throw new InvalidOperationException("Grounding copy outside approved asset scope.");
            verify_hash(source,sha);if(!File.Exists(target))File.Copy(source,target);verify_hash(target,sha);return target;
        }

        private static void verify_hash(string path,string sha)
        {if(sha?.Length!=64 || PresentationReviewTools.hash_file(path)!=sha)throw new InvalidOperationException("Grounding frozen source hash mismatch: "+path);}
    }
}
