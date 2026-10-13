using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>References frozen accepted production exports without importing or changing their assets.</summary>
    public static class DarkWildernessTools
    {
        private const string ROOT="Assets/_Tests/EnvironmentShowcase/";
        private static readonly string[] IDS={"Headquarters","Workshop","Pasture"};
        private static readonly string[] FBX_HASHES={"caade4d341502d965706f9904daa935636f9882db3f0311dceb85485c932c58d","f38b563e805dd7a40126534b3465bcc2277113e6a08f96cde14b2d5dad19b5b4","2c5b73f3b8f628f69521bf7012de26debb262d368cbd4af7daba84e28c468acc"};
        private static readonly string[] PREFAB_HASHES={"cc811615968f8484be80453a5806fb17868626120d3f0a9d88605127c1c0c7d0","1e362bd17d4354f0e462b8c881a468aff3d95c956e7d23497152aa99c68778a6","f5c4311dc5c7b771e2b35344f19a202292363a3c1e3545eb468b5ec9c6159f8f"};
        [Serializable] private sealed class FileRecord
        {
            public string path,sha256;
        }
        [Serializable] private sealed class FrozenReport
        {
            public string policy;
            public FileRecord[] files;
        }

        [MenuItem("Zombie Game/Environment Showcase/Prepare and build dark wilderness")]
        public static void prepare_and_build_dark_preview()
        {
            EnvironmentShowcaseTools.create_scene();
            var config=AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if(!config.concept_layout) throw new InvalidOperationException("Prepare original concept preset first; never synthesize missing sources.");
            string dark_path=ROOT+"Settings/DarkWildernessConfig.asset";
            var dark=AssetDatabase.LoadAssetAtPath<DarkWildernessConfig>(dark_path);
            bool is_new=!dark;
            if(is_new)
            {
                dark=ScriptableObject.CreateInstance<DarkWildernessConfig>();
                AssetDatabase.CreateAsset(dark,dark_path);
            }
            if(!dark.layout)
            {
                dark.layout=UnityEngine.Object.Instantiate(config.concept_layout);
                dark.layout.name="Dark concept geometry";
                dark.layout.views[0]=new Vector2(.62f,.41f);
                dark.layout.sea_deep=new Color(.045f,.13f,.20f);
                dark.layout.sea_shallow=new Color(.16f,.27f,.29f);
                dark.layout.pond_deep=new Color(.06f,.13f,.14f);
                dark.layout.pond_shallow=new Color(.18f,.25f,.22f);
                dark.layout.handoff_record=dark.source_record;
                AssetDatabase.CreateAsset(dark.layout,ROOT+"Settings/DarkConceptLayout.asset");
            }
            dark.frozen_sources=prepare_frozen_sources();
            dark.stone_candidate=NativeBoulderImport.prepare_candidate();
            if(is_new) dark.placements=build_placements(dark.frozen_sources);
            if(!Array.Exists(dark.placements,entry=>entry.source_id=="BoulderNative"))
            {
                var placements=new List<DarkWildernessConfig.Placement>(dark.placements);
                var centers=new[]{new Vector2(.51f,.68f),new Vector2(.82f,.39f),new Vector2(.43f,.57f)};
                for(int index=0;index<centers.Length;index++)
                {
                    placements.Add(new DarkWildernessConfig.Placement
                    {
                        source_id="BoulderNative",
                        center=centers[index],
                        yaw=index*61,
                        uniform_scale=1.8f+index*.15f,
                        ore=true
                    });
                }
                dark.placements=placements.ToArray();
            }
            config.dark_layout=dark;
            EditorUtility.SetDirty(dark);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EnvironmentShowcaseTools.build_player();
        }

        private static EnvironmentShowcaseConfig.SourceAsset[] prepare_frozen_sources()
        {
            Directory.CreateDirectory(ROOT+"Art/FrozenBuildings");
            var result=new EnvironmentShowcaseConfig.SourceAsset[IDS.Length];
            var files=new List<FileRecord>();
            var paths=new HashSet<string>();
            for(int index=0;index<IDS.Length;index++)
            {
                string id=IDS[index];
                string model="Assets/_Game/Art/CoastalImports/"+id+"/"+id+".fbx";
                string prefab_path="Assets/_Game/Resources/CoastalBuildings/"+id+".prefab";
                verify_hash(model,FBX_HASHES[index]);
                verify_hash(prefab_path,PREFAB_HASHES[index]);
                var accepted=AssetDatabase.LoadAssetAtPath<GameObject>(prefab_path);
                if(!accepted) throw new InvalidOperationException("Accepted historical building missing: "+prefab_path);
                collect_dependency_records(prefab_path,paths,files);
                result[index]=prepare_frozen_wrapper(accepted,id,model,prefab_path,index);
            }
            preserve_frozen_record(files);
            AssetDatabase.Refresh();
            return result;
        }

        private static void collect_dependency_records(string prefab_path,HashSet<string> paths,List<FileRecord> files)
        {
            foreach(string dependency in AssetDatabase.GetDependencies(prefab_path,true))
            {
                if(File.Exists(dependency)&&paths.Add(dependency))
                {
                    files.Add(new FileRecord{path=dependency,sha256=hash_file(dependency)});
                }
            }
        }

        /// <summary>Wrap a frozen accepted assembly, retaining every child transform and material reference.</summary>
        private static EnvironmentShowcaseConfig.SourceAsset prepare_frozen_wrapper(GameObject accepted,string id,
            string model,string prefab_path,int index)
        {
            var wrapper=new GameObject(id+" historical frozen export");
            try
            {
                var child=(GameObject)PrefabUtility.InstantiatePrefab(accepted);
                child.transform.SetParent(wrapper.transform,false);
                if(wrapper.GetComponentsInChildren<Collider>(true).Length>0||wrapper.GetComponentsInChildren<MonoBehaviour>(true).Length>0)
                    throw new InvalidOperationException("Historical building is not a static art source: "+id);
                var bounds=measure_vertices(wrapper);
                var materials=collect_material_bindings(wrapper);
                var saved=PrefabUtility.SaveAsPrefabAsset(wrapper,ROOT+"Art/FrozenBuildings/"+id+".prefab");
                var source=new EnvironmentShowcaseConfig.SourceAsset
                {
                    id=id,
                    prefab=saved,
                    native_dimensions=bounds.size,
                    native_bounds_center=bounds.center,
                    native_min_y=bounds.min.y,
                    material_bindings=materials.ToArray(),
                    source_record="Historical accepted export; "+model+" SHA256="+FBX_HASHES[index]+"; "+prefab_path+" SHA256="+PREFAB_HASHES[index]+"; object mapping Assets/_Game/Art/CoastalImports/manifest.json. NOT current master re-export."
                };
                Debug.Log("[DarkWilderness] Frozen "+id+" native wrapper bounds="+bounds.size+" minY="+bounds.min.y);
                return source;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wrapper);
            }
        }

        private static List<EnvironmentShowcaseConfig.SourceMaterialBinding> collect_material_bindings(GameObject wrapper)
        {
            var materials=new List<EnvironmentShowcaseConfig.SourceMaterialBinding>();
            foreach(var renderer in wrapper.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach(var material in renderer.sharedMaterials)
                {
                    materials.Add(new EnvironmentShowcaseConfig.SourceMaterialBinding
                    {
                        material=material,
                        base_color=material.GetTexture("_MainTex") as Texture2D,
                        import_channels_pass=true,
                        normal_note="Historical accepted material retained; base colour only, no invented PBR channels."
                    });
                }
            }
            return materials;
        }

        private static void preserve_frozen_record(List<FileRecord> files)
        {
            string record_path=ROOT+"Settings/dark-frozen-source-record.json";
            if(File.Exists(record_path))
            {
                foreach(var record in JsonUtility.FromJson<FrozenReport>(File.ReadAllText(record_path)).files)
                {
                    verify_hash(record.path,record.sha256);
                }
            }
            else
            {
                File.WriteAllText(record_path,JsonUtility.ToJson(new FrozenReport
                {
                    policy="Parent-authorized reuse of already-accepted exports. Freeze current dependency checksums; no current Blender master validation or re-export. Source assembly transforms/materials unchanged.",
                    files=files.ToArray()
                },true));
            }
        }

        private static Bounds measure_vertices(GameObject root)
        {
            var bounds=new Bounds();
            bool has_vertex=false;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh.isReadable) throw new InvalidOperationException("Historical mesh not readable; do not mutate shared importer: "+filter.name);
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
            if(!has_vertex) throw new InvalidOperationException("Historical source has no vertices: "+root.name);
            return bounds;
        }

        private static DarkWildernessConfig.Placement[] build_placements(EnvironmentShowcaseConfig.SourceAsset[] sources)
        {
            var list=new List<DarkWildernessConfig.Placement>();
            var centers=new[]{new Vector2(.60f,.32f),new Vector2(.75f,.39f),new Vector2(.56f,.53f)};
            var widths=new[]{12f,8f,13f};
            for(int index=0;index<sources.Length;index++)
            {
                list.Add(new DarkWildernessConfig.Placement
                {
                    source_id=sources[index].id,
                    center=centers[index],
                    yaw=index*17,
                    uniform_scale=widths[index]/Mathf.Max(sources[index].native_dimensions.x,sources[index].native_dimensions.z)
                });
            }
            list.Add(new DarkWildernessConfig.Placement{source_id="EuropeanStoneHouse",center=new Vector2(.47f,.57f),yaw=12});
            list.Add(new DarkWildernessConfig.Placement{source_id="NorthAfricanRuins",center=new Vector2(.39f,.78f),yaw=5});
            list.Add(new DarkWildernessConfig.Placement{source_id="MiddleEasternCourtyard",center=new Vector2(.87f,.51f),yaw=0});
            list.Add(new DarkWildernessConfig.Placement{source_id="EuropeanStoneHouse",center=new Vector2(.56f,.80f),yaw=27});
            var ores=new[]{new Vector2(.43f,.35f),new Vector2(.70f,.62f),new Vector2(.90f,.64f),new Vector2(.47f,.72f),new Vector2(.82f,.55f),new Vector2(.66f,.86f)};
            for(int index=0;index<ores.Length;index++)
            {
                list.Add(new DarkWildernessConfig.Placement
                {
                    source_id=index<3?"IronOreRock":"GoldOreRock",
                    center=ores[index],
                    yaw=index*47,
                    uniform_scale=2.5f,
                    ore=true
                });
            }
            return list.ToArray();
        }

        private static string hash_file(string path)
        {
            using(var algorithm=SHA256.Create())
            using(var stream=File.OpenRead(path))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            }
        }
        private static void verify_hash(string path,string expected)
        {
            if(!File.Exists(path)||hash_file(path)!=expected) throw new InvalidOperationException("Frozen accepted source checksum changed: "+path);
        }
    }
}
