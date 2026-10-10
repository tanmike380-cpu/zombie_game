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
        public string ammunition_type;
        public float large_target_multiplier;
        public bool implemented;
        public bool provisional;
        public bool stationary;
        public float cone_angle;
        public int ammunition_cost, ammunition_capacity;
        public float melee_damage, melee_attack_interval, melee_range;
        public float health, move_speed, acceleration, attack_range, damage, attack_interval;
        public float projectile_speed, explosion_radius, fuse_seconds, noise_radius;
        public int threat_tier;
        public float model_scale, navigation_radius, splash_radius, poison_damage_per_second, poison_duration;
        public bool map_revealed;

        internal UnitStats copy() => (UnitStats)MemberwiseClone();
    }

    [Serializable]
    public sealed class InfantryRankStats
    {
        public string id, name_zh;
        public int ammunition_capacity;
        public float attack_range_bonus;
    }

    [Serializable]
    public sealed class InfantryRankRules
    {
        public string default_rank, damage_progression;
        public InfantryRankStats[] ranks;
    }

    [Serializable]
    public sealed class BuildingStats
    {
        public float normal_health,headquarters_health;
        public int normal_infection_count,headquarters_infection_count;
        public string infection_unit;
        public bool provisional;
    }
    [Serializable]
    public sealed class BalanceConfig
    {
        public int version;
        public float human_sight, zombie_sight, noise_range_multiplier, noise_probe_radius, noise_propagation_speed, noise_pulse_duration;
        public float formation_spacing, chase_repath_seconds, unit_navigation_radius, zombie_navigation_radius, ammunition_depot_radius;
        public float zombie_attack_move_follow_through;
        public float zombie_queue_probe_seconds,zombie_queue_hold_seconds,zombie_queue_clearance;
        public UnitStats[] units;
        public BuildingStats buildings;
        public InfantryRankRules infantry_ranks;
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
        public static readonly string[] zombie_ids={"walker","runner","brute","exploder","zombie_hound","spitter","giant","boss"};
        public static readonly string[] human_ids={"firearm_infantry","archer","repeating_crossbowman","heavy_crossbowman","heavy_ballista","cannon","greek_fire"};
        public static readonly string[] infantry_ids={"firearm_infantry","archer","repeating_crossbowman","heavy_crossbowman"};
        public static bool is_human(string id)=>Array.IndexOf(human_ids,id)>=0;
        public static bool is_infantry(string id)=>Array.IndexOf(infantry_ids,id)>=0;
        public static float max_human_noise
        {
            get
            {
                float radius = 0, rank_range_bonus = 0;
                foreach (var rank in config.infantry_ranks.ranks)
                    rank_range_bonus = Mathf.Max(rank_range_bonus, rank.attack_range_bonus);
                foreach (string id in human_ids)
                {
                    var stats = get(id);
                    if (!stats.implemented) continue;
                    float unit_noise = human_noise(stats);
                    if (is_infantry(id) && stats.attack_range > 0)
                        unit_noise += rank_range_bonus * config.noise_range_multiplier;
                    radius = Mathf.Max(radius, unit_noise);
                }
                return radius;
            }
        }
        public static bool is_zombie(string id)=>Array.IndexOf(zombie_ids,id)>=0;
        // All zombie roles, including giants/bosses, use the same fixed tile footprint, never art scale.
        public static float navigation_radius(UnitStats stats)=>is_zombie(stats.id) ? config.zombie_navigation_radius
            : Mathf.Max(stats.navigation_radius,config.unit_navigation_radius);
        public static string source_hash { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void reset_cache() { cached = null; by_id.Clear(); }

        public static UnitStats get(string id)
        {
            ensure_loaded();
            if (!by_id.TryGetValue(id,out var stats)) throw new InvalidOperationException("Missing balance unit: " + id);
            return stats;
        }

        /// <summary>Resolve an explicit infantry rank without changing the shared base record or inventing damage bonuses.</summary>
        public static UnitStats get_infantry_ranked(string unit_id, string rank_id = null)
        {
            if (!is_infantry(unit_id)) throw new InvalidOperationException("Infantry ranks do not apply to unit: " + unit_id);
            var base_stats = get(unit_id);
            var rules = config.infantry_ranks;
            string resolved_rank = rank_id ?? rules.default_rank;
            var rank_stats = Array.Find(rules.ranks, rank => rank.id == resolved_rank);
            if (rank_stats == null) throw new InvalidOperationException("Missing infantry rank: " + resolved_rank);
            var resolved_stats = base_stats.copy();
            resolved_stats.ammunition_capacity = rank_stats.ammunition_capacity;
            resolved_stats.attack_range += rank_stats.attack_range_bonus;
            return resolved_stats;
        }

        private static void validate_infantry_ranks(BalanceConfig values)
        {
            var rules = values.infantry_ranks;
            if (rules == null || rules.default_rank != "militia" || rules.damage_progression != "pending"
                || rules.ranks == null || rules.ranks.Length != 3)
                throw new InvalidOperationException("Invalid infantry rank rules: require militia/veteran/elite and pending damage progression");
            var rank_ids = new HashSet<string>();
            foreach (var rank in rules.ranks)
            {
                if (rank == null || (rank.id != "militia" && rank.id != "veteran" && rank.id != "elite") || !rank_ids.Add(rank.id)
                    || rank.ammunition_capacity < 1 || rank.attack_range_bonus < 0
                    || float.IsNaN(rank.attack_range_bonus) || float.IsInfinity(rank.attack_range_bonus))
                    throw new InvalidOperationException("Invalid infantry rank capacity/range/id");
            }
            var default_stats = Array.Find(rules.ranks, rank => rank.id == rules.default_rank);
            foreach (string unit_id in infantry_ids)
            {
                var unit_stats = Array.Find(values.units, unit => unit.id == unit_id);
                if (unit_stats == null || unit_stats.ammunition_capacity != default_stats.ammunition_capacity
                    || Array.Exists(rules.ranks, rank => rank.ammunition_capacity < unit_stats.ammunition_cost))
                    throw new InvalidOperationException("Infantry base/default ammunition mismatch: " + unit_id);
            }
        }

        public static float human_noise(UnitStats stats) => stats.attack_range > 0 ? stats.attack_range * config.noise_range_multiplier : stats.noise_radius;

        public static void validate(BalanceConfig values)
        {
            if (values == null || values.version != 1 || values.units == null || values.human_sight <= 0 || values.zombie_sight <= 0
                || values.noise_range_multiplier <= 0 || values.noise_probe_radius <= 0 || values.noise_propagation_speed <= 0 || values.noise_pulse_duration <= 0
                || values.unit_navigation_radius<=0 || values.zombie_navigation_radius<=0 || values.zombie_attack_move_follow_through<=0 || values.ammunition_depot_radius<=0 || values.formation_spacing < values.unit_navigation_radius*2 || values.chase_repath_seconds <= 0)
                throw new InvalidOperationException("Invalid balance/unit_balance.json globals/schema");
            var ids = new HashSet<string>();
            foreach(float value in new[]{values.zombie_queue_probe_seconds,values.zombie_queue_hold_seconds,values.zombie_queue_clearance})
                if(value<=0||float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Invalid zombie queue hysteresis");
            var buildings=values.buildings;
            if(buildings==null||buildings.normal_health<=0||buildings.headquarters_health<=0||
                float.IsNaN(buildings.normal_health)||float.IsInfinity(buildings.normal_health)||float.IsNaN(buildings.headquarters_health)||float.IsInfinity(buildings.headquarters_health)||
                buildings.normal_infection_count<0||buildings.headquarters_infection_count<0||buildings.infection_unit!="walker")
                throw new InvalidOperationException("Invalid authored building health/infection balance");
            foreach (var stats in values.units)
            {
                if (stats == null || string.IsNullOrEmpty(stats.id) || !ids.Add(stats.id)) throw new InvalidOperationException("Duplicate/missing balance unit id");
                if (stats.ammunition_cost < 0) throw new InvalidOperationException("Negative ammunition cost: " + stats.id);
                foreach (float number in new[] {stats.health,stats.move_speed,stats.acceleration,stats.attack_range,stats.damage,stats.attack_interval,stats.projectile_speed,stats.explosion_radius,stats.fuse_seconds,stats.noise_radius,stats.melee_damage,stats.melee_attack_interval,stats.melee_range,stats.model_scale,stats.navigation_radius,stats.splash_radius,stats.poison_damage_per_second,stats.poison_duration})
                    if (float.IsNaN(number) || float.IsInfinity(number) || number < 0) throw new InvalidOperationException("Invalid numeric balance: " + stats.id);
                if(float.IsNaN(stats.cone_angle)||float.IsInfinity(stats.cone_angle)||stats.cone_angle<0||stats.cone_angle>180)throw new InvalidOperationException("Invalid flame cone: "+stats.id);
                if (stats.implemented && (stats.health <= 0 || (!stats.stationary&&(stats.move_speed <= 0 || stats.acceleration <= 0)) || stats.attack_range <= 0 || stats.damage <= 0 || stats.attack_interval <= 0))
                    throw new InvalidOperationException("Incomplete active unit balance: " + stats.id);
                if(stats.implemented&&is_zombie(stats.id)&&(stats.threat_tier<1||stats.threat_tier>5||stats.model_scale<=0||stats.noise_radius!=0))
                    throw new InvalidOperationException("Zombie tier, presentation scale or silence invalid: "+stats.id);
                if(stats.implemented&&is_human(stats.id)&&(stats.ammunition_capacity<stats.ammunition_cost||stats.ammunition_cost<1||
                    (stats.ammunition_type!="arrows"&&stats.ammunition_type!="gunpowder")||stats.projectile_speed<=0||(stats.id!="greek_fire"&&(stats.melee_damage<=0||stats.melee_attack_interval<=0||stats.melee_range<=0))))
                    throw new InvalidOperationException("Incomplete ranged human ammunition/melee stats: "+stats.id);
                if(stats.implemented&&(stats.id=="greek_fire"||stats.id=="flame_bastion")&&stats.cone_angle<=0)throw new InvalidOperationException("Missing flame cone: "+stats.id);
                if(stats.implemented&&stats.stationary&&(stats.ammunition_cost<1||stats.ammunition_type!="gunpowder"||stats.projectile_speed<=0))throw new InvalidOperationException("Incomplete defense weapon: "+stats.id);
                if(stats.implemented&&stats.id=="spitter"&&(stats.projectile_speed<=0||stats.splash_radius<=0||stats.poison_damage_per_second<=0||stats.poison_duration<=0))
                    throw new InvalidOperationException("Incomplete spitter projectile/poison stats");
            }
            foreach (string required in new[] { "firearm_infantry", "runner", "exploder", "walker", "archer" })
                if (!ids.Contains(required)) throw new InvalidOperationException("Required balance unit missing: " + required);
            validate_infantry_ranks(values);
            foreach (float number in new[] {values.human_sight,values.zombie_sight,values.noise_range_multiplier,values.noise_probe_radius,values.noise_propagation_speed,values.noise_pulse_duration,values.formation_spacing,values.chase_repath_seconds,values.unit_navigation_radius,values.zombie_navigation_radius,values.zombie_attack_move_follow_through,values.ammunition_depot_radius})
                if (float.IsNaN(number) || float.IsInfinity(number)) throw new InvalidOperationException("Non-finite balance global");
            var human_stats = Array.Find(values.units,unit => unit.id == "firearm_infantry");
            var runner_stats = Array.Find(values.units,unit => unit.id == "runner");
            var exploder_stats = Array.Find(values.units,unit => unit.id == "exploder");
            if (!human_stats.implemented || !runner_stats.implemented || !exploder_stats.implemented
                || human_stats.projectile_speed <= 0 || human_stats.ammunition_capacity<1 || human_stats.ammunition_cost<1
                || human_stats.melee_damage<=0 || human_stats.melee_range<=0 || human_stats.melee_attack_interval<=0
                || exploder_stats.explosion_radius <= 0 || exploder_stats.fuse_seconds <= 0
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
