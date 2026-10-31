using System.Collections.Generic;
using ToyCarArena.Rendering;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Chunky primitive meshes for the arena mechanics that build their look in code (a crab, a top, a pencil, mounds),
    /// and the little that puts them together. Every mesh carries smoothed normals in TEXCOORD3, which is what
    /// <c>sh_outline</c> pushes the hull out along: without them the faceted pieces would tear at their corners, as the
    /// baker for imported models explains.
    /// </summary>
    public static class ToyMeshes
    {
        /// <summary>
        /// An ellipsoid with the given radii, or the part of it between the two polar angles (0 is the top, pi the bottom;
        /// pi / 2 stops at the equator for a dome).
        /// </summary>
        public static Mesh Ellipsoid(Vector3 radii, int slices = 18, int stacks = 10, float toPhi = Mathf.PI)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var stack = 0; stack <= stacks; stack++) {
                var phi = toPhi * stack / stacks;
                for (var slice = 0; slice <= slices; slice++) {
                    var theta = Mathf.PI * 2f * slice / slices;
                    var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    vertices.Add(Vector3.Scale(unit, radii));
                    normals.Add(new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized);
                }
            }

            for (var stack = 0; stack < stacks; stack++) {
                for (var slice = 0; slice < slices; slice++) {
                    var a = stack * (slices + 1) + slice;
                    var b = a + slices + 1;
                    // Unity faces are clockwise from the front.
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
            }

            return Finish("Ellipsoid", vertices, normals, triangles);
        }

        /// <summary>A box centred on the origin.</summary>
        public static Mesh Box(Vector3 size)
        {
            var h = size * 0.5f;
            var builder = new Builder();
            Facing(builder, new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z));
            Facing(builder, new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z));
            Facing(builder, new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, -h.y, h.z));
            Facing(builder, new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, -h.y, -h.z));
            Facing(builder, new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z));
            Facing(builder, new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z));
            return builder.Build("Box");
        }

        // The box faces are listed counter-clockwise from outside; Unity wants them clockwise.
        private static void Facing(Builder builder, Vector3 a, Vector3 b, Vector3 c, Vector3 d) => builder.Quad(d, c, b, a);

        /// <summary>
        /// A cylinder or cone frustum standing on the Y axis, centred on the origin: <paramref name="bottom"/> and
        /// <paramref name="top"/> are its radii. Few sides make a prism, as a pencil's six.
        /// </summary>
        public static Mesh Cylinder(float bottom, float top, float height, int sides)
        {
            var builder = new Builder();
            var low = -height * 0.5f;
            var high = height * 0.5f;
            for (var i = 0; i < sides; i++) {
                var from = Mathf.PI * 2f * i / sides;
                var to = Mathf.PI * 2f * (i + 1) / sides;
                var a = new Vector3(Mathf.Cos(from), 0f, Mathf.Sin(from));
                var b = new Vector3(Mathf.Cos(to), 0f, Mathf.Sin(to));
                builder.Quad(a * bottom + Vector3.up * low, a * top + Vector3.up * high,
                    b * top + Vector3.up * high, b * bottom + Vector3.up * low);
                if (top > 0.001f) {
                    builder.Tri(Vector3.up * high, b * top + Vector3.up * high, a * top + Vector3.up * high);
                }

                if (bottom > 0.001f) {
                    builder.Tri(Vector3.up * low, a * bottom + Vector3.up * low, b * bottom + Vector3.up * low);
                }
            }

            return builder.Build("Cylinder");
        }

        /// <summary>A flat disc on the floor plane, facing up.</summary>
        public static Mesh Disc(float radius, int sides)
        {
            var builder = new Builder();
            for (var i = 0; i < sides; i++) {
                var from = Mathf.PI * 2f * i / sides;
                var to = Mathf.PI * 2f * (i + 1) / sides;
                builder.Tri(Vector3.zero, new Vector3(Mathf.Cos(to), 0f, Mathf.Sin(to)) * radius,
                    new Vector3(Mathf.Cos(from), 0f, Mathf.Sin(from)) * radius);
            }

            return builder.Build("Disc");
        }

        /// <summary>A flat ring on the floor plane, facing up.</summary>
        public static Mesh Ring(float inner, float outer, int sides)
        {
            var builder = new Builder();
            for (var i = 0; i < sides; i++) {
                var from = Mathf.PI * 2f * i / sides;
                var to = Mathf.PI * 2f * (i + 1) / sides;
                var a = new Vector3(Mathf.Cos(from), 0f, Mathf.Sin(from));
                var b = new Vector3(Mathf.Cos(to), 0f, Mathf.Sin(to));
                builder.Quad(a * inner, b * inner, b * outer, a * outer);
            }

            return builder.Build("Ring");
        }

        /// <summary>A unit square on the floor plane, facing up, for a decal scaled to size.</summary>
        public static Mesh Quad()
        {
            var builder = new Builder();
            builder.Quad(new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f));
            return builder.Build("Quad");
        }

        /// <summary>
        /// Adds a piece to <paramref name="parent"/> that casts and takes no shadow (the arena's real shadows are off
        /// anyway) and returns its transform.
        /// </summary>
        public static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position,
            Vector3 euler = default, Vector3? scale = null)
        {
            var part = new GameObject(name).transform;
            part.SetParent(parent, false);
            part.localPosition = position;
            part.localRotation = Quaternion.Euler(euler);
            part.localScale = scale ?? Vector3.one;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part;
        }

        /// <summary>
        /// A flat sticker on the floor (a decal, a footprint, a band): drawn under the cars' soft shadows, as the candy
        /// patches are.
        /// </summary>
        public static MeshRenderer Decal(Transform parent, string name, Mesh mesh, Material material, int sortingOrder)
        {
            var decal = Part(parent, name, mesh, material, Vector3.zero);
            var renderer = decal.GetComponent<MeshRenderer>();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>Outlines every solid piece below <paramref name="root"/>: the navy line of the other toys.</summary>
        public static void Outlined(GameObject root) => Outline.Apply(root);

        private static Mesh Finish(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(3, Smoothed(vertices, normals));
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The normal of every vertex averaged with those of the vertices at the same place, so the corners of a flat-shaded
        /// box share one direction for the outline hull to follow.
        /// </summary>
        private static List<Vector3> Smoothed(List<Vector3> vertices, List<Vector3> normals)
        {
            var sums = new Dictionary<Vector3Int, Vector3>();
            for (var i = 0; i < vertices.Count; i++) {
                var key = Vector3Int.RoundToInt(vertices[i] * 1000f);
                sums[key] = sums.TryGetValue(key, out var sum) ? sum + normals[i] : normals[i];
            }

            var smoothed = new List<Vector3>(vertices.Count);
            for (var i = 0; i < vertices.Count; i++) {
                smoothed.Add(sums[Vector3Int.RoundToInt(vertices[i] * 1000f)].normalized);
            }

            return smoothed;
        }

        // Flat-shaded faces from triangles and quads, normals taken from the winding.
        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<Vector3> normals = new();
            private readonly List<int> triangles = new();

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh Build(string name) => Finish(name, vertices, normals, triangles);
        }
    }
}
