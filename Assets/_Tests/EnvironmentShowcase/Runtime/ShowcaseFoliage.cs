using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Chunked instancing of approved source meshes, without mesh edits, colliders or per-clump scripts.</summary>
    public sealed class ShowcaseFoliage : IDisposable
    {
        private const int MAX_BATCH_SIZE = 1023;
        private readonly List<DrawBatch> batches = new List<DrawBatch>();
        private readonly List<Material> materials = new List<Material>();
        private readonly EnvironmentShowcaseConfig config;
        private readonly ShowcaseTerrain ground;
        private readonly System.Random random;
        private readonly Plane[] frustum_planes = new Plane[6];
        public bool show_grass = true;
        public bool show_decorations = true;
        public bool show_rocks = true;
        public bool show_trees = true;
        public bool show_shrubs = true;
        public int grass_instances { get; private set; }
        public int tree_instances { get; private set; }
        public int shrub_instances { get; private set; }
        public int rock_instances { get; private set; }
        public int visible_batches { get; private set; }
        public readonly List<Vector2> tree_positions = new List<Vector2>();
        [Serializable] public sealed class GrassProbe
        {
            public string source;
            public int samples,top_at_or_below_anchor;
            public float world_y_min=float.PositiveInfinity,world_y_max=float.NegativeInfinity;
            public float height_span_min=float.PositiveInfinity,height_span_max;
            public float minimum_top_above_anchor=float.PositiveInfinity,maximum_top_above_anchor;
            public float maximum_terrain_corner_span;
        }
        public readonly List<GrassProbe> grass_probes=new List<GrassProbe>();
        public readonly List<GrassProbe> shrub_probes=new List<GrassProbe>();
        [Serializable] public sealed class VisibilityProbe
        {
            public int grass_matrix_submissions,shrub_matrix_submissions;
            public int grass_camera_distance_rejected,shrub_camera_distance_rejected;
            public string[] material_bindings;
            public string distance_policy = "Flat only: metres from orthographic ground focus; legacy modes retain camera XZ distance.";
        }

        private sealed class SourcePart
        {
            public Mesh mesh;
            public Material material;
            public int submesh_index;
            public Matrix4x4 relative_transform;
            public float lod_min_height = 0;
            public float lod_max_height = float.PositiveInfinity;
            public float source_lod_height;
        }

        private sealed class DrawBatch
        {
            public SourcePart part;
            public Matrix4x4[] matrices;
            public Bounds bounds;
            public float draw_distance;
            public bool is_grass;
            public bool is_rock;
            public bool is_tree;
            public bool is_shrub;
            public float lod_height;
        }

        public ShowcaseFoliage(EnvironmentShowcaseConfig settings, ShowcaseTerrain terrain)
        {
            config = settings;
            ground = terrain;
            random = new System.Random(config.seed);
            var biome = terrain.biome;
            int grass_count = Mathf.RoundToInt(config.grass_count * (biome == null ? 1 : biome.grass_density));
            int tree_count = Mathf.RoundToInt(config.tree_count * (biome == null ? 1 : biome.tree_density));
            if (terrain.concept != null) tree_count = terrain.concept.layout.tree_budget;
            if(config.dark_enabled && config.active_dark_layout.tree_prefabs.Length==0) tree_count=0;
            if (terrain.routes.enabled && (biome == null || biome.tree_density > 0)) tree_count = terrain.routes.layout.tree_budget;
            int shrub_count = Mathf.RoundToInt(config.shrub_count * (biome == null ? 1 : biome.shrub_density));
            int rock_count = Mathf.RoundToInt(config.rock_count * (biome == null ? 1 : biome.rock_density));
            if (config.grass_variants != null && config.grass_variants.Length > 0)
                grass_instances = add_variants(config.grass_variants, grass_count, config.grass_scale_range, "grass");
            else if (config.grass_prefab) grass_instances = add_prefab(config.grass_prefab, grass_count, config.grass_scale_range, "grass");
            tree_instances = add_variants(config.dark_enabled ? config.active_dark_layout.tree_prefabs : config.tree_prefabs, tree_count, config.tree_scale_range, "tree");
            shrub_instances = add_variants(config.shrub_prefabs, shrub_count, config.shrub_scale_range, "shrub");
            rock_instances = add_variants(config.rock_prefabs, rock_count, config.rock_scale_range, "rock");
        }

        private int add_variants(GameObject[] prefabs, int count, Vector2 scale_range, string category)
        {
            if (prefabs == null || prefabs.Length == 0) return 0;
            int total = 0;
            for (int index = 0; index < prefabs.Length; index++)
                if (prefabs[index]) total += add_prefab(prefabs[index], count / prefabs.Length + (index < count % prefabs.Length ? 1 : 0), scale_range, category);
            return total;
        }

        private int add_prefab(GameObject prefab, int count, Vector2 scale_range, string category)
        {
            bool is_short_grass=config.dark_enabled && category=="grass" && prefab.name.StartsWith("GrassBermuda",StringComparison.Ordinal);
            if(config.dark_enabled && category=="grass") scale_range=is_short_grass ? config.active_dark_layout.bermuda_scale_range : config.active_dark_layout.medium_scale_range;
            var parts = read_source_parts(prefab,category);
            if (parts.Count == 0) throw new InvalidOperationException("Approved showcase asset has no renderable static source meshes: " + prefab.name);
            var cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
            var tree_candidates = category == "tree" && (ground.routes.enabled || ground.concept!=null) ? build_forest_candidates() : null;
            int placed = 0;
            int attempts = tree_candidates == null ? count * 12 : tree_candidates.Count;
            for (int attempt = 0; attempt < attempts && placed < count; attempt++)
            {
                float x = (float)random.NextDouble() * config.map_size;
                float z = (float)random.NextDouble() * config.map_size;
                if(config.flat_enabled && (category=="grass" || category=="shrub"))
                {
                    var preset = config.active_dark_layout;
                    int patch_index = random.Next(preset.vegetation_centers.Length);
                    var center = preset.vegetation_centers[patch_index]*config.map_size;
                    var radius = preset.vegetation_radii[patch_index];
                    x = center.x+((float)random.NextDouble()*2-1)*radius.x;
                    z = center.y+((float)random.NextDouble()*2-1)*radius.y;
                }
                else if (tree_candidates != null) { x = tree_candidates[attempt].x; z = tree_candidates[attempt].y; }
                else if (category == "grass" && random.NextDouble() < config.grass_focus_fraction)
                {
                    x = Mathf.Clamp01(config.grass_focus_center.x + ((float)random.NextDouble() * 2 - 1) * config.grass_focus_extent.x) * config.map_size;
                    z = Mathf.Clamp01(config.grass_focus_center.y + ((float)random.NextDouble() * 2 - 1) * config.grass_focus_extent.y) * config.map_size;
                }
                else if (category == "rock" && ground.concept == null && ground.biome != null && ground.biome.plateau_enabled && random.NextDouble() < ground.biome.talus_rock_fraction)
                {
                    var profile = ground.biome;
                    float angle = (float)random.NextDouble() * Mathf.PI * 2;
                    float eroded_radius = 1 + (Mathf.Sin(angle * 7) + Mathf.Sin(angle * 13) * .4f) * profile.erosion_strength;
                    float foot_distance = 1 + (float)random.NextDouble() * (profile.talus_width * .65f) +
                        (profile.dry_basin ? profile.cliff_edge_width * 2 : 0);
                    float physical_radius = new Vector2(Mathf.Cos(angle) * profile.plateau_radius.x,
                        Mathf.Sin(angle) * profile.plateau_radius.y).magnitude;
                    float normalized_distance = eroded_radius + foot_distance / physical_radius;
                    x = profile.plateau_center.x * config.map_size + Mathf.Cos(angle) * profile.plateau_radius.x * normalized_distance;
                    z = profile.plateau_center.y * config.map_size + Mathf.Sin(angle) * profile.plateau_radius.y * normalized_distance;
                    if (x < 0 || z < 0 || x > config.map_size || z > config.map_size) continue;
                }
                if (!can_place_category(x, z, category)) continue;
                if(config.dark_enabled && category=="grass")
                {
                    float plant_patch=Mathf.PerlinNoise(x/12+3.7f,z/12);
                    if(is_short_grass ? plant_patch>.62f : plant_patch<.42f) continue;
                }
                float scale = Mathf.Lerp(scale_range.x, scale_range.y, (float)random.NextDouble());
                Vector3 point = ground.sample_ground(x, z);
                var matrix = Matrix4x4.TRS(point, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0) * prefab.transform.localRotation,
                    prefab.transform.localScale * scale);
                var cell = new Vector2Int(Mathf.FloorToInt(x / config.grass_chunk_size), Mathf.FloorToInt(z / config.grass_chunk_size));
                if(config.dark_enabled && category=="grass") measure_grass_probe(prefab.name,point.y,matrix,parts);
                if(config.flat_enabled && category=="shrub") measure_grass_probe(prefab.name,point.y,matrix,parts,true);
                if (!cells.TryGetValue(cell, out var instances)) { instances = new List<Matrix4x4>(); cells.Add(cell, instances); }
                instances.Add(matrix);
                if (category == "tree") tree_positions.Add(new Vector2(x,z));
                placed++;
            }
            foreach (var cell in cells) build_batches(cell.Value, parts, category);
            return placed;
        }

        private bool can_place_category(float x, float z, string category)
        {
            if(config.flat_enabled && (category=="grass" || category=="shrub"))
            {
                if(x<0 || z<0 || x>config.map_size || z>config.map_size || !ground.can_place_greenery(x,z)) return false;
                float vegetation = ground.concept.vegetation_weight(x,z);
                float exposed = ground.concept.exposed_soil(x,z);
                if(category=="shrub") return vegetation>.20f && vegetation<.85f && exposed<.25f;
                return vegetation>.45f && exposed<.35f && random.NextDouble()<vegetation;
            }
            if (ground.concept != null)
            {
                if (!ground.concept.can_place(x,z)) return false;
                float slope = ground.terrain_data.GetSteepness(x/config.map_size,z/config.map_size);
                if (category == "rock") return slope < 35 && ground.concept.is_local_rock_patch(x,z);
                if (!ground.can_place_greenery(x,z)) return false;
                float density = ground.forest_weight(x,z);
                if (category == "tree") return random.NextDouble() < density;
                if (category == "shrub") return random.NextDouble() < Mathf.Max(.1f,density*.7f);
                if(config.dark_enabled) return random.NextDouble()<Mathf.Lerp(.15f,.95f,Mathf.PerlinNoise(x/5,z/5))*(1-ground.concept.exposed_soil(x,z)*.9f);
                return random.NextDouble() < .7f;
            }
            if (ground.routes.enabled && (ground.routes.measure_river(x,z) < ground.routes.layout.river_half_width + 2 ||
                ground.routes.measure_road(x,z) < ground.routes.layout.corridor_half_width ||
                ground.routes.measure_courtyard(x,z) < ground.routes.layout.landmark_clear_radius)) return false;
            if (z - ground.shoreline_z(x) <= config.shore_blend_width ||
                (!ground.routes.enabled && Mathf.Abs(x - ground.road_x(z)) <= config.road_half_width + config.road_edge_width)) return false;
            float forest = ground.forest_weight(x, z);
            if (category == "rock") return ground.terrain_data.GetSteepness(x / config.map_size, z / config.map_size) < 38 &&
                (ground.talus_weight(x, z) > .2f || forest > .3f || ground.terrain_data.GetSteepness(x / config.map_size, z / config.map_size) > 12);
            if (!ground.can_place_greenery(x, z)) return false;
            if (category == "tree") return forest > .65f && random.NextDouble() < forest;
            if (category == "shrub") return forest > .15f && forest < .9f && random.NextDouble() < .6f;
            float patch = Mathf.PerlinNoise(x / 7 + 12.6f, z / 7);
            return random.NextDouble() < Mathf.Lerp(.18f, .92f, patch) * (1 - forest * .6f);
        }

        private List<Vector2> build_forest_candidates()
        {
            var candidates = new List<Vector2>();
            float spacing = ground.concept!=null ? 2.55f : ground.routes.layout.tree_spacing;
            for (float z = spacing; z < config.map_size - spacing; z += spacing)
                for (float x = spacing; x < config.map_size - spacing; x += spacing)
                {
                    var point = new Vector2(x + ((float)random.NextDouble() - .5f) * spacing * .2f,
                        z + ((float)random.NextDouble() - .5f) * spacing * .2f);
                    if (ground.forest_weight(point.x,point.y) > .75f && ground.can_place_greenery(point.x,point.y)) candidates.Add(point);
                }
            for (int index = candidates.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                var point = candidates[index]; candidates[index] = candidates[other]; candidates[other] = point;
            }
            return candidates;
        }

        private List<SourcePart> read_source_parts(GameObject prefab, string category)
        {
            var parts = new List<SourcePart>();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!filter.sharedMesh || !renderer || !renderer.enabled) continue;
                var source_materials = renderer.sharedMaterials;
                for (int submesh_index = 0; submesh_index < filter.sharedMesh.subMeshCount; submesh_index++)
                {
                    if (submesh_index >= source_materials.Length || !source_materials[submesh_index])
                        throw new InvalidOperationException("Approved source has missing material: " + prefab.name + "/" + filter.name);
                    if (!source_materials[submesh_index].shader || !source_materials[submesh_index].shader.isSupported)
                        throw new InvalidOperationException("Approved showcase source shader unsupported by verified Built-in pipeline: " + prefab.name);
                    var material = new Material(source_materials[submesh_index]) { enableInstancing = true };
                    if (ground.concept != null && material.HasProperty("_AlphaToMask"))
                    {
                        material.SetFloat("_AlphaToMask",1);
                        material.SetFloat("_MipBias",category == "tree" ? .45f : .15f);
                        if(category == "tree") material.SetColor("_Color",new Color(.82f,.87f,.75f,1));
                        if(config.dark_enabled && category=="grass") material.SetColor("_Color",new Color(.82f,.86f,.74f,1));
                    }
                    materials.Add(material);
                    var part = new SourcePart { mesh = filter.sharedMesh, material = material, submesh_index = submesh_index,
                        relative_transform = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix };
                    read_source_lod(prefab, renderer, part);
                    if(ground.concept != null && category=="tree" && part.source_lod_height>0)
                    {
                        // Select the two already-approved source meshes; no re-meshing or extra trees.
                        if(float.IsPositiveInfinity(part.lod_max_height)) part.lod_min_height=ground.concept.layout.tree_near_lod_threshold;
                        else part.lod_max_height=ground.concept.layout.tree_near_lod_threshold;
                    }
                    parts.Add(part);
                }
            }
            return parts;
        }

        private static void read_source_lod(GameObject prefab, Renderer renderer, SourcePart part)
        {
            var group = renderer.GetComponentInParent<LODGroup>();
            if (!group) return;
            var lods = group.GetLODs();
            for (int lod_index = 0; lod_index < lods.Length; lod_index++)
            {
                if (Array.IndexOf(lods[lod_index].renderers, renderer) < 0) continue;
                part.lod_min_height = lods[lod_index].screenRelativeTransitionHeight;
                part.lod_max_height = lod_index == 0 ? float.PositiveInfinity : lods[lod_index - 1].screenRelativeTransitionHeight;
                part.source_lod_height = group.size * (prefab.transform.worldToLocalMatrix * group.transform.localToWorldMatrix).MultiplyVector(Vector3.up).magnitude;
                return;
            }
            throw new InvalidOperationException("Source renderer belongs to LODGroup but no source LOD: " + prefab.name + "/" + renderer.name);
        }

        private void build_batches(List<Matrix4x4> instances, List<SourcePart> parts, string category)
        {
            foreach (var part in parts)
                for (int offset = 0; offset < instances.Count; offset += MAX_BATCH_SIZE)
                {
                    int count = Mathf.Min(MAX_BATCH_SIZE, instances.Count - offset);
                    var matrices = new Matrix4x4[count];
                    var bounds = new Bounds();
                    float lod_height = 0;
                    for (int index = 0; index < count; index++)
                    {
                        matrices[index] = instances[offset + index] * part.relative_transform;
                        lod_height = Mathf.Max(lod_height, part.source_lod_height * instances[offset + index].lossyScale.y);
                        var mesh_bounds = transform_bounds(part.mesh.bounds, matrices[index]);
                        if (index == 0) bounds = mesh_bounds; else bounds.Encapsulate(mesh_bounds);
                    }
                    batches.Add(new DrawBatch { part = part, matrices = matrices, bounds = bounds, lod_height = lod_height, is_grass = category == "grass", is_rock = category == "rock", is_tree = category == "tree", is_shrub = category == "shrub",
                        draw_distance = category == "grass" ? config.grass_draw_distance : category == "tree" ? config.tree_draw_distance :
                            category == "shrub" ? config.shrub_draw_distance : config.rock_draw_distance });
                }
        }

        private static Bounds transform_bounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 x_axis = matrix.MultiplyVector(new Vector3(bounds.extents.x, 0, 0));
            Vector3 y_axis = matrix.MultiplyVector(new Vector3(0, bounds.extents.y, 0));
            Vector3 z_axis = matrix.MultiplyVector(new Vector3(0, 0, bounds.extents.z));
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center), new Vector3(
                Mathf.Abs(x_axis.x) + Mathf.Abs(y_axis.x) + Mathf.Abs(z_axis.x),
                Mathf.Abs(x_axis.y) + Mathf.Abs(y_axis.y) + Mathf.Abs(z_axis.y),
                Mathf.Abs(x_axis.z) + Mathf.Abs(y_axis.z) + Mathf.Abs(z_axis.z)) * 2);
        }

        /// <summary>Once at construction, sample 32 actual submitted matrices per source. Bounds are geometric, not alpha-visible blade coverage.</summary>
        private void measure_grass_probe(string source,float anchor_height,Matrix4x4 instance,List<SourcePart> parts,bool is_shrub=false)
        {
            var probes = is_shrub ? shrub_probes : grass_probes;
            var probe=probes.Find(item=>item.source==source);
            if(probe==null){probe=new GrassProbe{source=source};probes.Add(probe);}
            if(probe.samples>=32) return;
            var bounds=new Bounds();bool first=true;
            foreach(var part in parts)
            {
                var part_bounds=transform_bounds(part.mesh.bounds,instance*part.relative_transform);
                if(first){bounds=part_bounds;first=false;}else bounds.Encapsulate(part_bounds);
            }
            float top=bounds.max.y-anchor_height;
            probe.samples++;
            probe.world_y_min=Mathf.Min(probe.world_y_min,bounds.min.y);
            probe.world_y_max=Mathf.Max(probe.world_y_max,bounds.max.y);
            probe.height_span_min=Mathf.Min(probe.height_span_min,bounds.size.y);
            probe.height_span_max=Mathf.Max(probe.height_span_max,bounds.size.y);
            probe.minimum_top_above_anchor=Mathf.Min(probe.minimum_top_above_anchor,top);
            probe.maximum_top_above_anchor=Mathf.Max(probe.maximum_top_above_anchor,top);
            if(top<=.01f) probe.top_at_or_below_anchor++;
            float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
            foreach(float x in new[]{bounds.min.x,bounds.max.x}) foreach(float z in new[]{bounds.min.z,bounds.max.z})
            {
                float height=ground.sample_ground(x-ground.origin.x,z-ground.origin.z).y;
                minimum=Mathf.Min(minimum,height);maximum=Mathf.Max(maximum,height);
            }
            probe.maximum_terrain_corner_span=Mathf.Max(probe.maximum_terrain_corner_span,maximum-minimum);
        }

        public void draw_batches(Camera camera)
        {
            visible_batches = 0;
            if (!show_decorations || !SystemInfo.supportsInstancing) return;
            GeometryUtility.CalculateFrustumPlanes(camera, frustum_planes);
            foreach (var batch in batches)
            {
                if (batch.is_grass && !show_grass) continue;
                if (batch.is_rock && !show_rocks) continue;
                if (batch.is_tree && !show_trees) continue;
                if (batch.is_shrub && !show_shrubs) continue;
                if (batch.lod_height > 0)
                {
                    float relative_height = batch.lod_height / (camera.orthographicSize * 2);
                    if (relative_height < batch.part.lod_min_height || relative_height >= batch.part.lod_max_height) continue;
                }
                Vector3 planar_delta = batch.bounds.center - measure_distance_anchor(camera);
                planar_delta.y = 0;
                if (planar_delta.sqrMagnitude > batch.draw_distance * batch.draw_distance ||
                    !GeometryUtility.TestPlanesAABB(frustum_planes, batch.bounds)) continue;
                bool cast_shadows = !batch.is_grass && (!batch.is_tree || planar_delta.sqrMagnitude <= config.tree_shadow_draw_distance * config.tree_shadow_draw_distance);
                Graphics.DrawMeshInstanced(batch.part.mesh, batch.part.submesh_index, batch.part.material, batch.matrices,
                    batch.matrices.Length, null, cast_shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    true, 0, camera, LightProbeUsage.Off);
                visible_batches++;
            }
        }

        private Vector3 measure_distance_anchor(Camera camera)
        {
            if(!config.flat_enabled || !camera.orthographic) return camera.transform.position;
            var ray = camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            float distance = (config.active_dark_layout.flat_height-ray.origin.y)/ray.direction.y;
            return ray.GetPoint(distance);
        }

        /// <summary>QA snapshot of actual source matrix submissions, with the former camera-distance exclusions for comparison.</summary>
        public VisibilityProbe sample_visibility(Camera camera)
        {
            var evidence = new VisibilityProbe();
            GeometryUtility.CalculateFrustumPlanes(camera,frustum_planes);
            var anchor = measure_distance_anchor(camera);
            foreach(var batch in batches)
            {
                if(!GeometryUtility.TestPlanesAABB(frustum_planes,batch.bounds)) continue;
                var old_delta = batch.bounds.center-camera.transform.position;
                var delta = batch.bounds.center-anchor;
                old_delta.y = 0;
                delta.y = 0;
                if(old_delta.sqrMagnitude>batch.draw_distance*batch.draw_distance)
                {
                    if(batch.is_grass) evidence.grass_camera_distance_rejected += batch.matrices.Length;
                    if(batch.is_shrub) evidence.shrub_camera_distance_rejected += batch.matrices.Length;
                }
                if(delta.sqrMagnitude>batch.draw_distance*batch.draw_distance) continue;
                if(batch.is_grass) evidence.grass_matrix_submissions += batch.matrices.Length;
                if(batch.is_shrub) evidence.shrub_matrix_submissions += batch.matrices.Length;
            }
            var bindings = new List<string>();
            foreach(var material in materials)
            {
                bindings.Add(material.name+"; shader="+material.shader.name+"; base="+material.GetTexture("_MainTex")?.name+
                    "; Cull="+material.GetFloat("_Cull")+"; Cutoff="+material.GetFloat("_Cutoff")+"; AlphaToMask="+material.GetFloat("_AlphaToMask"));
            }
            evidence.material_bindings = bindings.ToArray();
            return evidence;
        }

        public void Dispose()
        {
            foreach (var material in materials) if (material) UnityEngine.Object.Destroy(material);
            batches.Clear();
            materials.Clear();
        }
    }
}
