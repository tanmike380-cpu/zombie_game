using UnityEngine;

namespace ZombieGame.Presentation
{
    /// <summary>Visual-only sampled animation. It never changes the navigation root or combat stats.</summary>
    public sealed class CharacterView : MonoBehaviour
    {
        private CharacterFrames frames;
        private MeshFilter mesh_filter;
        private float died_at = -1;
        private float phase;
        public CharacterPose current_pose { get; private set; }

        public void initialize(bool human, bool explosive = false)
        {
            phase = Mathf.Repeat(transform.position.x * .37f + transform.position.z * .19f, 1);
            frames = Resources.Load<CharacterFrames>("CharacterGenerated/" + (human ? "Human" : explosive ? "Exploder" : "Zombie"));
            if (frames == null) throw new System.InvalidOperationException("Character bake missing. Use Tools/Zombie Game/Characters/Bake Models first.");
            mesh_filter = gameObject.AddComponent<MeshFilter>();
            var renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = frames.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void update_pose(bool alive, Vector3 velocity, float last_attack, float attack_interval)
        {
            if (!alive && died_at < 0) died_at = Time.time;
            float attack_age = Time.time - last_attack;
            current_pose = !alive ? CharacterPose.Death : velocity.sqrMagnitude > .04f ? CharacterPose.Run
                : attack_age < Mathf.Min(.45f, attack_interval) ? CharacterPose.Attack : CharacterPose.Idle;
            float age = current_pose == CharacterPose.Death ? Time.time - died_at
                : current_pose == CharacterPose.Attack ? attack_age : Time.time + phase;
            mesh_filter.sharedMesh = frames.poses[(int)current_pose].frames[frames.frame_index(current_pose, age)];
        }

        public Vector3 muzzle_position() => transform.TransformPoint(frames.poses[(int)CharacterPose.Attack].muzzle_positions[0]);
    }
}
