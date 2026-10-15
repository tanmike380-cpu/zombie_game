using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Controls;

namespace ZombieGame.World
{
    public sealed partial class FrontierGame : IRtsMapBounds
    {
        public DefenseScenario defense_scenario;
        public Rect playable_bounds => defense_scenario ? defense_scenario.world_bounds : new Rect(-128,-128,256,256);

        private void initialize_scenario_session()
        {
            defense_scenario.validate();
            coastal_skirmish = true;coastal_defense = true;
            session_config = defense_scenario.session;session = new CoastalSession();
        }

        private void filter_scenario_recipes(HeadquartersConfig config)
        {
            var available = new HashSet<string>(defense_scenario.available_unit_recipes ?? Array.Empty<string>());
            var recipes = new List<HeadquartersRecipe>();
            foreach(var recipe in config.recipes)
                if(recipe.is_unit ? available.Contains(recipe.recruit_id) && has_imported_unit(recipe.recruit_id) : defense_scenario.allow_new_buildings)
                    recipes.Add(recipe);
            config.recipes = recipes.ToArray();
        }

        /// <summary>Permanent scenario volumes use the existing native carver; geometry-only box sources do not subtract the backing floor.</summary>
        private void initialize_scenario_navigation()
        {
            if(current.crowd.walls.Length != 0)throw new InvalidOperationException("Scenario permanent navigation must initialize once, before combat-building carvers.");
            foreach(var region in defense_scenario.regions)
            {
                if(region.kind == LandscapeKind.Building)continue;
                var obstacle = current.crowd.add_building(region.bounds);
                if(region.nav_rounding_radius > 0)round_scenario_obstacle(obstacle,region.bounds,region.nav_rounding_radius);
            }
        }

        /// <summary>Two boxes plus four vertical capsules conservatively round the source AABB; the crowd keeps exactly one original Bounds record.</summary>
        private static void round_scenario_obstacle(GameObject obstacle,Bounds source_bounds,float radius)
        {
            obstacle.name = "Scenario farm hut — source AABB plus rounded navigation only";
            var original = obstacle.GetComponent<NavMeshObstacle>();
            original.size = new Vector3(source_bounds.size.x,source_bounds.size.y,source_bounds.size.z+radius*2);
            var cross = add_scenario_carver(obstacle.transform,"Rounded hut horizontal box",Vector3.zero);
            cross.shape = NavMeshObstacleShape.Box;
            cross.size = new Vector3(source_bounds.size.x+radius*2,source_bounds.size.y,source_bounds.size.z);
            foreach(float x in new[]{-source_bounds.extents.x,source_bounds.extents.x})
                foreach(float z in new[]{-source_bounds.extents.z,source_bounds.extents.z})
                {
                    var corner = add_scenario_carver(obstacle.transform,"Rounded hut corner capsule",new Vector3(x,0,z));
                    corner.shape = NavMeshObstacleShape.Capsule;corner.radius = radius;
                    // Keep the full-radius cylindrical band across the original vertical span, including the ground plane.
                    corner.height = source_bounds.size.y+radius*2;
                }
        }

        private static NavMeshObstacle add_scenario_carver(Transform parent,string name,Vector3 offset)
        {
            var child = new GameObject(name);child.transform.SetParent(parent,false);child.transform.localPosition = offset;
            var carver = child.AddComponent<NavMeshObstacle>();carver.carving = true;carver.carveOnlyStationary = false;
            return carver;
        }

        private void initialize_scenario_economy()
        {
            economy.arrow_workshops = 0;
            foreach(var region in map.regions)
                if(region.kind == LandscapeKind.Building && region.label == "ARROW WORKS")economy.arrow_workshops++;
        }
    }

    public sealed partial class FrontierMap
    {
        public readonly DefenseScenario scenario;

        private void populate_scenario_regions(DefenseScenario source)
        {
            foreach(var entry in source.regions)
            {
                var region = new LandscapeRegion {bounds = entry.bounds,kind = entry.kind,label = entry.label,art_id = entry.model_path};
                regions.Add(region);
            }
        }

        private void populate_scenario_spawns(DefenseScenario source)
        {
            for(int index = 0; index < HUMAN_CAPACITY; index++)
            {
                var spawn = source.humans[Mathf.Min(index,source.humans.Length-1)];
                spawns[index] = spawn.position;unit_ids[index] = spawn.unit_id;
            }
            for(int index = 0; index < source.zombies.Length; index++)
            {
                var spawn = source.zombies[index];int slot = HUMAN_CAPACITY+index;
                spawns[slot] = spawn.position;unit_ids[slot] = spawn.unit_id;explosive[slot] = spawn.unit_id == "exploder";
            }
        }
    }
}
