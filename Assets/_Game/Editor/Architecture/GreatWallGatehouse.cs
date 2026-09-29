using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieGame.EditorTools.Architecture
{
    /// <summary>Authored architectural sample. Dimensions are art dimensions, not combat stats.</summary>
    public sealed class GreatWallGatehouse
    {
        public const string ASSET_ROOT = "Assets/_Game/Art/Architecture/GreatWallGatehouse";
        private readonly Dictionary<string, GatehouseGeometry> parts = new Dictionary<string, GatehouseGeometry>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly System.Random variation = new System.Random(29);

        public GameObject build_model()
        {
            Directory.CreateDirectory(ASSET_ROOT);
            AssetDatabase.Refresh();
            create_materials();
            build_foundation();
            build_archway();
            build_battlements();
            build_pavilion();
            build_roof();
            build_gate_doors();
            build_banners();
            return save_model();
        }

        private GatehouseGeometry part(string name) => parts[name];
        private void box(string material, float x, float y, float z, float width, float height, float depth)
        {
            if (material.Contains("Stone")) part(material).add_stone(new Vector3(x, y, z), new Vector3(width, height, depth));
            else part(material).add_box(new Vector3(x, y, z), new Vector3(width, height, depth));
        }
        private string stone() => "Stone" + variation.Next(4);

        private void create_materials()
        {
            add_material("Stone0", new Color(.39f, .40f, .37f));
            add_material("Stone1", new Color(.46f, .45f, .40f));
            add_material("Stone2", new Color(.35f, .37f, .36f));
            add_material("Stone3", new Color(.51f, .49f, .43f));
            add_material("DressedStone", new Color(.60f, .57f, .48f));
            add_material("RedTimber", new Color(.34f, .075f, .045f));
            add_material("DarkWood", new Color(.15f, .095f, .06f));
            add_material("Plaster", new Color(.73f, .66f, .51f));
            add_material("Roof", new Color(.13f, .17f, .19f), .12f, .28f);
            add_material("TileEdges", new Color(.23f, .27f, .28f), .08f, .3f);
            add_material("Bronze", new Color(.43f, .29f, .12f), .7f, .32f);
            add_material("Iron", new Color(.09f, .105f, .11f), .7f, .28f);
            add_material("Banner", new Color(.47f, .045f, .025f));
        }

        private void add_material(string name, Color color, float metallic = 0, float smoothness = .15f)
        {
            string path = ASSET_ROOT + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true;
            if (name.Contains("Stone") || name == "Plaster" || name.Contains("Wood"))
                material.mainTexture = create_surface_grain(name, name.Contains("Wood"));
            EditorUtility.SetDirty(material);
            materials.Add(name, material);
            parts.Add(name, new GatehouseGeometry());
        }

        private Texture2D create_surface_grain(string name, bool is_wood)
        {
            string path = ASSET_ROOT + "/" + name + "Grain.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;
            texture = new Texture2D(128, 128, TextureFormat.RGB24, true) { name = name + " grain" };
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float broad = Mathf.PerlinNoise(x * (is_wood ? .24f : .075f), y * (is_wood ? .009f : .075f));
                    float fine = Mathf.PerlinNoise(x * .73f, y * .73f);
                    float value = .65f + broad * .25f + fine * .10f;
                    pixels[y * 128 + x] = new Color(value, value, value);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private void build_foundation()
        {
            // Solid piers with a real empty passage; no slab spans the gate opening.
            box("Stone2", -4.55f, 2.8f, 0, 5.2f, 5.6f, 7.8f);
            box("Stone2", 4.55f, 2.8f, 0, 5.2f, 5.6f, 7.8f);
            box("Stone2", 0, 5.2f, 0, 4, 1.2f, 7.8f);
            for (int row = 0; row < 14; row++)
            {
                float y = row * .4f + .2f;
                float face = 4.2f - y * .045f;
                for (int side = -1; side <= 1; side += 2)
                {
                    brick_course(-7.1f, -2.32f, y, side * face, row);
                    brick_course(2.32f, 7.1f, y, side * face, row);
                    if (row >= 12) brick_course(-2.32f, 2.32f, y, side * face, row);
                    for (int i = 0; i < 10; i++)
                        box(stone(), side * (7.15f - y * .025f), y, -3.8f + i * .83f,
                            .24f, .376f, .805f);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                box("DressedStone", side * 4.6f, .15f, 0, 5.4f, .3f, 8.65f);
                box("DressedStone", side * 10.1f, .15f, 1.1f, 5.6f, .3f, 4.5f);
                box("Stone2", side * 10.1f, 2.1f, 1.1f, 5.4f, 4.2f, 4);
                for (int row = 0; row < 10; row++)
                    foreach (float z in new[] { -1.02f, 3.22f })
                        brick_course(side * 10.1f - 2.7f, side * 10.1f + 2.7f,
                            row * .4f + .35f, z, row);
                box("DressedStone", side * 10.1f, 4.3f, 1.1f, 5.5f, .25f, 4.45f);
                for (int row = 0; row < 10; row++)
                    for (int i = 0; i < 5; i++)
                        box(stone(), side * 12.85f, row * .4f + .35f, -.55f + i * .83f, .2f, .376f, .805f);
                for (int x = 0; x < 7; x++)
                    for (int z = 0; z < 5; z++)
                        box(stone(), side * 10.1f - 2.28f + x * .76f, 4.46f, -.42f + z * .76f, .73f, .08f, .73f);
            }
            box("DressedStone", 0, 5.68f, 0, 14.55f, .25f, 8.5f);
            // Walkway flagstones.
            for (int x = -9; x <= 9; x++)
                for (int z = -5; z <= 5; z++)
                    box(stone(), x * .735f, 5.83f, z * .74f, .714f, .08f, .719f);
        }

        private void brick_course(float left, float right, float y, float z, int row)
        {
            float cursor = left;
            while (cursor < right - .02f)
            {
                float width = Mathf.Min(cursor == left && row % 2 == 1 ? .43f : .86f, right - cursor);
                box(stone(), cursor + width * .5f, y, z, width - .025f, .376f, .26f);
                cursor += width;
            }
        }

        private void build_archway()
        {
            const float RADIUS = 1.86f;
            const float SPRING = 2.45f;
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 6; row++)
                    box("DressedStone", side * 2.09f, row * .405f + .22f, 0, .44f, .39f, 8.36f);
            for (int i = 0; i < 17; i++)
            {
                float a = i * Mathf.PI / 17 + .004f, b = (i + 1) * Mathf.PI / 17 - .004f;
                var inner_a = new Vector2(Mathf.Cos(a) * RADIUS, SPRING + Mathf.Sin(a) * RADIUS);
                var inner_b = new Vector2(Mathf.Cos(b) * RADIUS, SPRING + Mathf.Sin(b) * RADIUS);
                var outer_a = new Vector2(Mathf.Cos(a) * 2.33f, SPRING + Mathf.Sin(a) * 2.33f);
                var outer_b = new Vector2(Mathf.Cos(b) * 2.33f, SPRING + Mathf.Sin(b) * 2.33f);
                part("DressedStone").add_prism(new[] { inner_a, outer_a, outer_b, inner_b }, -4.21f, 4.21f);
                // Masonry spandrel fills everything above each wedge without covering the arch.
                part(stone()).add_prism(new[] { outer_b, outer_a, new Vector2(outer_a.x, 5.55f),
                    new Vector2(outer_b.x, 5.55f) }, -3.97f, 3.97f);
            }
            box("DarkWood", 0, 5.16f, -4.14f, 2.9f, .49f, .17f);
            box("Bronze", 0, 5.38f, -4.25f, 2.94f, .035f, .04f);
            box("Bronze", 0, 4.94f, -4.25f, 2.94f, .035f, .04f);
            for (int i = -1; i <= 1; i++)
            {
                box("Bronze", i * .64f, 5.16f, -4.25f, .22f, .04f, .035f);
                box("Bronze", i * .64f, 5.16f, -4.25f, .04f, .24f, .035f);
            }
        }

        private void add_parapet(float x, float y, float z, bool rotated = false)
        {
            Vector3 size = rotated ? new Vector3(.5f, .60f, .94f) : new Vector3(.94f, .60f, .5f);
            part(stone()).add_stone(new Vector3(x, y, z), size);
            part("DressedStone").add_stone(new Vector3(x, y + .33f, z), size + new Vector3(.08f, -.49f, .08f));
        }

        private void build_battlements()
        {
            foreach (float z in new[] { -4.0f, 4.0f })
            {
                box("Stone1", 0, 6.07f, z, 14.2f, .45f, .48f);
                for (int i = -5; i <= 5; i++) add_parapet(i * 1.35f, 6.58f, z);
            }
            foreach (float x in new[] { -6.85f, 6.85f })
            {
                box("Stone1", x, 6.05f, 0, .48f, .45f, 8);
                for (int i = -2; i <= 2; i++) add_parapet(x, 6.58f, i * 1.4f, true);
            }
            foreach (float x in new[] { -10.1f, 10.1f })
                foreach (float z in new[] { -.92f, 3.14f })
                {
                    box("Stone1", x, 4.62f, z, 5.5f, .46f, .48f);
                    for (int i = -2; i <= 2; i++) add_parapet(x + i * 1.23f, 5.1f, z);
                }
        }

        private void build_pavilion()
        {
            box("DressedStone", 0, 6.05f, 0, 9.5f, .38f, 5.2f);
            box("Plaster", 0, 7.3f, 0, 8.65f, 2.45f, 4.35f);
            foreach (float z in new[] { -2.25f, 2.25f })
            {
                box("RedTimber", 0, 6.4f, z, 9, .19f, .21f);
                box("RedTimber", 0, 8.4f, z, 9.3f, .23f, .27f);
                box("DarkWood", 0, 8.67f, z, 9.6f, .25f, .44f);
                for (int i = -2; i <= 2; i++)
                {
                    float x = i * 2.15f;
                    part("RedTimber").add_tube(new Vector3(x, 6.2f, z), new Vector3(x, 8.65f, z), .145f, 12);
                    box("DressedStone", x, 6.32f, z, .38f, .23f, .38f);
                    // Layered dougong bracket arms under the deep eaves.
                    for (int level = 0; level < 3; level++)
                    {
                        box("DarkWood", x, 8.50f + level * .15f, z, .46f + level * .27f, .13f, .47f + level * .18f);
                        box("RedTimber", x, 8.56f + level * .15f, z, .15f, .18f, .85f + level * .26f);
                    }
                }
                for (int i = -1; i <= 1; i++) build_window(i * 2.15f, z * 1.015f);
            }
            foreach (float x in new[] { -4.4f, 4.4f })
                foreach (float y in new[] { 6.4f, 8.4f }) box("RedTimber", x, y, 0, .20f, .22f, 4.8f);
        }

        private void build_window(float x, float z)
        {
            box("DarkWood", x, 7.55f, z, 1.63f, 1.14f, .13f);
            box("RedTimber", x, 6.96f, z * 1.025f, 1.79f, .13f, .15f);
            for (int i = -3; i <= 3; i++) box("RedTimber", x + i * .20f, 7.55f, z * 1.04f, .045f, 1.01f, .05f);
            for (int i = -2; i <= 2; i++) box("RedTimber", x, 7.55f + i * .2f, z * 1.04f, 1.52f, .045f, .05f);
        }

        private static Vector3 roof_point(float x, float t, int side)
        {
            float corner_lift = Mathf.Pow(Mathf.Abs(x) / 5.6f, 8) * t * t * .43f;
            return new Vector3(x, 11.18f - 2.9f * t + 1.35f * t * t + corner_lift, side * t * 3.55f);
        }

        private void build_roof()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int column = 0; column < 47; column++)
                {
                    float x = -5.6f + column * 11.2f / 46;
                    for (int row = 0; row < 18; row++)
                    {
                        float t = row / 18f, next = (row + 1) / 18f;
                        var a = roof_point(x, t, side);
                        var b = roof_point(x, next, side);
                        // Raised overlapping pan tiles, individually visible at the eave.
                        part(row % 4 == 0 ? "TileEdges" : "Roof").add_tube(a, b + (b - a) * .075f, .091f, 8);
                        Vector3 c = roof_point(x + .24f, next, side), d = roof_point(x + .24f, t, side);
                        if (side < 0) part("Roof").add_quad(d, c, b, a);
                        else part("Roof").add_quad(a, b, c, d);
                    }
                }
                for (int segment = 0; segment < 46; segment++)
                {
                    float x = -5.6f + segment * 11.2f / 46;
                    part("DarkWood").add_beam(roof_point(x, 1, side) - Vector3.up * .13f,
                        roof_point(x + .244f, 1, side) - Vector3.up * .13f, .17f, .17f);
                }
                foreach (float x in new[] { -5.65f, 5.85f })
                    for (int segment = 0; segment < 18; segment++)
                        part("TileEdges").add_tube(roof_point(x, segment / 18f, side),
                            roof_point(x, (segment + 1) / 18f, side), .14f, 10);
            }
            part("TileEdges").add_tube(new Vector3(-5.75f, 11.25f, 0), new Vector3(5.95f, 11.25f, 0), .17f, 12);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 5.68f;
                part("TileEdges").add_tube(new Vector3(x, 11.25f, 0), new Vector3(x + side * .18f, 11.7f, 0), .19f, 8);
                part("TileEdges").add_tube(new Vector3(x + side * .18f, 11.7f, 0), new Vector3(x - side * .08f, 11.85f, 0), .12f, 8);
                // Gable infill beneath the curved tile roof.
                for (int i = 0; i < 12; i++)
                {
                    float z = (i - 5.5f) * .39f;
                    float top = roof_point(0, Mathf.Abs(z) / 3.55f, 1).y - .22f;
                    box("RedTimber", side * 4.45f, (top + 8.65f) * .5f, z, .17f, top - 8.65f, .37f);
                }
            }
        }

        private void build_gate_doors()
        {
            foreach (int side in new[] { -1, 1 })
            {
                var hinge = new Vector3(side * 1.82f, 0, .8f);
                var rotation = Quaternion.Euler(0, side * -65, 0);
                for (int plank = 0; plank < 6; plank++)
                {
                    float x = -side * (.15f + plank * .29f);
                    float height = 2.45f + Mathf.Sqrt(Mathf.Max(0, 1.8f * 1.8f - Mathf.Pow(1.8f - Mathf.Abs(x), 2)));
                    part("DarkWood").add_box(hinge + rotation * new Vector3(x, height * .5f, 0), new Vector3(.274f, height, .17f), rotation);
                }
                foreach (float y in new[] { .55f, 1.6f, 2.65f })
                {
                    part("Iron").add_box(hinge + rotation * new Vector3(-side * .89f, y, -.12f), new Vector3(1.72f, .14f, .08f), rotation);
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 center = hinge + rotation * new Vector3(-side * (.15f + i * .35f), y, -.19f);
                        part("Bronze").add_tube(center, center + rotation * Vector3.back * .035f, .047f);
                    }
                }
            }
        }

        private void build_banners()
        {
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 6.05f;
                part("DarkWood").add_tube(new Vector3(x, 5.85f, -.7f), new Vector3(x, 10.05f, -.7f), .065f);
                part("Bronze").add_tube(new Vector3(x - .6f, 9.8f, -.7f), new Vector3(x + .6f, 9.8f, -.7f), .05f);
                for (int i = 0; i < 12; i++)
                {
                    float left = x - .53f + i * .088f;
                    float wave_a = Mathf.Sin(i * .6f) * .08f;
                    float wave_b = Mathf.Sin((i + 1) * .6f) * .08f;
                    var a = new Vector3(left, 9.7f, -.7f + wave_a);
                    var b = new Vector3(left + .088f, 9.7f, -.7f + wave_b);
                    var c = new Vector3(b.x, 7.5f + (i % 3 == 1 ? .13f : 0), b.z);
                    var d = new Vector3(a.x, c.y, a.z);
                    part("Banner").add_quad(d, c, b, a);
                    part("Banner").add_quad(a, b, c, d);
                }
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI / 12, b = (i + 1) * Mathf.PI / 12;
                    part("Bronze").add_tube(new Vector3(x + Mathf.Cos(a) * .30f, 8.8f + Mathf.Sin(a) * .30f, -.81f),
                        new Vector3(x + Mathf.Cos(b) * .30f, 8.8f + Mathf.Sin(b) * .30f, -.81f), .017f, 5);
                }
            }
        }

        private GameObject save_model()
        {
            var root = new GameObject("Great Wall Gatehouse - Art Prototype");
            foreach (var entry in parts)
            {
                var mesh = entry.Value.create_mesh(entry.Key);
                string path = ASSET_ROOT + "/" + entry.Key + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                var child = new GameObject(entry.Key);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = materials[entry.Key];
            }
            PrefabUtility.SaveAsPrefabAsset(root, ASSET_ROOT + "/GreatWallGatehouse.prefab");
            AssetDatabase.SaveAssets();
            return root;
        }
    }
}
