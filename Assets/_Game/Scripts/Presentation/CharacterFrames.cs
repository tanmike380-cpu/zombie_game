using UnityEngine;

namespace ZombieGame.Presentation
{
    public enum CharacterPose { Idle, Run, Attack, Death, MeleeAttack, MeleeIdle, MeleeRun }

    [System.Serializable]
    public sealed class PoseFrames
    {
        public string source_clip;
        public float duration;
        public Mesh[] frames;
        public Vector3[] muzzle_positions;
    }

    [PreferBinarySerialization]
    public sealed class CharacterFrames : ScriptableObject
    {
        public Material material;
        public int visual_revision;
        public PoseFrames[] poses;
        public int frame_index(CharacterPose pose, float age)
        {
            PoseFrames clip = poses[(int)pose];
            float phase = pose == CharacterPose.Death ? Mathf.Clamp01(age / clip.duration) : Mathf.Repeat(age / clip.duration, 1);
            return Mathf.Min(clip.frames.Length - 1, Mathf.FloorToInt(phase * clip.frames.Length));
        }
    }
}
