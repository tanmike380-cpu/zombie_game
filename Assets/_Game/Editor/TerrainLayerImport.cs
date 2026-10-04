using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieGame.World;

namespace ZombieGame.EditorTools
{
    /// <summary>Reproducible Built-in TerrainLayer assets from pinned CC0 maps.</summary>
    public static class TerrainLayerImport
    {
        private const string ROOT="Assets/_Game/Resources/";
        [MenuItem("Zombie Game/Art/Prepare six terrain layers")]
        public static void prepare_layers()
        {
            AssetDatabase.Refresh();
            var config=TerrainPresentation.load_config();
            Directory.CreateDirectory(ROOT+TerrainPresentation.GENERATED_ROOT);
            foreach(var layer in config.layers)prepare_layer(layer);
            string path=ROOT+TerrainPresentation.GENERATED_ROOT+"TerrainStandard.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find("Nature/Terrain/Standard");
            if(!shader)throw new InvalidOperationException("Built-in Terrain Standard shader unavailable; do not silently switch render pipeline");
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.enableInstancing=true;material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
            Debug.Log("[TerrainArt] Prepared six CC0 TerrainLayers, normal maps and roughness-to-smoothness alpha; Built-in Standard two-pass terrain");
        }
        /// <summary>Unity requires a serialized Terrain in a build scene to include native runtime terrain resources.</summary>
        public static void ensure_scene_placeholder()
        {
            const string name="Terrain build resource keeper — inactive, never gameplay";
            foreach(var existing in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(existing.name==name)return;
            string path=ROOT+TerrainPresentation.GENERATED_ROOT+"BuildResourceKeeper.asset";
            var terrain_data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if(!terrain_data)
            {
                terrain_data=new TerrainData{heightmapResolution=33,alphamapResolution=16,size=Vector3.one};
                AssetDatabase.CreateAsset(terrain_data,path);
            }
            var config=TerrainPresentation.load_config();var layers=new TerrainLayer[config.layers.Length];
            for(int i=0;i<layers.Length;i++)layers[i]=Resources.Load<TerrainLayer>(TerrainPresentation.GENERATED_ROOT+config.layers[i].id);
            terrain_data.terrainLayers=layers;EditorUtility.SetDirty(terrain_data);
            var keeper=new GameObject(name);keeper.SetActive(false);
            var terrain=keeper.AddComponent<Terrain>();terrain.terrainData=terrain_data;terrain.drawInstanced=true;
            terrain.materialTemplate=Resources.Load<Material>(TerrainPresentation.GENERATED_ROOT+"TerrainStandard");
            AssetDatabase.SaveAssets();
        }
        private static void prepare_layer(TerrainPresentation.Layer profile)
        {
            string source=ROOT+"FreeEnvironment/"+profile.source+"/"+profile.source;
            string diffuse_path=source+"_diff_1k.jpg",rough_path=source+"_rough_1k.jpg",normal_path=source+"_nor_gl_1k.jpg";
            foreach(string path in new[]{diffuse_path,rough_path,normal_path})
                if(!File.Exists(path))throw new FileNotFoundException("Run tools/download_terrain_materials.sh before preparing terrain",path);
            string output=ROOT+TerrainPresentation.GENERATED_ROOT+profile.id;
            pack_smoothness(diffuse_path,rough_path,output+"_albedo_smoothness.png",profile.smoothness_scale);
            var importer=(TextureImporter)AssetImporter.GetAtPath(output+"_albedo_smoothness.png");
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=false;importer.maxTextureSize=1024;importer.mipmapEnabled=true;
            importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Repeat;importer.isReadable=false;
            importer.SaveAndReimport();
            var normal_importer=(TextureImporter)AssetImporter.GetAtPath(normal_path);
            normal_importer.textureType=TextureImporterType.NormalMap;normal_importer.sRGBTexture=false;
            normal_importer.anisoLevel=8;normal_importer.SaveAndReimport();
            var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(output+".terrainlayer");
            if(!layer){layer=new TerrainLayer();AssetDatabase.CreateAsset(layer,output+".terrainlayer");}
            layer.name=profile.id;layer.diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(output+"_albedo_smoothness.png");
            layer.normalMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(normal_path);
            layer.tileSize=Vector2.one*profile.tile_size;layer.tileOffset=Vector2.zero;
            layer.normalScale=profile.normal_strength;layer.metallic=0;layer.smoothness=1;
            EditorUtility.SetDirty(layer);
        }
        private static void pack_smoothness(string diffuse_path,string rough_path,string output_path,float smoothness_scale)
        {
            var diffuse=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
            var rough=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if(!diffuse.LoadImage(File.ReadAllBytes(diffuse_path))||!rough.LoadImage(File.ReadAllBytes(rough_path)))
                    throw new InvalidOperationException("Cannot decode terrain sources: "+diffuse_path);
                if(diffuse.width!=rough.width||diffuse.height!=rough.height)throw new InvalidOperationException("Terrain channel sizes differ: "+diffuse_path);
                var pixels=diffuse.GetPixels32();var roughness=rough.GetPixels32();
                for(int i=0;i<pixels.Length;i++)pixels[i].a=(byte)Mathf.RoundToInt((255-roughness[i].r)*smoothness_scale);
                diffuse.SetPixels32(pixels);diffuse.Apply();File.WriteAllBytes(output_path,diffuse.EncodeToPNG());
                AssetDatabase.ImportAsset(output_path,ImportAssetOptions.ForceSynchronousImport);
            }
            finally{UnityEngine.Object.DestroyImmediate(diffuse);UnityEngine.Object.DestroyImmediate(rough);}
        }
    }
}
