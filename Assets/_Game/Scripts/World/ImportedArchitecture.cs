using UnityEngine;

namespace ZombieGame.World
{
    /// <summary>Fits original textured imports to authored footprints without adding colliders or statistics.</summary>
    public static class ImportedArchitecture
    {
        public static GameObject place(LandscapeRegion region,Transform parent)
        {
            string id=asset_for(region.label);
            if(id==null)return null;
            var prefab=Resources.Load<GameObject>("CoastalBuildings/"+id);
            if(prefab==null)throw new System.InvalidOperationException("Missing imported architecture: "+id);
            var root=new GameObject(region.label);root.transform.SetParent(parent,false);
            var model=Object.Instantiate(prefab,root.transform);model.name=id;
            if(region.label=="STONE WALL SIDE")model.transform.rotation=Quaternion.Euler(0,90,0);
            var renderers=model.GetComponentsInChildren<Renderer>();
            foreach(var renderer in renderers){renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            bool wall=region.label.Contains("WALL");
            float original_y_scale=model.transform.localScale.y;
            float scale=Mathf.Min(region.bounds.size.x/bounds.size.x,region.bounds.size.z/bounds.size.z);
            model.transform.localScale*=scale;
            if(wall)model.transform.localScale=new Vector3(model.transform.localScale.x,original_y_scale*3.2f/bounds.size.y,model.transform.localScale.z);
            if(region.label.Contains("TOWER")||region.label=="OUTPOST")model.transform.localScale=new Vector3(model.transform.localScale.x,original_y_scale*4.2f/bounds.size.y,model.transform.localScale.z);
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
            root.transform.position=new Vector3(region.bounds.center.x,0,region.bounds.center.z);
            return root;
        }
        private static string asset_for(string label)
        {
            switch(label)
            {
                case "COMMAND HALL":return "Headquarters";
                case "PASTURE":return "Pasture";
                case "STONE WALL":case "STONE WALL SIDE":return "WallMiddle";
                case "WALL PIER":return "Wall";
                case "GATE TOWER":return "GateTower";
                case "OUTPOST":return "GateTower";
                case "FIRE TOWER":return "FireTower";
                case "GREEK FIRE — ANIMATION DISPLAY":return "GreekFire";
                case "BARRACKS":return "Barrier";
                default:return "Workshop";
            }
        }
        public static void place_equipment_display(Transform parent)
        {
            place(new LandscapeRegion(-94,-83,3,4,3,LandscapeKind.Building,"GREEK FIRE — ANIMATION DISPLAY"),parent);
        }
    }
}
