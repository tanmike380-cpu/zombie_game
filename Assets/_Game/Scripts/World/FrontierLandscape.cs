using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.World
{
    /// <summary>Original low-cost landscape blockout. All geometry is batched; no per-tree AI or colliders.</summary>
    public sealed class FrontierLandscape : System.IDisposable
    {
        private readonly GameObject root;
        private readonly Mesh cube,cone,roof;
        private readonly List<Mesh> owned_meshes=new List<Mesh>();
        private readonly List<Material> owned_materials=new List<Material>();
        private readonly Dictionary<Color,List<CombineInstance>> batches=new Dictionary<Color,List<CombineInstance>>();
        private readonly System.Random random=new System.Random(20260920);
        private static readonly Color GRASS=new Color(.32f,.39f,.20f), WATER=new Color(.13f,.37f,.44f);

        public FrontierLandscape(FrontierMap map, Transform parent, Shader shader)
        {
            root=new GameObject("Frontier landscape - authored blockout");root.transform.SetParent(parent,false);
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(primitive);cone=build_cone();roof=build_roof();owned_meshes.Add(cone);owned_meshes.Add(roof);
            for(int z=-124;z<128;z+=8) for(int x=-124;x<128;x+=8)
                add(cube,new Vector3(x,-.18f,z),new Vector3(8,.35f,8),GRASS*((x/8+z/8)%3==0 ? .96f:1f));
            // Settlement roads are purely visual and do not restrict player commands.
            add(cube,new Vector3(-85,.005f,-87),new Vector3(66,.015f,3),new Color(.48f,.40f,.28f));
            add(cube,new Vector3(-98,.005f,-83),new Vector3(3,.015f,70),new Color(.48f,.40f,.28f));
            foreach(var region in map.regions) build_region(region);
            // Established farm and exposed resource seams; throughput is documented in the scenario economy config.
            for(int i=0;i<12;i++) add(cube,new Vector3(-115+i*.65f,.04f,-112),new Vector3(.3f,.09f,12),new Color(.57f,.49f,.20f));
            add_deposit(new Vector3(-61,0,-116),new Color(.29f,.28f,.27f));
            add_deposit(new Vector3(-118,0,-44),new Color(.47f,.45f,.40f));
            add_deposit(new Vector3(44,0,-101),new Color(.29f,.28f,.27f));
            add_deposit(new Vector3(79,0,60),new Color(.47f,.45f,.40f));
            foreach(var site in map.resource_sites)
            {
                add(cube,site.position+new Vector3(-2,1.5f,0),new Vector3(.15f,3,.15f),new Color(.3f,.2f,.12f));
                add(cube,site.position+new Vector3(-1.5f,2.7f,0),new Vector3(1,.6f,.08f),new Color(.85f,.62f,.21f));
            }
            flush(shader);
        }

        private void build_region(LandscapeRegion region)
        {
            Bounds b=region.bounds; Vector3 p=b.center;
            switch(region.kind)
            {
                case LandscapeKind.River:
                    add(cube,new Vector3(p.x,.035f,p.z),new Vector3(b.size.x,.04f,b.size.z),WATER);
                    add(cube,new Vector3(b.min.x,.07f,p.z),new Vector3(.45f,.12f,b.size.z),new Color(.60f,.55f,.39f));
                    add(cube,new Vector3(b.max.x,.07f,p.z),new Vector3(.45f,.12f,b.size.z),new Color(.60f,.55f,.39f));
                    break;
                case LandscapeKind.Forest:
                    add(cube,new Vector3(p.x,.01f,p.z),new Vector3(b.size.x,.02f,b.size.z),new Color(.19f,.26f,.12f));
                    for(int i=0;i<4;i++)
                    {
                        Vector3 tree=new Vector3(p.x+(i%2-.5f)*1.7f,.0f,p.z+(i/2-.5f)*1.7f);
                        float height=3.1f+(float)random.NextDouble()*1.4f;
                        add(cube,tree+Vector3.up*height*.4f,new Vector3(.18f,height*.8f,.18f),new Color(.27f,.20f,.12f));
                        add(cone,tree+Vector3.up*1.1f,new Vector3(2.1f,height,2.1f),new Color(.15f,.28f,.13f));
                        add(cone,tree+Vector3.up*2,new Vector3(1.5f,height*.75f,1.5f),new Color(.21f,.34f,.16f));
                    }
                    break;
                case LandscapeKind.Cliff:
                    add(cube,p,b.size,new Color(.37f,.37f,.32f));
                    add(cube,p+Vector3.up*(b.extents.y+.02f),new Vector3(b.size.x,.08f,b.size.z),GRASS*.85f);
                    for(float x=b.min.x+1;x<b.max.x;x+=3)
                        add(cube,new Vector3(x,b.extents.y,b.min.z+.3f),new Vector3(1.7f,b.size.y,1),new Color(.31f,.32f,.29f));
                    break;
                case LandscapeKind.Building:
                    add(cube,new Vector3(p.x,.15f,p.z),new Vector3(b.size.x+.7f,.3f,b.size.z+.7f),new Color(.43f,.42f,.37f));
                    add(cube,new Vector3(p.x,1.3f,p.z),new Vector3(b.size.x,2.3f,b.size.z),new Color(.70f,.64f,.49f));
                    add(roof,new Vector3(p.x,2.5f,p.z),new Vector3(b.size.x+1.8f,1.6f,b.size.z+1.8f),new Color(.20f,.27f,.27f));
                    for(int side=-1;side<=1;side+=2)
                        add(cube,new Vector3(p.x+side*(b.extents.x-.3f),1.4f,b.min.z-.02f),new Vector3(.22f,2.6f,.25f),new Color(.34f,.17f,.11f));
                    add(cube,new Vector3(p.x,1,b.min.z-.03f),new Vector3(1.2f,1.8f,.08f),new Color(.22f,.15f,.09f));
                    break;
            }
        }

        private void add_deposit(Vector3 center,Color color)
        {
            for(int i=0;i<8;i++) add(cone,center+new Vector3((i%4)*1.1f,0,(i/4)*1.4f),new Vector3(2.2f,1.3f+(i%3)*.3f,2),color);
        }
        private void add(Mesh mesh,Vector3 position,Vector3 scale,Color color)
        {
            if(!batches.TryGetValue(color,out var list)) { list=new List<CombineInstance>();batches.Add(color,list); }
            list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,Quaternion.identity,scale)});
        }
        private void flush(Shader shader)
        {
            foreach(var batch in batches)
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name="Batched landscape"};
                mesh.CombineMeshes(batch.Value.ToArray(),true,true);owned_meshes.Add(mesh);
                var material=new Material(shader){color=batch.Key};material.SetFloat("_Glossiness",.05f);owned_materials.Add(material);
                var group=new GameObject("Landscape material batch");group.transform.SetParent(root.transform,false);
                group.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=group.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            batches.Clear();
        }
        private static Mesh build_cone()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;int start=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));
                vertices.Add(Vector3.up);vertices.Add(new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f));
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
            }
            var mesh=new Mesh{name="Original eight-sided foliage and rock"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
        private static Mesh build_roof()
        {
            var mesh=new Mesh{name="Original pitched tile roof"};
            mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,1,0),new Vector3(.5f,1,0)};
            mesh.triangles=new[]{0,4,1,1,4,5,2,3,4,3,5,4,0,2,4,1,5,3};mesh.RecalculateNormals();return mesh;
        }
        public void Dispose()
        { Object.Destroy(root);foreach(var mesh in owned_meshes) Object.Destroy(mesh);foreach(var material in owned_materials) Object.Destroy(material); }
    }
}
