using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    public sealed partial class FrontierHud
    {
        private int last_boss=-1;
        private static Rect threat_button()=>new Rect(Screen.width-220*scale,Screen.height-224*scale,210*scale,32*scale);
        private static Rect roster_rect()=>new Rect(Screen.width-545*scale,18*scale,525*scale,350*scale);
        private void draw_threat_intelligence()
        {
            int bosses=0;
            for(int i=game.current.soldier_count;i<game.current.total_count;i++)
            {
                var stats=game.current.stats_for(i);if(!stats.map_revealed||game.current.health[i]<=0)continue;
                bosses++;
                Vector3 projected=Camera.main.WorldToScreenPoint(game.current.positions[i]+Vector3.up*stats.model_scale*1.5f);
                if(projected.z<=0||projected.x<0||projected.x>Screen.width||projected.y<250*scale||projected.y>Screen.height)continue;
                Rect marker=new Rect(projected.x-105*scale,Screen.height-projected.y-24*scale,210*scale,28*scale);
                fill(marker,new Color(.22f,.035f,.05f,.92f));
                GUI.Label(marker,$"◆ Lv.{stats.threat_tier} {stats.name_zh} · 已知位置",title);
            }
            if(GUI.Button(threat_button(),$"尸王 {bosses} · 点击定位 / Z 图鉴",button))focus_next_boss();
            if(show_roster)draw_roster();else draw_enemy_hover();
        }
        private void focus_next_boss()
        {
            int start=Mathf.Max(game.current.soldier_count,last_boss+1);
            for(int offset=0;offset<game.current.zombie_count;offset++)
            {
                int index=game.current.soldier_count+(start-game.current.soldier_count+offset)%game.current.zombie_count;
                if(game.current.health[index]<=0||!game.current.stats_for(index).map_revealed)continue;
                last_boss=index;game.focus_camera(game.current.positions[index]);return;
            }
        }
        private void draw_roster()
        {
            Rect area=roster_rect();panel(area);float s=scale;
            GUI.Label(new Rect(area.x+12*s,area.y+8*s,area.width-24*s,28*s),"僵尸图鉴 · Lv 表示威胁阶级 · Z 关闭",title);
            GUI.Label(new Rect(area.x+12*s,area.y+40*s,area.width-24*s,24*s),"类型 / 阶级         生命    伤害    间隔    移速    射程",small);
            for(int i=0;i<UnitBalance.zombie_ids.Length;i++)
            {
                var stats=UnitBalance.get(UnitBalance.zombie_ids[i]);float y=area.y+(70+i*29)*s;
                GUI.Label(new Rect(area.x+12*s,y,195*s,26*s),$"Lv.{stats.threat_tier} {stats.name_zh.Replace("（临时平衡）","")}",text);
                float[] values={stats.health,stats.damage,stats.attack_interval,stats.move_speed,stats.attack_range};
                for(int column=0;column<values.Length;column++)
                    GUI.Label(new Rect(area.x+(215+column*59)*s,y,59*s,26*s),values[column].ToString("0.#"),small);
            }
            GUI.Label(new Rect(area.x+12*s,area.y+308*s,area.width-24*s,28*s),"新增类型为临时平衡；Boss 位置公开，不揭开周边迷雾。",small);
        }
        private void draw_enemy_hover()
        {
            Vector2 mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
            if(contains(mouse))return;
            int best=-1;float nearest=28*scale;
            for(int i=game.current.soldier_count;i<game.current.total_count;i++)
            {
                if(game.current.health[i]<=0||(!game.reveal_map&&!game.current_fog.is_visible(game.current.positions[i])))continue;
                var stats=game.current.stats_for(i);
                Vector3 p=Camera.main.WorldToScreenPoint(game.current.positions[i]+Vector3.up*stats.model_scale*.8f);
                float distance=Vector2.Distance(mouse,new Vector2(p.x,Screen.height-p.y));
                if(p.z>0&&distance<nearest){nearest=distance;best=i;}
            }
            if(best<0)return;
            var unit=game.current.stats_for(best);
            Rect area=new Rect(Mathf.Clamp(mouse.x+18*scale,0,Screen.width-305*scale),Mathf.Clamp(mouse.y-72*scale,0,Screen.height-300*scale),300*scale,68*scale);
            panel(area);
            GUI.Label(new Rect(area.x+8*scale,area.y+4*scale,285*scale,28*scale),$"Lv.{unit.threat_tier} {unit.name_zh}",title);
            GUI.Label(new Rect(area.x+8*scale,area.y+33*scale,285*scale,26*scale),$"生命 {game.current.health[best]:0}/{unit.health:0}  伤害 {unit.damage:0}  移速 {unit.move_speed:0.#}",small);
        }
    }
}
