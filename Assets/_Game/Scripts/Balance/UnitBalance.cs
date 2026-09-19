using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZombieGame.Balance
{
    [Serializable]
    public sealed class UnitStats
    {
        public string id, name_zh;
        public bool implemented;
        public float health, move_speed, acceleration, attack_range, damage, attack_interval;
        public float projectile_speed, explosion_radius, fuse_seconds, noise_radius;
    }

    [Serializable]
    public sealed class BalanceConfig
    {
        public int version;
        public float human_sight, zombie_sight, noise_range_multiplier, noise_propagation_speed, noise_pulse_duration;
        public float formation_spacing, chase_repath_seconds;
        public UnitStats[] units;
    }

    /// <summary>Single authored source: repository balance/unit_balance.json. Builds embed an exact generated snapshot.</summary>
    public static class UnitBalance
    {
        private static BalanceConfig cached;
        private static readonly Dictionary<string,UnitStats> by_id = new Dictionary<string,UnitStats>();
        public static BalanceConfig config { get { ensure_loaded(); return cached; } }
        public static UnitStats human => get("firearm_infantry");
        public static UnitStats runner => get("runner");
        public static UnitStats exploder => get("exploder");
        public static string source_hash { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void reset_cache() { cached = null; by_id.Clear(); }

        public static UnitStats get(string id)
        {
            ensure_loaded();
            if (!by_id.TryGetValue(id,out var stats)) throw new InvalidOperationException("Missing balance unit: " + id);
            return stats;
        }

        public static float human_noise(UnitStats stats) => stats.attack_range > 0 ? stats.attack_range * config.noise_range_multiplier : stats.noise_radius;

        public static void validate(BalanceConfig values)
        {
            if (values == null || values.version != 1 || values.units == null || values.human_sight <= 0 || values.zombie_sight <= 0
                || values.noise_range_multiplier <= 0 || values.noise_propagation_speed <= 0 || values.noise_pulse_duration <= 0
                || values.formation_spacing < .6f || values.chase_repath_seconds <= 0)
                throw new InvalidOperationException("Invalid balance/unit_balance.json globals/schema");
            var ids = new HashSet<string>();
            foreach (var stats in values.units)
            {
                if (stats == null || string.IsNullOrEmpty(stats.id) || !ids.Add(stats.id)) throw new InvalidOperationException("Duplicate/missing balance unit id");
                foreach (float number in new[] {stats.health,stats.move_speed,stats.acceleration,stats.attack_range,stats.damage,stats.attack_interval,stats.projectile_speed,stats.explosion_radius,stats.fuse_seconds,stats.noise_radius})
                    if (float.IsNaN(number) || float.IsInfinity(number) || number < 0) throw new InvalidOperationException("Invalid numeric balance: " + stats.id);
                if (stats.implemented && (stats.health <= 0 || stats.move_speed <= 0 || stats.acceleration <= 0 || stats.attack_range <= 0 || stats.damage <= 0 || stats.attack_interval <= 0))
                    throw new InvalidOperationException("Incomplete active unit balance: " + stats.id);
            }
            foreach (string required in new[] { "firearm_infantry", "runner", "exploder", "walker", "archer" })
                if (!ids.Contains(required)) throw new InvalidOperationException("Required balance unit missing: " + required);
            foreach (float number in new[] {values.human_sight,values.zombie_sight,values.noise_range_multiplier,values.noise_propagation_speed,values.noise_pulse_duration,values.formation_spacing,values.chase_repath_seconds})
                if (float.IsNaN(number) || float.IsInfinity(number)) throw new InvalidOperationException("Non-finite balance global");
            var human_stats = Array.Find(values.units,unit => unit.id == "firearm_infantry");
            var runner_stats = Array.Find(values.units,unit => unit.id == "runner");
            var exploder_stats = Array.Find(values.units,unit => unit.id == "exploder");
            if (!human_stats.implemented || !runner_stats.implemented || !exploder_stats.implemented
                || human_stats.projectile_speed <= 0 || exploder_stats.explosion_radius <= 0 || exploder_stats.fuse_seconds <= 0
                || runner_stats.move_speed <= human_stats.move_speed || exploder_stats.move_speed <= human_stats.move_speed
                || runner_stats.noise_radius != 0 || exploder_stats.noise_radius != 0)
                throw new InvalidOperationException("Invalid combat roles: check projectile/explosion stats, silent zombies and zombies faster than Shenji");
        }

        private static void ensure_loaded()
        {
            if (cached != null) return;
            string json;
#if UNITY_EDITOR
            json = File.ReadAllText(Path.Combine(Application.dataPath,"../balance/unit_balance.json"));
#else
            var asset = Resources.Load<TextAsset>("BalanceGenerated/unit_balance");
            if (asset == null) throw new InvalidOperationException("Build is missing generated unit balance snapshot");
            json = asset.text;
#endif
            var parsed = JsonUtility.FromJson<BalanceConfig>(json); validate(parsed);
            by_id.Clear(); foreach (var stats in parsed.units) by_id.Add(stats.id,stats);
            cached = parsed; source_hash = Hash128.Compute(json).ToString();
            Debug.Log($"[Balance] source=balance/unit_balance.json hash={source_hash} human={human.move_speed} runner={runner.move_speed} exploder={exploder.move_speed}");
        }
    }
}
