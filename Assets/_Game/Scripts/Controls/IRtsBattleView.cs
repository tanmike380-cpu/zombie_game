using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Vision;

namespace ZombieGame.Controls
{
    public interface IRtsBattleView
    {
        BattleSimulation current { get; }
        CombatFog current_fog { get; }
        bool can_control { get; }
        bool reveal_map { get; }
        Vector3 camera_focus { get; }
        void focus_camera(Vector3 point);
        bool pointer_over_ui(Vector2 point);
        Color32 obstacle_color(int index);
    }
}
