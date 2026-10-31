using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// A floor of any shape but a circle, made of the tiles of an <see cref="ArenaShape"/>: each tile one mesh of one or a
    /// few faces with a slab under it, a groove drawn round it like the ring blocks' and a pivot in the middle of its
    /// slab to rattle and tumble about, plus the core that never crumbles. The one collider is the faces still standing,
    /// extruded to the same thickness, with a wall only along the boundary of the standing floor: no seam between two
    /// tiles, a pit or a hole is a gap in it, and a tile that has fallen is simply missing the next time it is built,
    /// which is when a wave falls and no oftener.
    /// <para>
    /// Every tile has its own object, so a floor costs about as many draw calls as the sandbox's rings do: a few dozen
    /// with one material.
    /// </para>
    /// </summary>
    public sealed class TiledLayout : ArenaLayout
    {
        // Depth of the floor below the driving surface, in metres; the same as the ring blocks'.
        private const float Thickness = 0.5f;

        // The most spots a wave puts dust up from.
        private const int MaxSpots = 36;

        private readonly ArenaShape shape;
        private readonly GridEdge edge;
        private readonly float halfSeam;
        private readonly List<int>[] waveFaces;
        private readonly CrumbleWave[] waves;

        public TiledLayout(ArenaShape shape, GridEdge edge, float seam)
        {
            this.shape = shape;
            this.edge = edge;
            this.halfSeam = seam * 0.5f;

            var faceCount = shape.FaceCount;
            var groupFaces = new List<int>[shape.GroupCount];
            for (var g = 0; g < groupFaces.Length; g++) {
                groupFaces[g] = new List<int>();
            }

            waveFaces = new List<int>[shape.WaveCount];
            waves = new CrumbleWave[shape.WaveCount];
            var waveGroups = new List<int>[shape.WaveCount];
            for (var w = 0; w < waveFaces.Length; w++) {
                waveFaces[w] = new List<int>();
                waveGroups[w] = new List<int>();
            }

            for (var f = 0; f < faceCount; f++) {
                var group = shape.GroupOf(f);
                if (group >= 0) {
                    groupFaces[group].Add(f);
                    waveFaces[shape.WaveOf(group)].Add(f);
                }
            }

            for (var g = 0; g < groupFaces.Length; g++) {
                waveGroups[shape.WaveOf(g)].Add(g);
            }

            for (var w = 0; w < waveGroups.Length; w++) {
                var inner = float.MaxValue;
                var outer = 0f;
                var spots = new List<Vector4>();
                foreach (var g in waveGroups[w]) {
                    AddTile(g, groupFaces[g], w);
                    foreach (var f in groupFaces[g]) {
                        var middle = Vector2.zero;
                        foreach (var id in shape.Face(f)) {
                            var vertex = shape.Vertices[id];
                            middle += vertex;
                            inner = Mathf.Min(inner, vertex.magnitude);
                            outer = Mathf.Max(outer, vertex.magnitude);
                        }

                        middle /= shape.Face(f).Length;
                        var drift = shape.DriftOf(g);
                        spots.Add(new Vector4(middle.x, middle.y, drift.x, drift.y));
                    }
                }

                waves[w] = new CrumbleWave(w, inner, outer, Thin(spots));
            }

            CoreMesh = Keep(BuildGroupMesh(ArenaShape.Core, Vector3.zero, Quaternion.identity, false, "Arena Floor"));
            ColliderMesh = Keep(BuildCollider());
        }

        public override int WaveCount => waves.Length;

        public override CrumbleWave Wave(int wave)
        {
            return waves[wave];
        }

        public override bool Contains(int wave, Vector3 position)
        {
            foreach (var face in waveFaces[wave]) {
                if (edge.Contains(face, position.x, position.z)) {
                    return true;
                }
            }

            return false;
        }

        public override bool Refresh(IReadOnlyList<WaveState> states)
        {
            if (!edge.Update(states)) {
                return false;
            }

            ColliderMesh = Keep(BuildCollider());
            return true;
        }

        private static Vector4[] Thin(List<Vector4> spots)
        {
            if (spots.Count <= MaxSpots) {
                return spots.ToArray();
            }

            var result = new Vector4[MaxSpots];
            for (var i = 0; i < MaxSpots; i++) {
                result[i] = spots[i * spots.Count / MaxSpots];
            }

            return result;
        }

        private void AddTile(int group, List<int> groupFaces, int wave)
        {
            // The pivot is the middle of the tile's slab; the tile turns so its +X is the way it drifts as it falls.
            var middle = Vector2.zero;
            var count = 0;
            foreach (var f in groupFaces) {
                foreach (var id in shape.Face(f)) {
                    middle += shape.Vertices[id];
                    count++;
                }
            }

            middle /= Mathf.Max(1, count);
            var drift = shape.DriftOf(group);
            var rotation = Quaternion.Euler(0f, -Mathf.Atan2(drift.y, drift.x) * Mathf.Rad2Deg, 0f);
            var pivot = new Vector3(middle.x, -Thickness * 0.5f, middle.y);
            var mesh = Keep(BuildGroupMesh(group, pivot, Quaternion.Inverse(rotation), true, "Arena Tile"));
            tiles.Add(new FloorTile($"Wave {wave} Tile {group}", mesh, pivot, rotation, wave));
        }

        /// <summary>
        /// The faces of a group as a solid: tops (fanned, they are convex), a bottom if wanted, and a wall along every
        /// edge the group does not share with itself. The outline is pulled in by half the seam all round, so two tiles
        /// side by side leave an even groove; a vertex where the outline turns moves along the bisector.
        /// </summary>
        private Mesh BuildGroupMesh(int group, Vector3 pivot, Quaternion unrotate, bool hasBottom, string name)
        {
            var boundary = new List<(int from, int to, Vector2 normal)>();
            var edgesA = shape.EdgeA;
            var edgesB = shape.EdgeB;
            for (var e = 0; e < edgesA.Length; e++) {
                var inA = In(shape.EdgeFaceA[e], group);
                var inB = In(shape.EdgeFaceB[e], group);
                if (inA == inB) {
                    continue;
                }

                var from = inA ? edgesA[e] : edgesB[e];
                var to = inA ? edgesB[e] : edgesA[e];
                var direction = shape.Vertices[to] - shape.Vertices[from];
                boundary.Add((from, to, new Vector2(direction.y, -direction.x).normalized));
            }

            var corners = new Dictionary<int, (Vector2 first, Vector2 second, int count)>();
            foreach (var (from, to, normal) in boundary) {
                Touch(corners, from, normal);
                Touch(corners, to, normal);
            }

            var moved = new Dictionary<int, Vector2>();
            foreach (var pair in corners) {
                var (first, second, count) = pair.Value;
                if (count != 2) {
                    moved[pair.Key] = shape.Vertices[pair.Key];
                    continue;
                }

                var denominator = 1f + Vector2.Dot(first, second);
                var inward = denominator > 0.05f ? (first + second) / denominator : first;
                moved[pair.Key] = shape.Vertices[pair.Key] - inward * halfSeam;
            }

            var builder = new FloorMeshBuilder();
            var down = Vector3.down * Thickness;
            for (var f = 0; f < shape.FaceCount; f++) {
                if (shape.GroupOf(f) != group) {
                    continue;
                }

                var ids = shape.Face(f);
                for (var i = 1; i + 1 < ids.Length; i++) {
                    var a = Local(Position(moved, ids[0]), 0f, pivot, unrotate);
                    var b = Local(Position(moved, ids[i]), 0f, pivot, unrotate);
                    var c = Local(Position(moved, ids[i + 1]), 0f, pivot, unrotate);
                    builder.AddTriangle(a, b, c, Vector3.up);
                    if (hasBottom) {
                        builder.AddTriangle(a + down, b + down, c + down, Vector3.down);
                    }
                }
            }

            foreach (var (from, to, normal) in boundary) {
                var a = Local(Position(moved, from), 0f, pivot, unrotate);
                var b = Local(Position(moved, to), 0f, pivot, unrotate);
                var facing = unrotate * new Vector3(normal.x, 0f, normal.y);
                builder.AddQuad(a, b, b + down, a + down, facing, facing, facing, facing);
            }

            return builder.ToMesh(name);
        }

        /// <summary>
        /// The seamless collider: the faces still holding the solid floor, the top of each and a wall along the boundary
        /// of the standing floor.
        /// </summary>
        private Mesh BuildCollider()
        {
            var builder = new FloorMeshBuilder();
            var down = Vector3.down * Thickness;
            for (var f = 0; f < shape.FaceCount; f++) {
                if (!edge.IsFaceStanding(f, true)) {
                    continue;
                }

                var ids = shape.Face(f);
                var a = Point(shape.Vertices[ids[0]]);
                for (var i = 1; i + 1 < ids.Length; i++) {
                    builder.AddTriangle(a, Point(shape.Vertices[ids[i]]), Point(shape.Vertices[ids[i + 1]]), Vector3.up);
                }
            }

            var edgesA = shape.EdgeA;
            var edgesB = shape.EdgeB;
            for (var e = 0; e < edgesA.Length; e++) {
                var inA = edge.IsFaceStanding(shape.EdgeFaceA[e], true);
                var inB = shape.EdgeFaceB[e] >= 0 && edge.IsFaceStanding(shape.EdgeFaceB[e], true);
                if (inA == inB) {
                    continue;
                }

                var from = Point(shape.Vertices[inA ? edgesA[e] : edgesB[e]]);
                var to = Point(shape.Vertices[inA ? edgesB[e] : edgesA[e]]);
                var direction = to - from;
                var normal = new Vector3(direction.z, 0f, -direction.x).normalized;
                builder.AddQuad(from, to, to + down, from + down, normal, normal, normal, normal);
            }

            var mesh = builder.ToMesh("Arena Floor Collider");
            return mesh;
        }

        private bool In(int face, int group)
        {
            return face >= 0 && shape.GroupOf(face) == group;
        }

        private static void Touch(Dictionary<int, (Vector2 first, Vector2 second, int count)> corners, int id,
            Vector2 normal)
        {
            corners.TryGetValue(id, out var corner);
            if (corner.count == 0) {
                corner.first = normal;
            }
            else {
                corner.second = normal;
            }

            corner.count++;
            corners[id] = corner;
        }

        private Vector2 Position(Dictionary<int, Vector2> moved, int id)
        {
            return moved.TryGetValue(id, out var position) ? position : shape.Vertices[id];
        }

        private static Vector3 Point(Vector2 flat)
        {
            return new Vector3(flat.x, 0f, flat.y);
        }

        private static Vector3 Local(Vector2 flat, float height, Vector3 pivot, Quaternion unrotate)
        {
            return unrotate * (new Vector3(flat.x, height, flat.y) - pivot);
        }
    }
}
