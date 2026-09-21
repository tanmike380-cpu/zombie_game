using UnityEngine;

namespace ZombieGame.Controls
{
    /// <summary>Screen-relative edge scrolling. Input is local to the focused Game view/window,
    /// not the editor or desktop bounds. Thin physical edges remain active over the RTS HUD.</summary>
    public static class RtsCameraPan
    {
        public static Vector2 edge_direction(Vector2 mouse,Vector2 size,bool focused,float edge_pixels=16)
        {
            // Native right/top edge coordinates can equal the drawable width/height.
            // Use inclusive bounds, like the left/bottom zero coordinates, on every frame.
            if(!focused||size.x<=0||size.y<=0||mouse.x<0||mouse.y<0||mouse.x>size.x||mouse.y>size.y)return Vector2.zero;
            float x=mouse.x<edge_pixels?-1:mouse.x>=size.x-edge_pixels?1:0;
            float y=mouse.y<edge_pixels?-1:mouse.y>=size.y-edge_pixels?1:0;
            return Vector2.ClampMagnitude(new Vector2(x,y),1);
        }
        public static Vector3 world_direction(Vector2 input,Quaternion camera_rotation)
        {
            Vector3 right=camera_rotation*Vector3.right,forward=camera_rotation*Vector3.forward;
            right.y=forward.y=0;
            return right.normalized*input.x+forward.normalized*input.y;
        }
    }
}
