using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Builds the chunky toy shapes of the arena objects (jelly pads, the candy stick, the train, the jack-in-the-box) out
    /// of boxes, cylinders and domes, once, in code: flat unlit colours with a navy hull round them, the same sticker look
    /// as the Ball. Every face is wound to look away from the middle of its part (or the way a caller says), so a part
    /// is described by its shape alone and never by the order of its corners.
    /// </summary>
    public sealed class PropMesh
    {
        private const float WeldScale = 1000f;

        private readonly List<Vector3> vertices = new();
        private readonly List<List<int>> submeshes = new() { new List<int>() };
        private Vector3 pivot;
        private int current;

        /// <summary>The submesh the next shapes go to: one material each.</summary>
        public int Submesh
        {
            get => current;
            set
            {
                while (submeshes.Count <= value) {
                    submeshes.Add(new List<int>());
                }

                current = value;
            }
        }

        // ---- Solid shapes: faces wound away from the shape's middle. ----

        public void Box(Vector3 centre, Vector3 half, Quaternion rotation)
        {
            pivot = centre;
            var corner = new Vector3[8];
            for (var i = 0; i < 8; i++) {
                var local = new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z);
                corner[i] = centre + rotation * local;
            }

            Quad(corner[0], corner[1], corner[3], corner[2]);
            Quad(corner[4], corner[5], corner[7], corner[6]);
            Quad(corner[0], corner[1], corner[5], corner[4]);
            Quad(corner[2], corner[3], corner[7], corner[6]);
            Quad(corner[0], corner[2], corner[6], corner[4]);
            Quad(corner[1], corner[3], corner[7], corner[5]);
        }

        /// <summary>A cylinder or cone frustum standing on its local Y axis.</summary>
        public void Cylinder(Vector3 centre, Quaternion rotation, float bottomRadius, float topRadius, float halfHeight,
            int sides)
        {
            pivot = centre;
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (var i = 0; i < sides; i++) {
                var angle = Mathf.PI * 2f * i / sides;
                var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bottom[i] = centre + rotation * (around * bottomRadius + Vector3.down * halfHeight);
                top[i] = centre + rotation * (around * topRadius + Vector3.up * halfHeight);
            }

            var down = centre + rotation * (Vector3.down * halfHeight);
            var up = centre + rotation * (Vector3.up * halfHeight);
            for (var i = 0; i < sides; i++) {
                var next = (i + 1) % sides;
                Quad(bottom[i], bottom[next], top[next], top[i]);
                Facing(down, bottom[next], bottom[i], rotation * Vector3.down);
                Facing(up, top[i], top[next], rotation * Vector3.up);
            }
        }

        /// <summary>
        /// An ellipsoid of the given radii; <paramref name="dome"/> keeps only the upper half and closes it with a flat base.
        /// </summary>
        public void Sphere(Vector3 centre, Quaternion rotation, Vector3 radii, int slices, int stacks, bool dome)
        {
            pivot = centre;
            var rows = dome ? stacks / 2 : stacks;
            var grid = new Vector3[(rows + 1) * slices];
            for (var row = 0; row <= rows; row++) {
                var phi = Mathf.PI * row / stacks;
                for (var slice = 0; slice < slices; slice++) {
                    var theta = Mathf.PI * 2f * slice / slices;
                    var local = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta) * radii.x, Mathf.Cos(phi) * radii.y,
                        Mathf.Sin(phi) * Mathf.Sin(theta) * radii.z);
                    grid[row * slices + slice] = centre + rotation * local;
                }
            }

            for (var row = 0; row < rows; row++) {
                for (var slice = 0; slice < slices; slice++) {
                    var next = (slice + 1) % slices;
                    var a = grid[row * slices + slice];
                    var b = grid[row * slices + next];
                    var c = grid[(row + 1) * slices + next];
                    var d = grid[(row + 1) * slices + slice];
                    Quad(a, b, c, d);
                }
            }

            if (dome) {
                for (var slice = 0; slice < slices; slice++) {
                    var next = (slice + 1) % slices;
                    Facing(centre, grid[rows * slices + next], grid[rows * slices + slice], rotation * Vector3.down);
                }
            }
        }

        // ---- Flat decals on the floor: faces look up. ----

        public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Facing(a, b, c, Vector3.up);
            Facing(a, c, d, Vector3.up);
        }

        public void FlatTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            Facing(a, b, c, Vector3.up);
        }

        public void FlatDisc(Vector3 centre, float radius, int sides)
        {
            FlatRing(centre, 0f, radius, sides, 0f, Mathf.PI * 2f);
        }

        /// <summary>A ring sector between two radii, from one angle to another (radians, counter-clockwise from +X).</summary>
        public void FlatRing(Vector3 centre, float inner, float outer, int sides, float from, float to)
        {
            for (var i = 0; i < sides; i++) {
                var a = Mathf.Lerp(from, to, (float)i / sides);
                var b = Mathf.Lerp(from, to, (float)(i + 1) / sides);
                var oa = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * outer;
                var ob = centre + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * outer;
                if (inner <= 0.0001f) {
                    Facing(centre, oa, ob, Vector3.up);
                }
                else {
                    var ia = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * inner;
                    var ib = centre + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * inner;
                    FlatQuad(ia, oa, ob, ib);
                }
            }
        }

        // ---- Output ----

        /// <summary>The shapes as a mesh with a submesh for each material index used.</summary>
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = submeshes.Count;
            for (var i = 0; i < submeshes.Count; i++) {
                mesh.SetTriangles(submeshes[i], i);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The same shapes grown outwards by <paramref name="thickness"/> along smoothed normals and turned inside out,
        /// so that seen from the front only a rim of it shows round the shapes: the navy outline. Normals are averaged
        /// over corners that share a place, as the outline baker does for the car models, so the hull does not split at
        /// a box's edges. One submesh.
        /// </summary>
        public Mesh ToHull(string name, float thickness)
        {
            var sums = new Dictionary<Vector3Int, Vector3>();
            foreach (var triangles in submeshes) {
                for (var i = 0; i < triangles.Count; i += 3) {
                    var a = vertices[triangles[i]];
                    var b = vertices[triangles[i + 1]];
                    var c = vertices[triangles[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a);
                    for (var k = 0; k < 3; k++) {
                        var key = Weld(vertices[triangles[i + k]]);
                        sums[key] = sums.TryGetValue(key, out var sum) ? sum + normal : normal;
                    }
                }
            }

            var grown = new Vector3[vertices.Count];
            for (var i = 0; i < grown.Length; i++) {
                var normal = sums[Weld(vertices[i])];
                grown[i] = vertices[i] + (normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up) * thickness;
            }

            var inside = new List<int>();
            foreach (var triangles in submeshes) {
                for (var i = 0; i < triangles.Count; i += 3) {
                    inside.Add(triangles[i]);
                    inside.Add(triangles[i + 2]);
                    inside.Add(triangles[i + 1]);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(grown);
            mesh.SetTriangles(inside, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3Int Weld(Vector3 point)
        {
            return new Vector3Int(Mathf.RoundToInt(point.x * WeldScale), Mathf.RoundToInt(point.y * WeldScale),
                Mathf.RoundToInt(point.z * WeldScale));
        }

        // ---- Faces ----

        /// <summary>A quad whose front looks away from the part's middle.</summary>
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var away = (a + b + c + d) * 0.25f - pivot;
            Facing(a, b, c, away);
            Facing(a, c, d, away);
        }

        /// <summary>A triangle wound so that its front looks the way <paramref name="toward"/> points.</summary>
        private void Facing(Vector3 a, Vector3 b, Vector3 c, Vector3 toward)
        {
            var flip = Vector3.Dot(Vector3.Cross(b - a, c - a), toward) < 0f;
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(flip ? c : b);
            vertices.Add(flip ? b : c);
            var triangles = submeshes[current];
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}
