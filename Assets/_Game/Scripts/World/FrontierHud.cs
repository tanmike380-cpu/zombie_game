using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    /// <summary>RTS deck: left tactical map, centre unit status/control groups, right command grid.</summary>
    public sealed class FrontierHud
    {
        private readonly FrontierGame game;
        private GUIStyle text,number,button,title,small;
        private Font font;
        public bool show_help;
        public static float scale => Mathf.Clamp(Screen.height/900f,.8f,2.5f);
        public static bool contains(Vector2 point) => point.y<76*scale || point.y>Screen.height-242*scale;
        private static readonly Color GOLD=new Color(.69f,.55f,.30f);
        private static readonly Color INK=new Color(.065f,.084f,.089f,.98f);
        public FrontierHud(FrontierGame game) { this.game=game; }

        private void prepare_styles()
        {
            if(font==null)font=Font.CreateDynamicFontFromOSFont("Arial Unicode MS",18);
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=Mathf.RoundToInt(16*scale),alignment=TextAnchor.MiddleLeft,wordWrap=false};
            text.normal.textColor=new Color(.86f,.84f,.75f);
            number=new GUIStyle(text){fontSize=Mathf.RoundToInt(23*scale),fontStyle=FontStyle.Bold};
            title=new GUIStyle(text){fontSize=Mathf.RoundToInt(18*scale),fontStyle=FontStyle.Bold};
            title.normal.textColor=new Color(.90f,.74f,.43f);
            small=new GUIStyle(text){fontSize=Mathf.RoundToInt(13*scale)};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=Mathf.RoundToInt(15*scale),alignment=TextAnchor.MiddleCenter,wordWrap=true};
        }
        private static void fill(Rect rect,Color color)
        { GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white; }
        private static void panel(Rect rect)
        {
            fill(rect,INK);
            fill(new Rect(rect.x,rect.y,rect.width,2*scale),GOLD);
            fill(new Rect(rect.x,rect.y,scale,rect.height),new Color(.28f,.25f,.19f));
        }
        public void draw()
        {
            prepare_styles();GUI.matrix=Matrix4x4.identity;GUI.depth=1;
            draw_resources();
            float s=scale,y=Screen.height-242*s;
            panel(new Rect(0,y,224*s,242*s));
            GUI.Label(new Rect(14*s,y+4*s,200*s,25*s),"战术地图   256 × 256",small);
            float centre_x=234*s,right_x=Screen.width-306*s;
            draw_selection(new Rect(centre_x,y,right_x-centre_x-10*s,242*s));
            draw_commands(new Rect(right_x,y,306*s,242*s));
            if(show_help)
            {
                panel(new Rect(15*s,92*s,600*s,106*s));
                GUI.Label(new Rect(28*s,100*s,575*s,28*s),"左拖框选 · 右键移动 · A 后左键进攻 · Q 巡逻",text);
                GUI.Label(new Rect(28*s,130*s,575*s,28*s),"Ctrl + 数字保存编队 · 数字/卡片召回 · 双击聚焦",text);
                GUI.Label(new Rect(28*s,160*s,575*s,28*s),"方向键移镜头 · 滚轮缩放 · C 看部队 · Home 基地 · H 隐藏",small);
            }
        }
        private void draw_resources()
        {
            float s=scale;
            panel(new Rect(0,0,Screen.width,76*s));
            var e=game.economy;
            string[] names={"粮食","木材","石料","铁矿","箭矢","火药"};
            float[] values={e.food,e.wood,e.stone,e.iron,e.arrows,e.gunpowder};
            float cell=(Screen.width-185*s)/6;
            for(int i=0;i<6;i++)
            {
                float x=16*s+i*cell;
                GUI.Label(new Rect(x,3*s,cell,25*s),names[i],small);
                GUI.Label(new Rect(x,29*s,cell,37*s),values[i].ToString("F0"),number);
                if(i>0)fill(new Rect(x-10*s,15*s,s,44*s),new Color(.25f,.25f,.21f));
            }
            GUI.Label(new Rect(Screen.width-180*s,6*s,175*s,28*s),"边境聚落",title);
            GUI.Label(new Rect(Screen.width-180*s,38*s,175*s,28*s),$"部队 {game.current.soldier_count-game.current.dead_soldiers} / 400",text);
        }
        private void draw_selection(Rect area)
        {
            panel(area);float s=scale,x=area.x+14*s,y=area.y;
            int count=game.controls.selected_count();float health=0,max_health=0;
            for(int i=0;i<game.current.soldier_count;i++)
                if(game.current.selected[i]&&game.current.health[i]>0){health+=game.current.health[i];max_health+=game.current.stats_for(i).health;}
            draw_unit_icon(new Rect(x,y+15*s,70*s,77*s),count>0);
            GUI.Label(new Rect(x+84*s,y+12*s,area.width-105*s,28*s),count>0?$"神机营   × {count}":"未选择部队",title);
            GUI.Label(new Rect(x+84*s,y+43*s,area.width-105*s,24*s),$"伤害 {UnitBalance.human.damage:0}    射程 {UnitBalance.human.attack_range:0}    移速 {UnitBalance.human.move_speed:0.0}",small);
            var bar=new Rect(x+84*s,y+76*s,Mathf.Max(40*s,area.width-112*s),8*s);
            fill(bar,new Color(.16f,.18f,.16f));fill(new Rect(bar.x,bar.y,bar.width*(max_health>0?health/max_health:0),bar.height),new Color(.33f,.57f,.30f));
            GUI.Label(new Rect(x,y+98*s,area.width-28*s,24*s),$"总生命 {health:0} / {max_health:0}     编队：Ctrl + 数字保存",small);
            float card_width=(area.width-28*s)/10;
            for(int slot=0;slot<10;slot++)
            {
                int group=(slot+1)%10,n=game.controls.group_count(group);
                var card=new Rect(x+slot*card_width,y+129*s,card_width-4*s,67*s);
                GUI.enabled=game.can_control;
                if(GUI.Button(card,"",button))game.controls.click_group(group);
                GUI.enabled=true;
                GUI.Label(new Rect(card.x+4*s,card.y+2*s,card.width-6*s,20*s),group.ToString(),small);
                draw_unit_icon(new Rect(card.x+card.width*.40f,card.y+7*s,card.width*.45f,30*s),n>0);
                GUI.Label(new Rect(card.x+5*s,card.y+40*s,card.width-6*s,22*s),n>0?n.ToString():"—",small);
            }
            GUI.Label(new Rect(x,y+207*s,area.width-28*s,25*s),game.is_paused?"已暂停 · 空格继续":"单击编队召回 · 双击聚焦   |   H 查看操作帮助",small);
        }
        private static void draw_unit_icon(Rect rect,bool active)
        {
            // Original UI silhouette: infantry head/body and a long matchlock, not an imported game icon.
            Color cloth=active?new Color(.51f,.63f,.65f):new Color(.22f,.25f,.26f);
            fill(new Rect(rect.x+rect.width*.29f,rect.y+rect.height*.08f,rect.width*.29f,rect.height*.22f),cloth);
            fill(new Rect(rect.x+rect.width*.19f,rect.y+rect.height*.34f,rect.width*.48f,rect.height*.42f),cloth);
            fill(new Rect(rect.x+rect.width*.25f,rect.y+rect.height*.73f,rect.width*.12f,rect.height*.22f),cloth);
            fill(new Rect(rect.x+rect.width*.49f,rect.y+rect.height*.73f,rect.width*.12f,rect.height*.22f),cloth);
            fill(new Rect(rect.x+rect.width*.78f,rect.y+rect.height*.08f,rect.width*.05f,rect.height*.8f),active?GOLD:cloth);
            fill(new Rect(rect.x+rect.width*.55f,rect.y+rect.height*.53f,rect.width*.3f,rect.height*.09f),cloth);
        }
        private void draw_commands(Rect area)
        {
            panel(area);float s=scale;
            GUI.Label(new Rect(area.x+12*s,area.y+4*s,area.width-24*s,26*s),"部队指令",title);
            string[] labels={"进攻\nA","移动\nM","停止\nS","巡逻\nQ","全选\nF2","占领\nE",
                game.economy.powder_works?"火药坊\n生产中":"火药坊\n已暂停",game.economy.arrow_works?"箭坊\n生产中":"箭坊\n已暂停","聚焦部队\nC"};
            float width=(area.width-24*s)/3;
            GUI.enabled=game.can_control;
            for(int i=0;i<9;i++)
                if(GUI.Button(new Rect(area.x+12*s+i%3*width,area.y+37*s+i/3*56*s,width-5*s,50*s),labels[i],button))execute_command(i);
            GUI.enabled=true;
            string mode=game.controls.pending=="Attack"?"进攻：左键指定目标":game.controls.pending=="Move"?"移动：左键指定地点":game.controls.pending=="Patrol"?"巡逻：左键指定终点":"待命 · 右键移动";
            GUI.Label(new Rect(area.x+12*s,area.y+207*s,area.width-24*s,25*s),mode,small);
        }
        private void execute_command(int command)
        {
            switch(command)
            {
                case 0:game.controls.arm("Attack");break;
                case 1:game.controls.arm("Move");break;
                case 2:game.controls.issue_selected(SoldierOrder.Stop,Vector3.zero);game.controls.cancel_command();break;
                case 3:game.controls.arm("Patrol");break;
                case 4:game.controls.select_all();break;
                case 5:game.claim_site();break;
                case 6:game.economy.powder_works=!game.economy.powder_works;break;
                case 7:game.economy.arrow_works=!game.economy.arrow_works;break;
                case 8:game.focus_camera(game.controls.selection_center());break;
            }
        }
    }
}
