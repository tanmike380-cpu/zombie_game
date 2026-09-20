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

        [MenuItem("Tools/Zombie Game/Characters/Bake Models")]
        public static void bake_models()
        {
            Directory.CreateDirectory(OUTPUT); AssetDatabase.Refresh();
            string smoke_path = OUTPUT + "/MusketSmoke.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(smoke_path) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("ZombieGame/MusketSmoke")),smoke_path);
            bake_character("Characters_Shaun", "Human", new[] { "Idle_Gun", "Run_Gun", "Idle_Gun", "Death" });
            bake_character("Zombie_Basic", "Zombie", new[] { "Idle", "Run", "Punch", "Death" });
            bake_character("Zombie_Chubby", "Exploder", new[] { "Idle", "Run", "Punch", "Death" });
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterBake] PASS human/basic/chubby: idle/run/attack/death, 12 shared frames per pose; human uses original matchlock mesh and recoil over Idle_Gun");
        }

        public static void ensure_models()
        {
            if (AssetDatabase.LoadAssetAtPath<CharacterFrames>(OUTPUT + "/Human.asset") == null ||
                AssetDatabase.LoadAssetAtPath<CharacterFrames>(OUTPUT + "/Zombie.asset") == null ||
                AssetDatabase.LoadAssetAtPath<CharacterFrames>(OUTPUT + "/Exploder.asset") == null) bake_models();
        }

        private static void bake_character(string source, string name, string[] clips)
        {
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
                    .Where(r => r is SkinnedMeshRenderer || r.gameObject.name == "Rifle").ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("No character meshes: " + source);
                var atlas = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().FirstOrDefault();
                if (atlas == null) throw new InvalidOperationException("No character atlas: " + source);
                string output = OUTPUT + "/" + name + ".asset";
                if (File.Exists(output)) AssetDatabase.DeleteAsset(output); // Regenerable bake, never source art.
                var asset = ScriptableObject.CreateInstance<CharacterFrames>(); asset.poses = new PoseFrames[4];
                AssetDatabase.CreateAsset(asset, output);
                asset.material = new Material(Shader.Find("ZombieGame/CharacterAtlas")) { name = name + " Atlas", enableInstancing = true, mainTexture = atlas };
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                for (int pose = 0; pose < clips.Length; pose++)
                {
                    var clip = animations.FirstOrDefault(c => c.name == clips[pose]);
                    if (clip == null) throw new InvalidOperationException("Missing animation: " + clips[pose] + " in " + string.Join(",", animations.Select(c=>c.name)));
                    var frames = new PoseFrames { source_clip = clip.name, duration = pose == 2 && name == "Human" ? .4f : clip.length, frames = new Mesh[FRAME_COUNT], muzzle_positions = new Vector3[FRAME_COUNT] };
                    for (int frame = 0; frame < FRAME_COUNT; frame++)
                    {
                        float fraction = frame / (float)(pose == 3 ? FRAME_COUNT - 1 : FRAME_COUNT);
                        clip.SampleAnimation(model, fraction * clip.length);
                        Transform rifle = renderers.FirstOrDefault(r=>r.name == "Rifle")?.transform;
                        frames.muzzle_positions[frame] = rifle == null ? Vector3.up : model.transform.InverseTransformPoint(rifle.TransformPoint(MusketMeshBuilder.to_grip(new Vector3(0,.12f,1.27f))));
                        Mesh mesh = bake_frame(model, renderers);
                        if (name == "Exploder")
                        {
                            var colors = new Color[mesh.vertexCount];
                            Vector3[] vertices = mesh.vertices;
                            for (int v=0;v<colors.Length;v++) colors[v] = vertices[v].y > .45f && vertices[v].y < 1.05f
                                ? new Color(.61f,.12f,.40f,.72f) : new Color(.39f,.24f,.42f,.45f);
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

        private static Mesh bake_frame(GameObject model, Renderer[] renderers)
        {
            var combines = new List<CombineInstance>(); var temporary = new List<Mesh>();
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    mesh = new Mesh(); skin.BakeMesh(mesh); mesh.colors = new Color[mesh.vertexCount]; temporary.Add(mesh);
                }
                else { mesh = MusketMeshBuilder.build(); temporary.Add(mesh); }
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    combines.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = model.transform.worldToLocalMatrix * renderer.localToWorldMatrix });
            }
            var result = new Mesh(); result.CombineMeshes(combines.ToArray(), true, true); result.RecalculateBounds();
            foreach (Mesh mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh);
            return result;
        }
    }
}
