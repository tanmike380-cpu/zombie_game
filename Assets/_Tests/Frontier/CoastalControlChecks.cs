using System;
using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Controls;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    /// <summary>Disposable input-handler checks; not a replacement controller or physical keyboard test.</summary>
    public static class CoastalControlChecks
    {
        public static void check_briefing(FrontierGame game)
        {
            var controls=game.controls;
            var saved_focus=game.camera_focus;
            var saved_selection=(bool[])game.current.selected.Clone();
            var saved_orders=(SoldierOrder[])game.current.orders.Clone();
            try
            {
                controls.select_all();
                var minimap=controls.minimap_bounds;
                send_mouse(controls,EventType.MouseDown,0,minimap.center);
                send_mouse(controls,EventType.MouseDrag,0,minimap.center+Vector2.right*minimap.width*.1f);
                require(controls.minimap_dragging&&Mathf.Abs(game.camera_focus.x-25.6f)<.01f,"briefing minimap drag moves camera");
                send_mouse(controls,EventType.MouseUp,0,minimap.center);
                send_mouse(controls,EventType.MouseDown,1,new Vector2(Screen.width*.5f,Screen.height*.4f));
                for(int index=0;index<saved_orders.Length;index++)
                    require(game.current.orders[index]==saved_orders[index],"briefing blocks world orders");
                require(!controls.minimap_dragging&&!game.can_control,"briefing capture releases without starting battle");
            }
            finally
            {
                controls.cancel_pointer_capture();controls.cancel_command();
                Array.Copy(saved_selection,game.current.selected,saved_selection.Length);
                game.focus_camera(saved_focus);
            }
            Debug.Log("[CoastalControls] PASS paused briefing minimap capture and blocked world orders");
        }

        public static void check_live_orders(FrontierGame game)
        {
            var controls=game.controls;
            var saved_focus=game.camera_focus;
            float saved_zoom=Camera.main.orthographicSize;
            var saved_selection=(bool[])game.current.selected.Clone();
            int unit=Array.FindIndex(game.current.health,health=>health>0);
            require(unit>=0&&unit<game.current.soldier_count,"living defender available");
            try
            {
                Camera.main.orthographicSize=12;
                game.focus_camera(game.current.positions[unit]);
                Vector2 center=gui_point(game.current.positions[unit]+Vector3.up*.6f);
                send_mouse(controls,EventType.MouseDown,0,center-Vector2.one*12);
                send_mouse(controls,EventType.MouseUp,0,center+Vector2.one*12);
                require(game.current.selected[unit]&&controls.selected_count()>0,"world mouse rectangle selects original defender");
                int selected_count=controls.selected_count();
                var destination=gui_point(game.current.positions[unit]+Vector3.back*2);
                require(!game.pointer_over_ui(destination),"world destination is outside HUD");
                send_mouse(controls,EventType.MouseDown,1,destination);
                require(game.current.orders[unit]==SoldierOrder.Move,$"right-click issues production move; unit={unit} selected={selected_count} notice={controls.notice} start={game.current.positions[unit]} goal={game.current.order_goals[unit]} enabled={game.current.crowd.agents[unit].enabled} onMesh={game.current.crowd.agents[unit].isOnNavMesh}");
                controls.arm("Attack");send_mouse(controls,EventType.MouseDown,0,destination);
                require(game.current.orders[unit]==SoldierOrder.AttackMove,"armed A-left-click issues production attack move");
                controls.store_group(1);controls.select_all();controls.recall_group(1,false);
                require(controls.selected_count()==selected_count&&game.current.selected[unit],"control group restores mouse selection");
                controls.issue_selected(SoldierOrder.Stop,Vector3.zero);
                controls.press_select_all(10);controls.press_select_all(10.2f);
                require(controls.selected_count()==game.current.living_soldiers,"select-all includes all living defenders");
                require(Vector3.Distance(game.camera_focus,controls.selection_center())<.01f,"double select-all focuses dense army");
                game.select_headquarters(false);
                require(game.headquarters_selected,"headquarters command panel opens");
            }
            finally
            {
                controls.cancel_pointer_capture();controls.cancel_command();
                controls.selection_changed?.Invoke();
                Array.Copy(saved_selection,game.current.selected,saved_selection.Length);
                Camera.main.orthographicSize=saved_zoom;game.focus_camera(saved_focus);
            }
            Debug.Log("[CoastalControls] PASS production mouse box/right-move/A-left-attack/group recall/double-all/HQ panel; physical keyboard checked separately");
        }

        private static Vector2 gui_point(Vector3 world)
        {
            Vector3 screen=Camera.main.WorldToScreenPoint(world);
            return new Vector2(screen.x,Screen.height-screen.y);
        }
        private static void send_mouse(RtsBattleInput controls,EventType type,int button,Vector2 point)
        {controls.handle_mouse(new Event{type=type,button=button,mousePosition=point});}
        private static void require(bool condition,string message)
        {
            if(condition)return;
            Debug.LogError("[CoastalControls] FAIL "+message);Application.Quit(1);
            throw new InvalidOperationException("[CoastalControls] FAIL "+message);
        }
    }
}
