using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.World
{
    /// <summary>Batched frontier terrain and Eastern architecture; presentation shares the navigation footprints.</summary>
    public sealed class FrontierLandscape : System.IDisposable
    {
        private readonly GameObject root;
        private readonly Mesh cube,cone,roof,rock;
        private readonly List<Mesh> owned_meshes=new List<Mesh>();
        private readonly List<Material> owned_materials=new List<Material>();
        private readonly Dictionary<Color,List<CombineInstance>> batches=new Dictionary<Color,List<CombineInstance>>();
        private readonly System.Random random=new System.Random(20260920);
        private readonly Dictionary<string,GameObject> facilities=new Dictionary<string,GameObject>();
        private static readonly Color GRASS=new Color(.38f,.34f,.20f), WATER=new Color(.14f,.23f,.21f);
        private static readonly Color TIMBER=new Color(.23f,.135f,.075f), TILE=new Color(.22f,.235f,.20f), STONE=new Color(.46f,.42f,.32f);

        public FrontierLandscape(FrontierMap map, Transform parent, Shader shader)
        {
            root=new GameObject("Frontier landscape - Eastern settlement");root.transform.SetParent(parent,false);
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(primitive);cone=build_cone();roof=build_roof();owned_meshes.Add(cone);owned_meshes.Add(roof);
            rock=build_rock();owned_meshes.Add(rock);
            for(int z=-124;z<128;z+=8) for(int x=-124;x<128;x+=8)
                add(cube,new Vector3(x,-.18f,z),new Vector3(8,.35f,8),GRASS);
            // Settlement roads are purely visual and do not restrict player commands.
            add(cube,new Vector3(-85,.005f,-87),new Vector3(66,.015f,3),new Color(.48f,.40f,.28f));
            add(cube,new Vector3(-98,.005f,-83),new Vector3(3,.015f,70),new Color(.48f,.40f,.28f));
            foreach(var region in map.regions)
                if(!region.label.StartsWith("PLOT:"))build_region(region);
                else build_foundation(region.bounds);
            // Established farm and exposed resource seams; throughput is documented in the scenario economy config.
            for(int i=0;i<12;i++) add(cube,new Vector3(-115+i*.65f,.04f,-112),new Vector3(.3f,.09f,12),new Color(.57f,.49f,.20f));
            add_deposit(new Vector3(-61,0,-116),new Color(.29f,.28f,.27f));
            add_deposit(new Vector3(-118,0,-44),new Color(.47f,.45f,.40f));
            add_deposit(new Vector3(44,0,-101),new Color(.29f,.28f,.27f));
            add_deposit(new Vector3(79,0,60),new Color(.47f,.45f,.40f));
            // Rubble, dry vegetation and wheel-ruts break up flat ground without changing walkability.
            for(int i=0;i<2400;i++)
            {
                var p=new Vector3(-126+(float)random.NextDouble()*252,.03f,-126+(float)random.NextDouble()*252);
                if(map.blocked(p,.3f))continue;
                add(rock,p,new Vector3(.12f,.06f,.2f),i%2==0?STONE*.7f:GRASS*.8f);
            }
            flush(shader);
            foreach(var region in map.regions)
            {
                if(!region.label.StartsWith("PLOT:"))continue;
                string id=region.label.Substring(5);
                var facility=new GameObject("Facility - "+id);facility.transform.SetParent(root.transform,false);
                build_building(region.bounds,false);build_industry(region.bounds,id);flush(shader,facility.transform);
                facility.SetActive(false);facilities.Add(id,facility);
            }
        }

        public void complete_facility(string id)
        {
            if(!facilities.TryGetValue(id,out var facility))throw new System.InvalidOperationException("Missing facility plot: "+id);
            facility.SetActive(true);
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
                    add(cube,new Vector3(p.x,.01f,p.z),new Vector3(b.size.x,.02f,b.size.z),new Color(.23f,.25f,.12f));
                    for(int i=0;i<4;i++)
                    {
                        Vector3 tree=new Vector3(p.x+(i%2-.5f)*1.7f,.0f,p.z+(i/2-.5f)*1.7f);
                        float height=3.1f+(float)random.NextDouble()*1.4f;
                        add(cube,tree+Vector3.up*height*.4f,new Vector3(.18f,height*.8f,.18f),new Color(.27f,.20f,.12f));
                        for(int crown=0;crown<5;crown++)
                        {
                            float angle=crown*2.4f+i;
                            Vector3 offset=new Vector3(Mathf.Cos(angle)*.6f,height*.65f+crown*.17f,Mathf.Sin(angle)*.6f);
                            add(rock,tree+offset,new Vector3(1.7f,1.5f,1.7f),i%3==0?new Color(.40f,.27f,.10f):new Color(.22f,.27f,.115f));
                        }
                    }
                    break;
                case LandscapeKind.Cliff:
                    add(cube,p,b.size,new Color(.34f,.31f,.255f));
                    add(cube,p+Vector3.up*(b.extents.y+.02f),new Vector3(b.size.x,.08f,b.size.z),GRASS*.85f);
                    for(float x=b.min.x+1;x<b.max.x;x+=3)
                        add(rock,new Vector3(x,b.extents.y,b.min.z+.4f),new Vector3(3.2f,b.size.y*1.1f,2),new Color(.36f,.33f,.28f));
                    break;
                case LandscapeKind.Building:
                    build_building(b,region.label=="COMMAND HALL");
                    if(region.label=="POWDER WORKS")build_industry(b,"powder");
                    if(region.label=="LUMBER CAMP")build_industry(b,"wood");
                    if(region.label=="ARROW WORKS")build_industry(b,"arrows");
                    break;
            }
        }

        private void build_foundation(Bounds b)
        {
            add(cube,new Vector3(b.center.x,.03f,b.center.z),new Vector3(b.size.x,.06f,b.size.z),STONE*.7f);
            for(int i=0;i<4;i++)add(cube,new Vector3(i%2==0?b.min.x:b.max.x,.5f,i/2==0?b.min.z:b.max.z),new Vector3(.14f,1,.14f),TIMBER);
        }
        private void build_building(Bounds b,bool headquarters)
        {
            Vector3 p=b.center;float wall_height=headquarters?3.1f:2.1f;
            add(cube,new Vector3(p.x,.22f,p.z),new Vector3(b.size.x+.6f,.44f,b.size.z+.6f),STONE);
            add(cube,new Vector3(p.x,wall_height*.5f+.4f,p.z),new Vector3(b.size.x,wall_height,b.size.z),new Color(.62f,.54f,.39f));
            for(int side=-1;side<=1;side+=2)
            {
                float z=p.z+side*b.extents.z;
                add(cube,new Vector3(p.x,wall_height+.35f,z),new Vector3(b.size.x,.22f,.23f),TIMBER);
                for(float x=b.min.x+.35f;x<b.max.x;x+=1.6f)
                {
                    add(cube,new Vector3(x,wall_height*.5f+.4f,z),new Vector3(.18f,wall_height,.22f),TIMBER);
                    add(cube,new Vector3(x+.55f,1.65f,z+side*.02f),new Vector3(.65f,.8f,.08f),new Color(.16f,.17f,.13f));
                    for(int rail=0;rail<3;rail++)add(cube,new Vector3(x+.34f+rail*.2f,1.65f,z+side*.08f),new Vector3(.045f,.8f,.05f),TIMBER);
                }
            }
            add(cube,new Vector3(p.x,1.2f,b.min.z-.05f),new Vector3(1.4f,1.9f,.13f),TIMBER*.65f);
            build_tiled_roof(new Vector3(p.x,wall_height+.35f,p.z),b.size.x+1.2f,b.size.z+1.2f,1.5f);
            if(headquarters)
            {
                add(cube,new Vector3(p.x,5,p.z),new Vector3(b.size.x*.6f,1.5f,b.size.z*.57f),new Color(.51f,.40f,.26f));
                build_tiled_roof(new Vector3(p.x,5.6f,p.z),b.size.x*.72f,b.size.z*.75f,1.45f);
                add(cube,new Vector3(p.x,2.7f,b.min.z-.25f),new Vector3(2.2f,.55f,.16f),new Color(.16f,.12f,.08f));
                for(int i=0;i<4;i++)add(cube,new Vector3(p.x,.08f+i*.075f,b.min.z-.9f+i*.2f),new Vector3(2.7f,.15f,.7f),STONE);
                for(int side=-1;side<=1;side+=2)
                {
                    add(cube,new Vector3(p.x+side*3.4f,3,b.min.z-.2f),new Vector3(.1f,5.5f,.1f),TIMBER);
                    add(cube,new Vector3(p.x+side*3.4f,4.5f,b.min.z-.2f),new Vector3(.9f,1.4f,.06f),new Color(.38f,.12f,.075f));
                }
            }
        }
        private void build_tiled_roof(Vector3 p,float width,float depth,float height)
        {
            add(roof,p,new Vector3(width,height,depth),TILE);
            add(cube,p+Vector3.up*(height+.035f),new Vector3(width+.15f,.16f,.20f),TILE*.75f);
            for(int side=-1;side<=1;side+=2)
            {
                add(cube,p+new Vector3(0,.05f,side*depth*.5f),new Vector3(width,.14f,.16f),TIMBER);
                for(int row=0;row<8;row++)
                {
                    float t=(row+.5f)/8;
                    float y=height*(1-t)+.24f*t*t*t;
                    add(cube,p+new Vector3(0,y,side*depth*.5f*t),new Vector3(width,.07f,.12f),row%2==0?TILE*.85f:TILE);
                }
            }
        }
        private void build_industry(Bounds b,string kind)
        {
            Vector3 p=new Vector3(b.center.x,.5f,b.min.z+.65f);
            if(kind=="powder")
            {
                add(cube,new Vector3(b.max.x-.8f,2.4f,b.max.z-.8f),new Vector3(.8f,4.8f,.8f),STONE*.65f);
                for(int i=0;i<3;i++)add(rock,p+new Vector3(i-.8f,0,0),new Vector3(.6f,1,.6f),TIMBER);
            }
            else if(kind=="wood"||kind=="arrows")
                for(int i=0;i<7;i++)add(cube,p+new Vector3((i%3)*.5f-1,i/3*.24f,0),new Vector3(.32f,.28f,2.2f),TIMBER*1.5f);
            else if(kind=="food")
                for(int i=0;i<5;i++)add(rock,p+new Vector3(i*.5f-1,0,0),new Vector3(.45f,.8f,.6f),new Color(.62f,.49f,.22f));
            else
            {
                add(cube,p+Vector3.up*.5f,new Vector3(2,2,.4f),TIMBER);
                for(int i=0;i<5;i++)add(rock,p+new Vector3(i*.45f-1,-.2f,.4f),new Vector3(.7f,.5f,.7f),kind=="iron"?new Color(.24f,.24f,.21f):STONE);
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
        private void flush(Shader shader,Transform parent=null)
        {
            foreach(var batch in batches)
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name="Batched landscape"};
                mesh.CombineMeshes(batch.Value.ToArray(),true,true);owned_meshes.Add(mesh);
                var material=new Material(shader){color=batch.Key};material.SetFloat("_Glossiness",.05f);owned_materials.Add(material);
                var group=new GameObject("Landscape material batch");group.transform.SetParent(parent==null?root.transform:parent,false);
                group.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=group.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
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
        private static Mesh build_rock()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            const int rings=6,sides=10;
            for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++)
            {
                float latitude=ring*Mathf.PI/rings,longitude=side*Mathf.PI*2/sides;
                float radius=.5f*(1+.08f*Mathf.Sin(longitude*3+latitude*5));
                vertices.Add(new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude))*radius);
                if(ring==rings||side==sides)continue;
                int a=ring*(sides+1)+side,b=a+sides+1;
                triangles.Add(a);triangles.Add(a+1);triangles.Add(b);triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b);
            }
            var mesh=new Mesh{name="Shared irregular foliage and stone"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
        public void Dispose()
        { Object.Destroy(root);foreach(var mesh in owned_meshes) Object.Destroy(mesh);foreach(var material in owned_materials) Object.Destroy(material); }
    }
}
