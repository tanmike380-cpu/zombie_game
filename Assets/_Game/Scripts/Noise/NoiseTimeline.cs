using System;
using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.Noise
{
    public struct NoiseSignal
    {
        public long sequence;
        public Vector3 origin;
        public float emitted_at, radius;
    }

    /// <summary>Bounded arrival wheel. Listener positions are sampled at emission, not continuously.
    /// Within one AI tick only the newest audible emission matters; no per-shot allocations.</summary>
    public sealed class NoiseTimeline
    {
        private struct Arrival { public long tick; public NoiseSignal signal; }
        private readonly Arrival[] arrivals;
        private readonly NoiseSignal[] delivered;
        private readonly int listener_count, bucket_count;
        private readonly float quantum, max_radius;
        private long sequence, processed_tick = -1;

        public NoiseTimeline(int listener_count, float max_radius, float quantum = .1f)
        {
            if (listener_count < 1 || max_radius <= 0 || quantum <= 0)
                throw new ArgumentException("Noise timeline requires listeners, positive radius and tick interval");
            this.listener_count = listener_count; this.max_radius = max_radius; this.quantum = quantum;
            bucket_count = Mathf.CeilToInt(max_radius / UnitBalance.config.noise_propagation_speed / quantum) + 3;
            arrivals = new Arrival[checked(listener_count * bucket_count)];
            delivered = new NoiseSignal[listener_count];
        }

        public NoiseSignal create_signal(Vector3 origin, float radius, float now)
        {
            if (radius <= 0 || radius > max_radius) throw new ArgumentOutOfRangeException(nameof(radius));
            return new NoiseSignal { sequence = ++sequence, origin = origin, emitted_at = now, radius = radius };
        }

        public bool queue_listener(int index, NoiseSignal signal, Vector3 position)
        {
            Vector3 delta = position - signal.origin; delta.y = 0;
            float distance = delta.magnitude;
            if (distance > signal.radius) return false;
            long tick = Math.Max(processed_tick + 1,
                (long)Math.Ceiling((signal.emitted_at + distance / UnitBalance.config.noise_propagation_speed) / quantum));
            int slot = (int)(tick % bucket_count) * listener_count + index;
            if (arrivals[slot].tick != tick || arrivals[slot].signal.sequence < signal.sequence)
                arrivals[slot] = new Arrival { tick = tick, signal = signal };
            return true;
        }

        public void advance(float now)
        {
            long tick = (long)Math.Floor(now / quantum);
            if (tick <= processed_tick) return;
            // Scan each wheel bucket at most once even after a long frame or pause.
            long first = Math.Max(processed_tick + 1, tick - bucket_count + 1);
            for (long step = first; step <= tick; step++)
            {
                int start = (int)(step % bucket_count) * listener_count;
                for (int i = 0; i < listener_count; i++)
                {
                    Arrival arrival = arrivals[start + i];
                    if (arrival.signal.sequence == 0 || arrival.tick > tick || arrival.tick <= processed_tick) continue;
                    if (arrival.signal.sequence > delivered[i].sequence) delivered[i] = arrival.signal;
                    arrivals[start + i] = default;
                }
            }
            processed_tick = tick;
        }

        public bool try_hear(int index, out NoiseSignal signal)
        {
            signal = delivered[index]; delivered[index] = default;
            return signal.sequence > 0;
        }
    }
}
