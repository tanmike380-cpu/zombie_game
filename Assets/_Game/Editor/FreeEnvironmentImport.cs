using UnityEditor;
using UnityEngine;

namespace ZombieGame.EditorTools
{
    public sealed class FreeEnvironmentImport:AssetPostprocessor
    {
        private bool is_environment=>assetPath.StartsWith("Assets/_Game/Resources/FreeEnvironment/");
        private void OnPreprocessTexture()
        {
            if(!is_environment)return;
            var importer=(TextureImporter)assetImporter;
            importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.anisoLevel=4;
            importer.wrapMode=TextureWrapMode.Repeat;importer.isReadable=false;
            if(assetPath.Contains("_nor_gl_"))importer.textureType=TextureImporterType.NormalMap;
            else importer.sRGBTexture=assetPath.Contains("_diff_");
        }
        private void OnPreprocessModel()
        {
            if(!is_environment)return;
            var importer=(ModelImporter)assetImporter;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.addCollider=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
        }
    }
}
