using System;
using System.IO;
using UnityEngine;
using ZombieGame.Balance;

internal static class RankBalanceChecks
{
    private static int assertion_count;

    private static int Main(string[] arguments)
    {
        if (arguments.Length != 1) throw new ArgumentException("Pass the repository root as the only argument.");
        Application.dataPath = Path.Combine(Path.GetFullPath(arguments[0]), "Assets");
        verify_authored_ranks();
        verify_non_infantry_scope();
        verify_noise_upper_bound();
        verify_invalid_rules(arguments[0]);
        Console.WriteLine($"[RankBalanceChecks] PASS {assertion_count} assertions; real UnitBalance.cs, CPU-only Unity API adapter");
        return 0;
    }

    /// <summary>Read authored rank records and confirm only approved capacity/range values vary.</summary>
    private static void verify_authored_ranks()
    {
        UnitBalance.validate(UnitBalance.config);
        require(UnitBalance.config.infantry_ranks.damage_progression == "pending", "damage progression remains pending");
        foreach (string unit_id in UnitBalance.infantry_ids)
        {
            var base_stats = UnitBalance.get(unit_id);
            var militia = UnitBalance.get_infantry_ranked(unit_id, "militia");
            var veteran = UnitBalance.get_infantry_ranked(unit_id, "veteran");
            var elite = UnitBalance.get_infantry_ranked(unit_id, "elite");
            require(militia.ammunition_capacity == 30 && veteran.ammunition_capacity == 30 && elite.ammunition_capacity == 50, "rank capacities " + unit_id);
            require(militia.attack_range == base_stats.attack_range && veteran.attack_range == base_stats.attack_range && elite.attack_range == base_stats.attack_range + 1, "rank range bonuses " + unit_id);
            require(militia.damage == base_stats.damage && veteran.damage == base_stats.damage && elite.damage == base_stats.damage, "no unapproved rank damage " + unit_id);
            require(elite.move_speed == base_stats.move_speed && elite.attack_interval == base_stats.attack_interval && elite.ammunition_cost == base_stats.ammunition_cost, "unrelated stats preserved " + unit_id);
            require(UnitBalance.human_noise(elite) == elite.attack_range * UnitBalance.config.noise_range_multiplier, "noise follows resolved range " + unit_id);
            require(UnitBalance.get_infantry_ranked(unit_id).ammunition_capacity == militia.ammunition_capacity, "explicit default militia " + unit_id);
            elite.ammunition_capacity = 999;
            require(base_stats.ammunition_capacity == 30 && UnitBalance.get_infantry_ranked(unit_id, "elite").ammunition_capacity == 50, "rank copy cannot mutate source " + unit_id);
        }
        expect_invalid(() => UnitBalance.get_infantry_ranked("archer", "unknown"), "unknown rank");
        expect_invalid(() => UnitBalance.get_infantry_ranked("archer", ""), "empty explicit rank");
    }

    private static void verify_non_infantry_scope()
    {
        require(UnitBalance.get("greek_fire").ammunition_capacity == 100, "Greek fire keeps 100 ammo");
        require(UnitBalance.get("heavy_ballista").ammunition_capacity == 100, "ballista keeps 100 ammo");
        require(UnitBalance.get("cannon").ammunition_capacity == 50, "cannon keeps 50 ammo");
        foreach (string unit_id in new[] { "greek_fire", "heavy_ballista", "cannon", "walker", "cannon_bastion", "unimplemented_javelin" })
            expect_invalid(() => UnitBalance.get_infantry_ranked(unit_id, "elite"), "non-infantry rank rejected " + unit_id);
    }

    /// <summary>Isolate the hearing-radius calculation and restore every temporary fixture change.</summary>
    private static void verify_noise_upper_bound()
    {
        require(UnitBalance.max_human_noise == 30, "current engineering noise bound unchanged");
        var engineers = new[] { UnitBalance.get("heavy_ballista"), UnitBalance.get("cannon"), UnitBalance.get("greek_fire") };
        var original_ranges = Array.ConvertAll(engineers, stats => stats.attack_range);
        var elite = Array.Find(UnitBalance.config.infantry_ranks.ranks, rank => rank.id == "elite");
        float original_bonus = elite.attack_range_bonus;
        try
        {
            foreach (var stats in engineers) stats.attack_range = 1; // Named CPU-only hearing-bound fixture, not game balance.
            require(UnitBalance.max_human_noise == 24, "hearing bound covers explicit elite +1 range");
            elite.attack_range_bonus = 2;
            require(UnitBalance.max_human_noise == 27, "hearing bound follows authored maximum rank bonus");
        }
        finally
        {
            for (int index = 0; index < engineers.Length; index++) engineers[index].attack_range = original_ranges[index];
            elite.attack_range_bonus = original_bonus;
        }
        require(UnitBalance.max_human_noise == 30 && UnitBalance.get("cannon").attack_range == 10, "hearing fixture resets completely");
    }

    /// <summary>Corrupt fresh parsed fixtures, never the cached source, to exercise production validation.</summary>
    private static void verify_invalid_rules(string repository_root)
    {
        string json = File.ReadAllText(Path.Combine(repository_root, "balance/unit_balance.json"));
        check_invalid_fixture(json, values => values.infantry_ranks = null, "missing rank rules");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks = null, "missing ranks");
        check_invalid_fixture(json, values => values.infantry_ranks.default_rank = "elite", "invalid default rank");
        check_invalid_fixture(json, values => values.infantry_ranks.damage_progression = "approved", "unimplemented damage progression");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks[0] = null, "null rank");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks[1].id = "militia", "duplicate rank");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks[1].id = "commander", "unknown configured rank");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks[2].ammunition_capacity = 0, "zero capacity");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks[2].attack_range_bonus = -1, "negative range bonus");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks[2].attack_range_bonus = float.NaN, "NaN range bonus");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks[2].attack_range_bonus = float.PositiveInfinity, "infinite range bonus");
        check_invalid_fixture(json, values => values.infantry_ranks.ranks[0].ammunition_capacity = 10, "base/default capacity mismatch");
        UnitBalance.validate(UnitBalance.config);
        require(UnitBalance.get("archer").ammunition_capacity == 30, "invalid fixtures leave cached source untouched");
    }

    private static void check_invalid_fixture(string json, Action<BalanceConfig> mutate_fixture, string label)
    {
        var values = JsonUtility.FromJson<BalanceConfig>(json);
        mutate_fixture(values);
        expect_invalid(() => UnitBalance.validate(values), label);
    }

    private static void expect_invalid(Action callback, string label)
    {
        try { callback(); }
        catch (InvalidOperationException) { assertion_count++; return; }
        throw new InvalidOperationException("[RankBalanceChecks] expected rejection: " + label);
    }

    private static void require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("[RankBalanceChecks] FAIL " + label);
        assertion_count++;
    }
}
