using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.EditorTools.Architecture
{
    /// <summary>Editor-only mesh assembly; merges architectural detail by material.</summary>
    public sealed class GatehouseGeometry
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> indices = new List<int>();
        private readonly List<Vector2> coordinates = new List<Vector2>();

        public void add_quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            coordinates.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        public void add_box(Vector3 center, Vector3 size, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = center + rotation * Vector3.Scale(size * .5f,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            add_quad(corners[0], corners[2], corners[3], corners[1]);
            add_quad(corners[4], corners[5], corners[7], corners[6]);
            add_quad(corners[0], corners[4], corners[6], corners[2]);
            add_quad(corners[1], corners[3], corners[7], corners[5]);
            add_quad(corners[2], corners[6], corners[7], corners[3]);
            add_quad(corners[0], corners[1], corners[5], corners[4]);
        }

        public void add_beam(Vector3 start, Vector3 end, float width, float depth)
        {
            add_box((start + end) * .5f, new Vector3(width, (end - start).magnitude, depth),
                Quaternion.FromToRotation(Vector3.up, end - start));
        }

        /// <summary>Small stone chamfers catch light without separate decorative edge objects.</summary>
        public void add_stone(Vector3 center, Vector3 size, float bevel = .018f)
        {
            Vector3 half = size * .5f;
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                foreach (int sign in new[] { -1, 1 })
                {
                    var corners = new Vector3[4];
                    for (int i = 0; i < 4; i++)
                    {
                        corners[i][axis] = sign * half[axis];
                        corners[i][u] = (i == 0 || i == 3 ? -1 : 1) * (half[u] - bevel);
                        corners[i][v] = (i < 2 ? -1 : 1) * (half[v] - bevel);
                    }
                    Vector3 normal = Vector3.zero; normal[axis] = sign;
                    add_outward_quad(center, corners[0], corners[1], corners[2], corners[3], normal);
                }
                foreach (int sign_u in new[] { -1, 1 })
                    foreach (int sign_v in new[] { -1, 1 })
                    {
                        Vector3 a = Vector3.zero, b = Vector3.zero;
                        a[axis] = b[axis] = -half[axis] + bevel;
                        a[u] = sign_u * half[u]; a[v] = sign_v * (half[v] - bevel);
                        b[u] = sign_u * (half[u] - bevel); b[v] = sign_v * half[v];
                        Vector3 c = b, d = a;
                        c[axis] = d[axis] = half[axis] - bevel;
                        Vector3 normal = Vector3.zero; normal[u] = sign_u; normal[v] = sign_v;
                        add_outward_quad(center, a, b, c, d, normal);
                    }
            }
            for (int i = 0; i < 8; i++)
            {
                var normal = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                Vector3 inset = Vector3.Scale(normal, half - Vector3.one * bevel);
                Vector3 a = inset, b = inset, c = inset;
                a.x += normal.x * bevel; b.y += normal.y * bevel; c.z += normal.z * bevel;
                add_outward_quad(center, a, b, c, a, normal);
            }
        }

        private void add_outward_quad(Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0)
                add_quad(center + a, center + b, center + c, center + d);
            else add_quad(center + d, center + c, center + b, center + a);
        }

        public void add_tube(Vector3 start, Vector3 end, float radius, int sides = 8)
        {
            var axis = (end - start).normalized;
            var right = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
            var up = Vector3.Cross(right, axis).normalized;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                float b = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 offset_a = radius * (right * Mathf.Cos(a) + up * Mathf.Sin(a));
                Vector3 offset_b = radius * (right * Mathf.Cos(b) + up * Mathf.Sin(b));
                add_quad(start + offset_a, end + offset_a, end + offset_b, start + offset_b);
                add_quad(start, start + offset_a, start + offset_b, start);
                add_quad(end, end + offset_b, end + offset_a, end);
            }
        }

        /// <summary>Extrudes a counterclockwise x/y polygon through the gate's depth.</summary>
        public void add_prism(Vector2[] polygon, float front, float back)
        {
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                add_quad(new Vector3(a.x, a.y, front), new Vector3(a.x, a.y, back),
                    new Vector3(b.x, b.y, back), new Vector3(b.x, b.y, front));
            }
            for (int i = 1; i < polygon.Length - 1; i++)
            {
                Vector2 a = polygon[0], b = polygon[i], c = polygon[i + 1];
                add_quad(new Vector3(a.x, a.y, front), new Vector3(c.x, c.y, front),
                    new Vector3(b.x, b.y, front), new Vector3(a.x, a.y, front));
                add_quad(new Vector3(a.x, a.y, back), new Vector3(b.x, b.y, back),
                    new Vector3(c.x, c.y, back), new Vector3(a.x, a.y, back));
            }
        }

        public Mesh create_mesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.SetUVs(0, coordinates);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
