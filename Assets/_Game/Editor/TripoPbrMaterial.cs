using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieGame.EditorTools
{
    /// <summary>Maps the user's Tripo PBR maps into Unity Standard's metallic(R)/smoothness(A) convention.</summary>
    public static class TripoPbrMaterial
    {
        public static Material create(Texture2D color, string folder)
        {
            var paths = Directory.GetFiles(folder);
            string metallic_path = paths.First(path => path.Contains("metallic") && !path.EndsWith(".meta"));
            string roughness_path = paths.First(path => path.Contains("roughness") && !path.EndsWith(".meta"));
            string normal_path = paths.First(path => path.Contains("normal_") && !path.EndsWith(".meta"));
            var packed = pack_metallic_smoothness(metallic_path, roughness_path);
            string output = "Assets/_Game/Art/TripoCrossbow/UnityMetallicSmoothness.png";
            File.WriteAllBytes(output, packed.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(output);
            var mask_importer = (TextureImporter)AssetImporter.GetAtPath(output);
            mask_importer.sRGBTexture = false;
            mask_importer.SaveAndReimport();
            var normal_importer = (TextureImporter)AssetImporter.GetAtPath(normal_path);
            normal_importer.textureType = TextureImporterType.NormalMap;
            normal_importer.SaveAndReimport();
            var material = new Material(Shader.Find("Standard")) { name = "Tripo crossbow original PBR", mainTexture = color, enableInstancing = true };
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(output));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal_path));
            material.SetFloat("_GlossMapScale", 1);
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.EnableKeyword("_NORMALMAP");
            return material;
        }

        private static Texture2D pack_metallic_smoothness(string metal_path, string roughness_path)
        {
            var metal = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!metal.LoadImage(File.ReadAllBytes(metal_path)) || !roughness.LoadImage(File.ReadAllBytes(roughness_path)))
                    throw new InvalidOperationException("Tripo PBR map could not be decoded");
                if (metal.width != roughness.width || metal.height != roughness.height)
                    throw new InvalidOperationException("Tripo PBR map sizes differ");
                var colors = metal.GetPixels32();
                var rough = roughness.GetPixels32();
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(colors[i].r, 0, 0, (byte)(255 - rough[i].r));
                var packed = new Texture2D(metal.width, metal.height, TextureFormat.RGBA32, false, true);
                packed.SetPixels32(colors); packed.Apply();
                return packed;
            }
            finally { UnityEngine.Object.DestroyImmediate(metal); UnityEngine.Object.DestroyImmediate(roughness); }
        }
    }
}
