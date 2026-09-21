using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.EditorTools
{
    /// <summary>Original provisional weapon silhouettes for role recognition, not replacement premium character art.</summary>
    public static class RangedWeaponMeshBuilder
    {
        public static Mesh build(string role,bool grip)
        {
            var parts=new List<CombineInstance>();var meshes=new List<Mesh>();
            Color wood=new Color(.36f,.19f,.08f),iron=new Color(.18f,.20f,.21f),cord=new Color(.76f,.66f,.44f);
            bool machine=role=="Ballista"||role=="Cannon";
            if(machine)
            {
                part(parts,meshes,PrimitiveType.Cube,new Vector3(0,.55f,0),new Vector3(1.3f,.25f,1.8f),Quaternion.identity,wood);
                for(int side=-1;side<=1;side+=2)
                    part(parts,meshes,PrimitiveType.Cylinder,new Vector3(side*.85f,.45f,0),new Vector3(.9f,.13f,.9f),Quaternion.Euler(0,0,90),wood);
            }
            if(role=="Cannon")
            {
                part(parts,meshes,PrimitiveType.Cylinder,new Vector3(0,.95f,.25f),new Vector3(.52f,1.05f,.52f),Quaternion.Euler(90,0,0),iron);
                part(parts,meshes,PrimitiveType.Cylinder,new Vector3(0,.95f,1.31f),new Vector3(.38f,.013f,.38f),Quaternion.Euler(90,0,0),Color.black);
            }
            else if(role=="Archer")
            {
                Vector3 previous=new Vector3(0,-.75f,.15f);
                for(int i=1;i<=12;i++)
                {
                    float t=i/12f;Vector3 next=new Vector3(0,Mathf.Lerp(-.75f,.75f,t),.15f+Mathf.Sin(t*Mathf.PI)*.3f);
                    rod(parts,meshes,previous,next,.038f,wood);previous=next;
                }
                rod(parts,meshes,new Vector3(0,-.75f,.15f),new Vector3(0,.75f,.15f),.009f,cord);
                rod(parts,meshes,new Vector3(0,0,-.2f),new Vector3(0,0,.8f),.018f,wood);
            }
            else
            {
                float width=machine?1.25f:role=="Crossbow"?.62f:.45f,y=machine?.9f:.06f,length=machine?1.9f:1;
                part(parts,meshes,PrimitiveType.Cube,new Vector3(0,y,.1f),new Vector3(.10f,.12f,length),Quaternion.identity,wood);
                rod(parts,meshes,new Vector3(-width,y,.32f),new Vector3(0,y,.52f),.045f,wood);
                rod(parts,meshes,new Vector3(width,y,.32f),new Vector3(0,y,.52f),.045f,wood);
                rod(parts,meshes,new Vector3(-width,y,.32f),new Vector3(0,y,-.12f),.01f,cord);
                rod(parts,meshes,new Vector3(width,y,.32f),new Vector3(0,y,-.12f),.01f,cord);
                if(role=="Repeater")part(parts,meshes,PrimitiveType.Cube,new Vector3(0,y+.15f,.08f),new Vector3(.22f,.20f,.38f),Quaternion.identity,wood);
            }
            var mesh=new Mesh{name="Provisional "+role};mesh.CombineMeshes(parts.ToArray(),true,true);
            if(grip)
            {
                var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=MusketMeshBuilder.to_grip(vertices[i]);mesh.vertices=vertices;
            }
            mesh.RecalculateNormals();mesh.RecalculateBounds();foreach(var source in meshes)Object.DestroyImmediate(source);return mesh;
        }
        private static void rod(List<CombineInstance> parts,List<Mesh> meshes,Vector3 from,Vector3 to,float width,Color color)
        {part(parts,meshes,PrimitiveType.Cube,(from+to)*.5f,new Vector3(width,width,Vector3.Distance(from,to)),Quaternion.LookRotation(to-from),color);}
        private static void part(List<CombineInstance> parts,List<Mesh> meshes,PrimitiveType kind,Vector3 point,Vector3 size,Quaternion rotation,Color color)
        {
            var primitive=GameObject.CreatePrimitive(kind);var mesh=Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(primitive);
            var colors=new Color[mesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=color;mesh.colors=colors;meshes.Add(mesh);
            parts.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(point,rotation,size)});
        }
    }
}
