using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.Presentation
{
    /// <summary>Shared sampled meshes, grouped by character/pose/frame. No per-unit skeleton or Animator.</summary>
    public sealed class CharacterCrowdRenderer
    {
        private readonly CharacterFrames[] characters;
        private readonly Matrix4x4[][] matrices;
        private readonly int[] counts;
        private readonly int frames_per_pose;
        private readonly int pose_count;
        public int submitted { get; private set; }

        public CharacterCrowdRenderer(int capacity,string style=null)
        {
            characters = new CharacterFrames[3];set_style(style);
            if (Array.Exists(characters,c=>c==null)) throw new InvalidOperationException("Bake character models before enabling animated crowd");
            frames_per_pose = characters[0].poses[0].frames.Length;
            pose_count=characters[0].poses.Length;
            int buckets = characters.Length * pose_count * frames_per_pose;
            matrices = new Matrix4x4[buckets][]; counts = new int[buckets];
            for (int i = 0; i < buckets; i++) matrices[i] = new Matrix4x4[capacity];
        }
        public void set_style(string style)
        {
            string prefix="CharacterGenerated/"+(string.IsNullOrEmpty(style)?"":style+"/");
            string[] names={"Human","Zombie","Exploder"};
            for(int i=0;i<3;i++)
            {
                var asset=Resources.Load<CharacterFrames>(prefix+names[i]);
                if(asset==null)throw new InvalidOperationException("Missing baked art variant: "+prefix+names[i]);
                characters[i]=asset;
            }
        }

        public void begin_frame() { Array.Clear(counts, 0, counts.Length); submitted = 0; }

        public void add(bool human, CharacterPose pose, float age, Vector3 position, Quaternion rotation, bool explosive = false,float model_scale=1)
        {
            int character = human ? 0 : explosive ? 2 : 1;
            int frame = characters[character].frame_index(pose, age);
            int bucket = (character * pose_count + (int)pose) * frames_per_pose + frame;
            matrices[bucket][counts[bucket]++] = Matrix4x4.TRS(position, rotation, Vector3.one*model_scale);
            submitted++;
        }

        public void draw()
        {
            for (int bucket = 0; bucket < counts.Length; bucket++)
            {
                if (counts[bucket] == 0) continue;
                int character = bucket / (pose_count * frames_per_pose), pose = bucket / frames_per_pose % pose_count, frame = bucket % frames_per_pose;
                var parameters = new RenderParams(characters[character].material) { worldBounds = new Bounds(Vector3.zero,new Vector3(260,30,260)), shadowCastingMode = character==0?ShadowCastingMode.On:ShadowCastingMode.Off, receiveShadows = true };
                for (int start = 0; start < counts[bucket]; start += 1023)
                    Graphics.RenderMeshInstanced(parameters, characters[character].poses[pose].frames[frame], 0,
                        matrices[bucket], Math.Min(1023, counts[bucket]-start), start);
            }
        }

        public Vector3 human_muzzle(Vector3 position, Quaternion rotation) => position + rotation * characters[0].poses[2].muzzle_positions[0];
    }
}
