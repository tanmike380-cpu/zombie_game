using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.EditorTools
{
    /// <summary>Original low-poly wood-stock/long-barrel silhouette fitted to the licensed rifle grip.</summary>
    public static class MusketMeshBuilder
    {
        // The pack's attached rifle has its grip rotation baked into vertices, unlike its standalone rifle.
        // Basis verified against all 2709 matching source vertices (RMS < 3e-8).
        public static Vector3 to_grip(Vector3 point) =>
            new Vector3(-.03881479f,-.91533910f,.41972099f)*point.x +
            new Vector3(.50106332f,.34678560f,.80261678f)*point.y +
            new Vector3(-.87346851f,.23960823f,.44176795f)*point.z;

        public static Mesh build()
        {
            var parts = new List<CombineInstance>(); var meshes = new List<Mesh>();
            Color wood = new Color(.30f,.13f,.045f,1), iron = new Color(.22f,.24f,.25f,1), brass = new Color(.48f,.36f,.12f,1);
            add_part(parts,meshes,PrimitiveType.Cube,new Vector3(0,.035f,-.25f),new Vector3(.085f,.11f,.76f),Quaternion.identity,wood);
            add_part(parts,meshes,PrimitiveType.Cube,new Vector3(0,-.06f,-.55f),new Vector3(.11f,.20f,.30f),Quaternion.Euler(12,0,0),wood);
            add_part(parts,meshes,PrimitiveType.Cylinder,new Vector3(0,.12f,.51f),new Vector3(.062f,.76f,.062f),Quaternion.Euler(90,0,0),iron);
            add_part(parts,meshes,PrimitiveType.Cube,new Vector3(.055f,.06f,-.03f),new Vector3(.04f,.08f,.15f),Quaternion.identity,brass);
            // Raised serpentine match holder and a pale cord: deliberately no magazine, scope or modern receiver.
            add_part(parts,meshes,PrimitiveType.Cube,new Vector3(.075f,.16f,-.08f),new Vector3(.018f,.18f,.026f),Quaternion.Euler(0,0,-28),iron);
            add_part(parts,meshes,PrimitiveType.Cylinder,new Vector3(.11f,.23f,-.03f),new Vector3(.014f,.065f,.014f),Quaternion.Euler(65,0,0),new Color(.72f,.60f,.39f,1));
            foreach (float z in new[] { .15f,.7f })
                add_part(parts,meshes,PrimitiveType.Cylinder,new Vector3(0,.12f,z),new Vector3(.075f,.022f,.075f),Quaternion.Euler(90,0,0),brass);
            var result = new Mesh { name = "Original Musket" }; result.CombineMeshes(parts.ToArray(),true,true);
            var vertices = result.vertices; var normals = result.normals;
            for(int i=0;i<vertices.Length;i++) { vertices[i]=to_grip(vertices[i]); normals[i]=to_grip(normals[i]); }
            result.vertices=vertices; result.normals=normals; result.RecalculateBounds();
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            return result;
        }

        private static void add_part(List<CombineInstance> parts,List<Mesh> meshes,PrimitiveType type,Vector3 position,Vector3 scale,Quaternion rotation,Color color)
        {
            var primitive = GameObject.CreatePrimitive(type);
            Mesh mesh = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(primitive);
            var colors = new Color[mesh.vertexCount]; for (int i=0;i<colors.Length;i++) colors[i]=color;
            mesh.colors = colors; meshes.Add(mesh);
            parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position,rotation,scale) });
        }
    }
}
