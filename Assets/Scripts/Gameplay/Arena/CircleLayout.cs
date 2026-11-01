using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// A round floor: a solid disc of <see cref="GameTuning.ArenaSettings.MinRadius"/> inside rings about
    /// <see cref="GameTuning.ArenaSettings.CrumbleStep"/> wide out to the full radius, each ring cut into blocks. A wave
    /// is a ring, counted from the outside in. The one collider is a plain disc as wide as the rings still in place,
    /// swapped for a smaller one when a ring falls: a car never catches on a seam between two blocks, and a car on a
    /// falling ring has nothing under it.
    /// </summary>
    public sealed class CircleLayout : ArenaLayout
    {
        // Sides of the whole circle; enough that the rim reads as a circle from the camera's distance.
        private const int Segments = 96;

        // Depth of the floor below the driving surface, in metres; seen on the rim and on falling blocks.
        private const float Thickness = SlabThickness;

        private readonly CircleEdge edge;
        private readonly FloorUv uv;
        private readonly Ring[] rings;
        private readonly float innerRadius;

        // [i] is a disc out to ring i's outer edge, the last one the solid disc in the middle; the collider is the one
        // for the outermost ring still in place.
        private readonly Mesh[] colliderMeshes;
        private int colliderRing;

        /// <param name="edge">The edge this layout keeps up to date.</param>
        /// <param name="blockLength">Length a block aims for along its ring, in metres.</param>
        /// <param name="minBlocks">Fewest blocks a ring is cut into.</param>
        public CircleLayout(GameTuning.ArenaSettings settings, CircleEdge edge, float blockLength, int minBlocks)
            : this(settings, edge, blockLength, minBlocks, FloorUv.ForCircle(settings.Radius))
        {
        }

        /// <param name="uv">Where the floor's texture lies on it; baked into every mesh.</param>
        public CircleLayout(GameTuning.ArenaSettings settings, CircleEdge edge, float blockLength, int minBlocks,
            FloorUv uv)
        {
            this.edge = edge;
            this.uv = uv;

            // The band between the solid middle and the full radius divides into whole rings close to the step, so no
            // ring is left a sliver. No step, or no band, leaves the whole floor solid.
            var band = settings.Radius - settings.MinRadius;
            var ringCount = band > 0f && settings.CrumbleStep > 0f
                ? Mathf.Max(1, Mathf.RoundToInt(band / settings.CrumbleStep))
                : 0;
            innerRadius = ringCount > 0 ? settings.MinRadius : settings.Radius;
            var width = ringCount > 0 ? band / ringCount : 0f;

            rings = new Ring[ringCount];
            colliderMeshes = new Mesh[ringCount + 1];
            for (var i = 0; i < ringCount; i++) {
                var outer = settings.Radius - i * width;
                var inner = outer - width;
                var middle = (inner + outer) * 0.5f;
                var count = Mathf.Max(minBlocks, Mathf.RoundToInt(2f * Mathf.PI * middle / blockLength));
                rings[i] = new Ring(inner, outer, count);
                colliderMeshes[i] = Keep(BuildDisc(outer, "Arena Floor Collider"));
            }

            colliderMeshes[ringCount] = Keep(BuildDisc(innerRadius, "Arena Floor Collider"));
            // All visual circles use the same cuts, including every block end. Independent arc approximations leave
            // slivers between rings even with no inset. The collider deliberately keeps its original 96-sided mesh.
            var turns = BuildTurns();
            var outerRim = BuildRim(settings.Radius, turns);
            for (var i = 0; i < ringCount; i++) {
                var radius = i + 1 < ringCount ? rings[i + 1].Outer : innerRadius;
                var innerRim = BuildRim(radius, turns);
                BuildRing(i, innerRim, outerRim, turns);
                outerRim = innerRim;
            }

            CoreMesh = Keep(BuildCore(outerRim));
            colliderRing = 0;
            ColliderMesh = colliderMeshes[0];
            Refresh(new WaveState[ringCount]);
        }

        public override int WaveCount => rings.Length;

        public override CrumbleWave Wave(int wave)
        {
            return new CrumbleWave(wave, rings[wave].Inner, rings[wave].Outer);
        }

        public override bool Contains(int wave, Vector3 position)
        {
            var distance = new Vector2(position.x, position.z).magnitude;
            return distance >= rings[wave].Inner && distance <= rings[wave].Outer;
        }

        public override bool Refresh(IReadOnlyList<WaveState> states)
        {
            // The safe radius reaches out over the unbroken run of standing rings from the middle.
            var safe = innerRadius;
            for (var i = rings.Length - 1; i >= 0 && states[i] == WaveState.Standing; i--) {
                safe = rings[i].Outer;
            }

            // The collider reaches out over the unbroken run of rings from the middle that have not fallen, shaking or not.
            var ring = rings.Length;
            while (ring > 0 && states[ring - 1] is WaveState.Standing or WaveState.Warning) {
                ring--;
            }

            var isNew = ring != colliderRing;
            if (isNew) {
                colliderRing = ring;
                ColliderMesh = colliderMeshes[ring];
            }

            var solid = ring < rings.Length ? rings[ring].Outer : innerRadius;
            edge.Set(safe, solid);
            return isNew;
        }

        /// <summary>
        /// Includes every block end in every ring and the core, so both sides of a shared edge have the same vertices.
        /// Double turns keep distinct cuts apart without introducing nearly duplicate cuts for equal fractions.
        /// </summary>
        private double[] BuildTurns()
        {
            var turns = new SortedSet<double>();
            for (var i = 0; i < Segments; i++) {
                turns.Add((double)i / Segments);
            }

            foreach (var ring in rings) {
                for (var j = 0; j < ring.Count; j++) {
                    turns.Add((2d * j + 1d) / (2d * ring.Count));
                }
            }

            var result = new double[turns.Count];
            turns.CopyTo(result);
            return result;
        }

        private static Vector3[] BuildRim(float radius, double[] turns)
        {
            var rim = new Vector3[turns.Length];
            for (var i = 0; i < rim.Length; i++) {
                rim[i] = Point(radius, (float)(turns[i] * (2d * Mathf.PI)));
            }

            return rim;
        }

        /// <summary>
        /// Retains each block's pivot and rest rotation for the existing shake and tumble, but takes its corners and UVs
        /// from shared floor-space rims rather than rotating a separate approximation of its arcs.
        /// </summary>
        private void BuildRing(int index, Vector3[] inner, Vector3[] outer, double[] turns)
        {
            var ring = rings[index];
            var middle = (ring.Inner + ring.Outer) * 0.5f;
            var count = ring.Count;
            var halfAngle = Mathf.PI / count;
            // Blocks turn about their own middle as they tumble, so the mesh is built round it.
            var pivot = new Vector3(middle, -Thickness * 0.5f, 0f);

            for (var j = 0; j < count; j++) {
                // A turn about the vertical by minus the angle takes the block's +X, its outward direction, to that angle.
                var rotation = Quaternion.Euler(0f, -j * 2f * halfAngle * Mathf.Rad2Deg, 0f);
                var position = rotation * pivot;
                var fromTurn = (2d * (j == 0 ? count : j) - 1d) / (2d * count);
                var toTurn = (2d * j + 1d) / (2d * count);
                var from = System.Array.BinarySearch(turns, fromTurn);
                var to = System.Array.BinarySearch(turns, toTurn);
                var mesh = Keep(BuildBlock(inner, outer, from, to, position, Quaternion.Inverse(rotation)));
                tiles.Add(new FloorTile($"Ring {index} Block {j}", mesh, position, rotation, index));
            }
        }

        /// <summary>The core meets the innermost ring on its exact polygon, without a gap or overlapping top faces.</summary>
        private Mesh BuildCore(Vector3[] rim)
        {
            var builder = new FloorMeshBuilder();
            var down = Vector3.down * Thickness;
            for (var i = 0; i < rim.Length; i++) {
                var a = rim[i];
                var b = rim[(i + 1) % rim.Length];
                builder.AddTriangle(Vector3.zero, a, b, Vector3.up, uv.Top(0f, 0f), uv.Top(a.x, a.z), uv.Top(b.x, b.z));
                SideQuad(builder, a, b, down, a, b, a.normalized, b.normalized);
            }

            return builder.ToMesh("Arena Floor");
        }

        private static Vector3 Point(float radius, float angle)
        {
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private static Vector3 Outward(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        /// <summary>
        /// The top as a fan round the centre and the rim as a band facing out, each with its own vertices so the edge
        /// between them stays sharp.
        /// </summary>
        private Mesh BuildDisc(float radius, string name)
        {
            var builder = new FloorMeshBuilder();
            var down = Vector3.down * Thickness;
            for (var i = 0; i < Segments; i++) {
                var from = i * Mathf.PI * 2f / Segments;
                var to = (i + 1) * Mathf.PI * 2f / Segments;
                var a = Point(radius, from);
                var b = Point(radius, to);
                builder.AddTriangle(Vector3.zero, a, b, Vector3.up, uv.Top(0f, 0f), uv.Top(a.x, a.z), uv.Top(b.x, b.z));
                var turnFrom = FloorUv.Turn(a.x, a.z);
                var turnTo = FloorUv.NextTo(FloorUv.Turn(b.x, b.z), turnFrom);
                builder.AddQuad(a, b, b + down, a + down, Outward(from), Outward(to), Outward(to), Outward(from),
                    FloorUv.Side(turnFrom, 0f), FloorUv.Side(turnTo, 0f), FloorUv.Side(turnTo, 1f),
                    FloorUv.Side(turnFrom, 1f));
            }

            return builder.ToMesh(name);
        }

        /// <summary>
        /// A closed slab between two shared rims. Walls start at the top edge and extend only downwards, so they are
        /// hidden by the continuous top at rest and become visible when the block moves.
        /// </summary>
        private Mesh BuildBlock(Vector3[] near, Vector3[] far, int from, int to, Vector3 pivot, Quaternion unrotate)
        {
            var down = Vector3.down * Thickness;
            var builder = new FloorMeshBuilder();
            var i = from;
            do {
                var next = (i + 1) % near.Length;
                var wn0 = near[i];
                var wn1 = near[next];
                var wf0 = far[i];
                var wf1 = far[next];
                var n0 = unrotate * (wn0 - pivot);
                var n1 = unrotate * (wn1 - pivot);
                var f0 = unrotate * (wf0 - pivot);
                var f1 = unrotate * (wf1 - pivot);

                builder.AddQuad(n0, n1, f1, f0, Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                    uv.Top(wn0.x, wn0.z), uv.Top(wn1.x, wn1.z), uv.Top(wf1.x, wf1.z), uv.Top(wf0.x, wf0.z));
                var bottom = FloorUv.Side(0f, 0.5f);
                builder.AddQuad(n0 + down, n1 + down, f1 + down, f0 + down,
                    Vector3.down, Vector3.down, Vector3.down, Vector3.down, bottom, bottom, bottom, bottom);
                SideQuad(builder, f0, f1, down, wf0, wf1, unrotate * wf0.normalized, unrotate * wf1.normalized);
                SideQuad(builder, n0, n1, down, wn0, wn1, unrotate * -wn0.normalized, unrotate * -wn1.normalized);
                i = next;
            } while (i != to);

            AddEnd(builder, near[from], far[from], pivot, down, unrotate, false);
            AddEnd(builder, near[to], far[to], pivot, down, unrotate, true);
            return builder.ToMesh("Arena Block");
        }

        private static void AddEnd(FloorMeshBuilder builder, Vector3 near, Vector3 far, Vector3 pivot, Vector3 down,
            Quaternion unrotate, bool isLast)
        {
            var a = unrotate * (near - pivot);
            var b = unrotate * (far - pivot);
            var tangent = Vector3.Cross(far.normalized, Vector3.up);
            var normal = unrotate * (isLast ? tangent : -tangent);
            SideQuad(builder, a, b, down, near, far, normal, normal);
        }

        /// <summary>A wall below the top edge a-b: the side strip of the texture, wrapping round the middle.</summary>
        private static void SideQuad(FloorMeshBuilder builder, Vector3 a, Vector3 b, Vector3 down, Vector3 worldA,
            Vector3 worldB, Vector3 normalA, Vector3 normalB)
        {
            var turnA = FloorUv.Turn(worldA.x, worldA.z);
            var turnB = FloorUv.NextTo(FloorUv.Turn(worldB.x, worldB.z), turnA);
            builder.AddQuad(a, b, b + down, a + down, normalA, normalB, normalB, normalA,
                FloorUv.Side(turnA, 0f), FloorUv.Side(turnB, 0f), FloorUv.Side(turnB, 1f), FloorUv.Side(turnA, 1f));
        }

        private sealed class Ring
        {
            public readonly float Inner;
            public readonly float Outer;
            public readonly int Count;

            public Ring(float inner, float outer, int count)
            {
                this.Inner = inner;
                this.Outer = outer;
                this.Count = count;
            }
        }
    }
}
