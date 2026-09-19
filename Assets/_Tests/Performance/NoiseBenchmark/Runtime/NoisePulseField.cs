using UnityEngine;

namespace ZombieGame.PerformanceTests
{
    /// <summary>One test pulse, ten one-tile bands. No raycasts, echoes or wall acoustics.</summary>
    public sealed class NoisePulseField
    {
        public const float RADIUS = 10f;
        public const float PROPAGATION_SPEED = 5f;
        public const float PULSE_DURATION = .6f;
        public Vector3 source { get; private set; }
        public double emitted_at { get; private set; } = double.NegativeInfinity;

        public void emit(Vector3 position, double now)
        {
            source = position;
            emitted_at = now;
        }

        public static int distance_percent(float distance)
        {
            if (float.IsNaN(distance) || distance < 0 || distance > RADIUS) return 0;
            int band = Mathf.Max(1, Mathf.CeilToInt(distance));
            return (11 - band) * 10;
        }

        public int sample_percent(Vector3 position, double now)
        {
            float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(source.x, source.z));
            int strength = distance_percent(distance);
            double arrival = emitted_at + distance / PROPAGATION_SPEED;
            return strength > 0 && now >= arrival && now <= arrival + PULSE_DURATION ? strength : 0;
        }

        public float wave_radius(double now) => Mathf.Clamp((float)(now - emitted_at) * PROPAGATION_SPEED, 0, RADIUS);
        public bool has_live_wave(double now) => now >= emitted_at && now <= emitted_at + RADIUS / PROPAGATION_SPEED + PULSE_DURATION;
    }

    /// <summary>Noise memory is independent of current intensity and subordinate to a direct target.</summary>
    public sealed class NoiseInvestigation
    {
        public bool has_memory { get; private set; }
        public Vector3 remembered_position { get; private set; }
        public int remembered_strength { get; private set; }
        public bool has_direct_target;

        public bool hear(Vector3 source, int strength)
        {
            if (has_direct_target || strength <= 0) return false;
            if (has_memory && source == remembered_position) return false;
            has_memory = true;
            remembered_position = source;
            remembered_strength = strength;
            return true;
        }
    }
}
