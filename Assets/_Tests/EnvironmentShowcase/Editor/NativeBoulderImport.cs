using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Untouched licensed native FBX. Only original LOD selection and Built-in material adaptation.</summary>
    public static class NativeBoulderImport
    {
        private const string MANIFEST="art/environment/2026-10-14-free-source-review.json";
        private const string ROOT="Assets/_Tests/EnvironmentShowcase/Art/FreeCandidates/Boulder/";
        [Serializable] private sealed class Manifest
        {
            public Asset[] assets;
        }
        [Serializable] private sealed class Asset
        {
            public string id,license,source_url,model_path,model_sha256;
            public Group[] material_channel_groups;
            public Selection selection;
        }
        [Serializable] private sealed class Selection
        {
            public string near_native_object,far_native_object;
            public int near_triangles,far_triangles;
        }
        [Serializable] private sealed class Group
        {
            public string name;
            public Channels channels;
        }
        [Serializable] private sealed class Channels
        {
            public TextureRecord base_color,normal_gl,roughness,AO;
        }
        [Serializable] private sealed class TextureRecord
        {
            public string path,sha256;
        }

        public static EnvironmentShowcaseConfig.SourceAsset prepare_candidate()
        {
            var source = load_verified_source();
            Directory.CreateDirectory(ROOT);
            string model_path = copy_verified(source.model_path,source.model_sha256);
            var channels = source.material_channel_groups[0].channels;
            var base_path = copy_verified(channels.base_color.path,channels.base_color.sha256);
            var normal_path = copy_verified(channels.normal_gl.path,channels.normal_gl.sha256);
            var rough_path = copy_verified(channels.roughness.path,channels.roughness.sha256);
            var ao_path = copy_verified(channels.AO.path,channels.AO.sha256);
            AssetDatabase.Refresh();
            prepare_model_import(model_path);
            var color = prepare_texture(base_path,true,false);
            var normal = prepare_texture(normal_path,false,true);
            var rough = prepare_texture(rough_path,false,false);
            var ao = prepare_texture(ao_path,false,false);
            var material = prepare_material(color,normal,rough,ao);
            var binding = new EnvironmentShowcaseConfig.SourceMaterialBinding
            {
                material = material,
                base_color = color,
                normal = normal,
                roughness = rough,
                occlusion = ao,
                import_channels_pass = true,
                normal_note = "Official nor_gl source; no green flip. AO actual source, metallic zero."
            };
            return prepare_native_prefab(source,model_path,binding);
        }

        private static Asset load_verified_source()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(MANIFEST));
            var source=Array.Find(manifest.assets,entry=>entry.id=="boulder_01");
            if(source==null||source.license!="CC0-1.0"||source.material_channel_groups.Length!=1||source.selection.near_triangles!=16530||source.selection.far_triangles!=8264)
                throw new InvalidOperationException("Boulder licensed/native-LOD handoff incomplete.");
            return source;
        }

        private static void prepare_model_import(string model_path)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(model_path);
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.isReadable=true;
            importer.importCameras=false;
            importer.importLights=false;
            importer.SaveAndReimport();
        }

        /// <summary>Save a wrapper with exactly the approved native LOD tiers; never edit the FBX assembly.</summary>
        private static EnvironmentShowcaseConfig.SourceAsset prepare_native_prefab(Asset source,string model_path,
            EnvironmentShowcaseConfig.SourceMaterialBinding binding)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(model_path);
            var root=new GameObject("BoulderNative — licensed stone candidate");
            try
            {
                var child=(GameObject)PrefabUtility.InstantiatePrefab(model);
                child.transform.SetParent(root.transform,false);
                // Unity already generated a native LODGroup. Remove only the duplicate presentation component on this wrapper instance.
                foreach(var source_group in child.GetComponentsInChildren<LODGroup>(true))
                {
                    UnityEngine.Object.DestroyImmediate(source_group);
                }
                MeshRenderer near=null,far=null;
                foreach(var renderer in child.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.enabled=false;
                    if(renderer.name==source.selection.near_native_object)
                    {
                        near=renderer;
                        verify_triangles(renderer,source.selection.near_triangles);
                    }
                    if(renderer.name==source.selection.far_native_object)
                    {
                        far=renderer;
                        verify_triangles(renderer,source.selection.far_triangles);
                    }
                    renderer.sharedMaterial=binding.material;
                }
                if(!near||!far) throw new InvalidOperationException("Exact native Boulder LOD names not imported.");
                near.enabled=true;
                far.enabled=true;
                var bounds=measure_selected_vertices(root,new[]{near,far});
                if(Vector3.Distance(bounds.size,new Vector3(1.2727f,1.0036f,1.829f))>.025f)
                    throw new InvalidOperationException("Native Boulder unit/axis bounds mismatch: "+bounds.size);
                var group=root.AddComponent<LODGroup>();
                group.SetLODs(new[]{new LOD(.18f,new Renderer[]{near}),new LOD(.015f,new Renderer[]{far})});
                group.RecalculateBounds();
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,ROOT+"BoulderNative.prefab");
                return new EnvironmentShowcaseConfig.SourceAsset
                {
                    id="BoulderNative",
                    prefab=prefab,
                    native_dimensions=bounds.size,
                    native_bounds_center=bounds.center,
                    native_min_y=bounds.min.y,
                    source_record=MANIFEST+"#boulder_01; "+source.source_url+"; CC0-1.0 Rico Cilliers; native FBX SHA256="+source.model_sha256+"; original LOD2/LOD3 16530/8264, no re-export/decimation. Natural stone candidate, NOT user-authored stone mine.",
                    material_bindings=new[]{binding}
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string copy_verified(string source,string expected)
        {
            string destination=ROOT+Path.GetFileName(source);
            using(var hash=SHA256.Create())
            using(var stream=File.OpenRead(source))
            {
                if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=expected)
                    throw new InvalidOperationException("Native source checksum mismatch: "+source);
            }
            if(!File.Exists(destination))
            {
                File.Copy(source,destination);
            }
            else if(!File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(destination)))
            {
                throw new InvalidOperationException("Candidate copy differs from exact source: "+destination);
            }
            return destination;
        }
        private static Texture2D prepare_texture(string path,bool color,bool normal)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=color;
            importer.mipmapEnabled=true;
            importer.maxTextureSize=2048;
            importer.filterMode=FilterMode.Trilinear;
            importer.wrapMode=TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static Material prepare_material(Texture2D color,Texture2D normal,Texture2D rough,Texture2D ao)
        {
            string path=ROOT+"BoulderNative.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material)
            {
                material=new Material(Shader.Find("ZombieGame/EnvironmentShowcase/SourceOpaque"));
                AssetDatabase.CreateAsset(material,path);
            }
            material.SetTexture("_MainTex",color);
            material.SetTexture("_BumpMap",normal);
            material.SetTexture("_Roughness",rough);
            material.SetTexture("_OcclusionMap",ao);
            material.SetFloat("_HasRoughness",1);
            material.SetFloat("_HasOcclusion",1);
            material.SetFloat("_BumpScale",.4f);
            material.SetFloat("_HasMetallic",0);
            material.SetFloat("_MetallicConstant",0);
            material.SetFloat("_Cull",2);
            EditorUtility.SetDirty(material);
            return material;
        }
        private static void verify_triangles(MeshRenderer renderer,int expected)
        {
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            if(mesh.triangles.Length/3!=expected||mesh.uv.Length!=mesh.vertexCount) throw new InvalidOperationException("Native Boulder topology/UV mismatch: "+renderer.name);
        }
        private static Bounds measure_selected_vertices(GameObject root,MeshRenderer[] renderers)
        {
            var bounds=new Bounds();
            bool has_vertex=false;
            foreach(var renderer in renderers)
            {
                var filter=renderer.GetComponent<MeshFilter>();
                var matrix=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var point=matrix.MultiplyPoint3x4(vertex);
                    if(!has_vertex)
                    {
                        bounds=new Bounds(point,Vector3.zero);
                        has_vertex=true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            return bounds;
        }
    }
}
