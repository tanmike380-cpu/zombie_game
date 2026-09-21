using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.Combat;
using ZombieGame.Vision;

namespace ZombieGame.Presentation
{
    /// <summary>Permanent, unlit unit feedback. Pixel-sized bars and rings are independent of art styles.</summary>
    public sealed class UnitFeedbackOverlay:IDisposable
    {
        private readonly Mesh mesh=new Mesh {name="Unit feedback batch",indexFormat=IndexFormat.UInt32};
        private readonly Material material;
        private readonly List<Vector3> vertices=new List<Vector3>(80000);
        private readonly List<Color32> colors=new List<Color32>(80000);
        private readonly List<int> triangles=new List<int>(120000);
        private Camera camera;
        public int health_bars {get;private set;}
        public int ammunition_bars {get;private set;}
        public int selection_rings {get;private set;}
        public static readonly Color32 HEALTH=new Color32(62,232,91,255);
        public static readonly Color32 AMMO=new Color32(48,158,255,255);
        private static readonly Color32 BACKGROUND=new Color32(14,20,24,245);

        public UnitFeedbackOverlay()
        {
            var shader=Resources.Load<Shader>("UnitFeedback");
            if(shader==null)throw new InvalidOperationException("Missing UnitFeedback shader");
            material=new Material(shader);mesh.MarkDynamic();
        }
        public void draw(BattleSimulation battle,CombatFog fog,bool reveal,Camera view)
        {
            camera=view;vertices.Clear();colors.Clear();triangles.Clear();
            health_bars=ammunition_bars=selection_rings=0;
            for(int i=0;i<battle.total_count;i++)
            {
                if(battle.health[i]<=0||battle.is_reserve(i))continue;
                bool human=i<battle.soldier_count;
                if(!human&&!reveal&&(fog==null||!fog.is_visible(battle.positions[i])))continue;
                Vector3 foot=camera.WorldToScreenPoint(battle.positions[i]);
                if(foot.z<=0||foot.x<0||foot.x>camera.pixelWidth||foot.y<0||foot.y>camera.pixelHeight)continue;
                var stats=battle.stats_for(i);
                if(human&&battle.selected[i]){draw_ring(battle.positions[i],foot);selection_rings++;}
                if(!human&&battle.health[i]>=stats.health)continue;
                float scale=human?1:stats.model_scale;
                Vector3 head=camera.WorldToScreenPoint(battle.positions[i]+Vector3.up*2.35f*scale);
                float width=Mathf.Clamp(camera.pixelHeight/camera.orthographicSize*.9f,24,44);
                float y=Mathf.Max(head.y,foot.y+14)+7;
                draw_bar(new Rect(head.x-width/2,y,width,6),battle.health[i]/stats.health,HEALTH);health_bars++;
                if(human)
                {
                    draw_bar(new Rect(head.x-width/2,y-8,width,5),battle.ammunition[i]/(float)stats.ammunition_capacity,AMMO);
                    ammunition_bars++;
                }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            if(vertices.Count>0)Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        private void draw_bar(Rect rect,float fraction,Color32 color)
        {
            add_quad(new Vector2(rect.x-1,rect.y-1),new Vector2(rect.xMax+1,rect.y-1),new Vector2(rect.xMax+1,rect.yMax+1),new Vector2(rect.x-1,rect.yMax+1),BACKGROUND);
            float right=rect.x+rect.width*Mathf.Clamp01(fraction);
            if(right>rect.x)add_quad(new Vector2(rect.x,rect.y),new Vector2(right,rect.y),new Vector2(right,rect.yMax),new Vector2(rect.x,rect.yMax),color);
        }
        private void draw_ring(Vector3 origin,Vector3 centre)
        {
            const int SEGMENTS=32;
            for(int i=0;i<SEGMENTS;i++)
            {
                Vector2 a=ring_point(origin,centre,i*2*Mathf.PI/SEGMENTS);
                Vector2 b=ring_point(origin,centre,(i+1)*2*Mathf.PI/SEGMENTS);
                Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized;
                add_quad(a-normal*2,b-normal*2,b+normal*2,a+normal*2,BACKGROUND);
                add_quad(a-normal,b-normal,b+normal,a+normal,HEALTH);
            }
        }
        private Vector2 ring_point(Vector3 origin,Vector3 centre,float angle)
        {
            Vector3 projected=camera.WorldToScreenPoint(origin+new Vector3(Mathf.Cos(angle)*.52f,.06f,Mathf.Sin(angle)*.52f));
            Vector2 offset=(Vector2)(projected-centre);
            return (Vector2)centre+offset.normalized*Mathf.Max(5,offset.magnitude);
        }
        private void add_quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color32 color)
        {
            int start=vertices.Count;
            add_vertex(a,color);add_vertex(b,color);add_vertex(c,color);add_vertex(d,color);
            triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
            triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
        }
        private void add_vertex(Vector2 point,Color32 color)
        {vertices.Add(camera.ScreenToWorldPoint(new Vector3(point.x,point.y,camera.nearClipPlane+.1f)));colors.Add(color);}
        public void Dispose(){UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);}
    }
}
