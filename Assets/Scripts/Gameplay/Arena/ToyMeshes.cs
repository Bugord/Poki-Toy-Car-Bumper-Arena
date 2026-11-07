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

        /// <summary>
        /// A smooth solid of revolution about the Y axis: <paramref name="profile"/> lists (radius, height) from the foot
        /// outwards and up to the top's middle (radius 0), and is closed underneath by a flat disc. With
        /// <paramref name="lobes"/> the radius swells and narrows round the axis by <paramref name="lobeDepth"/> of itself,
        /// as a fluted jelly mould does; the swelling fades out along the profile from <paramref name="lobeFade"/> of the
        /// way up, so a flat top stays round.
        /// </summary>
        public static Mesh Revolved(IReadOnlyList<Vector2> profile, int slices, int lobes = 0, float lobeDepth = 0f,
            float lobeFade = 1f)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            var rows = profile.Count;
            for (var row = 0; row < rows; row++) {
                var along = rows > 1 ? (float)row / (rows - 1) : 0f;
                var swell = lobeDepth * Mathf.Clamp01((1f - along) / Mathf.Max(0.01f, 1f - lobeFade));
                for (var slice = 0; slice <= slices; slice++) {
                    var theta = Mathf.PI * 2f * slice / slices;
                    vertices.Add(PointOf(profile[row], theta, lobes, swell));
                }
            }

            // Normals from the two directions of the surface: along the profile and round the axis.
            for (var row = 0; row < rows; row++) {
                for (var slice = 0; slice <= slices; slice++) {
                    var up = vertices[Mathf.Min(rows - 1, row + 1) * (slices + 1) + slice];
                    var down = vertices[Mathf.Max(0, row - 1) * (slices + 1) + slice];
                    // The seam's last column is its first again, so round the axis it wraps over slices, not slices + 1.
                    var around = slice % slices;
                    var next = vertices[row * (slices + 1) + (around + 1) % slices];
                    var previous = vertices[row * (slices + 1) + (around + slices - 1) % slices];
                    var normal = Vector3.Cross(up - down, next - previous);
                    if (normal.sqrMagnitude < 1e-10f) {
                        normal = Vector3.up;
                    }

                    normals.Add(normal.normalized);
                }
            }

            for (var row = 0; row < rows - 1; row++) {
                for (var slice = 0; slice < slices; slice++) {
                    var a = row * (slices + 1) + slice;
                    var b = a + slices + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }

            // The bottom, facing down.
            var centre = vertices.Count;
            vertices.Add(new Vector3(0f, profile[0].y, 0f));
            normals.Add(Vector3.down);
            var rim = vertices.Count;
            for (var slice = 0; slice <= slices; slice++) {
                vertices.Add(vertices[slice]);
                normals.Add(Vector3.down);
            }

            for (var slice = 0; slice < slices; slice++) {
                triangles.Add(centre); triangles.Add(rim + slice); triangles.Add(rim + slice + 1);
            }

            return Finish("Revolved", vertices, normals, triangles);
        }

        private static Vector3 PointOf(Vector2 point, float theta, int lobes, float swell)
        {
            var radius = point.x * (1f + (lobes > 0 ? swell * Mathf.Cos(lobes * theta) : 0f));
            return new Vector3(Mathf.Cos(theta) * radius, point.y, Mathf.Sin(theta) * radius);
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
