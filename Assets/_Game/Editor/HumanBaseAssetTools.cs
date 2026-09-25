using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieGame.EditorTools
{
    /// <summary>Inspect the approved art master without replacing crowd animation assets.</summary>
    public static class HumanBaseAssetTools
    {
        private const string MASTER_PATH = "art/characters/human_base/human_base.blend";
        private const string PREVIEW_PATH = "Assets/_Game/ArtGenerated/HumanBase/human_base.glb";

        [MenuItem("Tools/Zombie Game/Characters/Reveal Approved Human Master")]
        public static void reveal_master()
        {
            string path = Path.GetFullPath(MASTER_PATH);
            if (!File.Exists(path))
                throw new FileNotFoundException("The approved human art master is missing.", path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Tools/Zombie Game/Characters/Inspect Approved Human Model")]
        public static void inspect_preview()
        {
            GameObject model = load_preview();
            Selection.activeObject = model;
            EditorGUIUtility.PingObject(model);
            Debug.Log("Approved human art preview selected. This is a static inspection asset; the existing animated crowd is unchanged.");
        }

        public static void verify_preview()
        {
            GameObject model = load_preview();
            MeshFilter[] meshes = model.GetComponentsInChildren<MeshFilter>(true);
            if (meshes.Length == 0)
                throw new InvalidOperationException("Approved human preview imported without mesh geometry.");
            foreach (MeshFilter filter in meshes)
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0 || renderer == null)
                    throw new InvalidOperationException("Invalid human preview mesh: " + filter.name);
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                        throw new InvalidOperationException("Invalid human preview material: " + filter.name);
            }
            Debug.Log("HUMAN_BASE_UNITY_IMPORT_VERIFIED: " + meshes.Length + " mesh parts; static inspection only.");
        }

        private static GameObject load_preview()
        {
            if (!File.Exists(PREVIEW_PATH))
                throw new FileNotFoundException("Generate the inspection model with tools/art/export_human_preview.py in Blender first.", PREVIEW_PATH);
            AssetDatabase.ImportAsset(PREVIEW_PATH, ImportAssetOptions.ForceSynchronousImport);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(PREVIEW_PATH);
            if (model == null)
                throw new InvalidOperationException("Unity could not import the approved human preview.");
            return model;
        }
    }
}
