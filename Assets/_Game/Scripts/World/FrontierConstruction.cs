using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    public sealed class PlacedFacility
    {
        public HeadquartersRecipe recipe;
        public LandscapeRegion region;
        public GameObject model,navigation;
        public bool complete;
    }
    /// <summary>Ghost placement, validated footprints, reserved harvest cells and Unity obstacle carving.</summary>
    public sealed class FrontierConstruction : IDisposable
    {
        private readonly FrontierGame game;
        private readonly FrontierLandscape landscape;
        private readonly Shader shader;
        private readonly ResourceTerrain terrain;
        private readonly Dictionary<string,HashSet<int>> claimed=new Dictionary<string,HashSet<int>>();
        public readonly List<PlacedFacility> facilities=new List<PlacedFacility>();
        public int pending {get;private set;}=-1;
        public bool active=>pending>=0;
        public Vector3 position {get;private set;}
        public bool can_place {get;private set;}
        public float expected_yield {get;private set;}
        public string notice {get;private set;}="";
        private int[] preview_cells=Array.Empty<int>();
        private GameObject ghost;
        private Renderer[] ghost_renderers;
        private readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        private readonly Material line_material;
        private readonly LineRenderer preview_ring;
        private readonly List<LineRenderer> depot_rings=new List<LineRenderer>();
        public FrontierConstruction(FrontierGame game,FrontierLandscape landscape,Shader shader)
        {
            this.game=game;this.landscape=landscape;this.shader=shader;terrain=new ResourceTerrain(game.map);
            line_material=new Material(Shader.Find("Sprites/Default"));
            preview_ring=create_ring("Construction preview coverage");preview_ring.gameObject.SetActive(false);
            game.production.cancelled=cancel_facility;
            add_depot_ring(game.map.initial_depot);
        }
        private HashSet<int> claimed_cells(string id)
        {if(!claimed.TryGetValue(id,out var cells)){cells=new HashSet<int>();claimed.Add(id,cells);}return cells;}
        public void begin(int recipe_index)
        {
            if(recipe_index<1||recipe_index>=game.production.config.recipes.Length)return;
            cancel_preview();pending=recipe_index;game.controls.cancel_command();
            var recipe=game.production.config.recipes[pending];
            ghost=landscape.create_facility(recipe.id,new Vector2(recipe.width,recipe.depth),shader);
            ghost_renderers=ghost.GetComponentsInChildren<Renderer>();notice="左键放置 · 右键 / Esc 取消";
        }
        public void cancel_preview()
        {
            if(ghost!=null)UnityEngine.Object.Destroy(ghost);
            ghost=null;pending=-1;can_place=false;preview_ring.gameObject.SetActive(false);
        }
        public void update()
        {
            foreach(var ring in depot_rings)ring.gameObject.SetActive(game.headquarters_selected||active||game.controls.selected_count()>0);
            if(!active)return;
            if(Input.GetKeyDown(KeyCode.Escape)){cancel_preview();return;}
            var mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
            if(game.pointer_over_ui(mouse)){ghost.SetActive(false);preview_ring.gameObject.SetActive(false);return;}
            var ray=Camera.main.ScreenPointToRay(Input.mousePosition);
            if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))return;
            Vector3 point=ray.GetPoint(distance);position=new Vector3(Mathf.Round(point.x),0,Mathf.Round(point.z));
            var recipe=game.production.config.recipes[pending];
            can_place=validate(recipe,position,out string reason,out float rate,out preview_cells);
            expected_yield=rate;notice=reason;
            ghost.SetActive(true);ghost.transform.position=position;
            var color=can_place?new Color(.3f,.85f,.4f):new Color(.85f,.2f,.12f);
            tint.SetColor("_Color",color);foreach(var renderer in ghost_renderers)renderer.SetPropertyBlock(tint);
            float radius=recipe.id=="depot"?UnitBalance.config.ammunition_depot_radius:recipe.gather_radius;
            preview_ring.gameObject.SetActive(radius>0);set_ring(preview_ring,position,radius,color);
        }
        public bool handle_input(Event input)
        {
            if(!active||game.pointer_over_ui(input.mousePosition))return false;
            if(input.type==EventType.MouseDown)
            {
                if(input.button==1)cancel_preview();
                else if(input.button==0&&try_place(pending,position))cancel_preview();
                input.Use();return true;
            }
            return input.isMouse;
        }
        public bool validate(HeadquartersRecipe recipe,Vector3 point,out string reason,out float rate,out int[] cells)
        {
            rate=0;cells=Array.Empty<int>();
            var footprint=new Bounds(point+Vector3.up*1.5f,new Vector3(recipe.width,3,recipe.depth));
            var clearance=footprint;clearance.Expand(UnitBalance.config.unit_navigation_radius*2);
            reason="可建造";
            if(footprint.min.x< -127||footprint.max.x>127||footprint.min.z< -127||footprint.max.z>127){reason="超出地图边界";return false;}
            for(int i=0;i<5;i++)
            {
                var check=i==4?point:new Vector3(i%2==0?footprint.min.x:footprint.max.x,0,i/2==0?footprint.min.z:footprint.max.z);
                if(!game.current_fog.is_explored(check)){reason="需要先探索地块";return false;}
                if(!NavMesh.SamplePosition(check,out var hit,.2f,NavMesh.AllAreas)){reason="需要完整可通行地面";return false;}
            }
            foreach(var region in game.map.regions)
                if(clearance.Intersects(region.bounds)){reason="与地形或建筑重叠";return false;}
            var unit_clearance=footprint;unit_clearance.Expand(2);
            for(int i=0;i<game.current.total_count;i++)
                if(game.current.health[i]>0&&unit_clearance.Contains(game.current.positions[i]+Vector3.up)){reason="地块有单位，请先移开";return false;}
            rate=terrain.estimate(recipe,point,claimed_cells(recipe.id),out cells);
            if(recipe.gather_radius>0&&cells.Length==0){reason="覆盖范围内没有未分配的有效资源格";return false;}
            if(recipe.gather_radius>0)reason=$"可建造 · {cells.Length} 格 · +{rate:0.#}/分钟";
            if(recipe.id=="food")reason+=$" · 平均肥力 {(cells.Length>0?rate/(cells.Length*recipe.yield_per_cell_minute)*100:0):0}%";
            if(recipe.id=="depot")reason=$"可建造 · 补给半径 {UnitBalance.config.ammunition_depot_radius:0} 格";
            var economy=game.economy;
            if(economy.food<recipe.food||economy.wood<recipe.wood||economy.stone<recipe.stone||economy.iron<recipe.iron){reason="资源不足";return false;}
            if(game.production.queue.Count>=game.production.config.queue_capacity){reason="生产队列已满";return false;}
            return true;
        }
        public bool try_place(int index,Vector3 point)
        {
            if(index<1||index>=game.production.config.recipes.Length)return false;
            var recipe=game.production.config.recipes[index];
            if(!validate(recipe,point,out string reason,out float rate,out int[] cells)){notice=reason;return false;}
            var order=recipe.at(point,rate,cells);
            if(!game.production.enqueue(index,game.current.reserve_soldiers,order)){notice=game.production.notice;return false;}
            float obstacle_height=recipe.id=="food"?.7f:recipe.id=="stone"?1.2f:3;
            var region=new LandscapeRegion(point.x,point.z,recipe.width,recipe.depth,obstacle_height,LandscapeKind.Building,"BUILT:"+recipe.id);
            var model=landscape.create_facility(recipe.id,new Vector2(recipe.width,recipe.depth),shader);model.transform.position=point;
            tint.SetColor("_Color",new Color(.32f,.29f,.21f));foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(tint);
            var placed=new PlacedFacility{recipe=order,region=region,model=model,navigation=game.current.crowd.add_building(region.bounds)};
            facilities.Add(placed);game.map.regions.Add(region);foreach(int cell in cells)claimed_cells(recipe.id).Add(cell);
            notice="已放置，等待建造完成";return true;
        }
        public void finish(HeadquartersRecipe order)
        {
            var facility=facilities.Find(entry=>ReferenceEquals(entry.recipe,order));
            if(facility==null)throw new InvalidOperationException("Production has no placed footprint: "+order.id);
            facility.complete=true;foreach(var renderer in facility.model.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(null);
            switch(order.id)
            {
                case "food":game.economy.food_bonus_minute+=order.yield_per_minute;break;
                case "wood":game.economy.wood_bonus_minute+=order.yield_per_minute;break;
                case "stone":game.economy.stone_bonus_minute+=order.yield_per_minute;break;
                case "iron":game.economy.iron_bonus_minute+=order.yield_per_minute;break;
                case "powder":game.economy.powder_workshops++;break;
                case "arrows":game.economy.arrow_workshops++;break;
                case "depot":game.supply.depots.Add(order.position);add_depot_ring(order.position);break;
            }
        }
        private void cancel_facility(HeadquartersRecipe order)
        {
            if(order.id=="soldier")return;
            var facility=facilities.Find(entry=>ReferenceEquals(entry.recipe,order));if(facility==null)return;
            game.current.crowd.remove_building(facility.region.bounds,facility.navigation);game.map.regions.Remove(facility.region);
            UnityEngine.Object.Destroy(facility.model);facilities.Remove(facility);
            foreach(int cell in order.resource_cells)claimed_cells(order.id).Remove(cell);
        }
        private LineRenderer create_ring(string name)
        {
            var obj=new GameObject(name);obj.transform.SetParent(game.transform,false);
            var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=line_material;line.useWorldSpace=true;
            line.widthMultiplier=.065f;line.loop=true;line.positionCount=96;return line;
        }
        private static void set_ring(LineRenderer line,Vector3 center,float radius,Color color)
        {
            line.startColor=line.endColor=color;
            for(int i=0;i<96;i++)line.SetPosition(i,center+new Vector3(Mathf.Cos(i*Mathf.PI/48)*radius,.15f,Mathf.Sin(i*Mathf.PI/48)*radius));
        }
        private void add_depot_ring(Vector3 point)
        {var line=create_ring("Ammunition supply - 20 tiles");set_ring(line,point,UnitBalance.config.ammunition_depot_radius,new Color(.53f,.72f,.8f));depot_rings.Add(line);}
        public void Dispose()
        {
            cancel_preview();UnityEngine.Object.Destroy(preview_ring.gameObject);
            foreach(var line in depot_rings)if(line!=null)UnityEngine.Object.Destroy(line.gameObject);
            UnityEngine.Object.Destroy(line_material);
        }
    }
}
