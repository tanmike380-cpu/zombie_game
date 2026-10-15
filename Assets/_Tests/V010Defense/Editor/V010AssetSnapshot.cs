using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieGame.Presentation;

namespace ZombieGame.Tests.V010Defense.EditorTools
{
    /// <summary>Copies approved render data without changing geometry, UVs, source material values or child transforms.</summary>
    internal sealed class V010AssetSnapshot
    {
        private readonly string folder;
        private readonly Dictionary<UnityEngine.Object,UnityEngine.Object> snapshots = new Dictionary<UnityEngine.Object,UnityEngine.Object>();
        private readonly List<string> source_records = new List<string>();
        private int next_asset;
        public string[] records => source_records.ToArray();

        public V010AssetSnapshot(string output_folder)
        {folder = output_folder;Directory.CreateDirectory(folder);AssetDatabase.Refresh();}

        public void freeze_root(GameObject root)
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))filter.sharedMesh = freeze(filter.sharedMesh);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for(int index = 0; index < materials.Length; index++) materials[index] = freeze(materials[index]);
                renderer.sharedMaterials = materials;
            }
            foreach(var terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                terrain.terrainData = freeze(terrain.terrainData);
                terrain.materialTemplate = freeze(terrain.materialTemplate);
                var collider = terrain.GetComponent<TerrainCollider>();
                if(collider) collider.terrainData = terrain.terrainData;
            }
            var view = root.GetComponent<FrozenEnvironmentView>();
            foreach(var batch in view.batches){batch.mesh = freeze(batch.mesh);batch.material = freeze(batch.material);}
        }

        private T freeze<T>(T source) where T:UnityEngine.Object
        {
            if(!source) return null;
            if(snapshots.TryGetValue(source,out var cached)) return (T)cached;
            string source_path = AssetDatabase.GetAssetPath(source);
            if(source_path.StartsWith("Assets/_Game/Art/",StringComparison.Ordinal) ||
                source_path == "Resources/unity_builtin_extra" || source_path == "Library/unity default resources") return source;
            string safe_name = source.name;
            foreach(char character in Path.GetInvalidFileNameChars())safe_name = safe_name.Replace(character,'_');
            string extension = source is Material ? ".mat" : ".asset";
            string path = folder+"/"+(next_asset++).ToString("D4")+"-"+safe_name+extension;
            T snapshot;
            if(source is Texture && !string.IsNullOrEmpty(source_path) && !source_path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase))
            {
                path = Path.ChangeExtension(path,Path.GetExtension(source_path));
                snapshot = AssetDatabase.LoadAssetAtPath<T>(path);
                if(!snapshot)
                {
                    if(!AssetDatabase.CopyAsset(source_path,path))throw new InvalidOperationException("Cannot freeze approved texture: "+source_path);
                    snapshot = AssetDatabase.LoadAssetAtPath<T>(path);
                }
            }
            else
            {
                snapshot = AssetDatabase.LoadAssetAtPath<T>(path);
                if(snapshot)EditorUtility.CopySerialized(source,snapshot);
                else {snapshot = UnityEngine.Object.Instantiate(source);snapshot.hideFlags = HideFlags.None;AssetDatabase.CreateAsset(snapshot,path);}
            }
            if(!snapshot) throw new InvalidOperationException("Approved snapshot asset is unavailable: "+path);
            snapshots.Add(source,snapshot);
            source_records.Add(path+" <= "+(string.IsNullOrEmpty(source_path) ? "frozen generated six-layer/water/instance asset: "+source.name : source_path));
            if(snapshot is Material material)
            {
                var original = (Material)(UnityEngine.Object)source;
                foreach(string property in original.GetTexturePropertyNames())
                {
                    var texture = original.GetTexture(property);
                    if(texture) material.SetTexture(property,freeze(texture));
                }
                material.enableInstancing = original.enableInstancing;
            }
            if(snapshot is TerrainData data)
            {
                // CreateAsset may clear references to unsaved objects on the clone. Resolve from
                // the still-live source, not the just-persisted snapshot, before saving dependencies.
                var layers = ((TerrainData)(UnityEngine.Object)source).terrainLayers;
                for(int index = 0; index < layers.Length; index++) layers[index] = freeze(layers[index]);
                data.terrainLayers = layers;
            }
            if(snapshot is TerrainLayer layer)
            {
                var original = (TerrainLayer)(UnityEngine.Object)source;
                layer.diffuseTexture = freeze(original.diffuseTexture);layer.normalMapTexture = freeze(original.normalMapTexture);
                layer.maskMapTexture = freeze(original.maskMapTexture);
            }
            EditorUtility.SetDirty(snapshot);
            return snapshot;
        }
    }
}
