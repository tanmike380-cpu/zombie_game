using UnityEngine;

namespace ZombieGame.CombatTests
{
    /// <summary>World-owned explosion visual, independent of victims and the dead actor.</summary>
    public sealed class BlastVisual : MonoBehaviour
    {
        private const float DURATION = .85f;
        private LineRenderer ring;
        private float radius;
        private float started_at;

        public void initialize(LineRenderer line, float blast_radius)
        {
            ring = line;
            radius = blast_radius;
            started_at = Time.time;
            ring.widthMultiplier = .3f;
            transform.localScale = Vector3.one * .15f;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.time - started_at) / DURATION);
            transform.localScale = Vector3.one * Mathf.Lerp(.15f, radius, Mathf.Min(1, progress * 3));
            ring.widthMultiplier = Mathf.Lerp(.3f, .015f, progress);
            if (progress >= 1) Destroy(gameObject);
        }
    }
}
