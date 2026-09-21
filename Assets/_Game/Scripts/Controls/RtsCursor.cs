using System;
using UnityEngine;

namespace ZombieGame.Controls
{
    /// <summary>Original code-drawn bronze pointer and eight double-chevron pan cursors.</summary>
    public sealed class RtsCursor:IDisposable
    {
        private readonly Texture2D[] textures=new Texture2D[10];
        private int current=-2;
        public RtsCursor()
        {
            for(int i=0;i<textures.Length;i++)textures[i]=build_texture(i);
        }
        public static int direction_index(Vector2 direction)
        {
            if(direction.sqrMagnitude<.01f)return 0;
            int sector=Mathf.RoundToInt(Mathf.Atan2(direction.y,direction.x)*4/Mathf.PI);
            return 1+(sector+8)%8;
        }
        public void update(Vector2 edge,bool focused,bool attack)
        {
            int index=focused?(edge.sqrMagnitude>0?direction_index(edge):attack?9:0):-1;
            if(index==current)return;
            current=index;
            Cursor.SetCursor(index<0?null:textures[index],index<0?Vector2.zero:index==0?new Vector2(4,2.5f):new Vector2(16,16),CursorMode.Auto);
        }
        private static Texture2D build_texture(int kind)
        {
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="RTS cursor "+kind,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[4096];var mask=new bool[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                Vector2 point=new Vector2(x,63-y);
                bool inside;
                if(kind==0)inside=in_triangle(point,new Vector2(8,5),new Vector2(10,37),new Vector2(19,27))||
                    in_triangle(point,new Vector2(8,5),new Vector2(19,27),new Vector2(34,25))||
                    in_triangle(point,new Vector2(18,23),new Vector2(29,42),new Vector2(33,39));
                else if(kind==9)
                {Vector2 p=point-new Vector2(32,32);float radius=p.magnitude;inside=(radius>12&&radius<15)||(Mathf.Abs(p.x)<1.5f&&Mathf.Abs(p.y)<23)||(Mathf.Abs(p.y)<1.5f&&Mathf.Abs(p.x)<23);}
                else
                {
                    float angle=(kind-1)*Mathf.PI/4;Vector2 p=new Vector2(point.x-32,32-point.y);
                    float forward=p.x*Mathf.Cos(angle)+p.y*Mathf.Sin(angle),side=-p.x*Mathf.Sin(angle)+p.y*Mathf.Cos(angle);
                    inside=Mathf.Abs(side)<=15&&((Mathf.Abs(forward-(18-Mathf.Abs(side)))<3)||(Mathf.Abs(forward-(3-Mathf.Abs(side)))<3));
                }
                mask[y*64+x]=inside;
            }
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                int index=y*64+x;bool border=false;
                for(int dy=-2;dy<=2&&!border;dy++)for(int dx=-2;dx<=2;dx++)
                    if(x+dx>=0&&x+dx<64&&y+dy>=0&&y+dy<64&&mask[(y+dy)*64+x+dx]){border=true;break;}
                pixels[index]=mask[index]?(kind==9?new Color32(235,97,56,255):new Color32((byte)(190+y/2),(byte)(145+y/2),83,255)):
                    border?new Color32(25,20,14,255):new Color32(0,0,0,0);
            }
            // Supersampled 32px desktop-sized cursor; hotspot scales with the artwork, not the edge hot zone.
            var compact=new Color32[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                Color sum=Color.clear;
                for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)sum+=(Color)pixels[(y*2+dy)*64+x*2+dx];
                compact[y*32+x]=sum*.25f;
            }
            texture.SetPixels32(compact);texture.Apply();return texture;
        }
        private static bool in_triangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {
            float x=cross(b-a,p-a),y=cross(c-b,p-b),z=cross(a-c,p-c);
            return (x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0);
        }
        private static float cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public void Dispose(){Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);foreach(var texture in textures)UnityEngine.Object.Destroy(texture);}
    }
}
