using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.World
{
    [Serializable]
    public sealed class HeadquartersRecipe
    {
        public string id, name, unit_id;
        public bool is_unit => id=="soldier" || !string.IsNullOrEmpty(unit_id);
        public string recruit_id => string.IsNullOrEmpty(unit_id)?"firearm_infantry":unit_id;
        public float food, wood, stone, iron, seconds;
        public float width,depth,gather_radius,yield_per_cell_minute;
        [NonSerialized] public Vector3 position;
        [NonSerialized] public float yield_per_minute;
        [NonSerialized] public int[] resource_cells;
        public HeadquartersRecipe at(Vector3 position,float yield_per_minute,int[] cells)
        {
            var placed=(HeadquartersRecipe)MemberwiseClone();placed.position=position;
            placed.yield_per_minute=yield_per_minute;placed.resource_cells=cells;return placed;
        }
    }
    [Serializable]
    public sealed class HeadquartersConfig
    {
        public bool provisional;
        public int queue_capacity;
        public HeadquartersRecipe[] recipes;
    }

    /// <summary>Paid FIFO production; costs/times are provisional economy, never unit combat attributes.</summary>
    public sealed class HeadquartersProduction
    {
        public readonly HeadquartersConfig config;
        public readonly List<HeadquartersRecipe> queue=new List<HeadquartersRecipe>();
        public readonly Dictionary<string,int> completed=new Dictionary<string,int>();
        public Action<HeadquartersRecipe> cancelled;
        public bool operational=true;
        private readonly FrontierEconomy economy;
        public float elapsed {get;private set;}
        public string notice {get;private set;}="选择项目加入生产队列";
        public float progress => queue.Count==0?0:Mathf.Clamp01(elapsed/queue[0].seconds);
        public HeadquartersProduction(FrontierEconomy economy,HeadquartersConfig config)
        {
            this.economy=economy??throw new ArgumentNullException(nameof(economy));
            this.config=config??throw new ArgumentNullException(nameof(config));
            if(config.queue_capacity<1||config.recipes==null)throw new ArgumentException("Invalid headquarters queue config");
            var ids=new HashSet<string>();
            foreach(var recipe in config.recipes)
            {
                if(recipe==null||string.IsNullOrEmpty(recipe.id)||!ids.Add(recipe.id)||recipe.seconds<=0)
                    throw new ArgumentException("Invalid/duplicate headquarters recipe");
                if(!recipe.is_unit&&(recipe.width<=0||recipe.depth<=0))throw new ArgumentException("Missing building footprint: "+recipe.id);
                if(recipe.is_unit&&!ZombieGame.Balance.UnitBalance.is_human(recipe.recruit_id))throw new ArgumentException("Unknown recruit: "+recipe.recruit_id);
                foreach(float value in new[]{recipe.food,recipe.wood,recipe.stone,recipe.iron,recipe.seconds,recipe.width,recipe.depth,recipe.gather_radius,recipe.yield_per_cell_minute})
                    if(float.IsNaN(value)||float.IsInfinity(value)||value<0)throw new ArgumentException("Invalid recipe costs: "+recipe.id);
            }
        }
        public bool enqueue(int index,int available_recruits,HeadquartersRecipe placed=null)
        {
            if(!operational||index<0||index>=config.recipes.Length)return false;
            var recipe=placed??config.recipes[index];
            if(recipe.id!=config.recipes[index].id)return false;
            if(queue.Count>=config.queue_capacity){notice="生产队列已满";return false;}
            if(recipe.is_unit)
            {
                int queued=0;foreach(var entry in queue)if(entry.is_unit)queued++;
                if(queued>=available_recruits){notice="本轮测试的预留招募名额已满";return false;}
            }
            else if(placed==null){notice="请先选择建筑位置";return false;}
            if(economy.food<recipe.food||economy.wood<recipe.wood||economy.stone<recipe.stone||economy.iron<recipe.iron)
            {notice="资源不足："+cost_label(recipe);return false;}
            economy.food-=recipe.food;economy.wood-=recipe.wood;economy.stone-=recipe.stone;economy.iron-=recipe.iron;
            queue.Add(recipe);notice=recipe.name+"已加入队列";return true;
        }
        public void cancel_last()
        {
            if(!operational||queue.Count==0)return;
            int index=queue.Count-1;var recipe=queue[index];queue.RemoveAt(index);
            economy.food+=recipe.food;economy.wood+=recipe.wood;economy.stone+=recipe.stone;economy.iron+=recipe.iron;
            if(index==0)elapsed=0;
            cancelled?.Invoke(recipe);
            notice="已取消 "+recipe.name+"，资源全额返还";
        }
        public void step(float seconds,Func<HeadquartersRecipe,bool> finish)
        {
            if(!operational||queue.Count==0)return;
            var recipe=queue[0];elapsed=Mathf.Min(recipe.seconds,elapsed+Mathf.Max(0,seconds));
            if(elapsed<recipe.seconds)return;
            if(!finish(recipe)){notice="出口拥堵，等待空位再出兵";return;}
            completed.TryGetValue(recipe.id,out int count);completed[recipe.id]=count+1;
            queue.RemoveAt(0);elapsed=0;notice=recipe.name+"完成";
        }
        public void remove_destroyed_order(HeadquartersRecipe recipe)
        {
            int index=queue.IndexOf(recipe);if(index<0)return;
            queue.RemoveAt(index);if(index==0)elapsed=0;
            notice=recipe.name+"已被摧毁，资源不返还";
        }
        public static string cost_label(HeadquartersRecipe recipe)
        {
            string label="";
            if(recipe.food>0)label+=$"粮{recipe.food:0} ";
            if(recipe.wood>0)label+=$"木{recipe.wood:0} ";
            if(recipe.stone>0)label+=$"石{recipe.stone:0} ";
            if(recipe.iron>0)label+=$"铁{recipe.iron:0} ";
            return label+$"· {recipe.seconds:0}秒";
        }
    }
}
