using UnityEngine;

namespace ZombieGame.World
{
    /// <summary>Places authored assemblies verbatim; only standalone legacy assets use footprint fitting.</summary>
    public static class ImportedArchitecture
    {
        public static float maximum_aspect_error {get;private set;}
        public static GameObject place(LandscapeRegion region,Transform parent)
        {
            string id=region.art_id??asset_for(region.label);
            if(id==null)return null;
            var prefab=Resources.Load<GameObject>("CoastalBuildings/"+id);
            if(prefab==null)throw new System.InvalidOperationException("Missing imported architecture: "+id);
            var root=new GameObject(region.label);root.transform.SetParent(parent,false);
            var model=Object.Instantiate(prefab,root.transform);model.name=id;
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)throw new System.InvalidOperationException("Imported architecture has no renderer: "+id);
            foreach(var renderer in renderers){renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;}
            if(region.art_authored_transform)
            {
                // The child retains FBX unit/up-axis conversion. Apply the Blender handoff only once.
                // Never recenter each slice: its asymmetric cross section shares the assembly origin.
                root.transform.SetPositionAndRotation(region.art_position,Quaternion.Euler(0,region.art_yaw,0));
                root.transform.localScale=Vector3.one*region.art_scale;
                return root;
            }
            fit_standalone_model(region,root,model,renderers);
            return root;
        }

        private static void fit_standalone_model(LandscapeRegion region,GameObject root,GameObject model,Renderer[] renderers)
        {
            // Keep the FBX's axis conversion; yaw is additional, not a replacement rest rotation.
            if(region.art_sideways||region.label=="STONE WALL SIDE"||region.label=="FORTRESS WALL SIDE")
                model.transform.localRotation=Quaternion.Euler(0,90,0)*model.transform.localRotation;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            Vector3 original_size=bounds.size;
            float scale=Mathf.Min(region.bounds.size.x/bounds.size.x,region.bounds.size.z/bounds.size.z);
            model.transform.localScale*=scale;
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float x_ratio=bounds.size.x/original_size.x,y_ratio=bounds.size.y/original_size.y,z_ratio=bounds.size.z/original_size.z;
            maximum_aspect_error=Mathf.Max(maximum_aspect_error,Mathf.Abs(x_ratio-y_ratio)/scale,Mathf.Abs(x_ratio-z_ratio)/scale);
            model.transform.position+=new Vector3(-bounds.center.x,region.art_id==null?-bounds.min.y:0,-bounds.center.z);
            root.transform.position=new Vector3(region.bounds.center.x,0,region.bounds.center.z);
        }
        private static string asset_for(string label)
        {
            switch(label)
            {
                case "COMMAND HALL":return "Headquarters";
                case "PASTURE":return "Pasture";
                case "STONE WALL":case "STONE WALL SIDE":return "WallMiddle";
                case "FORTRESS WALL":case "FORTRESS WALL SIDE":return "Wall";
                case "WALL PIER":return "Wall";
                case "GATE TOWER":return "GateTower";
                case "OUTPOST":return "GateTower";
                case "FIRE TOWER":return "FireTower";
                case "GREEK FIRE — ANIMATION DISPLAY":return "GreekFire";
                case "BARRACKS":return "Barrier";
                default:return "Workshop";
            }
        }
    }
}
