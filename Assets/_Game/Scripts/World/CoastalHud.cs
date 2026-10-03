using UnityEngine;

namespace ZombieGame.World
{
    public sealed partial class FrontierHud
    {
        private void draw_session_toolbar()
        {
            Rect area=style_toolbar();panel(area);float s=scale;
            GUI.Label(new Rect(area.x+8*s,area.y+2*s,345*s,26*s),$"存活 {game.current.living_soldiers} · 击败 {game.current.dead_zombies}/{game.current.zombie_count} · 空格暂停",small);
            if(GUI.Button(new Rect(area.x+359*s,area.y+3*s,100*s,24*s),"回基地",button))game.focus_camera(game.map.base_center);
            if(GUI.Button(new Rect(area.x+465*s,area.y+3*s,100*s,24*s),"重新开局",button))game.restart_session();
        }
        private void draw_session_overlay()
        {
            float s=scale;bool preparing=game.session.outcome==CoastalOutcome.Preparing;
            Rect area=new Rect((Screen.width-630*s)/2,(Screen.height-350*s)/2,630*s,350*s);
            fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,.65f));panel(area);
            string heading=preparing?"海岸坚守 · 小型可玩版":game.session.outcome==CoastalOutcome.Won?"守城成功":"主基地失守";
            GUI.Label(new Rect(area.x+24*s,area.y+17*s,580*s,35*s),heading,number);
            string[] lines=preparing?new[]{
                $"{game.session_config.initial_humans} 名守军 · {game.session_config.zombies} 只僵尸 · 三波进攻",
                "目标：守住主基地，消灭全部进攻尸群。",
                $"开始后有 {game.session_config.preparation_seconds:0} 秒部署：框选部队，右键调整位置。",
                "A + 左键进攻 · F2全选 · Ctrl+数字编队 · 空格暂停",
                "B 打开主基地造兵 / 建造；弹药库附近自动补弹。",
                "全部兵、僵尸、建筑复用你的 Blender 导入模型。"
            }:new[]{
                $"击败僵尸 {game.current.dead_zombies} · 幸存友军 {game.current.living_soldiers}",
                game.session.outcome==CoastalOutcome.Won?"三波尸潮已清除，主基地仍然存活。":"主基地被摧毁。本局已暂停，可以重新部署再挑战。",
                "提示：前排弩手拉扯，希腊火守缺口，留意蓝色弹药条。"
            };
            for(int i=0;i<lines.Length;i++)GUI.Label(new Rect(area.x+24*s,area.y+(66+i*31)*s,584*s,29*s),lines[i],text);
            if(GUI.Button(new Rect(area.x+24*s,area.y+285*s,280*s,42*s),preparing?"开始守城  [Enter]":"再来一局",button))
            {if(preparing)game.begin_session();else game.restart_session();}
            if(GUI.Button(new Rect(area.x+323*s,area.y+285*s,280*s,42*s),"退出试玩",button))Application.Quit();
        }
    }
}
