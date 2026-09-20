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
        public int submitted { get; private set; }

        public CharacterCrowdRenderer(int capacity)
        {
            characters = new[] { Resources.Load<CharacterFrames>("CharacterGenerated/Human"), Resources.Load<CharacterFrames>("CharacterGenerated/Zombie"), Resources.Load<CharacterFrames>("CharacterGenerated/Exploder") };
            if (Array.Exists(characters,c=>c==null)) throw new InvalidOperationException("Bake character models before enabling animated crowd");
            frames_per_pose = characters[0].poses[0].frames.Length;
            int buckets = characters.Length * 4 * frames_per_pose;
            matrices = new Matrix4x4[buckets][]; counts = new int[buckets];
            for (int i = 0; i < buckets; i++) matrices[i] = new Matrix4x4[capacity];
        }

        public void begin_frame() { Array.Clear(counts, 0, counts.Length); submitted = 0; }

        public void add(bool human, CharacterPose pose, float age, Vector3 position, Quaternion rotation, bool explosive = false)
        {
            int character = human ? 0 : explosive ? 2 : 1;
            int frame = characters[character].frame_index(pose, age);
            int bucket = (character * 4 + (int)pose) * frames_per_pose + frame;
            matrices[bucket][counts[bucket]++] = Matrix4x4.TRS(position, rotation, Vector3.one);
            submitted++;
        }

        public void draw()
        {
            for (int bucket = 0; bucket < counts.Length; bucket++)
            {
                if (counts[bucket] == 0) continue;
                int character = bucket / (4 * frames_per_pose), pose = bucket / frames_per_pose % 4, frame = bucket % frames_per_pose;
                var parameters = new RenderParams(characters[character].material) { worldBounds = new Bounds(Vector3.zero,new Vector3(260,30,260)), shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                for (int start = 0; start < counts[bucket]; start += 1023)
                    Graphics.RenderMeshInstanced(parameters, characters[character].poses[pose].frames[frame], 0,
                        matrices[bucket], Math.Min(1023, counts[bucket]-start), start);
            }
        }

        public Vector3 human_muzzle(Vector3 position, Quaternion rotation) => position + rotation * characters[0].poses[2].muzzle_positions[0];
    }
}
