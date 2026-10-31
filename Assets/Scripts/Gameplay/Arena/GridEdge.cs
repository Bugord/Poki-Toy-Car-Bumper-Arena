using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The edge of a floor of any shape, as a grid of signed distances about half a metre a node: positive on the floor,
    /// negative off it, in metres to the nearest void (the rim, the hole of a donut, a pit). The distance at a node is
    /// exact (to the segments of the floor's boundary, not to the neighbouring nodes), the grid is read with bilinear
    /// interpolation and the direction away from the void is the gradient of that, so a car heading home on a floor with
    /// holes goes away from the nearest one. Reading it costs a few multiplications a call; it is rebuilt when a wave
    /// starts to shake (the safe grid) and when one falls (the solid grid), a handful of times a round.
    /// <para>
    /// The distance is kept only out to <see cref="Reach"/> metres from a void: farther than that every rule has stopped
    /// caring (the edge help starts at five), and the grid stays small. Past the grid's own border the value goes on
    /// falling at one metre per metre, so a car flung far out still has a distance below zero and a way back.
    /// </para>
    /// </summary>
    public sealed class GridEdge : IArenaEdge
    {
        private const float Cell = 0.5f;
        private const float Reach = 8f;
        private const float Pad = 4f;

        // Where there is no preferred way: a gradient this short is on a plateau or a ridge.
        private const float MinGradient = 0.2f;

        // Bucket size of the lookup of the face under a point, in metres.
        private const float Bucket = 2f;

        private readonly ArenaShape shape;
        private readonly float[] vx;
        private readonly float[] vz;
        private readonly int[][] faces;
        private readonly int[] faceGroup;
        private readonly int[] groupWave;
        private readonly float[] faceRadius;
        private readonly Vector2[] faceMin;
        private readonly Vector2[] faceMax;

        private readonly int[] edgeA;
        private readonly int[] edgeB;
        private readonly int[] edgeFaceA;
        private readonly int[] edgeFaceB;

        private readonly float minX;
        private readonly float minZ;
        private readonly int nx;
        private readonly int nz;
        private readonly int[] nodeFace;
        private readonly float[] safeGrid;
        private readonly float[] solidGrid;
        private readonly float[] unsigned;

        private readonly int bucketsX;
        private readonly int bucketsZ;
        private readonly int[][] buckets;

        private readonly bool[] faceSafe;
        private readonly bool[] faceSolid;
        private readonly bool[] waveSafe;
        private readonly bool[] waveSolid;
        private readonly List<Vector3> safeOutline = new();
        private readonly List<Vector3> solidOutline = new();
        private readonly HashSet<int> outlineSeen = new();
        private readonly Vector4[] segments;

        private float safeExtent;
        private float solidExtent;
        private float maxDepth = 1f;

        public GridEdge(ArenaShape shape)
        {
            this.shape = shape;
            var count = shape.Vertices.Count;
            vx = new float[count];
            vz = new float[count];
            var low = new Vector2(float.MaxValue, float.MaxValue);
            var high = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < count; i++) {
                vx[i] = shape.Vertices[i].x;
                vz[i] = shape.Vertices[i].y;
                low = Vector2.Min(low, shape.Vertices[i]);
                high = Vector2.Max(high, shape.Vertices[i]);
            }

            faces = new int[shape.FaceCount][];
            faceGroup = new int[shape.FaceCount];
            faceRadius = new float[shape.FaceCount];
            faceMin = new Vector2[shape.FaceCount];
            faceMax = new Vector2[shape.FaceCount];
            for (var f = 0; f < faces.Length; f++) {
                faces[f] = shape.Face(f);
                faceGroup[f] = shape.GroupOf(f);
                var lowFace = new Vector2(float.MaxValue, float.MaxValue);
                var highFace = new Vector2(float.MinValue, float.MinValue);
                var radius = 0f;
                foreach (var id in faces[f]) {
                    lowFace = Vector2.Min(lowFace, shape.Vertices[id]);
                    highFace = Vector2.Max(highFace, shape.Vertices[id]);
                    radius = Mathf.Max(radius, shape.Vertices[id].magnitude);
                }

                faceMin[f] = lowFace;
                faceMax[f] = highFace;
                faceRadius[f] = radius;
            }

            edgeA = shape.EdgeA;
            edgeB = shape.EdgeB;
            edgeFaceA = shape.EdgeFaceA;
            edgeFaceB = shape.EdgeFaceB;
            segments = new Vector4[edgeA.Length];

            groupWave = new int[shape.GroupCount];
            for (var g = 0; g < groupWave.Length; g++) {
                groupWave[g] = shape.WaveOf(g);
            }

            minX = low.x - Pad;
            minZ = low.y - Pad;
            nx = Mathf.CeilToInt((high.x + Pad - minX) / Cell) + 1;
            nz = Mathf.CeilToInt((high.y + Pad - minZ) / Cell) + 1;
            nodeFace = new int[nx * nz];
            safeGrid = new float[nx * nz];
            solidGrid = new float[nx * nz];
            unsigned = new float[nx * nz];
            for (var i = 0; i < nodeFace.Length; i++) {
                nodeFace[i] = -1;
            }

            // Which face each node lies in: the faces do not overlap, so the first that holds the node is the one.
            for (var f = 0; f < faces.Length; f++) {
                var from = NodeRange(faceMin[f], 0f);
                var to = NodeRange(faceMax[f], 0f);
                for (var iz = Mathf.Max(0, from.y); iz <= Mathf.Min(nz - 1, to.y + 1); iz++) {
                    for (var ix = Mathf.Max(0, from.x); ix <= Mathf.Min(nx - 1, to.x + 1); ix++) {
                        if (Contains(f, minX + ix * Cell, minZ + iz * Cell)) {
                            nodeFace[iz * nx + ix] = f;
                        }
                    }
                }
            }

            bucketsX = Mathf.CeilToInt((high.x - low.x) / Bucket) + 1;
            bucketsZ = Mathf.CeilToInt((high.y - low.y) / Bucket) + 1;
            var lists = new List<int>[bucketsX * bucketsZ];
            for (var f = 0; f < faces.Length; f++) {
                var x0 = Mathf.FloorToInt((faceMin[f].x - low.x) / Bucket);
                var x1 = Mathf.FloorToInt((faceMax[f].x - low.x) / Bucket);
                var z0 = Mathf.FloorToInt((faceMin[f].y - low.y) / Bucket);
                var z1 = Mathf.FloorToInt((faceMax[f].y - low.y) / Bucket);
                for (var z = z0; z <= z1; z++) {
                    for (var x = x0; x <= x1; x++) {
                        var at = z * bucketsX + x;
                        lists[at] ??= new List<int>();
                        lists[at].Add(f);
                    }
                }
            }

            buckets = new int[lists.Length][];
            for (var i = 0; i < lists.Length; i++) {
                buckets[i] = lists[i]?.ToArray() ?? Array.Empty<int>();
            }

            bucketOrigin = low;

            faceSafe = new bool[faces.Length];
            faceSolid = new bool[faces.Length];
            waveSafe = new bool[shape.WaveCount];
            waveSolid = new bool[shape.WaveCount];
            for (var w = 0; w < waveSafe.Length; w++) {
                waveSafe[w] = true;
                waveSolid[w] = true;
            }

            FullExtent = shape.FullExtent;
            Recompute(safe: true);
            Recompute(safe: false);
        }

        private readonly Vector2 bucketOrigin;

        public event Action Changed;

        public float FullExtent { get; }
        public float SafeExtent => safeExtent;
        public float SolidExtent => solidExtent;
        public float Span => Mathf.Max(1f, safeExtent * 2f);

        /// <summary>Milliseconds the last rebuild of a grid took, for the diagnostics; 0 before the first.</summary>
        public float LastRebuildMilliseconds { get; private set; }

        /// <summary>How many rebuilds of either grid there have been since the first.</summary>
        public int Rebuilds { get; private set; }

        /// <summary>
        /// Takes in how far each wave has got and rebuilds what changed: the safe grid when a wave starts to shake or
        /// falls, the solid one when a wave falls.
        /// </summary>
        /// <returns>True when the solid floor changed, which is when the collider has to change.</returns>
        public bool Update(IReadOnlyList<WaveState> states)
        {
            var safeChanged = false;
            var solidChanged = false;
            for (var w = 0; w < waveSafe.Length; w++) {
                var safe = states[w] == WaveState.Standing;
                var solid = states[w] == WaveState.Standing || states[w] == WaveState.Warning;
                safeChanged |= safe != waveSafe[w];
                solidChanged |= solid != waveSolid[w];
                waveSafe[w] = safe;
                waveSolid[w] = solid;
            }

            if (safeChanged) {
                Recompute(safe: true);
            }

            if (solidChanged) {
                Recompute(safe: false);
            }

            if (safeChanged || solidChanged) {
                Changed?.Invoke();
            }

            return solidChanged;
        }

        /// <summary>Whether the face lies on standing floor in the safe or the solid view.</summary>
        public bool IsFaceStanding(int face, bool solid)
        {
            return solid ? faceSolid[face] : faceSafe[face];
        }

        public float DistanceToEdge(Vector3 position, float inset = 0f)
        {
            return Sample(safeGrid, position.x, position.z) - inset;
        }

        public float DistanceToSolidEdge(Vector3 position)
        {
            return Sample(solidGrid, position.x, position.z);
        }

        public bool IsSolid(Vector3 position)
        {
            var face = FaceAt(position.x, position.z);
            return face >= 0 && faceSolid[face];
        }

        public Vector3 Inward(Vector3 position)
        {
            Gradient(safeGrid, position.x, position.z, out var x, out var z);
            var length = Mathf.Sqrt(x * x + z * z);
            return length < MinGradient ? Vector3.zero : new Vector3(x / length, 0f, z / length);
        }

        public float Proximity(Vector3 position, float near, float far)
        {
            return Mathf.InverseLerp(near, far, DistanceToEdge(position));
        }

        public float Nearness(Vector3 position)
        {
            return 1f - Mathf.Clamp01(DistanceToEdge(position) / maxDepth);
        }

        public Vector3 ClampToFloor(Vector3 position)
        {
            position.y = 0f;
            for (var i = 0; i < 4; i++) {
                var distance = DistanceToEdge(position);
                if (distance >= 0f) {
                    return position;
                }

                var inward = Inward(position);
                if (inward == Vector3.zero) {
                    return Vector3.ClampMagnitude(position, safeExtent);
                }

                position += inward * (-distance + 0.05f);
            }

            return position;
        }

        public float SolidHalfChord(Vector3 origin, Vector3 direction)
        {
            SolidChord(origin, direction, out var behind, out var ahead);
            return (behind + ahead) * 0.5f;
        }

        public void SolidChord(Vector3 origin, Vector3 direction, out float behind, out float ahead)
        {
            direction.y = 0f;
            direction.Normalize();
            behind = ReachAlong(origin, -direction);
            ahead = ReachAlong(origin, direction);
        }

        public IReadOnlyList<Vector3> Outline(bool solid)
        {
            return solid ? solidOutline : safeOutline;
        }

        public bool TryGetSpawn(int index, int count, out Vector3 position)
        {
            if (shape.TrySpawn(index, count, out var flat)) {
                position = new Vector3(flat.x, 0f, flat.y);
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        /// The id of the face under a point, or -1 off the floor's lattice (a void, or outside).
        /// </summary>
        public int FaceAt(float x, float z)
        {
            var bx = Mathf.FloorToInt((x - bucketOrigin.x) / Bucket);
            var bz = Mathf.FloorToInt((z - bucketOrigin.y) / Bucket);
            if (bx < 0 || bz < 0 || bx >= bucketsX || bz >= bucketsZ) {
                return -1;
            }

            foreach (var face in buckets[bz * bucketsX + bx]) {
                if (Contains(face, x, z)) {
                    return face;
                }
            }

            return -1;
        }

        /// <summary>Whether a point is in a face, a hair beyond its sides counting.</summary>
        public bool Contains(int face, float x, float z)
        {
            var ids = faces[face];
            for (var i = 0; i < ids.Length; i++) {
                var a = ids[i];
                var b = ids[(i + 1) % ids.Length];
                // The faces run counter-clockwise: inside is to the left of every side.
                var cross = (vx[b] - vx[a]) * (z - vz[a]) - (vz[b] - vz[a]) * (x - vx[a]);
                if (cross < -1e-4f) {
                    return false;
                }
            }

            return true;
        }

        private float ReachAlong(Vector3 origin, Vector3 direction)
        {
            var far = 0f;
            var limit = FullExtent * 2f + 1f;
            for (var along = 0.25f; along <= limit; along += 0.25f) {
                if (IsSolid(origin + direction * along)) {
                    far = along;
                }
            }

            return far;
        }

        // ----- rebuilding -----------------------------------------------------------------------------------------

        private void Recompute(bool safe)
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var flags = safe ? faceSafe : faceSolid;
            var waves = safe ? waveSafe : waveSolid;
            var grid = safe ? safeGrid : solidGrid;
            var outline = safe ? safeOutline : solidOutline;
            var extent = 0f;
            for (var f = 0; f < flags.Length; f++) {
                var group = faceGroup[f];
                flags[f] = group < 0 || waves[shape.WaveOf(group)];
                if (flags[f]) {
                    extent = Mathf.Max(extent, faceRadius[f]);
                }
            }

            // The boundary: an edge with floor on one side only.
            var count = 0;
            outline.Clear();
            outlineSeen.Clear();
            for (var e = 0; e < edgeA.Length; e++) {
                var first = flags[edgeFaceA[e]];
                var second = edgeFaceB[e] >= 0 && flags[edgeFaceB[e]];
                if (first == second) {
                    continue;
                }

                segments[count++] = new Vector4(vx[edgeA[e]], vz[edgeA[e]], vx[edgeB[e]], vz[edgeB[e]]);
                foreach (var id in new[] { edgeA[e], edgeB[e] }) {
                    if (outlineSeen.Add(id)) {
                        outline.Add(new Vector3(vx[id], 0f, vz[id]));
                    }
                }
            }

            for (var i = 0; i < unsigned.Length; i++) {
                unsigned[i] = Reach;
            }

            for (var s = 0; s < count; s++) {
                var segment = segments[s];
                var low = NodeRange(new Vector2(Mathf.Min(segment.x, segment.z), Mathf.Min(segment.y, segment.w)), Reach);
                var high = NodeRange(new Vector2(Mathf.Max(segment.x, segment.z), Mathf.Max(segment.y, segment.w)), -Reach);
                var dx = segment.z - segment.x;
                var dz = segment.w - segment.y;
                var lengthSquared = dx * dx + dz * dz;
                for (var iz = Mathf.Max(0, low.y); iz <= Mathf.Min(nz - 1, high.y); iz++) {
                    var pz = minZ + iz * Cell;
                    for (var ix = Mathf.Max(0, low.x); ix <= Mathf.Min(nx - 1, high.x); ix++) {
                        var px = minX + ix * Cell;
                        var along = lengthSquared > 0f
                            ? Mathf.Clamp01(((px - segment.x) * dx + (pz - segment.y) * dz) / lengthSquared)
                            : 0f;
                        var ox = px - (segment.x + dx * along);
                        var oz = pz - (segment.y + dz * along);
                        var squared = ox * ox + oz * oz;
                        var at = iz * nx + ix;
                        if (squared < unsigned[at] * unsigned[at]) {
                            unsigned[at] = Mathf.Sqrt(squared);
                        }
                    }
                }
            }

            var deepest = 1f;
            for (var i = 0; i < grid.Length; i++) {
                var face = nodeFace[i];
                var inside = face >= 0 && flags[face];
                grid[i] = inside ? unsigned[i] : -unsigned[i];
                if (inside && unsigned[i] > deepest) {
                    deepest = unsigned[i];
                }
            }

            if (safe) {
                safeExtent = extent;
                maxDepth = deepest;
            }
            else {
                solidExtent = extent;
            }

            Rebuilds++;
            LastRebuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000f
                / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>The node a point is at, pushed by <paramref name="grow"/> metres (positive down and left).</summary>
        private Vector2Int NodeRange(Vector2 point, float grow)
        {
            return new Vector2Int(Mathf.FloorToInt((point.x - grow - minX) / Cell), Mathf.FloorToInt((point.y - grow - minZ) / Cell));
        }

        // ----- reading --------------------------------------------------------------------------------------------

        private float Sample(float[] grid, float x, float z)
        {
            var fx = (x - minX) / Cell;
            var fz = (z - minZ) / Cell;
            var outside = 0f;
            if (fx < 0f || fz < 0f || fx > nx - 1 || fz > nz - 1) {
                var ox = fx < 0f ? -fx : fx > nx - 1 ? fx - (nx - 1) : 0f;
                var oz = fz < 0f ? -fz : fz > nz - 1 ? fz - (nz - 1) : 0f;
                outside = Mathf.Sqrt(ox * ox + oz * oz) * Cell;
                fx = Mathf.Clamp(fx, 0f, nx - 1);
                fz = Mathf.Clamp(fz, 0f, nz - 1);
            }

            var ix = Mathf.Min((int)fx, nx - 2);
            var iz = Mathf.Min((int)fz, nz - 2);
            var tx = fx - ix;
            var tz = fz - iz;
            var at = iz * nx + ix;
            var low = grid[at] + (grid[at + 1] - grid[at]) * tx;
            var high = grid[at + nx] + (grid[at + nx + 1] - grid[at + nx]) * tx;
            return low + (high - low) * tz - outside;
        }

        /// <summary>The gradient of the grid at a point, per metre: it points towards the floor.</summary>
        private void Gradient(float[] grid, float x, float z, out float gx, out float gz)
        {
            var fx = (x - minX) / Cell;
            var fz = (z - minZ) / Cell;
            if (fx < 0f || fz < 0f || fx > nx - 1 || fz > nz - 1) {
                // Outside the grid the way back is towards it.
                var cx = Mathf.Clamp(fx, 0f, nx - 1);
                var cz = Mathf.Clamp(fz, 0f, nz - 1);
                gx = cx - fx;
                gz = cz - fz;
                return;
            }

            var ix = Mathf.Min((int)fx, nx - 2);
            var iz = Mathf.Min((int)fz, nz - 2);
            var tx = fx - ix;
            var tz = fz - iz;
            var at = iz * nx + ix;
            var v00 = grid[at];
            var v10 = grid[at + 1];
            var v01 = grid[at + nx];
            var v11 = grid[at + nx + 1];
            gx = ((v10 - v00) * (1f - tz) + (v11 - v01) * tz) / Cell;
            gz = ((v01 - v00) * (1f - tx) + (v11 - v10) * tx) / Cell;
        }
    }
}
