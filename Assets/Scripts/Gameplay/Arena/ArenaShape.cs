using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The geometry of a floor that is not a circle, worked out from <see cref="GameTuning.ArenaSettings"/> alone: a
    /// lattice of convex faces that share whole edges, the faces gathered into tiles that crumble in waves (and the part
    /// that never crumbles), the outline with its holes, and the contour the cars start on. Nothing here touches the scene,
    /// so the props, the world below and the camera can read the outline when they are built, before the floor exists;
    /// the floor itself (<see cref="TiledLayout"/>) and its edge field (<see cref="GridEdge"/>) are made from it.
    /// <para>
    /// Faces, not cells: a pit or the hole of a donut is simply a place no face covers, so the outline, the collider and
    /// the distance field all come out of the faces' boundary and a curved rim stays smooth instead of stepping like a
    /// grid. Each shape cuts its lattice by hand and a final pass inserts the vertices a finer face leaves on the edge of a
    /// coarser neighbour, so the faces of a tile and its neighbours always meet without a crack.
    /// </para>
    /// </summary>
    public sealed class ArenaShape
    {
        /// <summary>The group of the faces that never crumble.</summary>
        public const int Core = -1;

        private const float Join = 0.001f;
        private const float SpawnStep = 0.25f;
        private const float PitClearance = 2f;

        // Sides of a quarter-turn corner or of a degree-10 wedge's arc: enough that a rim reads as round.
        private const int CornerSteps = 6;

        private readonly List<Vector2> vertices = new();
        private readonly Dictionary<Vector2Int, int> lookup = new();
        private readonly List<int[]> faces = new();
        private readonly List<int> faceGroup = new();
        private readonly List<int> groupWave = new();
        private readonly List<Vector2> groupDrift = new();
        private readonly List<Vector2[]> voids = new();
        private Vector2[] outline = Array.Empty<Vector2>();

        // Where the cars may start: the contour densified, only the stretches clear of pits and the middle, with the
        // distance along them.
        private Vector2[] spawnPoints = Array.Empty<Vector2>();
        private float[] spawnLength = Array.Empty<float>();

        private ArenaShape(GameTuning.ArenaShapeKind kind)
        {
            this.Kind = kind;
        }

        public GameTuning.ArenaShapeKind Kind { get; }

        /// <summary>Corners of the faces' lattice.</summary>
        public IReadOnlyList<Vector2> Vertices => vertices;

        public int FaceCount => faces.Count;

        /// <summary>The corners of a face in order, counter-clockwise seen from above (x to the right, z up).</summary>
        public int[] Face(int face) => faces[face];

        /// <summary>The tile a face belongs to, or <see cref="Core"/>.</summary>
        public int GroupOf(int face) => faceGroup[face];

        public int GroupCount => groupWave.Count;

        /// <summary>The wave a tile falls in.</summary>
        public int WaveOf(int group) => groupWave[group];

        /// <summary>The flat direction a tile drifts in as it falls: away from the rim, or into a hole.</summary>
        public Vector2 DriftOf(int group) => groupDrift[group];

        /// <summary>
        /// Every edge of the lattice (<see cref="EdgeA"/> to <see cref="EdgeB"/>, in the order of the face that has it
        /// first) with the faces on either side of it; -1 where there is none, which makes it a boundary.
        /// </summary>
        public int[] EdgeA { get; private set; } = Array.Empty<int>();
        public int[] EdgeB { get; private set; } = Array.Empty<int>();
        public int[] EdgeFaceA { get; private set; } = Array.Empty<int>();
        public int[] EdgeFaceB { get; private set; } = Array.Empty<int>();

        /// <summary>How many waves the floor crumbles in.</summary>
        public int WaveCount { get; private set; }

        /// <summary>The rim of the whole floor, counter-clockwise.</summary>
        public IReadOnlyList<Vector2> Outline => outline;

        /// <summary>The holes of the whole floor at the start (a donut's hole, the pits), each counter-clockwise.</summary>
        public IReadOnlyList<Vector2[]> Voids => voids;

        /// <summary>Radius of the smallest circle round the middle that holds the floor.</summary>
        public float FullExtent { get; private set; }

        /// <summary>Whether the cars have a contour to start on.</summary>
        public bool HasSpawn => spawnPoints.Length > 1;

        /// <summary>
        /// Builds the shape the settings describe; null for a circle, which has no tiles of this kind. The random numbers
        /// only order the crumbling, so a round can be told from the one before.
        /// </summary>
        public static ArenaShape Create(GameTuning.ArenaSettings settings, System.Random random)
        {
            ArenaShape shape;
            switch (settings.Shape) {
                case GameTuning.ArenaShapeKind.RoundedSquare:
                    shape = new ArenaShape(settings.Shape);
                    shape.BuildSquare(settings);
                    break;
                case GameTuning.ArenaShapeKind.HexDonut:
                    shape = new ArenaShape(settings.Shape);
                    shape.BuildDonut(settings, random);
                    break;
                case GameTuning.ArenaShapeKind.Stadium:
                    shape = new ArenaShape(settings.Shape);
                    shape.BuildStadium(settings, random);
                    break;
                default:
                    return null;
            }

            shape.Finish();
            return shape;
        }

        /// <summary>
        /// Distance from the middle to the rim along a flat direction (not necessarily a unit one).
        /// </summary>
        public float RimRadius(Vector2 direction)
        {
            var best = 0f;
            for (var i = 0; i < outline.Length; i++) {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                var edge = b - a;
                var denominator = Cross(direction, edge);
                if (Mathf.Abs(denominator) < 1e-6f) {
                    continue;
                }

                var along = Cross(a, edge) / denominator;
                var across = Cross(a, direction) / denominator;
                if (along > 0f && across >= -1e-4f && across <= 1.0001f) {
                    best = Mathf.Max(best, along * direction.magnitude);
                }
            }

            return best;
        }

        /// <summary>
        /// Where the car <paramref name="index"/> of <paramref name="count"/> starts: evenly by length along the contour,
        /// the first at the bottom of the screen and the next ones to the right of it, skipping the stretches near a pit or
        /// the middle.
        /// </summary>
        public bool TrySpawn(int index, int count, out Vector2 position)
        {
            position = default;
            if (!HasSpawn || count <= 0) {
                return false;
            }

            var total = spawnLength[spawnLength.Length - 1];
            var target = total * index / count;
            for (var i = 1; i < spawnPoints.Length; i++) {
                if (spawnLength[i] >= target) {
                    var span = spawnLength[i] - spawnLength[i - 1];
                    var share = span > 1e-5f ? (target - spawnLength[i - 1]) / span : 0f;
                    position = Vector2.Lerp(spawnPoints[i - 1], spawnPoints[i], share);
                    return true;
                }
            }

            position = spawnPoints[spawnPoints.Length - 1];
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        // ----- lattice -------------------------------------------------------------------------------------------

        private int Vertex(Vector2 point)
        {
            var key = new Vector2Int(Mathf.RoundToInt(point.x * 1000f), Mathf.RoundToInt(point.y * 1000f));
            if (!lookup.TryGetValue(key, out var id)) {
                id = vertices.Count;
                vertices.Add(new Vector2(key.x * Join, key.y * Join));
                lookup[key] = id;
            }

            return id;
        }

        private int NewGroup(int wave, Vector2 drift)
        {
            groupWave.Add(wave);
            groupDrift.Add(drift.sqrMagnitude > 1e-6f ? drift.normalized : Vector2.right);
            return groupWave.Count - 1;
        }

        private void AddFace(int group, IList<Vector2> points)
        {
            var ids = new List<int>(points.Count);
            foreach (var point in points) {
                var id = Vertex(point);
                if (ids.Count == 0 || ids[ids.Count - 1] != id) {
                    ids.Add(id);
                }
            }

            if (ids.Count > 1 && ids[0] == ids[ids.Count - 1]) {
                ids.RemoveAt(ids.Count - 1);
            }

            if (ids.Count < 3) {
                return;
            }

            if (Area(ids) < 0f) {
                ids.Reverse();
            }

            faces.Add(ids.ToArray());
            faceGroup.Add(group);
        }

        private float Area(IList<int> ids)
        {
            var sum = 0f;
            for (var i = 0; i < ids.Count; i++) {
                sum += Cross(vertices[ids[i]], vertices[ids[(i + 1) % ids.Count]]);
            }

            return sum * 0.5f;
        }

        private static Vector2 Centroid(IList<Vector2> points)
        {
            var sum = Vector2.zero;
            foreach (var point in points) {
                sum += point;
            }

            return sum / Mathf.Max(1, points.Count);
        }

        /// <summary>Points of an arc round a centre, both ends included.</summary>
        private static void Arc(List<Vector2> into, Vector2 centre, float radius, float from, float to, int steps)
        {
            for (var i = 0; i <= steps; i++) {
                var angle = Mathf.Lerp(from, to, (float)i / steps);
                into.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        // ----- the final pass ------------------------------------------------------------------------------------

        private void Finish()
        {
            RepairJunctions();
            BuildEdges();
            TraceOutline();
            var extent = 0f;
            foreach (var vertex in vertices) {
                extent = Mathf.Max(extent, vertex.magnitude);
            }

            FullExtent = extent;
            var wave = 0;
            foreach (var value in groupWave) {
                wave = Mathf.Max(wave, value + 1);
            }

            WaveCount = wave;
        }

        /// <summary>
        /// Puts every vertex that lies inside an edge of a face into that face's corners, so two faces that meet along
        /// part of an edge share the same vertices there.
        /// </summary>
        private void RepairJunctions()
        {
            var result = new List<int>();
            var onEdge = new List<(float, int)>();
            for (var f = 0; f < faces.Count; f++) {
                var ids = faces[f];
                result.Clear();
                var changed = false;
                for (var i = 0; i < ids.Length; i++) {
                    var a = ids[i];
                    var b = ids[(i + 1) % ids.Length];
                    result.Add(a);
                    var from = vertices[a];
                    var edge = vertices[b] - from;
                    var length = edge.magnitude;
                    onEdge.Clear();
                    for (var v = 0; v < vertices.Count; v++) {
                        if (v == a || v == b) {
                            continue;
                        }

                        var offset = vertices[v] - from;
                        var along = Vector2.Dot(offset, edge) / (length * length);
                        if (along <= 0f || along >= 1f) {
                            continue;
                        }

                        if (Mathf.Abs(Cross(offset, edge)) / length < Join * 2f) {
                            onEdge.Add((along, v));
                        }
                    }

                    onEdge.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                    foreach (var (_, v) in onEdge) {
                        result.Add(v);
                        changed = true;
                    }
                }

                if (changed) {
                    faces[f] = result.ToArray();
                }
            }
        }

        private void BuildEdges()
        {
            var lookupEdge = new Dictionary<(int, int), int>();
            var a = new List<int>();
            var b = new List<int>();
            var faceA = new List<int>();
            var faceB = new List<int>();
            for (var f = 0; f < faces.Count; f++) {
                var ids = faces[f];
                for (var i = 0; i < ids.Length; i++) {
                    var from = ids[i];
                    var to = ids[(i + 1) % ids.Length];
                    var key = from < to ? (from, to) : (to, from);
                    if (lookupEdge.TryGetValue(key, out var at)) {
                        faceB[at] = f;
                    }
                    else {
                        lookupEdge[key] = a.Count;
                        a.Add(from);
                        b.Add(to);
                        faceA.Add(f);
                        faceB.Add(-1);
                    }
                }
            }

            EdgeA = a.ToArray();
            EdgeB = b.ToArray();
            EdgeFaceA = faceA.ToArray();
            EdgeFaceB = faceB.ToArray();
        }

        /// <summary>
        /// Finds the loops the boundary of all faces makes: the one that encloses the others is the rim, the rest are holes.
        /// </summary>
        private void TraceOutline()
        {
            var counts = new Dictionary<(int, int), int>();
            foreach (var ids in faces) {
                for (var i = 0; i < ids.Length; i++) {
                    var a = ids[i];
                    var b = ids[(i + 1) % ids.Length];
                    var key = a < b ? (a, b) : (b, a);
                    counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
                }
            }

            var next = new Dictionary<int, int>();
            foreach (var ids in faces) {
                for (var i = 0; i < ids.Length; i++) {
                    var a = ids[i];
                    var b = ids[(i + 1) % ids.Length];
                    var key = a < b ? (a, b) : (b, a);
                    if (counts[key] == 1) {
                        next[a] = b;
                    }
                }
            }

            var seen = new HashSet<int>();
            var bestArea = float.NegativeInfinity;
            foreach (var start in new List<int>(next.Keys)) {
                if (seen.Contains(start)) {
                    continue;
                }

                var loop = new List<int>();
                var at = start;
                while (!seen.Contains(at) && next.TryGetValue(at, out var following)) {
                    seen.Add(at);
                    loop.Add(at);
                    at = following;
                }

                if (loop.Count < 3) {
                    continue;
                }

                var points = new Vector2[loop.Count];
                for (var i = 0; i < points.Length; i++) {
                    points[i] = vertices[loop[i]];
                }

                var area = Area(loop);
                if (area > 0f) {
                    if (area > bestArea) {
                        bestArea = area;
                        outline = points;
                    }
                }
                else {
                    Array.Reverse(points);
                    voids.Add(points);
                }
            }
        }

        // ----- rounded square ------------------------------------------------------------------------------------

        /// <summary>
        /// A board: a square core, a strip along each side cut into blocks, and a rounded block in each corner. The
        /// corners crumble first, then the left, right, far and near strips, side by side.
        /// </summary>
        private void BuildSquare(GameTuning.ArenaSettings s)
        {
            var hx = s.HalfSize.x;
            var hz = s.HalfSize.y;
            var c = Mathf.Min(s.CoreSize, Mathf.Min(hx, hz) - 1f);
            var radius = Mathf.Clamp(s.CornerRadius, 0f, Mathf.Min(hx, hz) - c - 0.5f);
            var blocks = Mathf.Max(1, s.TilesPerSide);

            AddFace(Core, new[] { new Vector2(-c, -c), new Vector2(c, -c), new Vector2(c, c), new Vector2(-c, c) });

            // Strips: 1 left, 2 right, 3 far, 4 near.
            for (var i = 0; i < blocks; i++) {
                var from = Mathf.Lerp(-c, c, (float)i / blocks);
                var to = Mathf.Lerp(-c, c, (float)(i + 1) / blocks);
                AddFace(NewGroup(1, Vector2.left),
                    new[] { new Vector2(-hx, from), new Vector2(-c, from), new Vector2(-c, to), new Vector2(-hx, to) });
                AddFace(NewGroup(2, Vector2.right),
                    new[] { new Vector2(c, from), new Vector2(hx, from), new Vector2(hx, to), new Vector2(c, to) });
                AddFace(NewGroup(3, Vector2.up),
                    new[] { new Vector2(from, c), new Vector2(to, c), new Vector2(to, hz), new Vector2(from, hz) });
                AddFace(NewGroup(4, Vector2.down),
                    new[] { new Vector2(from, -hz), new Vector2(to, -hz), new Vector2(to, -c), new Vector2(from, -c) });
            }

            // Corners, wave 0: each one block that follows the rounded rim.
            var corner = new List<Vector2>();
            foreach (var sx in new[] { 1f, -1f }) {
                foreach (var sz in new[] { 1f, -1f }) {
                    corner.Clear();
                    corner.Add(new Vector2(sx * c, sz * c));
                    corner.Add(new Vector2(sx * hx, sz * c));
                    if (radius > 0.01f) {
                        Arc(corner, new Vector2(hx - radius, hz - radius), radius, 0f, Mathf.PI * 0.5f, CornerSteps);
                        for (var i = 2; i < corner.Count; i++) {
                            corner[i] = new Vector2(sx * corner[i].x, sz * corner[i].y);
                        }
                    }
                    else {
                        corner.Add(new Vector2(sx * hx, sz * hz));
                    }

                    corner.Add(new Vector2(sx * c, sz * hz));
                    AddFace(NewGroup(0, new Vector2(sx, sz)), corner);
                }
            }

            // The contour the cars start on: the board pulled in.
            var inset = s.SpawnInset;
            var loop = new List<Vector2>();
            var r = Mathf.Max(0f, radius - inset);
            var ix = hx - inset;
            var iz = hz - inset;
            // From the bottom middle, counter-clockwise.
            loop.Add(new Vector2(0f, -iz));
            AddCorner(loop, new Vector2(ix - r, -(iz - r)), r, -Mathf.PI * 0.5f, 0f);
            AddCorner(loop, new Vector2(ix - r, iz - r), r, 0f, Mathf.PI * 0.5f);
            AddCorner(loop, new Vector2(-(ix - r), iz - r), r, Mathf.PI * 0.5f, Mathf.PI);
            AddCorner(loop, new Vector2(-(ix - r), -(iz - r)), r, Mathf.PI, Mathf.PI * 1.5f);
            SetSpawn(loop, s);
        }

        private static void AddCorner(List<Vector2> into, Vector2 centre, float radius, float from, float to)
        {
            if (radius < 0.01f) {
                into.Add(centre);
                return;
            }

            Arc(into, centre, radius, from, to, CornerSteps);
        }

        // ----- hexagon donut -------------------------------------------------------------------------------------

        /// <summary>
        /// A hexagon (a point towards and away from the camera, flats left and right) with a hexagonal hole, cut into rings
        /// between the apothems of the hole as it widens, the core and the rim. The rim crumbles in random tiles, each
        /// inner one only after the one outside it; the hole widens in two steps in between.
        /// </summary>
        private void BuildDonut(GameTuning.ArenaSettings s, System.Random random)
        {
            var outer = s.HalfSize.x;
            var core = Mathf.Min(s.CoreSize, outer - 1f);
            var holeFrom = s.HoleSize.x;
            var holeTo = Mathf.Min(s.HoleSize.y, core - 1f);
            var perSide = Mathf.Max(1, s.TilesPerSide);
            // 0 hole layer 1, 1 hole layer 2, 2 and 3 the core, 4 the inner rim layer, 5 the outer rim layer.
            var apothem = new[]
            {
                holeFrom, Mathf.Lerp(holeFrom, holeTo, 0.5f), holeTo, Mathf.Lerp(holeTo, core, 0.5f), core,
                Mathf.Lerp(core, outer, 0.5f), outer,
            };

            var rimOut = new int[perSide * 6];
            var rimIn = new int[perSide * 6];
            var holeIn = new int[6];
            var holeOut = new int[6];
            for (var layer = 0; layer < 6; layer++) {
                var segments = layer < 2 ? 1 : perSide;
                for (var side = 0; side < 6; side++) {
                    for (var j = 0; j < segments; j++) {
                        var t0 = (float)j / segments;
                        var t1 = (float)(j + 1) / segments;
                        var quad = new[]
                        {
                            Flat(apothem[layer], side, t0), Flat(apothem[layer], side, t1),
                            Flat(apothem[layer + 1], side, t1), Flat(apothem[layer + 1], side, t0),
                        };
                        var middle = Centroid(quad);
                        if (layer >= 2 && layer <= 3) {
                            AddFace(Core, quad);
                        }
                        else if (layer < 2) {
                            var group = NewGroup(-1, -middle);
                            (layer == 0 ? holeIn : holeOut)[side] = group;
                            AddFace(group, quad);
                        }
                        else {
                            var group = NewGroup(-1, middle);
                            (layer == 4 ? rimIn : rimOut)[side * perSide + j] = group;
                            AddFace(group, quad);
                        }
                    }
                }
            }

            // Waves: the hole widens in two of them, the rest take random rim tiles.
            var waves = Mathf.Max(4, s.Waves);
            var holeWaves = new[] { waves / 3, waves * 2 / 3 };
            foreach (var group in holeIn) {
                groupWave[group] = holeWaves[0];
            }

            foreach (var group in holeOut) {
                groupWave[group] = holeWaves[1];
            }

            var rimWaves = new List<int>();
            for (var w = 0; w < waves; w++) {
                if (w != holeWaves[0] && w != holeWaves[1]) {
                    rimWaves.Add(w);
                }
            }

            var assigned = new bool[perSide * 12];
            var remaining = perSide * 12;
            var available = new List<int>();
            for (var k = 0; k < rimWaves.Count; k++) {
                var take = k == rimWaves.Count - 1 ? remaining : Mathf.CeilToInt((float)remaining / (rimWaves.Count - k));
                available.Clear();
                for (var i = 0; i < perSide * 6; i++) {
                    if (!assigned[i]) {
                        available.Add(i);
                    }
                    else if (!assigned[perSide * 6 + i]) {
                        // The tile inside a fallen one: twice as likely, so the inner layer is not left to the end.
                        available.Add(perSide * 6 + i);
                        available.Add(perSide * 6 + i);
                    }
                }

                // Taken from a snapshot: a tile made available by this wave's own picks waits for the next.
                var picked = 0;
                var newly = new List<int>();
                while (picked < take && available.Count > 0) {
                    var at = random.Next(available.Count);
                    var tile = available[at];
                    available.RemoveAll(value => value == tile);
                    newly.Add(tile);
                    picked++;
                }

                if (k == rimWaves.Count - 1) {
                    for (var i = 0; i < assigned.Length; i++) {
                        if (!assigned[i] && !newly.Contains(i)) {
                            newly.Add(i);
                        }
                    }
                }

                foreach (var tile in newly) {
                    assigned[tile] = true;
                    groupWave[tile < perSide * 6 ? rimOut[tile] : rimIn[tile - perSide * 6]] = rimWaves[k];
                }

                remaining -= newly.Count;
            }

            // The contour the cars start on: a hexagon inside the rim.
            var loop = new List<Vector2>();
            var spawnApothem = outer - s.SpawnInset;
            for (var side = 0; side < 6; side++) {
                loop.Add(Flat(spawnApothem, side, 0f));
            }

            // Counter-clockwise from the bottom: the hexagon's corners go round from straight up, so start at the one
            // straight down (corner 3) and keep the order.
            loop = Rotate(loop, 3);
            SetSpawn(loop, s);
        }

        /// <summary>A point of a hexagon with flats left and right, at the apothem, on a side, a share of the way.</summary>
        private static Vector2 Flat(float apothem, int side, float t)
        {
            var radius = apothem / Mathf.Cos(Mathf.PI / 6f);
            var from = Mathf.PI * 0.5f + side * Mathf.PI / 3f;
            var to = from + Mathf.PI / 3f;
            var a = new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * radius;
            var b = new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * radius;
            return a + (b - a) * t;
        }

        private static List<Vector2> Rotate(List<Vector2> list, int by)
        {
            var result = new List<Vector2>(list.Count);
            for (var i = 0; i < list.Count; i++) {
                result.Add(list[(i + by) % list.Count]);
            }

            return result;
        }

        // ----- stadium -------------------------------------------------------------------------------------------

        /// <summary>
        /// A stadium, long along the camera's depth: two straight sides and a half disc at each end, with a pit left and
        /// right of the middle (not covered by any face). Nested bands from the rim to the core are cut into pieces that
        /// crumble one after another round the rim, the outer band first.
        /// </summary>
        private void BuildStadium(GameTuning.ArenaSettings s, System.Random random)
        {
            var hw = s.HalfSize.x;
            var straight = Mathf.Max(0f, s.HalfSize.y - hw);
            var cw = Mathf.Min(s.CoreSize, hw - 1f);
            var wedges = Mathf.Max(1, s.TilesPerSide);
            var hasPits = s.Pits.y > 0.01f;
            var pitInner = s.Pits.x;
            var pitOuter = s.Pits.x + s.Pits.y;
            var pitHalf = Mathf.Min(s.PitHalfLength, straight);
            var bands = hasPits && pitOuter < hw && pitInner > cw
                ? new[] { hw, pitOuter, pitInner, cw }
                : new[] { hw, hw - (hw - cw) / 3f, hw - (hw - cw) * 2f / 3f, cw };

            var cuts = new List<float> { -straight };
            if (hasPits && pitHalf > 0.1f && pitHalf < straight - 0.1f) {
                cuts.Add(-pitHalf);
                cuts.Add(0f);
                cuts.Add(pitHalf);
            }
            else if (straight > 4f) {
                cuts.Add(0f);
            }

            cuts.Add(straight);

            // The pieces of each band with a sort key that goes round the rim from the right, bottom to top.
            var bandPieces = new List<(float key, int group)>[3];
            for (var band = 0; band < 3; band++) {
                bandPieces[band] = new List<(float, int)>();
                var far = bands[band];
                var near = bands[band + 1];
                for (var side = 0; side < 2; side++) {
                    var sign = side == 0 ? 1f : -1f;
                    for (var j = 0; j + 1 < cuts.Count; j++) {
                        var z0 = cuts[j];
                        var z1 = cuts[j + 1];
                        if (hasPits && band == 1 && Mathf.Abs((z0 + z1) * 0.5f) < pitHalf) {
                            continue;
                        }

                        var group = NewGroup(-1, new Vector2(sign, 0f));
                        AddFace(group, new[]
                        {
                            new Vector2(sign * near, z0), new Vector2(sign * far, z0), new Vector2(sign * far, z1),
                            new Vector2(sign * near, z1),
                        });
                        // Right side from the bottom up (0..1), left side from the top down (2..3).
                        var along = (z0 + z1) * 0.5f + straight;
                        bandPieces[band].Add((side == 0 ? along : 2000f + (2f * straight - along), group));
                    }
                }

                for (var cap = 0; cap < 2; cap++) {
                    var centre = new Vector2(0f, cap == 0 ? straight : -straight);
                    for (var j = 0; j < wedges; j++) {
                        var a0 = Mathf.PI * j / wedges + (cap == 0 ? 0f : Mathf.PI);
                        var a1 = Mathf.PI * (j + 1) / wedges + (cap == 0 ? 0f : Mathf.PI);
                        // A wedge of a band curves both ways, so it is cut into one convex piece per step of its arc.
                        var steps = Mathf.Max(2, Mathf.CeilToInt(180f / wedges / 10f));
                        var middle = (a0 + a1) * 0.5f;
                        var group = NewGroup(-1, new Vector2(Mathf.Cos(middle), Mathf.Sin(middle)));
                        for (var step = 0; step < steps; step++) {
                            var from = new Vector2(Mathf.Cos(Mathf.Lerp(a0, a1, (float)step / steps)),
                                Mathf.Sin(Mathf.Lerp(a0, a1, (float)step / steps)));
                            var to = new Vector2(Mathf.Cos(Mathf.Lerp(a0, a1, (float)(step + 1) / steps)),
                                Mathf.Sin(Mathf.Lerp(a0, a1, (float)(step + 1) / steps)));
                            AddFace(group, new[] { centre + from * far, centre + to * far, centre + to * near, centre + from * near });
                        }

                        bandPieces[band].Add((1000f + (cap == 0 ? 0f : 2000f) + j, group));
                    }
                }
            }

            // The core: a rectangle and a fan of wedges in each cap.
            AddFace(Core, new[]
            {
                new Vector2(-cw, -straight), new Vector2(cw, -straight), new Vector2(cw, straight),
                new Vector2(-cw, straight),
            });
            for (var cap = 0; cap < 2; cap++) {
                var centre = new Vector2(0f, cap == 0 ? straight : -straight);
                for (var j = 0; j < wedges; j++) {
                    var a0 = Mathf.PI * j / wedges + (cap == 0 ? 0f : Mathf.PI);
                    var a1 = Mathf.PI * (j + 1) / wedges + (cap == 0 ? 0f : Mathf.PI);
                    var points = new List<Vector2> { centre };
                    Arc(points, centre, cw, a0, a1, Mathf.Max(2, Mathf.CeilToInt(180f / wedges / 10f)));
                    AddFace(Core, points);
                }
            }

            // Waves: the bands from the rim in, each piece after the one before it round the rim, in near-equal chunks.
            var order = new List<int>();
            for (var band = 0; band < 3; band++) {
                bandPieces[band].Sort((x, y) => x.key.CompareTo(y.key));
                var start = random.Next(bandPieces[band].Count);
                for (var i = 0; i < bandPieces[band].Count; i++) {
                    order.Add(bandPieces[band][(start + i) % bandPieces[band].Count].group);
                }
            }

            var waves = Mathf.Min(Mathf.Max(2, s.Waves), order.Count);
            for (var i = 0; i < order.Count; i++) {
                groupWave[order[i]] = Mathf.Min(waves - 1, i * waves / order.Count);
            }

            // The contour the cars start on: a smaller stadium from the bottom, counter-clockwise.
            var w = hw - s.SpawnInset;
            var loop = new List<Vector2>();
            Arc(loop, new Vector2(0f, -straight), w, Mathf.PI * 1.5f, Mathf.PI * 2f, 12);
            loop.Add(new Vector2(w, straight));
            Arc(loop, new Vector2(0f, straight), w, 0f, Mathf.PI, 24);
            loop.Add(new Vector2(-w, -straight));
            Arc(loop, new Vector2(0f, -straight), w, Mathf.PI, Mathf.PI * 1.5f, 12);
            SetSpawn(loop, s, hasPits ? new Rect(pitInner, -pitHalf, pitOuter - pitInner, pitHalf * 2f) : default,
                hasPits);
        }

        // ----- the starting contour ------------------------------------------------------------------------------

        private void SetSpawn(List<Vector2> loop, GameTuning.ArenaSettings s)
        {
            SetSpawn(loop, s, default, false);
        }

        /// <summary>
        /// Densifies the contour and keeps the samples clear of the pits (on both sides of the middle) and of the middle;
        /// the length runs along what is left, jumping over a stretch that was cut out.
        /// </summary>
        private void SetSpawn(List<Vector2> loop, GameTuning.ArenaSettings s, Rect pit, bool hasPit)
        {
            var points = new List<Vector2>();
            var lengths = new List<float>();
            var run = 0f;
            var previous = Vector2.zero;
            var hasPrevious = false;
            for (var i = 0; i < loop.Count; i++) {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                var steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / SpawnStep));
                for (var step = 0; step < steps; step++) {
                    var point = Vector2.Lerp(a, b, (float)step / steps);
                    if (!IsClear(point, s, pit, hasPit)) {
                        hasPrevious = false;
                        continue;
                    }

                    if (hasPrevious) {
                        run += Vector2.Distance(previous, point);
                    }

                    points.Add(point);
                    lengths.Add(run);
                    previous = point;
                    hasPrevious = true;
                }
            }

            // Back to the start so the last car is not left alone with the longest gap.
            if (points.Count > 1) {
                run += Vector2.Distance(points[points.Count - 1], points[0]) < SpawnStep * 3f
                    ? Vector2.Distance(points[points.Count - 1], points[0])
                    : 0f;
                points.Add(points[0]);
                lengths.Add(run);
            }

            spawnPoints = points.ToArray();
            spawnLength = lengths.ToArray();
        }

        private static bool IsClear(Vector2 point, GameTuning.ArenaSettings s, Rect pit, bool hasPit)
        {
            if (point.magnitude < s.SpawnClear) {
                return false;
            }

            if (!hasPit) {
                return true;
            }

            var dx = Mathf.Max(0f, Mathf.Max(pit.xMin - Mathf.Abs(point.x), Mathf.Abs(point.x) - pit.xMax));
            var dz = Mathf.Max(0f, Mathf.Max(pit.yMin - point.y, point.y - pit.yMax));
            return Mathf.Sqrt(dx * dx + dz * dz) >= PitClearance;
        }
    }
}
