using System;
using UnityEngine;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    /// <summary>Terrain material/ground-height regression; no camera or gameplay overrides.</summary>
    public static class TerrainArtChecks
    {
        public static void run()
        {
            var terrains=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            Terrain terrain=null;
            foreach(var item in terrains)if(item.name.StartsWith("Ground —")){terrain=item;break;}
            require(terrain!=null,"six-layer ground is actually active");
            var data=terrain.terrainData;var config=TerrainPresentation.load_config();
            require(data.terrainLayers.Length==6,"exactly six authored TerrainLayers");
            require(terrain.GetComponent<TerrainCollider>()==null,"presentation terrain does not add navigation/collision");
            require(Mathf.Abs(terrain.transform.position.y-LayeredTerrain.GROUND_Y)<.0001f,"existing ground elevation preserved");
            require(data.size.x==256&&data.size.z==256,"ground map size preserved");
            require(terrain.materialTemplate.shader.name=="Nature/Terrain/Standard"&&terrain.materialTemplate.shader.isSupported,"supported Built-in PBR terrain, no pipeline migration");
            for(int i=0;i<6;i++)
            {
                var layer=data.terrainLayers[i];
                require(layer.name==config.layers[i].id,"stable semantic layer order: "+i);
                require(layer.diffuseTexture&&layer.normalMapTexture,"scanned albedo/smoothness and normal: "+layer.name);
                require(layer.metallic==0,"nonmetal ground: "+layer.name);
                require(layer.diffuseTexture.mipmapCount>1&&layer.normalMapTexture.mipmapCount>1,"distance filtering available: "+layer.name);
            }
            var weights=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);
            var totals=new float[6];
            for(int z=0;z<data.alphamapHeight;z+=7)for(int x=0;x<data.alphamapWidth;x+=7)
            {
                float sum=0;for(int layer=0;layer<6;layer++){float weight=weights[z,x,layer];require(weight>=0&&!float.IsNaN(weight),"finite nonnegative material weights");sum+=weight;totals[layer]+=weight;}
                require(Mathf.Abs(sum-1)<.015f,"normalized blend weights");
                require(data.GetHeight(x*(data.heightmapResolution-1)/data.alphamapWidth,z*(data.heightmapResolution-1)/data.alphamapHeight)==0,"no unsupported visual displacement");
            }
            for(int layer=0;layer<6;layer++)require(totals[layer]>.01f,"layer contributes to current map: "+config.layers[layer].id);
            Debug.Log("[TerrainArtChecks] PASS six scanned PBR layers, normalized macro/shore/road blend, flat ground, no collider, Built-in pipeline; visual fog/performance still require framebuffer/playtest");
        }
        /// <summary>Same-frame render A/B catches missing native Terrain resources despite valid layers/shaders.</summary>
        public static void check_rendered_surface()
        {
            Terrain terrain=null;
            foreach(var item in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                if(item.name.StartsWith("Ground —")){terrain=item;break;}
            require(terrain!=null&&Camera.main!=null,"rendered terrain and camera available");
            var camera=Camera.main;var previous_target=camera.targetTexture;
            var previous_active=RenderTexture.active;bool previous_visibility=terrain.drawHeightmap;
            int width=Screen.width,height=Screen.height;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            var capture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;terrain.drawHeightmap=true;camera.Render();RenderTexture.active=target;
                capture.ReadPixels(new Rect(0,0,width,height),0,0);capture.Apply();var visible=capture.GetPixels32();
                terrain.drawHeightmap=false;camera.Render();RenderTexture.active=target;
                capture.ReadPixels(new Rect(0,0,width,height),0,0);capture.Apply();var hidden=capture.GetPixels32();
                int changed=0;
                for(int i=0;i<visible.Length;i++)
                    if(Mathf.Abs(visible[i].r-hidden[i].r)+Mathf.Abs(visible[i].g-hidden[i].g)+Mathf.Abs(visible[i].b-hidden[i].b)>24)changed++;
                require(changed>width*height*.01f,"terrain must contribute real visible pixels, not expose the blue water backing: "+changed);
                Debug.Log("[TerrainFramebuffer] PASS same-frame terrain on/off changed pixels="+changed+" resolution="+width+"x"+height);
            }
            finally
            {
                terrain.drawHeightmap=previous_visibility;camera.targetTexture=previous_target;RenderTexture.active=previous_active;
                RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(capture);
            }
        }
        private static void require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("[TerrainArtChecks] FAIL "+message);}
    }
}
