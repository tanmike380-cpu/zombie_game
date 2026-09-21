using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Balance;
using ZombieGame.Presentation;

namespace ZombieGame.World
{
    /// <summary>RTS deck: left tactical map, centre unit status/control groups, right command grid.</summary>
    public sealed partial class FrontierHud
    {
        private readonly FrontierGame game;
        private GUIStyle text,number,button,title,small;
        private Font font;
        public bool show_help;
        public bool show_roster;
        public static float scale => Mathf.Clamp(Screen.height/900f,.8f,2.5f);
        public static bool contains(Vector2 point,bool roster_open=false) => point.y>Screen.height-190*scale ||
            (point.x<224*scale && point.y>Screen.height-242*scale) || style_toolbar().Contains(point)||threat_button().Contains(point)||(roster_open&&roster_rect().Contains(point));
        private static Color GOLD=>VisualStyles.current.color(VisualStyles.current.accent);
        private static Color INK=>VisualStyles.current.color(VisualStyles.current.ink);
        private static Rect style_toolbar()=>new Rect(234*scale,Screen.height-222*scale,570*scale,30*scale);
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
            draw_threat_intelligence();
            if(game.siege!=null)
            {
                var siege=game.siege;
                string countdown=siege.remaining>0?$"{Mathf.CeilToInt(siege.remaining)/60:00}:{Mathf.CeilToInt(siege.remaining)%60:00}":"进攻中";
                string label=siege.config.defense_test?$"防守测试 · {countdown} · {(siege.waves==0?"全图尸群来袭":"守住阵地")}":$"尸潮 {siege.waves} · 下一波 {countdown}";
                panel(new Rect(Screen.width/2-240*scale,8*scale,480*scale,38*scale));
                GUI.Label(new Rect(Screen.width/2-230*scale,10*scale,460*scale,34*scale),label+$" · 压力 {siege.pressure:P0}",title);
            }
            float s=scale,y=Screen.height-190*s;
            panel(new Rect(0,Screen.height-242*s,224*s,242*s));
            GUI.Label(new Rect(14*s,Screen.height-238*s,200*s,25*s),"战术地图   256 × 256",small);
            float centre_x=234*s,resources_x=Screen.width-190*s,commands_x=resources_x-264*s;
            var selection_area=new Rect(centre_x,y,commands_x-centre_x-10*s,190*s);
            var command_area=new Rect(commands_x,y,254*s,190*s);
            if(game.headquarters_selected){draw_structure_labels();draw_headquarters(selection_area);draw_production_commands(command_area);}
            else {draw_selection(selection_area);draw_commands(command_area);}
            draw_resources(new Rect(resources_x,y,190*s,190*s));
            draw_style_toolbar();
            if(game.construction.active)
            {
                var hint=new Rect(Mathf.Clamp(Input.mousePosition.x+18*s,8*s,Screen.width-520*s),Mathf.Clamp(Screen.height-Input.mousePosition.y-64*s,8*s,Screen.height-260*s),505*s,54*s);
                panel(hint);GUI.Label(new Rect(hint.x+8*s,hint.y+3*s,hint.width-16*s,24*s),game.construction.notice,small);
                GUI.Label(new Rect(hint.x+8*s,hint.y+27*s,hint.width-16*s,22*s),"左键放置 · 右键 / Esc 取消 · 资源在确认位置时扣除",small);
            }
            if(show_help)
            {
                panel(new Rect(15*s,92*s,600*s,106*s));
                GUI.Label(new Rect(28*s,100*s,575*s,28*s),"左拖框选 · 右键移动 · A 后左键进攻 · Q 巡逻",text);
                GUI.Label(new Rect(28*s,130*s,575*s,28*s),"Ctrl + 数字保存编队 · 数字/卡片召回 · 双击聚焦",text);
                GUI.Label(new Rect(28*s,160*s,575*s,28*s),"贴边 / 方向键移镜头 · Z 尸群图鉴 · 滚轮缩放 · H 隐藏",small);
            }
        }
        private void draw_style_toolbar()
        {
            Rect area=style_toolbar();panel(area);float width=area.width/3;
            for(int i=0;i<3;i++)
            {
                GUI.backgroundColor=i==VisualStyles.index?GOLD:Color.gray;
                if(GUI.Button(new Rect(area.x+i*width+3*scale,area.y+3*scale,width-6*scale,24*scale),$"F{i+5}  {VisualStyles.all[i].name}",button))game.switch_visual_style(i);
            }
            GUI.backgroundColor=Color.white;
        }
        private void draw_resources(Rect area)
        {
            float s=scale;
            panel(area);
            var e=game.economy;
            string[] names={"粮食","木材","石料","铁矿","箭矢","火药"};
            float[] values={e.food,e.wood,e.stone,e.iron,e.arrows,e.gunpowder};
            GUI.Label(new Rect(area.x+12*s,area.y+3*s,area.width-24*s,24*s),"聚落资源",title);
            for(int i=0;i<6;i++)
            {
                float y=area.y+(31+i*22)*s;
                GUI.Label(new Rect(area.x+12*s,y,55*s,22*s),names[i],text);
                fill(new Rect(area.x+74*s,y+2*s,102*s,19*s),new Color(.035f,.046f,.05f));
                var value_style=new GUIStyle(text){alignment=TextAnchor.MiddleRight};
                GUI.Label(new Rect(area.x+77*s,y,95*s,22*s),values[i].ToString("F0"),value_style);
            }
            GUI.Label(new Rect(area.x+12*s,area.y+166*s,area.width-24*s,23*s),$"部队 {game.current.living_soldiers} / {game.current.soldier_count}",small);
        }
        private void draw_selection(Rect area)
        {
            panel(area);float s=scale,x=area.x+14*s,y=area.y;
            int count=game.controls.selected_count(),rounds=0,melee=0;float health=0,max_health=0;
            for(int i=0;i<game.current.soldier_count;i++)
                if(game.current.selected[i]&&game.current.health[i]>0){health+=game.current.health[i];max_health+=game.current.stats_for(i).health;rounds+=game.current.ammunition[i];if(game.current.uses_melee(i))melee++;}
            draw_unit_icon(new Rect(x,y+10*s,50*s,60*s),count>0);
            GUI.Label(new Rect(x+66*s,y+6*s,area.width-90*s,26*s),count>0?$"神机营   × {count}":"未选择部队",title);
            draw_unit_stats(new Rect(x+66*s,y+32*s,area.width-94*s,44*s),count>0);
            var bar=new Rect(x,y+81*s,Mathf.Max(40*s,area.width-28*s),5*s);
            fill(bar,new Color(.16f,.18f,.16f));fill(new Rect(bar.x,bar.y,bar.width*(max_health>0?health/max_health:0),bar.height),new Color(.33f,.57f,.30f));
            GUI.Label(new Rect(x,y+89*s,area.width-28*s,22*s),count>0?$"平均生命 {health/count:0.#}/{max_health/count:0.#} · 携弹 {rounds}/{count*UnitBalance.human.ammunition_capacity} · 拼刀 {melee} 人 · T 切换":"框选部队查看属性 · 下方数字卡片为保存的编队",small);
            float card_width=(area.width-28*s)/10;
            for(int slot=0;slot<10;slot++)
            {
                int group=(slot+1)%10,n=game.controls.group_count(group);
                var card=new Rect(x+slot*card_width,y+116*s,card_width-4*s,42*s);
                GUI.enabled=game.can_control;
                if(GUI.Button(card,"",button))game.controls.click_group(group);
                GUI.enabled=true;
                GUI.Label(new Rect(card.x+4*s,card.y+2*s,card.width-6*s,20*s),group.ToString(),small);
                draw_unit_icon(new Rect(card.x+card.width*.50f,card.y+5*s,card.width*.38f,22*s),n>0);
                GUI.Label(new Rect(card.x+5*s,card.y+22*s,card.width-6*s,20*s),n>0?n.ToString():"—",small);
            }
            GUI.Label(new Rect(x,y+161*s,area.width-28*s,24*s),game.is_paused?"已暂停 · 空格继续":"Ctrl+数字编队 · 双击数字 / F2 / ` 聚焦主力 · H 帮助",small);
        }
        /// <summary>Single-unit attributes from the same authored balance used by combat.</summary>
        private void draw_unit_stats(Rect area,bool selected)
        {
            if(!selected)return;
            var stats=UnitBalance.human;
            string[] values={
                $"单兵生命 {stats.health:0}",$"伤害 {stats.damage:0}",
                $"射程 {stats.attack_range:0.#} 格",$"攻击间隔 {stats.attack_interval:0.##} 秒",
                $"移速 {stats.move_speed:0.##}",$"视野 {UnitBalance.config.human_sight:0.#} 格",
                $"噪音 {UnitBalance.human_noise(stats):0.#} 格",$"刀 {stats.melee_damage:0} / {stats.melee_attack_interval:0.#}秒"};
            float width=area.width/4;
            for(int i=0;i<values.Length;i++)
                GUI.Label(new Rect(area.x+i%4*width,area.y+i/4*22*scale,width,22*scale),values[i],small);
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
            string[] labels={"进攻\nA","移动\nM","停止\nS","巡逻\nQ","全选\nF2","聚焦部队\nC","主基地\nB","枪 / 刀\nT"};
            float width=(area.width-24*s)/3;
            GUI.enabled=game.can_control;
            for(int i=0;i<labels.Length;i++)
                if(GUI.Button(new Rect(area.x+12*s+i%3*width,area.y+32*s+i/3*43*s,width-5*s,39*s),labels[i],button))execute_command(i);
            GUI.enabled=true;
            string mode=game.controls.pending=="Attack"?"进攻：左键指定目标":game.controls.pending=="Move"?"移动：左键指定地点":game.controls.pending=="Patrol"?"巡逻：左键指定终点":"待命 · 右键移动";
            GUI.Label(new Rect(area.x+12*s,area.y+164*s,area.width-24*s,23*s),mode,small);
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
                case 5:game.focus_camera(game.controls.selection_center());break;
                case 6:game.select_headquarters(false);break;
                case 7:game.toggle_selected_weapon();break;
            }
        }
        private void draw_headquarters(Rect area)
        {
            panel(area);float s=scale,x=area.x+14*s,y=area.y;
            GUI.Label(new Rect(x,y+5*s,area.width-28*s,26*s),"镇守府 · 主基地",title);
            GUI.Label(new Rect(x,y+33*s,area.width-28*s,22*s),"统一建造与训练入口 · 资源设施完工后自动采集",text);
            GUI.Label(new Rect(x,y+58*s,area.width-28*s,22*s),"选建筑 → 鼠标绿模放置 · 弹药库20格补给 · F2返回部队",small);
            var production=game.production;
            string active=production.queue.Count==0?"队列空闲":$"{production.queue[0].name}  {production.elapsed:0.0} / {production.queue[0].seconds:0} 秒";
            GUI.Label(new Rect(x,y+83*s,area.width-28*s,23*s),active,text);
            var bar=new Rect(x,y+110*s,area.width-28*s,6*s);fill(bar,new Color(.18f,.17f,.13f));
            fill(new Rect(bar.x,bar.y,bar.width*production.progress,bar.height),GOLD);
            for(int i=0;i<production.queue.Count;i++)
                GUI.Label(new Rect(x+i*(area.width-28*s)/8,y+120*s,(area.width-28*s)/8,25*s),$"{i+1}.{production.queue[i].name.Replace("训练", "").Replace("建造", "")}",small);
            GUI.Label(new Rect(x,y+156*s,area.width-28*s,27*s),production.notice,small);
        }
        private void draw_production_commands(Rect area)
        {
            panel(area);float s=scale,width=(area.width-24*s)/3;
            GUI.Label(new Rect(area.x+12*s,area.y+4*s,area.width-24*s,26*s),"基地生产",title);
            GUI.enabled=game.can_control;
            var recipes=game.production.config.recipes;
            for(int i=0;i<9;i++)
            {
                string label=i<recipes.Length?recipes[i].name.Replace("训练", "").Replace("建造", "")+(i==0?"\n训练":"\n建造"):"取消末项";
                string tip=i<recipes.Length?HeadquartersProduction.cost_label(recipes[i]):"全额退还末项资源";
                if(GUI.Button(new Rect(area.x+12*s+i%3*width,area.y+32*s+i/3*43*s,width-5*s,39*s),new GUIContent(label,tip),button))
                {
                    if(i==0)game.production.enqueue(i,game.current.reserve_soldiers);
                    else if(i<recipes.Length)game.construction.begin(i);
                    else game.production.cancel_last();
                }
            }
            GUI.enabled=true;
            GUI.Label(new Rect(area.x+12*s,area.y+164*s,area.width-24*s,23*s),string.IsNullOrEmpty(GUI.tooltip)?"悬停查看费用 · 队列上限 8":GUI.tooltip,small);
        }
        private void draw_structure_labels()
        {
            foreach(var region in game.map.regions)
            {
                bool headquarters=region.label=="COMMAND HALL";
                bool initial_depot=region.label=="AMMUNITION DEPOT";
                if(!headquarters&&!initial_depot&&!region.label.StartsWith("BUILT:"))continue;
                string id=headquarters?"":initial_depot?"depot":region.label.Substring(6),label=headquarters?"镇守府":"弹药库 · 20格补给";
                var facility=game.construction.facilities.Find(entry=>entry.region.bounds==region.bounds);
                bool complete=headquarters||initial_depot||facility!=null&&facility.complete;
                if(!headquarters)
                    foreach(var recipe in game.production.config.recipes)
                        if(recipe.id==id)label=recipe.name.Replace("建造", "")+(complete?id=="depot"?" · 20格补给":$" · +{facility?.recipe.yield_per_minute??0:0}/分":" · 建造中");
                var world=new Vector3(region.bounds.center.x,headquarters?7.7f:complete?4.7f:1.1f,region.bounds.center.z);
                Vector3 screen=Camera.main.WorldToScreenPoint(world);
                var rect=new Rect(screen.x-65*scale,Screen.height-screen.y,130*scale,22*scale);
                if(screen.z<=0||rect.yMax>Screen.height-195*scale||rect.x<0||rect.xMax>Screen.width)continue;
                fill(rect,new Color(.08f,.07f,.045f,.9f));
                GUI.Label(rect,label,new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
            }
        }
    }
}
