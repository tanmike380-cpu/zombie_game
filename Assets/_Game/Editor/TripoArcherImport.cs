using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieGame.Presentation;

namespace ZombieGame.EditorTools
{
    /// <summary>Preserves the rigged FBX and bakes its actual geometry for the existing instanced crowd renderer.</summary>
    public static class TripoArcherImport
    {
        public const string MODEL_PATH = "Assets/_Game/Art/TripoCrossbow/ByzantineCrossbow.fbx";
        public const string FRAME_PATH = "Assets/_Game/Resources/TripoCrossbow/Frames.asset";
        private const int FRAME_COUNT = 12;
        public const string REST_PATH = "Assets/_Game/Resources/TripoArcher/OriginalRest.asset";
        public const string EXPLODER_PATH = "Assets/_Game/Resources/TripoExploder/Frames.asset";

        public static void import_exploder()
        {
            const string source = "Assets/_Game/Art/TripoExploder/TripoExploder.fbx";
            const string textures = "Assets/_Game/Art/TripoExploder/Textures";
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(source);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true; importer.isReadable = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            // Authored PNG is the source of truth. Never extract embedded FBX textures over it.
            Directory.CreateDirectory(textures); AssetDatabase.Refresh();
            string texture_path = textures + "/ExploderPustules.png";
            if (!File.Exists(texture_path)) throw new InvalidOperationException("Missing Blender-authored exploder pustule texture");
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
            try { bake_model(model, AssetDatabase.LoadAssetAtPath<Texture2D>(texture_path), source, EXPLODER_PATH, false); }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        public static void bake_original_rest()
        {
            const string original_path = "Assets/_Game/Art/TripoArcher/OriginalTripoRig.fbx";
            Directory.CreateDirectory(Path.GetDirectoryName(REST_PATH));
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(original_path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(original_path));
            try
            {
                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                var mesh = bake_frame(model, renderers, Quaternion.identity, "Original Tripo rest - no authored animation");
                float scale = 2f / mesh.bounds.size.y;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] *= scale;
                mesh.vertices = vertices; mesh.RecalculateBounds();
                var asset = AssetDatabase.LoadAssetAtPath<CharacterFrames>(REST_PATH);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CharacterFrames>();
                    AssetDatabase.CreateAsset(asset, REST_PATH);
                }
                else foreach (var child in AssetDatabase.LoadAllAssetsAtPath(REST_PATH))
                    if (child != asset) UnityEngine.Object.DestroyImmediate(child, true);
                var texture_id = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art/TripoArcher/Textures" }).First();
                asset.material = new Material(Shader.Find("Standard")) { name = "Original Tripo 2K", enableInstancing = true,
                    mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(texture_id)) };
                asset.material.SetFloat("_Glossiness", .16f);
                AssetDatabase.AddObjectToAsset(mesh, asset); AssetDatabase.AddObjectToAsset(asset.material, asset);
                asset.poses = new PoseFrames[7];
                for (int i = 0; i < 7; i++) asset.poses[i] = new PoseFrames { source_clip = "Original rest (NO animation)", duration = 1,
                    frames = Enumerable.Repeat(mesh, FRAME_COUNT).ToArray(), muzzle_positions = new Vector3[FRAME_COUNT] };
                EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
                Debug.Log($"[TripoOriginal] PASS unchanged geometry triangles={mesh.triangles.Length / 3}, bones={renderers[0].bones.Length}; original FBX has no animation");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        public static void import_and_bake()
        {
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(MODEL_PATH);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.isReadable = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            string texture_folder = "Assets/_Game/Art/TripoCrossbow/Textures";
            Directory.CreateDirectory(texture_folder);
            importer.ExtractTextures(texture_folder);
            AssetDatabase.Refresh();
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] {texture_folder});
            if (textures.Length == 0) throw new InvalidOperationException("Tripo color texture was not extracted");
            var texture_path = textures.Select(AssetDatabase.GUIDToAssetPath).First(path => path.Contains("tripo_rgb"));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texture_path);
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH));
            try { bake_model(model, texture); }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        public static void bake_model(GameObject model, Texture2D texture, string model_path = MODEL_PATH, string frame_path = FRAME_PATH, bool use_pbr = true, bool imported_colour=false, bool exploder_motion=false)
        {
            foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
            var clips = AssetDatabase.LoadAllAssetsAtPath(model_path).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
            Debug.Log("[TripoArcher] clips=" + string.Join(",", clips.Select(clip => clip.name)));
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Missing Tripo skinned mesh");
            Directory.CreateDirectory(Path.GetDirectoryName(frame_path));
            AssetDatabase.Refresh();
            var frames = AssetDatabase.LoadAssetAtPath<CharacterFrames>(frame_path);
            if (frames == null)
            {
                frames = ScriptableObject.CreateInstance<CharacterFrames>();
                AssetDatabase.CreateAsset(frames, frame_path);
            }
            else
                foreach (var child in AssetDatabase.LoadAllAssetsAtPath(frame_path))
                    if (child != frames) UnityEngine.Object.DestroyImmediate(child, true);
            frames.material = use_pbr ? TripoPbrMaterial.create(texture, "Assets/_Game/Art/TripoCrossbow/Textures")
                : new Material(Shader.Find("Standard")) { name = "Tripo exploder original color", mainTexture = texture, enableInstancing = true };
            if (!use_pbr) frames.material.SetFloat("_Glossiness", .15f);
            if(imported_colour)frames.material.shader=Shader.Find("ZombieGame/ImportedColour");
            AssetDatabase.AddObjectToAsset(frames.material, frames);
            bool exploder = exploder_motion || frame_path == EXPLODER_PATH;
            frames.poses = new PoseFrames[exploder ? 8 : 7];
            // Melee slots are explicit presentation fallbacks, not a new melee animation.
            string[] names = exploder ? new[] { "Idle", "Walk", "Attack", "Idle", "Attack", "Idle", "Walk", "Run" }
                : new[] { "Idle", "Run", "Attack", "Idle", "Attack", "Idle", "Run" };
            float normalization = 1;
            var transforms = model.GetComponentsInChildren<Transform>();
            var left_eye=transforms.FirstOrDefault(item=>item.name=="Left_Eye");
            var right_eye=transforms.FirstOrDefault(item=>item.name=="Right_Eye");
            var head=transforms.FirstOrDefault(item=>item.name=="Head");
            Vector3 forward=left_eye!=null&&right_eye!=null&&head!=null?(left_eye.position+right_eye.position)*.5f-head.position:Vector3.forward;
            forward.y = 0;
            var facing = Quaternion.FromToRotation(forward.normalized, Vector3.forward);
            Debug.Log("[TripoArcher] authored facing=" + forward.normalized);
            Transform bow = transforms.FirstOrDefault(item => item.name == "Right_Hand")??model.transform;
            for (int pose = 0; pose < names.Length; pose++)
            {
                if (pose >= 4 && pose < 7) { frames.poses[pose] = frames.poses[pose == 4 ? 2 : pose == 5 ? 0 : 1]; continue; }
                var clip = clips.FirstOrDefault(item => item.name == names[pose] || item.name.EndsWith("|" + names[pose]) || item.name.EndsWith("_"+names[pose]));
                if (clip == null) throw new InvalidOperationException("Missing Tripo clip: " + names[pose]);
                var sequence = new PoseFrames { source_clip = clip.name, duration = clip.length,
                    frames = new Mesh[24], muzzle_positions = new Vector3[24] };
                for (int frame = 0; frame < sequence.frames.Length; frame++)
                {
                    clip.SampleAnimation(model, clip.length * frame / sequence.frames.Length);
                    sequence.frames[frame] = bake_frame(model, renderers, facing, names[pose] + "_" + frame);
                    var mesh = sequence.frames[frame];
                    if (pose == 0 && frame == 0)
                    {
                        if (mesh.bounds.size.y <= 0) throw new InvalidOperationException("Empty Tripo mesh");
                        normalization = 2f / mesh.bounds.size.y;
                    }
                    var vertices = mesh.vertices;
                    for (int vertex = 0; vertex < vertices.Length; vertex++) vertices[vertex] *= normalization;
                    mesh.vertices = vertices;
                    mesh.RecalculateBounds();
                    sequence.muzzle_positions[frame] = facing * bow.position * 2;
                    AssetDatabase.AddObjectToAsset(sequence.frames[frame], frames);
                }
                frames.poses[pose] = sequence;
            }
            frames.visual_revision = 1;
            EditorUtility.SetDirty(frames);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TripoArcher] PASS frames={7 * FRAME_COUNT} triangles={frames.poses[0].frames[0].triangles.Length / 3} bounds={frames.poses[0].frames[0].bounds}");
        }

        private static Mesh bake_frame(GameObject model, SkinnedMeshRenderer[] renderers, Quaternion facing, string name)
        {
            var combine = new CombineInstance[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var baked = new Mesh();
                renderers[i].BakeMesh(baked);
                // Infer imported forward from the eyes; do not assume FBX axis conversion.
                combine[i] = new CombineInstance { mesh = baked, transform = Matrix4x4.TRS(Vector3.zero,
                    facing, Vector3.one * 2) * renderers[i].transform.localToWorldMatrix };
            }
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(combine, true, true);
            foreach (var item in combine) UnityEngine.Object.DestroyImmediate(item.mesh);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
