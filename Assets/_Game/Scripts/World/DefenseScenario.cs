using System;
using UnityEngine;

namespace ZombieGame.World
{
    /// <summary>Opt-in authored map adapter. Population and pacing are fixtures; unit statistics remain in UnitBalance.</summary>
    public sealed class DefenseScenario : ScriptableObject
    {
        [Serializable] public sealed class Region
        {
            public Bounds bounds;
            public LandscapeKind kind;
            public string label;
            public string model_path;
            // Optional navigation-only Minkowski padding around the unchanged source AABB.
            public float nav_rounding_radius;
        }
        [Serializable] public sealed class Spawn
        {
            public Vector3 position;
            public string unit_id;
        }
        public string version,source_record;
        public GameObject environment_prefab;
        public Vector3 environment_translation;
        public Rect world_bounds = new Rect(-64,-64,128,128);
        public Vector3 base_center,headquarters_position,initial_depot,initial_camera_focus;
        public float camera_pitch = 48,camera_yaw,initial_camera_size = 24;
        public int infection_reserve = 256;
        public CoastalSessionConfig session;
        public Region[] regions = Array.Empty<Region>();
        public Spawn[] humans = Array.Empty<Spawn>(),zombies = Array.Empty<Spawn>();
        public string[] available_unit_recipes = {"archer","greek_fire"};
        public bool allow_new_buildings;
        public bool crop_cast_shadows = true;
        public float shadow_distance = 150;
        public int shadow_cascades = 2;
        public ShadowResolution shadow_resolution = ShadowResolution.High;

        public void validate()
        {
            if(!environment_prefab || string.IsNullOrWhiteSpace(source_record) || session == null)
                throw new InvalidOperationException("Defense scenario requires a frozen environment and traceable source record.");
            session.validate();
            if(world_bounds.width <= 0 || world_bounds.height <= 0 || infection_reserve < 0 ||
                humans == null || zombies == null || humans.Length != session.initial_humans || zombies.Length != session.zombies)
                throw new InvalidOperationException("Defense scenario bounds/populations do not match its session.");
            int headquarters_count = 0;
            foreach(var region in regions)
            {
                if(region.bounds.size.x <= 0 || region.bounds.size.z <= 0)
                    throw new InvalidOperationException("Defense scenario has an empty region: "+region.label);
                if(region.label == "COMMAND HALL") headquarters_count++;
                if(region.kind == LandscapeKind.Building && string.IsNullOrWhiteSpace(region.model_path))
                    throw new InvalidOperationException("Combat building has no authored model path: "+region.label);
                if(float.IsNaN(region.nav_rounding_radius) || float.IsInfinity(region.nav_rounding_radius) ||
                    region.nav_rounding_radius < 0 || region.nav_rounding_radius > .25f ||
                    region.nav_rounding_radius > 0 && (region.kind == LandscapeKind.Building ||
                        !(region.label ?? string.Empty).StartsWith("FARM HUT ONLY ",StringComparison.Ordinal) || string.IsNullOrWhiteSpace(region.model_path)))
                    throw new InvalidOperationException("Rounded navigation is restricted to traced farm huts, at most 0.25m: "+region.label);
            }
            if(headquarters_count != 1) throw new InvalidOperationException("Defense scenario must have one authored headquarters.");
            foreach(var spawn in humans) validate_spawn(spawn,true);
            foreach(var spawn in zombies) validate_spawn(spawn,false);
        }

        private void validate_spawn(Spawn spawn,bool human)
        {
            if(spawn == null || string.IsNullOrWhiteSpace(spawn.unit_id) || !world_bounds.Contains(new Vector2(spawn.position.x,spawn.position.z)))
                throw new InvalidOperationException("Defense scenario spawn lies outside its playable world.");
            var stats = Balance.UnitBalance.get(spawn.unit_id);
            if(!stats.implemented || Balance.UnitBalance.is_human(spawn.unit_id) != human)
                throw new InvalidOperationException("Defense scenario uses an unsupported role: "+spawn.unit_id);
        }
    }
}
