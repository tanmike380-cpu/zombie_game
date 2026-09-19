using System;
using UnityEngine;
using ZombieGame.AI;
using ZombieGame.Noise;
using ZombieGame.Balance;

namespace ZombieGame.CombatStressTests
{
    public static class NoiseRetargetChecks
    {
        public static void run()
        {
            float radius = UnitBalance.human_noise(UnitBalance.human);
            var timeline = new NoiseTimeline(2, radius);
            var memory = new SoundMemory();
            var old_sound = timeline.create_signal(Vector3.right * 15, radius, 0);
            timeline.queue_listener(0, old_sound, Vector3.zero);
            var new_sound = timeline.create_signal(Vector3.left * 2, radius, .2f);
            timeline.queue_listener(0, new_sound, Vector3.zero);
            require(!timeline.queue_listener(1, new_sound, Vector3.right * (radius + 5)), "Outside listener queued");
            timeline.advance(.8f);
            require(timeline.try_hear(0, out var heard) && memory.accept(heard) && memory.origin == new_sound.origin, "Newest arrived sound missing");
            timeline.advance(3.2f);
            require(timeline.try_hear(0, out heard) && !memory.accept(heard) && memory.origin == new_sound.origin, "Late old wave restored stale goal");
            require(!timeline.try_hear(1, out heard), "Outside listener heard sound");
            var same_tick_a = timeline.create_signal(Vector3.zero, radius, 3.3f);
            var same_tick_b = timeline.create_signal(Vector3.one, radius, 3.3f);
            timeline.queue_listener(0, same_tick_b, same_tick_b.origin);
            timeline.queue_listener(0, same_tick_a, same_tick_a.origin);
            timeline.advance(3.6f);
            require(timeline.try_hear(0, out heard) && heard.sequence == same_tick_b.sequence && memory.accept(heard), "Same-tick newest priority");
            var after_pause = timeline.create_signal(Vector3.zero, radius, 4);
            timeline.queue_listener(0, after_pause, Vector3.right * 5);
            timeline.advance(100);
            require(timeline.try_hear(0, out heard) && memory.accept(heard), "Long frame dropped pending arrival");
            Debug.Log("[NoiseMemorySmoke] PASS propagation delay, newest emission, late-old rejection, same-tick ordering, outside radius, long-frame delivery");
            verify_dense_burst(radius);
        }

        private static void verify_dense_burst(float radius)
        {
            const int LISTENERS = 10000, SHOTS = 400;
            var timeline = new NoiseTimeline(LISTENERS, radius);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            NoiseSignal latest = default;
            for (int shot = 0; shot < SHOTS; shot++)
            {
                latest = timeline.create_signal(Vector3.zero, radius, 0);
                for (int i = 0; i < LISTENERS; i++) timeline.queue_listener(i, latest, Vector3.right * 15);
            }
            timeline.advance(3.2f);
            for (int i = 0; i < LISTENERS; i++)
                require(timeline.try_hear(i, out var heard) && heard.sequence == latest.sequence, "Dense burst lost latest event");
            timer.Stop();
            Debug.Log($"[NoiseBurstSmoke] PASS 400 simultaneous shots x 10000 listeners; all selected newest; isolated_queue_and_delivery_ms={timer.Elapsed.TotalMilliseconds:F2} (not game FPS)");
        }

        private static void require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Noise memory regression: " + message);
        }
    }
}
