using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieGame.Presentation;

namespace ZombieGame.EditorTools
{
    /// <summary>Original, bone-attached Eastern equipment. Baked with the licensed animation,
    /// not rigid decorations left behind when the unit runs, attacks or falls.</summary>
    public sealed class CharacterStyleGeometry
    {
        private readonly GameObject model;
        private readonly VisualStyle style;
        private readonly Dictionary<string,Transform> bones;
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<int> triangles=new List<int>();
        private readonly List<Color> colors=new List<Color>();
        public CharacterStyleGeometry(GameObject model,VisualStyle style)
        {this.model=model;this.style=style;bones=model.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());}

        public Mesh build(bool human,bool explosive)
        {
            build_anatomy(human,explosive);
            if(human)build_uniform();else build_infected(explosive);
            var mesh=new Mesh{name=style.id+" original animated equipment"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.SetUVs(0,new List<Vector2>(new Vector2[vertices.Count]));mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void build_anatomy(bool human,bool explosive)
        {
            Color skin=human?style.color(style.skin):new Color(.42f,.44f,.34f);
            Color cloth=style.color(style.cloth)*(human?1:.55f);
            float bulk=explosive?1.65f:1;
            segment("Hips","Neck",.39f*bulk,.24f*bulk,human?cloth:skin*.75f);
            part("Hips",Vector3.zero,new Vector3(.31f*bulk,.23f,.25f*bulk),cloth);
            segment("Neck","Head",.09f,.095f,skin);
            foreach(string side in new[]{".L",".R"})
            {
                segment("Neck","UpperArm"+side,.14f*bulk,.17f*bulk,human?cloth:skin);
                segment("UpperArm"+side,"LowerArm"+side,.13f*bulk,.14f*bulk,human?cloth:skin);
                segment("LowerArm"+side,"Middle1"+side,.09f*bulk,.10f*bulk,skin);
                segment("Hips","UpperLeg"+side,.18f*bulk,.19f*bulk,cloth);
                segment("UpperLeg"+side,"LowerLeg"+side,.16f*bulk,.17f*bulk,cloth);
                segment("LowerLeg"+side,"Foot"+side,.115f*bulk,.13f*bulk,cloth*.8f);
                part("LowerArm"+side,Vector3.zero,Vector3.one*.105f*bulk,human?cloth:skin);
                part("LowerLeg"+side,Vector3.zero,new Vector3(.13f,.13f,.15f)*bulk,cloth);
                part("Foot"+side,new Vector3(0,.08f,-.02f),new Vector3(.13f*bulk,.26f,.16f),new Color(.17f,.15f,.12f));
            }
            if(explosive)part("Abdomen",new Vector3(0,.05f,.04f),new Vector3(.63f,.57f,.51f),skin*.78f);
            float scale=style.head_scale/.7f;
            part("Head",new Vector3(0,.09f,0),new Vector3(.165f,.23f,.18f)*scale,skin);
            part("Head",new Vector3(0,.089f,.08f),new Vector3(.027f,.05f,.035f),skin*.94f);
            part("Head",new Vector3(0,.05f,.083f),new Vector3(.047f,.008f,.01f),new Color(.23f,.15f,.12f));
            foreach(int side in new[]{-1,1})
            {
                part("Head",new Vector3(side*.036f,.122f,.071f),new Vector3(.023f,.01f,.009f),new Color(.10f,.09f,.075f));
                part("Head",new Vector3(side*.08f,.095f,0),new Vector3(.022f,.045f,.036f),skin*.85f);
            }
            foreach(string side in new[]{".L",".R"})
            {
                part("Middle1"+side,new Vector3(0,.015f,0),new Vector3(.075f,.095f,.035f),skin);
                for(int digit=0;digit<4;digit++)
                    part("Middle1"+side,new Vector3((digit-1.5f)*.017f,.071f,0),new Vector3(.015f,.065f,.020f),skin*.9f);
            }
            if(!human)
            {
                part("Head",new Vector3(.038f,.10f,.07f),new Vector3(.032f,.035f,.010f),new Color(.28f,.12f,.10f));
                if(explosive)part("Neck",new Vector3(.09f,.055f,0),new Vector3(.14f,.15f,.15f),new Color(.46f,.28f,.20f));
            }
        }
        private void build_uniform()
        {
            Color cloth=style.color(style.cloth),metal=style.color(style.metal),gold=style.color(style.accent);
            float width=style.shoulder_scale;
            plate("Torso",new Vector3(0,.025f,.085f),new Vector3(.35f*width,.30f,.085f),metal);
            plate("Torso",new Vector3(0,.025f,-.085f),new Vector3(.33f*width,.30f,.075f),metal*.85f);
            plate("Hips",new Vector3(0,-.045f,.025f),new Vector3(.34f,.19f,.24f),cloth);
            // Articulated lamellar skirt, pauldrons and shin guards.
            foreach(string side in new[]{".L",".R"})
            {
                plate("UpperArm"+side,new Vector3(0,.065f,0),new Vector3(.17f*width,.15f,.18f),metal);
                plate("LowerLeg"+side,new Vector3(0,.14f,.045f),new Vector3(.12f,.25f,.06f),metal*.72f);
            }
            if(style.id=="fortress")
            {
                part("Head",new Vector3(0,.19f,-.01f),new Vector3(.22f,.14f,.23f),metal);
                part("Head",new Vector3(0,.15f,.025f),new Vector3(.24f,.025f,.23f),gold);
                plate("Torso",new Vector3(0,.03f,-.13f),new Vector3(.28f,.30f,.14f),cloth);
                for(int row=0;row<3;row++)part("Torso",new Vector3(0,-.075f+row*.07f,.115f),new Vector3(.32f,.035f,.045f),gold);
            }
            else if(style.id=="dusk")
            {
                part("Head",new Vector3(0,.21f,0),new Vector3(.23f,.13f,.24f),cloth);
                part("Head",new Vector3(0,.155f,0),new Vector3(.36f,.025f,.36f),cloth*.75f);
                part("Hips",new Vector3(.16f,0,-.04f),new Vector3(.16f,.20f,.15f),gold*.65f);
                for(int i=0;i<5;i++)part("Torso",new Vector3(-.12f+i*.055f,.10f-i*.048f,.12f),new Vector3(.065f,.065f,.025f),gold*.7f);
            }
            else
            {
                part("Head",new Vector3(0,.19f,-.005f),new Vector3(.20f,.13f,.21f),metal);
                part("Head",new Vector3(0,.285f,-.015f),new Vector3(.028f,.10f,.028f),gold);
                part("Head",new Vector3(0,.255f,-.06f),new Vector3(.06f,.05f,.12f),cloth);
                plate("Torso",new Vector3(0,.005f,.10f),new Vector3(.33f,.29f,.07f),cloth);
                for(int row=0;row<4;row++)for(int column=0;column<4;column++)
                    part("Torso",new Vector3((column-1.5f)*.073f,(row-1.5f)*.064f,.145f),Vector3.one*.017f,gold);
            }
        }
        private void build_infected(bool explosive)
        {
            Color dead=style.id=="fortress"?new Color(.38f,.44f,.35f):new Color(.46f,.43f,.30f);
            foreach(string side in new[]{".L",".R"})
                part("UpperArm"+side,new Vector3(0,.04f,0),new Vector3(.11f,.21f,.12f),dead);
            if(explosive)
            {
                int sacs=style.id=="dusk"?9:style.id=="fortress"?6:7;
                for(int i=0;i<sacs;i++)
                {
                    float angle=i*Mathf.PI*2/sacs;
                    part("Torso",new Vector3(Mathf.Cos(angle)*.20f,.06f+(i%3)*.07f,Mathf.Sin(angle)*.17f),
                        new Vector3(.14f,.17f,.13f),i%2==0?new Color(.42f,.24f,.19f):new Color(.57f,.43f,.20f));
                }
                part("Abdomen",new Vector3(0,.04f,.13f),new Vector3(.38f,.34f,.25f),new Color(.43f,.29f,.23f));
            }
            else
            {
                for(int rib=0;rib<4;rib++)part("Torso",new Vector3(0,rib*.045f-.03f,.105f),new Vector3(.24f,.018f,.032f),dead*1.2f);
                part("Hips",new Vector3(.08f,.015f,.045f),new Vector3(.27f,.24f,.19f),style.color(style.cloth)*.6f);
            }
        }
        private void part(string bone_name,Vector3 offset,Vector3 size,Color color)
        {
            if(!bones.TryGetValue(bone_name,out var bone))throw new System.InvalidOperationException("Art bone missing: "+bone_name);
            Matrix4x4 matrix=model.transform.worldToLocalMatrix*bone.localToWorldMatrix*Matrix4x4.TRS(offset,Quaternion.identity,size);
            append_ellipsoid(matrix,color);
        }
        private void segment(string from,string to,float width,float depth,Color color)
        {
            Vector3 a=model.transform.InverseTransformPoint(bones[from].position),b=model.transform.InverseTransformPoint(bones[to].position);
            Quaternion rotation=Quaternion.FromToRotation(Vector3.up,b-a);
            append_taper(Matrix4x4.TRS((a+b)*.5f,rotation,new Vector3(width,Vector3.Distance(a,b)+width*.20f,depth)),color,false);
        }
        private void plate(string bone_name,Vector3 offset,Vector3 size,Color color)
        {
            Matrix4x4 matrix=model.transform.worldToLocalMatrix*bones[bone_name].localToWorldMatrix*Matrix4x4.TRS(offset,Quaternion.identity,size);
            append_taper(matrix,color,true);
        }
        private void append_taper(Matrix4x4 matrix,Color color,bool armor)
        {
            const int sides=8;int start=vertices.Count;
            float[] heights={-.5f,-.44f,.0f,.44f,.5f};
            float[] widths=armor?new[]{.72f,1f,1f,1f,.72f}:new[]{.64f,.78f,1f,.86f,.68f};
            for(int ring=0;ring<heights.Length;ring++)for(int side=0;side<=sides;side++)
            {
                float theta=side*Mathf.PI*2/sides+Mathf.PI/8;
                vertices.Add(matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(theta)*widths[ring]*.5f,heights[ring],Mathf.Sin(theta)*widths[ring]*.5f)));
                colors.Add(new Color(color.r,color.g,color.b,1));
                if(ring==heights.Length-1||side==sides)continue;
                int a=start+ring*(sides+1)+side,b=a+sides+1;
                triangles.Add(a);triangles.Add(b);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
            }
            for(int side=1;side<sides-1;side++)
            {
                triangles.Add(start);triangles.Add(start+side);triangles.Add(start+side+1);
                int top=start+(heights.Length-1)*(sides+1);
                triangles.Add(top);triangles.Add(top+side+1);triangles.Add(top+side);
            }
        }
        private void append_ellipsoid(Matrix4x4 matrix,Color color)
        {
            const int sides=12,rings=8;int start=vertices.Count;
            for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++)
            {
                float phi=ring*Mathf.PI/rings,theta=side*Mathf.PI*2/sides;
                vertices.Add(matrix.MultiplyPoint3x4(new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta))*.5f));
                colors.Add(new Color(color.r,color.g,color.b,1));
                if(ring==rings||side==sides)continue;
                int a=start+ring*(sides+1)+side,b=a+sides+1;
                triangles.Add(a);triangles.Add(a+1);triangles.Add(b);triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b);
            }
        }
    }
}
