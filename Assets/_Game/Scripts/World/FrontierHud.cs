using UnityEngine;
using ZombieGame.Combat;

namespace ZombieGame.World
{
    /// <summary>Readable resource ribbon and clickable command deck. World input never consumes panel clicks.</summary>
    public sealed class FrontierHud
    {
        private readonly FrontierGame game;
        private GUIStyle text,number,button,title;
        private Font font;
        public bool show_help;
        private static float scale => Mathf.Clamp(Screen.height/900f,.8f,2.5f);
        public static bool contains(Vector2 point) => point.y<86*scale ||
            (point.y>Screen.height-185*scale && point.x<Screen.width-270*scale);
        public FrontierHud(FrontierGame game) { this.game=game; }
        private void prepare_styles()
        {
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"PingFang SC","Arial Unicode MS","Arial"},18);
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=Mathf.RoundToInt(16*scale),alignment=TextAnchor.MiddleLeft};
            text.normal.textColor=new Color(.87f,.86f,.77f);
            number=new GUIStyle(text){fontSize=Mathf.RoundToInt(23*scale),fontStyle=FontStyle.Bold};
            title=new GUIStyle(text){fontSize=Mathf.RoundToInt(18*scale),fontStyle=FontStyle.Bold};
            title.normal.textColor=new Color(.91f,.73f,.39f);
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=Mathf.RoundToInt(16*scale),wordWrap=true};
        }
        private static void panel(Rect area)
        {
            GUI.color=new Color(.075f,.10f,.115f,.97f);GUI.DrawTexture(area,Texture2D.whiteTexture);
            GUI.color=new Color(.60f,.47f,.25f);GUI.DrawTexture(new Rect(area.x,area.y,area.width,2*scale),Texture2D.whiteTexture);GUI.color=Color.white;
        }
        public void draw()
        {
            prepare_styles();GUI.matrix=Matrix4x4.identity;
            float s=scale;
            panel(new Rect(0,0,Screen.width,86*s));
            var economy=game.economy;
            string[] names={"粮食","木材","石料","铁矿","箭矢","火药"};
            float[] values={economy.food,economy.wood,economy.stone,economy.iron,economy.arrows,economy.gunpowder};
            float cell=(Screen.width-185*s)/6;
            for(int i=0;i<6;i++)
            {
                GUI.Label(new Rect(16*s+i*cell,5*s,cell,25*s),names[i],text);
                GUI.Label(new Rect(16*s+i*cell,32*s,cell,40*s),values[i].ToString("F0"),number);
            }
            GUI.Label(new Rect(Screen.width-180*s,8*s,175*s,28*s),"边境聚落 · 256×256",title);
            GUI.Label(new Rect(Screen.width-180*s,40*s,175*s,30*s),$"部队 {game.current.soldier_count-game.current.dead_soldiers} / 400",text);
            float y=Screen.height-185*s,w=Screen.width-270*s;
            panel(new Rect(0,y,w,185*s));
            GUI.Label(new Rect(16*s,y+9*s,225*s,28*s),$"神机营 · 已选 {game.controls.selected_count()}",title);
            GUI.Label(new Rect(16*s,y+42*s,225*s,55*s),game.is_paused?"已暂停 · 空格继续":"右键移动 / A + 左键进攻\n火药不足时停止射击",text);
            float x=250*s,bw=Mathf.Max(55*s,(w-x-18*s)/4);
            GUI.enabled=game.can_control;
            if(GUI.Button(new Rect(x,y+15*s,bw-5*s,43*s),"进攻 [A]",button))game.controls.arm("Attack");
            if(GUI.Button(new Rect(x+bw,y+15*s,bw-5*s,43*s),"移动 [M]",button))game.controls.arm("Move");
            if(GUI.Button(new Rect(x+bw*2,y+15*s,bw-5*s,43*s),"巡逻 [Q]",button))game.controls.arm("Patrol");
            if(GUI.Button(new Rect(x+bw*3,y+15*s,bw-5*s,43*s),"停止 [S]",button)){game.controls.issue_selected(SoldierOrder.Stop,Vector3.zero);game.controls.cancel_command();}
            if(GUI.Button(new Rect(x,y+65*s,bw-5*s,43*s),"全选 [F2]",button))game.controls.select_all();
            if(GUI.Button(new Rect(x+bw,y+65*s,bw-5*s,43*s),"占领 [E]",button))game.claim_site();
            if(GUI.Button(new Rect(x+bw*2,y+65*s,bw-5*s,43*s),economy.powder_works?"火药坊：开":"火药坊：停",button))economy.powder_works=!economy.powder_works;
            if(GUI.Button(new Rect(x+bw*3,y+65*s,bw-5*s,43*s),economy.arrow_works?"箭坊：开":"箭坊：停",button))economy.arrow_works=!economy.arrow_works;
            GUI.enabled=true;
            GUI.Label(new Rect(16*s,y+114*s,w-30*s,30*s),"当前命令："+game.controls.pending+"  |  "+game.controls.notice,text);
            GUI.Label(new Rect(16*s,y+145*s,w-30*s,28*s),"方向键移镜头 · 滚轮拉近 · C 看部队 · Home 回基地 · F 全图 · H 帮助",text);
            if(show_help)
            {
                panel(new Rect(15*s,100*s,590*s,100*s));
                GUI.Label(new Rect(28*s,110*s,560*s,85*s),"左拖框选 · Ctrl+数字编队 · 双击数字聚焦\nV 调试开图 · 空格暂停\n占领资源：选中部队靠近 5 格，清掉周围 10 格僵尸。",text);
            }
        }
    }
}
