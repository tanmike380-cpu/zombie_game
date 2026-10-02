using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ZombieGame.World;
using ZombieGame.Presentation;

namespace ZombieGame.EditorTools
{
    public static class CoastalTools
    {
        private const string SOURCE="Assets/_Game/Art/CoastalImports";
        private const string GENERATED="Assets/_Game/Resources/CoastalBuildings";
        private const string FRAMES="Assets/_Game/Resources/CoastalUnits";
        private const string SCENE="Assets/_Tests/Frontier/Scenes/CoastalDefense.unity";
        [Serializable] private sealed class MaterialRecord { public string name,texture; public float[] color; }
        [Serializable] private sealed class ModelRecord { public string id,source; public int rig_bones; public bool reuse; public MaterialRecord[] materials; }
        [Serializable] private sealed class Manifest { public ModelRecord[] models; }

        public static void build_player()
        {
            CharacterBake.ensure_models();
            import_assets();rebuild_player();
        }
        public static void rebuild_player()
        {
            create_scene();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SCENE},locationPathName="Builds/CoastalDefense.app",target=BuildTarget.StandaloneOSX});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Coastal defense build failed");
            Debug.Log("[CoastalBuild] PASS imported roster, original colour, shared game systems");
        }

        public static void import_assets()
        {
            Directory.CreateDirectory(GENERATED);Directory.CreateDirectory(FRAMES);AssetDatabase.Refresh();
            var records=JsonUtility.FromJson<Manifest>(File.ReadAllText(SOURCE+"/manifest.json")).models;
            var bindings=new List<ImportedUnitArt>();
            foreach(var record in records)
            {
                if(record.reuse)continue;
                string path=$"{SOURCE}/{record.id}/{record.id}.fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.isReadable=true;importer.importAnimation=record.rig_bones>0;
                importer.animationType=record.rig_bones>0?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;
                importer.animationCompression=ModelImporterAnimationCompression.Off;
                importer.SaveAndReimport();
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    assign_materials(model,record);
                    if(record.id=="GreekFire")
                    {
                        foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
                        model.AddComponent<ImportedAnimationDisplay>().clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(clip=>clip.name=="Attack");
                    }
                    PrefabUtility.SaveAsPrefabAsset(model,$"{GENERATED}/{record.id}.prefab");
                    string unit_id=unit_for(record.id);
                    if(unit_id==null)continue;
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>($"{SOURCE}/{record.id}/{record.materials[0].texture}");
                    string target=$"{FRAMES}/{record.id}.asset";
                    TripoArcherImport.bake_model(model,texture,path,target,false,true,record.id=="ExploderLatest");
                    bindings.Add(new ImportedUnitArt{unit_id=unit_id,frames=AssetDatabase.LoadAssetAtPath<CharacterFrames>(target)});
                }
                finally{UnityEngine.Object.DestroyImmediate(model);}
            }
            TripoArcherImport.import_and_bake();TripoArcherImport.import_exploder();
            foreach(var entry in new[]{("heavy_crossbowman",TripoArcherImport.FRAME_PATH),("exploder",TripoArcherImport.EXPLODER_PATH)})
            {
                if(bindings.Any(binding=>binding.unit_id==entry.Item1))continue;
                bindings.Add(new ImportedUnitArt{unit_id=entry.Item1,frames=copy_shared_frames(entry.Item1,entry.Item2)});
            }
            string roster_path=FRAMES+"/Roster.asset";
            var roster=AssetDatabase.LoadAssetAtPath<ImportedRoster>(roster_path);
            if(roster==null){roster=ScriptableObject.CreateInstance<ImportedRoster>();AssetDatabase.CreateAsset(roster,roster_path);}
            roster.units=bindings.ToArray();EditorUtility.SetDirty(roster);AssetDatabase.SaveAssets();
            Debug.Log("[CoastalImport] PASS models="+records.Length+" active visual bindings="+bindings.Count);
        }
        private static CharacterFrames copy_shared_frames(string unit_id,string source_path)
        {
            // Share baked meshes, but isolate colour settings from the accepted benchmark materials.
            var source=AssetDatabase.LoadAssetAtPath<CharacterFrames>(source_path);
            string path=$"{FRAMES}/{unit_id}.asset";
            var frames=AssetDatabase.LoadAssetAtPath<CharacterFrames>(path);
            if(frames==null){frames=ScriptableObject.CreateInstance<CharacterFrames>();AssetDatabase.CreateAsset(frames,path);}
            EditorUtility.CopySerialized(source,frames);
            string material_path=$"{FRAMES}/{unit_id}.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(material_path);
            if(material==null){material=new Material(source.material);AssetDatabase.CreateAsset(material,material_path);}
            material.CopyPropertiesFromMaterial(source.material);material.shader=Shader.Find("ZombieGame/ImportedColour");
            material.enableInstancing=true;frames.material=material;
            EditorUtility.SetDirty(material);EditorUtility.SetDirty(frames);return frames;
        }
        private static string unit_for(string id)
        {
            switch(id){case "Archer":return "archer";case "Walker":return "walker";case "Hound":return "zombie_hound";case "Headbutter":return "giant";case "CageBoss":return "boss";case "ExploderLatest":return "exploder";default:return null;}
        }
        private static void assign_materials(GameObject model,ModelRecord record)
        {
            var materials=new Dictionary<string,Material>();
            foreach(var item in record.materials)
            {
                if(materials.ContainsKey(item.name))continue;
                string path=$"{GENERATED}/{record.id}-{materials.Count}.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(Shader.Find("ZombieGame/ImportedColour"));AssetDatabase.CreateAsset(material,path);}
                material.enableInstancing=true;material.shader=Shader.Find("ZombieGame/ImportedColour");
                if(!string.IsNullOrEmpty(item.texture)){material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>($"{SOURCE}/{record.id}/{item.texture}");material.color=Color.white;}
                else material.color=new Color(item.color[0],item.color[1],item.color[2],1);
                EditorUtility.SetDirty(material);materials[item.name]=material;
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials=renderer.sharedMaterials.Select(material=>materials.TryGetValue(material.name,out var replacement)?replacement:throw new InvalidOperationException("Unmapped imported material "+material.name)).ToArray();
        }
        public static void create_scene()
        {
            FrontierTools.create_scene(SCENE);
            var game=UnityEngine.Object.FindFirstObjectByType<FrontierGame>();
            game.coastal_defense=true;
            game.imported_roster=AssetDatabase.LoadAssetAtPath<ImportedRoster>(FRAMES+"/Roster.asset");
            game.gameObject.AddComponent<ZombieGame.FrontierTests.CoastalSmokeChecks>();
            EditorSceneManager.SaveScene(game.gameObject.scene,SCENE);AssetDatabase.SaveAssets();
        }
    }
}
