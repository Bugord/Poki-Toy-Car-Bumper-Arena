using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Effects
{
    /// <summary>
    /// The shapes the effects are drawn with, built in code as <see cref="Cars.CarLook"/> builds the player's marker:
    /// no textures, nothing to import, a few hundred vertices in all. Each mesh has two parts, the navy outline
    /// (submesh <see cref="OutlinePart"/>) and the flat fill (<see cref="FillPart"/>), so every effect has the thick
    /// outline of the screens' stickers. A solid shape (a puff, a chunk) gets its outline as a slightly larger copy turned
    /// inside out, which only shows round the silhouette; a flat shape gets a slightly wider copy just behind it.
    /// </summary>
    internal static class EffectShapes
    {
        public const int OutlinePart = 0;
        public const int FillPart = 1;

        // How far a flat shape's outline sits behind it, in the shape's own units: enough not to flicker with it.
        private const float OutlineDepth = 0.02f;

        /// <summary>
        /// A round puff of 1 unit across, made of <paramref name="segments"/> slices round and <paramref name="rings"/>
        /// from top to bottom; unlit, it reads as a flat disc whatever its facets. <paramref name="outline"/> is the
        /// outline's width as a share of the puff's size.
        /// </summary>
        public static Mesh Puff(int segments, int rings, float outline)
        {
            var points = new List<Vector3>();
            for (var ring = 0; ring <= rings; ring++) {
                var polar = Mathf.PI * ring / rings;
                for (var segment = 0; segment <= segments; segment++) {
                    var azimuth = Mathf.PI * 2f * segment / segments;
                    points.Add(0.5f * new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar),
                        Mathf.Sin(polar) * Mathf.Sin(azimuth)));
                }
            }

            var quads = new List<int>();
            var stride = segments + 1;
            for (var ring = 0; ring < rings; ring++) {
                for (var segment = 0; segment < segments; segment++) {
                    var corner = ring * stride + segment;
                    quads.Add(corner);
                    quads.Add(corner + 1);
                    quads.Add(corner + stride + 1);
                    quads.Add(corner + stride);
                }
            }

            return Solid("Effect Puff", points, quads, outline);
        }

        /// <summary>
        /// A cube of 1 unit, a chunk of the floor breaking off. <paramref name="outline"/> as for <see cref="Puff"/>.
        /// </summary>
        public static Mesh Chunk(float outline)
        {
            var points = new List<Vector3>();
            for (var i = 0; i < 8; i++) {
                points.Add(new Vector3((i & 1) - 0.5f, ((i >> 1) & 1) - 0.5f, ((i >> 2) & 1) - 0.5f));
            }

            // The six faces, as corners round each; Solid turns each to face out.
            var quads = new List<int> { 0, 1, 3, 2, 4, 5, 7, 6, 0, 1, 5, 4, 2, 3, 7, 6, 0, 2, 6, 4, 1, 3, 7, 5 };
            return Solid("Effect Chunk", points, quads, outline);
        }

        /// <summary>
        /// A flat star of 1 unit across with <paramref name="points"/> tips, its valleys at <paramref name="innerRatio"/>
        /// of the tips' radius; it faces -Z, the camera's side when it takes the camera's rotation. Its outline is
        /// <paramref name="outline"/> units wide.
        /// </summary>
        public static Mesh Star(int points, float innerRatio, float outline)
        {
            var rim = new Vector2[points * 2];
            for (var i = 0; i < rim.Length; i++) {
                // The first tip points up.
                var angle = Mathf.PI * 0.5f + Mathf.PI * i / points;
                var radius = i % 2 == 0 ? 0.5f : 0.5f * innerRatio;
                rim[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            var mesh = new Flat(false);
            mesh.AddFan(OutlinePart, Vector2.zero, Inflate(rim, outline), OutlineDepth);
            mesh.AddFan(FillPart, Vector2.zero, rim, 0f);
            return mesh.Build("Effect Star");
        }

        /// <summary>
        /// A flat ring facing up, 1 unit in radius out to its edge, its hole <paramref name="innerRatio"/> of that; the
        /// outline is <paramref name="outline"/> units wide on both edges. Meant to be scaled across only, so the
        /// outline's depth under it stays the same.
        /// </summary>
        public static Mesh Ring(float innerRatio, float outline, int segments)
        {
            var mesh = new Flat(true);
            mesh.AddBand(OutlinePart, Mathf.Max(0f, innerRatio - outline), 1f + outline, segments, OutlineDepth);
            mesh.AddBand(FillPart, innerRatio, 1f, segments, 0f);
            return mesh.Build("Effect Ring");
        }

        /// <summary>
        /// A speed line, 1 unit long along +Z and <paramref name="width"/> wide at its round head, tapering to a point at
        /// its tail; it lies flat, facing +Y. The outline is <paramref name="outline"/> units wide.
        /// </summary>
        public static Mesh Streak(float width, float outline)
        {
            const int HeadPoints = 7;
            var halfWidth = width * 0.5f;
            var headCentre = new Vector2(0f, 0.5f - halfWidth);
            var rim = new Vector2[HeadPoints + 1];
            for (var i = 0; i < HeadPoints; i++) {
                var angle = Mathf.PI * i / (HeadPoints - 1);
                rim[i] = headCentre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * halfWidth;
            }

            rim[HeadPoints] = new Vector2(0f, -0.5f);

            var mesh = new Flat(true);
            mesh.AddFan(OutlinePart, headCentre, Inflate(rim, outline), OutlineDepth);
            mesh.AddFan(FillPart, headCentre, rim, 0f);
            return mesh.Build("Effect Streak");
        }

        /// <summary>
        /// A closed solid from its points and its faces (four corners each, which may meet at a pole), with its outline:
        /// the same solid <paramref name="outline"/> larger, turned inside out. Each triangle is wound to face away from
        /// the centre, or towards it for the outline, so the winding never has to be worked out by hand.
        /// </summary>
        private static Mesh Solid(string meshName, List<Vector3> points, List<int> quads, float outline)
        {
            var vertices = new List<Vector3>(points.Count * 2);
            vertices.AddRange(points);
            foreach (var point in points) {
                vertices.Add(point * (1f + outline));
            }

            var fill = new List<int>();
            var hull = new List<int>();
            for (var i = 0; i < quads.Count; i += 4) {
                for (var half = 0; half < 2; half++) {
                    var a = quads[i];
                    var b = quads[i + 1 + half];
                    var c = quads[i + 2 + half];
                    AddFacing(fill, vertices, a, b, c, 1f);
                    AddFacing(hull, vertices, a + points.Count, b + points.Count, c + points.Count, -1f);
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(hull, OutlinePart);
            mesh.SetTriangles(fill, FillPart);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Adds the triangle wound so it faces out from the origin (<paramref name="side"/> 1) or in towards it (-1); a
        /// triangle with no area, at a pole, is left out.
        /// </summary>
        private static void AddFacing(List<int> triangles, List<Vector3> vertices, int a, int b, int c, float side)
        {
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.sqrMagnitude < 1e-10f) {
                return;
            }

            var centre = (vertices[a] + vertices[b] + vertices[c]) / 3f;
            var isOut = Vector3.Dot(normal, centre) > 0f;
            triangles.Add(a);
            if (isOut == side > 0f) {
                triangles.Add(b);
                triangles.Add(c);
            }
            else {
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        /// <summary>
        /// The outline round a polygon: every edge moved out by <paramref name="width"/>, each corner along the bisector
        /// far enough for both moved edges to meet. Works for the valleys of a star as well as for its tips.
        /// </summary>
        private static Vector2[] Inflate(Vector2[] polygon, float width)
        {
            var count = polygon.Length;
            var area = 0f;
            for (var i = 0; i < count; i++) {
                var from = polygon[i];
                var to = polygon[(i + 1) % count];
                area += from.x * to.y - to.x * from.y;
            }

            // Counter-clockwise (positive area), the outside of an edge is on its right.
            var outside = area > 0f ? 1f : -1f;
            var inflated = new Vector2[count];
            for (var i = 0; i < count; i++) {
                var previous = polygon[(i + count - 1) % count];
                var current = polygon[i];
                var next = polygon[(i + 1) % count];
                var normalIn = Right(current - previous).normalized * outside;
                var normalOut = Right(next - current).normalized * outside;
                var bisector = (normalIn + normalOut).normalized;
                inflated[i] = current + bisector * (width / Mathf.Max(0.2f, Vector2.Dot(bisector, normalIn)));
            }

            return inflated;
        }

        private static Vector2 Right(Vector2 direction) => new(direction.y, -direction.x);

        /// <summary>
        /// Builds a flat two-part mesh, either upright in the XY plane facing -Z (a billboard) or lying in the XZ plane
        /// facing +Y. Depth is how far a part sits behind the face: +Z upright, -Y lying.
        /// </summary>
        private sealed class Flat
        {
            private readonly bool isLying;
            private readonly List<Vector3> vertices = new();
            private readonly List<int>[] parts = { new(), new() };

            public Flat(bool isLying)
            {
                this.isLying = isLying;
            }

            private Vector3 Facing => isLying ? Vector3.up : Vector3.back;

            /// <summary>
            /// A polygon as a fan round <paramref name="centre"/>, which every corner must see.
            /// </summary>
            public void AddFan(int part, Vector2 centre, Vector2[] rim, float depth)
            {
                var first = vertices.Count;
                vertices.Add(Place(centre, depth));
                foreach (var point in rim) {
                    vertices.Add(Place(point, depth));
                }

                for (var i = 0; i < rim.Length; i++) {
                    AddTriangle(part, first, first + 1 + i, first + 1 + (i + 1) % rim.Length);
                }
            }

            /// <summary>
            /// A band round the origin between two radii.
            /// </summary>
            public void AddBand(int part, float inner, float outer, int segments, float depth)
            {
                var first = vertices.Count;
                for (var i = 0; i < segments; i++) {
                    var angle = Mathf.PI * 2f * i / segments;
                    var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    vertices.Add(Place(outward * inner, depth));
                    vertices.Add(Place(outward * outer, depth));
                }

                for (var i = 0; i < segments; i++) {
                    var next = (i + 1) % segments;
                    AddTriangle(part, first + i * 2, first + next * 2, first + i * 2 + 1);
                    AddTriangle(part, first + next * 2, first + next * 2 + 1, first + i * 2 + 1);
                }
            }

            public Mesh Build(string meshName)
            {
                var mesh = new Mesh { name = meshName };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(parts[OutlinePart], OutlinePart);
                mesh.SetTriangles(parts[FillPart], FillPart);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            private Vector3 Place(Vector2 point, float depth)
            {
                return isLying ? new Vector3(point.x, -depth, point.y) : new Vector3(point.x, point.y, depth);
            }

            /// <summary>
            /// Adds the triangle wound to face the shape's way; one with no area is left out.
            /// </summary>
            private void AddTriangle(int part, int a, int b, int c)
            {
                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude < 1e-12f) {
                    return;
                }

                var triangles = parts[part];
                triangles.Add(a);
                if (Vector3.Dot(normal, Facing) > 0f) {
                    triangles.Add(b);
                    triangles.Add(c);
                }
                else {
                    triangles.Add(c);
                    triangles.Add(b);
                }
            }
        }
    }
}
