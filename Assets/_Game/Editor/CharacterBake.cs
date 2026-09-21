using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieGame.Presentation;

namespace ZombieGame.EditorTools
{
    /// <summary>Converts licensed skeletal clips into shared mesh frames, avoiding one Animator per crowd unit.</summary>
    public static class CharacterBake
    {
        private const string OUTPUT = "Assets/_Game/Resources/CharacterGenerated";
        private const string SOURCE = "Assets/ThirdParty/Quaternius/";
        private const int FRAME_COUNT = 12;
        private const int VISUAL_REVISION = 4;

        [InitializeOnLoadMethod]
        private static void register_art_refresh()
        {
            if(Application.isBatchMode)return;
            EditorApplication.delayCall+=refresh_editor_art;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=refresh_editor_art;};
        }
        private static void refresh_editor_art()
        {if(!EditorApplication.isPlayingOrWillChangePlaymode)ensure_models();}

        [MenuItem("Tools/Zombie Game/Characters/Bake Models")]
        public static void bake_models()
        {
            Directory.CreateDirectory(OUTPUT); AssetDatabase.Refresh();
            string smoke_path = OUTPUT + "/MusketSmoke.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(smoke_path) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("ZombieGame/MusketSmoke")),smoke_path);
            bake_character("Characters_Shaun", "Human", new[] { "Idle_Gun", "Run_Gun", "Idle_Gun", "Death", "Punch", "Idle", "Run" });
            bake_character("Zombie_Basic", "Zombie", new[] { "Idle", "Run", "Punch", "Death", "Punch", "Idle", "Run" });
            bake_character("Zombie_Chubby", "Exploder", new[] { "Idle", "Run", "Punch", "Death", "Punch", "Idle", "Run" });
            foreach(var style in VisualStyles.all)
            {
                Directory.CreateDirectory(OUTPUT+"/"+style.id);AssetDatabase.Refresh();
                bake_character("Characters_Shaun","Human",new[]{"Idle_Gun","Run_Gun","Idle_Gun","Death","Punch","Idle","Run"},style);
                bake_character("Zombie_Basic","Zombie",new[]{"Idle","Run","Punch","Death","Punch","Idle","Run"},style);
                bake_character("Zombie_Chubby","Exploder",new[]{"Idle","Run","Punch","Death","Punch","Idle","Run"},style);
            }
            foreach(var style in new VisualStyle[]{null}.Concat(VisualStyles.all))
                foreach(string role in new[]{"Archer","Repeater","Crossbow","Ballista","Cannon"})
                    bake_character("Characters_Shaun",role,new[]{"Idle_Gun","Run_Gun","Idle_Gun","Death","Punch","Idle","Run"},style,role);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterBake] PASS human/basic/chubby: 7 gun/knife poses, 12 shared frames per pose; matchlock fire and knife Punch/Idle/Run");
        }

        public static void ensure_models()
        {
            var folders=new[]{""}.Concat(VisualStyles.all.Select(style=>style.id+"/"));
            foreach(string folder in folders)
                foreach(string name in new[]{"Human","Zombie","Exploder","Archer","Repeater","Crossbow","Ballista","Cannon"})
                {
                    var asset=AssetDatabase.LoadAssetAtPath<CharacterFrames>(OUTPUT+"/"+folder+name+".asset");
                    if(asset!=null&&asset.poses!=null&&asset.poses.Length==7&&asset.visual_revision==VISUAL_REVISION)continue;
                    bake_models();return;
                }
        }

        private static void bake_character(string source, string name, string[] clips,VisualStyle style=null,string weapon=null)
        {
            var art = CharacterArtSettings.load_settings();
            string path = SOURCE + source + ".gltf";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Model import missing: " + path);
            var model = UnityEngine.Object.Instantiate(prefab);
            model.name = source;
            try
            {
                var animations = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
                foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r is SkinnedMeshRenderer || r.gameObject.name == "Rifle" || r.gameObject.name=="Knife").ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("No character meshes: " + source);
                var atlas = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().FirstOrDefault();
                if (atlas == null) throw new InvalidOperationException("No character atlas: " + source);
                string output = OUTPUT + "/" +(style==null?"":style.id+"/")+ name + ".asset";
                if (File.Exists(output)) AssetDatabase.DeleteAsset(output); // Regenerable bake, never source art.
                var asset = ScriptableObject.CreateInstance<CharacterFrames>(); asset.poses = new PoseFrames[clips.Length];asset.visual_revision=VISUAL_REVISION;
                AssetDatabase.CreateAsset(asset, output);
                asset.material = new Material(Shader.Find("ZombieGame/CharacterAtlas")) { name = name + " Atlas", enableInstancing = true, mainTexture = atlas };
                asset.material.SetFloat("_Saturation", art.saturation);
                asset.material.SetColor("_Tint", art.tint);
                asset.material.SetFloat("_Ambient", art.ambient_light);
                if(style!=null){asset.material.SetFloat("_Saturation",.9f);asset.material.SetColor("_Tint",Color.white);asset.material.SetFloat("_Glossiness",style.roughness);}
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                float ground_offset=0;
                for (int pose = 0; pose < clips.Length; pose++)
                {
                    var clip = animations.FirstOrDefault(c => c.name == clips[pose]);
                    if (clip == null) throw new InvalidOperationException("Missing animation: " + clips[pose] + " in " + string.Join(",", animations.Select(c=>c.name)));
                    var frames = new PoseFrames { source_clip = clip.name, duration = pose == 2 && source == "Characters_Shaun" ? .4f : clip.length, frames = new Mesh[FRAME_COUNT], muzzle_positions = new Vector3[FRAME_COUNT] };
                    for (int frame = 0; frame < FRAME_COUNT; frame++)
                    {
                        float fraction = frame / (float)(pose == 3 ? FRAME_COUNT - 1 : FRAME_COUNT);
                        clip.SampleAnimation(model, fraction * clip.length);
                        Transform rifle = renderers.FirstOrDefault(r=>r.name == "Rifle")?.transform;
                        frames.muzzle_positions[frame] = rifle == null ? Vector3.up : model.transform.InverseTransformPoint(rifle.TransformPoint(MusketMeshBuilder.to_grip(new Vector3(0,.12f,1.27f))));
                        Mesh mesh = bake_frame(model, renderers, art, source=="Characters_Shaun",pose>=4,style,name=="Exploder",weapon);
                        // Leaner adult proportions; applied to all poses and the weapon socket, not navigation roots.
                        var proportions=name=="Exploder"?new Vector3(1,1.04f,.96f):style==null?new Vector3(.88f,1.06f,.91f):new Vector3(.80f,1.28f,.86f);
                        var shaped_vertices=mesh.vertices;
                        for(int v=0;v<shaped_vertices.Length;v++)shaped_vertices[v]=Vector3.Scale(shaped_vertices[v],proportions);
                        mesh.vertices=shaped_vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
                        frames.muzzle_positions[frame]=Vector3.Scale(frames.muzzle_positions[frame],proportions);
                        if(style!=null)
                        {
                            if(pose==0&&frame==0)ground_offset=mesh.bounds.min.y;
                            for(int v=0;v<shaped_vertices.Length;v++)shaped_vertices[v].y-=ground_offset;
                            mesh.vertices=shaped_vertices;mesh.RecalculateBounds();
                            frames.muzzle_positions[frame]-=Vector3.up*ground_offset;
                        }
                        if (name == "Exploder"&&style==null)
                        {
                            var colors = new Color[mesh.vertexCount];
                            Vector3[] vertices = mesh.vertices;
                            for (int v=0;v<colors.Length;v++) colors[v] = vertices[v].y > .45f && vertices[v].y < 1.05f
                                ? art.exploder_belly : art.exploder_body;
                            mesh.colors = colors;
                        }
                        // Small presentation-only recoil. Original pack supplies a rifle pose, not a firing clip.
                        if (name == "Human" && pose == 2)
                        {
                            Vector3[] vertices = mesh.vertices;
                            float recoil = Mathf.Sin(fraction * Mathf.PI) * .055f;
                            for (int v = 0; v < vertices.Length; v++) if (vertices[v].y > .65f) vertices[v].z -= recoil;
                            mesh.vertices = vertices; mesh.RecalculateBounds();
                        }
                        mesh.name = name + "_" + (CharacterPose)pose + "_" + frame;
                        frames.frames[frame] = mesh; AssetDatabase.AddObjectToAsset(mesh, asset);
                    }
                    asset.poses[pose] = frames;
                }
                EditorUtility.SetDirty(asset);
                Debug.Log($"[CharacterBake] {name} vertices={asset.poses[0].frames[0].vertexCount} bounds={asset.poses[0].frames[0].bounds} renderers={string.Join(",",renderers.Select(r=>r.name))}");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        private static Mesh bake_frame(GameObject model, Renderer[] renderers, CharacterArtSettings art,bool human,bool melee,VisualStyle style,bool explosive,string weapon)
        {
            var combines = new List<CombineInstance>(); var temporary = new List<Mesh>();
            foreach (Renderer renderer in renderers)
            {
                if(renderer.name=="Rifle"&&(melee||weapon=="Ballista"||weapon=="Cannon")||renderer.name=="Knife"&&!melee)continue;
                // Style variants replace the cartoon body, keeping only the licensed rig/clips and weapon socket.
                if(style!=null&&renderer is SkinnedMeshRenderer)continue;
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    mesh = new Mesh(); skin.BakeMesh(mesh); mesh.colors = build_garment_colors(skin,human,style); temporary.Add(mesh);
                }
                else if(renderer.name=="Knife")
                {
                    mesh=UnityEngine.Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                    var colors=new Color[mesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=new Color(.5f,.48f,.4f,1);
                    mesh.colors=colors;temporary.Add(mesh);
                }
                else { mesh = weapon==null?MusketMeshBuilder.build(art):RangedWeaponMeshBuilder.build(weapon,true); temporary.Add(mesh); }
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    combines.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = model.transform.worldToLocalMatrix * renderer.localToWorldMatrix });
            }
            if(style!=null)
            {
                var equipment=new CharacterStyleGeometry(model,style).build(human,explosive);temporary.Add(equipment);
                combines.Add(new CombineInstance{mesh=equipment,transform=Matrix4x4.identity});
            }
            if(!melee&&(weapon=="Ballista"||weapon=="Cannon"))
            {
                var machine=RangedWeaponMeshBuilder.build(weapon,false);temporary.Add(machine);
                combines.Add(new CombineInstance{mesh=machine,transform=Matrix4x4.Translate(new Vector3(0,0,.95f))});
            }
            var result = new Mesh(); result.CombineMeshes(combines.ToArray(), true, true); result.RecalculateBounds();
            foreach (Mesh mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh);
            return result;
        }

        /// <summary>Bone-based cloth/iron palette follows animation, unlike a world-height color mask.</summary>
        private static Color[] build_garment_colors(SkinnedMeshRenderer skin,bool human,VisualStyle style)
        {
            var weights=skin.sharedMesh.boneWeights;
            var colors=new Color[skin.sharedMesh.vertexCount];
            for(int i=0;i<colors.Length;i++)
            {
                if(i>=weights.Length)continue;
                var weight=weights[i];int bone=weight.boneIndex0;float strongest=weight.weight0;
                if(weight.weight1>strongest){bone=weight.boneIndex1;strongest=weight.weight1;}
                if(weight.weight2>strongest){bone=weight.boneIndex2;strongest=weight.weight2;}
                if(weight.weight3>strongest)bone=weight.boneIndex3;
                string name=skin.bones[bone].name.ToLowerInvariant();
                if(style!=null)
                {
                    Color color=human?style.color(style.cloth):new Color(.29f,.28f,.22f);
                    if(name.Contains("head")||name.Contains("neck")||name.Contains("arm")||name.Contains("finger")||name.Contains("thumb")||name.Contains("index")||name.Contains("middle")||name.Contains("pinky"))
                        color=human?style.color(style.skin):new Color(.44f,.46f,.34f);
                    if(name.Contains("foot"))color=new Color(.15f,.13f,.10f);
                    colors[i]=new Color(color.r,color.g,color.b,.97f);continue;
                }
                if(name.Contains("torso")||name.Contains("abdomen"))
                    colors[i]=human?new Color(.24f,.25f,.20f,.86f):new Color(.27f,.25f,.16f,.65f);
                else if(name.Contains("upperarm")||name.Contains("shoulder"))
                    colors[i]=human?new Color(.36f,.18f,.10f,.83f):new Color(.29f,.27f,.19f,.5f);
                else if(name.Contains("leg")||name.Contains("hips"))
                    colors[i]=human?new Color(.24f,.22f,.16f,.82f):new Color(.24f,.23f,.17f,.62f);
                else if(name.Contains("foot"))colors[i]=new Color(.14f,.115f,.08f,.9f);
            }
            return colors;
        }
    }
}
