using UnityEngine;

namespace ZombieGame.World
{
    public sealed partial class FrontierLandscape
    {
        private void add_architecture_details(Bounds bounds,bool headquarters,float height)
        {
            Vector3 p=new Vector3(bounds.center.x,0,bounds.center.z);
            Color accent=style.color(style.accent);
            if(style.id=="fortress")
            {
                // Broad masonry buttresses and armoured shutters: military, not a space-age gun.
                for(int side=-1;side<=1;side+=2)
                {
                    for(int row=-1;row<=1;row++)
                        add(cube,p+new Vector3(side*(bounds.extents.x-.18f),1.05f,row*bounds.extents.z*.72f),new Vector3(.50f,2.1f,.64f),STONE*.66f);
                    add(cube,p+new Vector3(side*bounds.extents.x*.75f,1.65f,-bounds.extents.z-.08f),new Vector3(.85f,.85f,.14f),TILE);
                    add(cube,p+new Vector3(side*bounds.extents.x*.75f,1.65f,-bounds.extents.z-.16f),new Vector3(.62f,.09f,.06f),accent);
                }
                add(cube,p+new Vector3(0,height+.32f,-bounds.extents.z-.13f),new Vector3(bounds.size.x,.22f,.18f),STONE*.65f);
                if(headquarters)for(int side=-1;side<=1;side+=2)
                {
                    add(cube,p+new Vector3(side*3.5f,3,-2.4f),new Vector3(1.0f,1.9f,1.0f),STONE*.7f);
                    add(roof,p+new Vector3(side*3.5f,3.95f,-2.4f),new Vector3(1.4f,.55f,1.4f),TILE);
                }
            }
            else if(style.id=="dusk")
            {
                // Copper cistern, stacked workshops, lanterns and visible service hardware.
                add(rock,p+new Vector3(bounds.extents.x-.8f,2,bounds.extents.z-.8f),new Vector3(1.15f,2.8f,1.15f),accent*.75f);
                for(int i=0;i<3;i++)add(cube,p+new Vector3(bounds.extents.x-.8f,1.25f+i*.7f,bounds.extents.z-.8f),new Vector3(1.17f,.08f,1.17f),TIMBER);
                add(cube,p+new Vector3(bounds.extents.x-.8f,3.5f,bounds.extents.z-.8f),new Vector3(.32f,1.0f,.32f),STONE*.65f);
                for(int side=-1;side<=1;side+=2)
                {
                    add(cube,p+new Vector3(side*1.2f,2.1f,-bounds.extents.z-.12f),new Vector3(.28f,.45f,.25f),accent*1.4f);
                    add(cube,p+new Vector3(side*1.2f,2.4f,-bounds.extents.z-.12f),new Vector3(.36f,.12f,.32f),TIMBER);
                }
                if(headquarters)
                {
                    add(rock,p+new Vector3(0,5.0f,-bounds.extents.z*.6f),new Vector3(.95f,.95f,.12f),accent);
                    add(cube,p+new Vector3(0,5.1f,-bounds.extents.z*.6f-.08f),new Vector3(.045f,.35f,.05f),TIMBER);
                }
            }
            else
            {
                // Dougong brackets and layered red timber cornices under the curved tile roof.
                for(float x=bounds.min.x+.6f;x<bounds.max.x;x+=1.0f)
                    for(int side=-1;side<=1;side+=2)
                    {
                        add(cube,new Vector3(x,height+.14f,p.z+side*bounds.extents.z),new Vector3(.56f,.14f,.46f),TIMBER);
                        add(cube,new Vector3(x,height-.03f,p.z+side*bounds.extents.z),new Vector3(.22f,.25f,.30f),TIMBER*.8f);
                    }
                for(int side=-1;side<=1;side+=2)
                    add(cube,p+new Vector3(side*1.15f,1.6f,-bounds.extents.z-.13f),new Vector3(.35f,1.6f,.09f),style.color(style.cloth));
                for(int step=0;step<3;step++)add(cube,p+new Vector3(0,.06f+step*.07f,-bounds.extents.z-.4f+step*.1f),new Vector3(2.8f-step*.15f,.12f,.7f),STONE);
            }
        }
        private void add_settlement_details()
        {
            // Ground contact paving and prop clusters; no new invisible collision blockers.
            for(int i=0;i<52;i++)
            {
                float x=-120+i*1.15f;
                add(cube,new Vector3(x,.025f,-87),new Vector3(1.0f,.035f,2.6f),STONE*.76f);
            }
            foreach(var centre in new[]{new Vector3(-100,0,-101),new Vector3(-84,0,-108),new Vector3(-72,0,-102)})
                for(int i=0;i<5;i++)
                {
                    var p=centre+new Vector3(i%3*.44f,.25f+i/3*.35f,0);
                    add(cube,p,new Vector3(.4f,.5f,.4f),TIMBER*1.2f);
                    add(cube,p+Vector3.up*.08f,new Vector3(.42f,.055f,.42f),STONE*.55f);
                }
        }
    }
}
