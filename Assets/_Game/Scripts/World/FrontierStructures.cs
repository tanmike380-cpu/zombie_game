using System.Collections.Generic;
using UnityEngine;
using ZombieGame.Combat;

namespace ZombieGame.World
{
    /// <summary>Connects shared combat buildings to scene visuals, production and resource ownership.</summary>
    public sealed class FrontierStructures:System.IDisposable
    {
        private readonly FrontierGame game;
        private FrontierLandscape landscape;
        private readonly Dictionary<BattleBuilding,PlacedFacility> placed=new Dictionary<BattleBuilding,PlacedFacility>();
        private readonly Dictionary<BattleBuilding,GameObject> ruins=new Dictionary<BattleBuilding,GameObject>();
        private readonly Material ruin_material;
        public BattleBuilding headquarters {get;private set;}
        public FrontierStructures(FrontierGame game,FrontierLandscape landscape,Shader shader)
        {
            this.game=game;this.landscape=landscape;
            ruin_material=new Material(shader){color=new Color(.24f,.12f,.15f)};
            foreach(var region in game.map.regions)
            {
                if(region.kind!=LandscapeKind.Building)continue;
                var building=game.current.add_building_target(region.bounds,region.label,region.label=="COMMAND HALL");
                if(building.headquarters)headquarters=building;
            }
            game.current.building_infected=infect_building;
        }
        public void register_facility(PlacedFacility facility)
        {placed.Add(game.current.add_building_target(facility.region.bounds,facility.recipe.name),facility);}
        public void remove_cancelled(PlacedFacility facility)
        {
            BattleBuilding found=null;foreach(var pair in placed)if(pair.Value==facility){found=pair.Key;break;}
            // Keep stable building indices while retiring a cancelled footprint without infection.
            if(found!=null){found.health=0;placed.Remove(found);}
        }
        public void rebuild_visuals(FrontierLandscape replacement)
        {
            landscape=replacement;
            foreach(var building in game.current.buildings)if(building.infected)hide_model(building);
        }
        private void hide_model(BattleBuilding building)
        {
            if(placed.TryGetValue(building,out var facility)){if(facility.model!=null)facility.model.SetActive(false);}
            else if(landscape.building_models.TryGetValue(building.bounds,out var model))model.SetActive(false);
        }
        private void infect_building(BattleBuilding building)
        {
            hide_model(building);
            var ruin=GameObject.CreatePrimitive(PrimitiveType.Cube);ruin.name="Infected ruin: "+building.label;
            Object.Destroy(ruin.GetComponent<Collider>());ruin.transform.SetParent(game.transform,false);
            ruin.transform.position=new Vector3(building.bounds.center.x,.3f,building.bounds.center.z);
            ruin.transform.localScale=new Vector3(building.bounds.size.x,.6f,building.bounds.size.z);
            ruin.GetComponent<Renderer>().sharedMaterial=ruin_material;ruins[building]=ruin;
            if(placed.TryGetValue(building,out var facility))game.construction.lose_facility(facility);
            else switch(building.label)
            {
                case "GRANARY":game.economy.food_sites=0;break;
                case "LUMBER CAMP":game.economy.wood_sites=0;break;
                case "POWDER WORKS":game.economy.powder_workshops=Mathf.Max(0,game.economy.powder_workshops-1);break;
                case "ARROW WORKS":game.economy.arrow_workshops=Mathf.Max(0,game.economy.arrow_workshops-1);break;
                case "AMMUNITION DEPOT":game.construction.remove_depot(game.map.initial_depot);break;
            }
            if(building.headquarters){game.production.operational=false;game.construction.cancel_preview();}
            Debug.Log($"[Building] infected={building.label} burst={building.infection_remaining} headquarters={building.headquarters}");
        }
        public void Dispose(){foreach(var ruin in ruins.Values)if(ruin!=null)Object.Destroy(ruin);Object.Destroy(ruin_material);}
    }
}
