using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.World
{
    /// <summary>Optional pinned CC0 art, never a source of colliders, navigation or gameplay state.</summary>
    public sealed class FreeEnvironmentArt:IDisposable
    {
        private const string ROOT="FreeEnvironment/";
        private readonly Texture2D ground,ground_rough,wall,wall_rough;
        private readonly GameObject rock_set;
        private readonly Material rock_material;
        private int rock_groups;
        public bool ready {get;private set;}

        public FreeEnvironmentArt(Shader shader)
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-legacyEnvironment")>=0)return;
            ground=load_texture("grass_path_2","diff");ground_rough=load_texture("grass_path_2","rough");
            wall=load_texture("plastered_stone_wall","diff");wall_rough=load_texture("plastered_stone_wall","rough");
            rock_set=Resources.Load<GameObject>(ROOT+"rock_moss_set_01/rock_moss_set_01_1k");
            var diffuse=load_texture("rock_moss_set_01","diff");
            var rough=load_texture("rock_moss_set_01","rough");
            var normal=load_texture("rock_moss_set_01","nor_gl");
            ready=ground&&ground_rough&&wall&&wall_rough&&rock_set&&diffuse&&rough&&normal;
            if(!ready){Debug.LogWarning("[FreeEnvironment] Missing downloaded samples; retaining legacy art. Run tools/download_free_environment.sh.");return;}
            rock_material=new Material(shader){name="CC0 moss rock",color=Color.white,enableInstancing=true};
            configure_material(rock_material,diffuse,rough,2,1);rock_material.SetTexture("_BumpMap",normal);
            Debug.Log("[FreeEnvironment] Loaded 3 CC0 packs: grass_path_2, rock_moss_set_01, plastered_stone_wall; 1K textures; max 12 rock groups.");
        }

        private static Texture2D load_texture(string id,string channel)
        {return Resources.Load<Texture2D>(ROOT+id+"/"+id+"_"+channel+"_1k");}

        public void apply_surface(Material material,bool is_ground,bool is_wall)
        {
            if(!ready||(!is_ground&&!is_wall))return;
            material.color=is_ground?new Color(.95f,1,.9f):new Color(.92f,.89f,.81f);
            configure_material(material,is_ground?ground:wall,is_ground?ground_rough:wall_rough,1,is_ground?.16f:.35f);
        }

        private static void configure_material(Material material,Texture2D diffuse,Texture2D rough,float mode,float scale)
        {
            material.SetTexture("_MainTex",diffuse);material.SetTexture("_RoughTex",rough);
            material.SetFloat("_TextureMode",mode);material.SetFloat("_TextureScale",scale);
        }

        /// <summary>Normalize the source group's bounds; limit instances and cull at distant zooms.</summary>
        public bool place_rocks(Transform parent,Vector3 point,float width)
        {
            if(!ready||rock_groups>=12)return false;
            var group=UnityEngine.Object.Instantiate(rock_set,parent);group.name="CC0 moss rocks";
            var renderers=group.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0){UnityEngine.Object.Destroy(group);return false;}
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float scale=width/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z));
            group.transform.localScale*=scale;
            group.transform.position=point-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            foreach(var renderer in renderers)
            {
                var materials=new Material[renderer.sharedMaterials.Length];
                for(int i=0;i<materials.Length;i++)materials[i]=rock_material;
                renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            var lod=group.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.025f,renderers)});lod.RecalculateBounds();
            rock_groups++;return true;
        }
        public void Dispose(){if(rock_material!=null)UnityEngine.Object.Destroy(rock_material);}
    }
}
