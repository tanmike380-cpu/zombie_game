using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    public sealed partial class FrontierHud
    {
        private UnitStats selected_stats()
        {
            for(int i=0;i<game.current.soldier_count;i++)if(game.current.selected[i]&&game.current.health[i]>0)return game.current.stats_for(i);
            return UnitBalance.human;
        }
        private string selected_unit_label()
        {
            var stats=selected_stats();
            for(int i=0;i<game.current.soldier_count;i++)if(game.current.selected[i]&&game.current.health[i]>0&&game.current.stats_for(i).id!=stats.id)return "混编部队 · 属性显示首类兵种";
            return stats.name_zh;
        }
        private void draw_building_health()
        {
            foreach(var building in game.current.buildings)
            {
                if(building.health<=0&&!building.infected)continue;
                var point=building.bounds.center;
                if(!game.reveal_map&&!game.current_fog.is_visible(point)&&!building.headquarters)continue;
                point.y=building.bounds.max.y+1;
                var screen=Camera.main.WorldToScreenPoint(point);
                float x=screen.x-35*scale,y=Screen.height-screen.y;
                if(screen.z<=0||x<0||x>Screen.width-70*scale||y<0||y>Screen.height-250*scale)continue;
                var bar=new Rect(x,y,70*scale,5*scale);fill(bar,Color.black);
                fill(new Rect(x+scale,y+scale,68*scale*(building.health/building.max_health),3*scale),new Color(.3f,.8f,.25f));
                GUI.Label(new Rect(x-25*scale,y-22*scale,120*scale,22*scale),building.infected?$"已感染 · +{building.spawned}":$"{building.health:0}/{building.max_health:0}",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
            }
            if(!game.defeated)return;
            var area=new Rect(Screen.width/2-280*scale,55*scale,560*scale,64*scale);panel(area);
            GUI.Label(area,"镇守府失守 · 防守失败\n可继续移动镜头观察战场；重新开始请重启本局",new GUIStyle(title){alignment=TextAnchor.MiddleCenter});
        }
        private void draw_resource_labels()
        {
            foreach(var site in game.map.resource_sites)
            {
                if(!game.reveal_map&&!game.current_fog.is_explored(site.position))continue;
                var screen=Camera.main.WorldToScreenPoint(site.position+Vector3.up*1.5f);
                float x=screen.x-70*scale,y=Screen.height-screen.y;
                if(screen.z<=0||x<0||x>Screen.width-140*scale||y<0||y>Screen.height-255*scale)continue;
                var area=new Rect(x,y,140*scale,22*scale);fill(area,new Color(.04f,.08f,.04f,.8f));
                GUI.Label(area,ResourceDistribution.label(site),new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
            }
        }
    }
}
