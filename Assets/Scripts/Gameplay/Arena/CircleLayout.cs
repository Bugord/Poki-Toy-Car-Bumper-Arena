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
        private const float Thickness = 0.5f;

        private readonly CircleEdge edge;
        private readonly int minBlocks;
        private readonly float blockLength;
        private readonly Ring[] rings;
        private readonly float innerRadius;

        // [i] is a disc out to ring i's outer edge, the last one the solid disc in the middle; the collider is the one
        // for the outermost ring still in place.
        private readonly Mesh[] colliderMeshes;
        private int colliderRing;

        /// <param name="edge">The edge this layout keeps up to date.</param>
        /// <param name="blockLength">Length a block aims for along its ring, in metres.</param>
        /// <param name="minBlocks">Fewest blocks a ring is cut into.</param>
        /// <param name="seam">Width of the groove drawn between two blocks, in metres.</param>
        public CircleLayout(GameTuning.ArenaSettings settings, CircleEdge edge, float blockLength, int minBlocks,
            float seam)
        {
            this.edge = edge;
            this.blockLength = blockLength;
            this.minBlocks = minBlocks;

            // The band between the solid middle and the full radius divides into whole rings close to the step, so no
            // ring is left a sliver. No step, or no band, leaves the whole floor solid.
            var band = settings.Radius - settings.MinRadius;
            var ringCount = band > 0f && settings.CrumbleStep > 0f
                ? Mathf.Max(1, Mathf.RoundToInt(band / settings.CrumbleStep))
                : 0;
            innerRadius = ringCount > 0 ? settings.MinRadius : settings.Radius;
            var width = ringCount > 0 ? band / ringCount : 0f;
            var halfSeam = ringCount > 0 ? seam * 0.5f : 0f;

            rings = new Ring[ringCount];
            colliderMeshes = new Mesh[ringCount + 1];
            for (var i = 0; i < ringCount; i++) {
                var outer = settings.Radius - i * width;
                rings[i] = BuildRing(i, outer - width, outer, halfSeam);
                colliderMeshes[i] = Keep(BuildDisc(outer, "Arena Floor Collider"));
            }

            colliderMeshes[ringCount] = Keep(BuildDisc(innerRadius, "Arena Floor Collider"));
            CoreMesh = Keep(BuildDisc(innerRadius - halfSeam, "Arena Floor"));
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
        /// Cuts a ring into equal blocks about <see cref="blockLength"/> long. The blocks of a ring are alike, so they
        /// share one mesh, turned into place.
        /// </summary>
        private Ring BuildRing(int index, float inner, float outer, float halfSeam)
        {
            var middle = (inner + outer) * 0.5f;
            var count = Mathf.Max(minBlocks, Mathf.RoundToInt(2f * Mathf.PI * middle / blockLength));
            var halfAngle = Mathf.PI / count;
            // Blocks turn about their own middle as they tumble, so the mesh is built round it.
            var pivot = new Vector3(middle, -Thickness * 0.5f, 0f);
            var mesh = Keep(BuildBlock(inner + halfSeam, outer - halfSeam, halfAngle, halfSeam, pivot));

            for (var j = 0; j < count; j++) {
                // A turn about the vertical by minus the angle takes the block's +X, its outward direction, to that angle.
                var rotation = Quaternion.Euler(0f, -j * 2f * halfAngle * Mathf.Rad2Deg, 0f);
                tiles.Add(new FloorTile($"Ring {index} Block {j}", mesh, rotation * pivot, rotation, index));
            }

            return new Ring(inner, outer);
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
        private static Mesh BuildDisc(float radius, string name)
        {
            var builder = new FloorMeshBuilder();
            var down = Vector3.down * Thickness;
            for (var i = 0; i < Segments; i++) {
                var from = i * Mathf.PI * 2f / Segments;
                var to = (i + 1) * Mathf.PI * 2f / Segments;
                var a = Point(radius, from);
                var b = Point(radius, to);
                builder.AddTriangle(Vector3.zero, a, b, Vector3.up);
                builder.AddQuad(a, b, b + down, a + down, Outward(from), Outward(to), Outward(to), Outward(from));
            }

            return builder.ToMesh(name);
        }

        /// <summary>
        /// One block of a ring, centred on +X and spanning twice the half angle, round the given pivot: a top, a bottom,
        /// the outer and inner rims and the two cut ends, all solid so it reads as a block while it tumbles. The cut ends
        /// are drawn the inset in from the true cut, parallel to it, so two blocks side by side leave an even seam.
        /// </summary>
        private static Mesh BuildBlock(float near, float far, float halfAngle, float inset, Vector3 pivot)
        {
            // The same inset is a wider angle on the inner arc than on the outer one.
            var nearEnd = halfAngle - Mathf.Asin(Mathf.Clamp01(inset / near));
            var farEnd = halfAngle - Mathf.Asin(Mathf.Clamp01(inset / far));
            var steps = Mathf.Max(1, Mathf.CeilToInt(Segments * halfAngle / Mathf.PI));
            var down = Vector3.down * Thickness;

            var builder = new FloorMeshBuilder();
            for (var i = 0; i < steps; i++) {
                var from = (float)i / steps;
                var to = (float)(i + 1) / steps;
                var nearFrom = Mathf.Lerp(-nearEnd, nearEnd, from);
                var nearTo = Mathf.Lerp(-nearEnd, nearEnd, to);
                var farFrom = Mathf.Lerp(-farEnd, farEnd, from);
                var farTo = Mathf.Lerp(-farEnd, farEnd, to);
                var n0 = Point(near, nearFrom) - pivot;
                var n1 = Point(near, nearTo) - pivot;
                var f0 = Point(far, farFrom) - pivot;
                var f1 = Point(far, farTo) - pivot;

                builder.AddQuad(n0, n1, f1, f0, Vector3.up, Vector3.up, Vector3.up, Vector3.up);
                builder.AddQuad(n0 + down, n1 + down, f1 + down, f0 + down,
                    Vector3.down, Vector3.down, Vector3.down, Vector3.down);
                builder.AddQuad(f0, f1, f1 + down, f0 + down,
                    Outward(farFrom), Outward(farTo), Outward(farTo), Outward(farFrom));
                builder.AddQuad(n0, n1, n1 + down, n0 + down,
                    -Outward(nearFrom), -Outward(nearTo), -Outward(nearTo), -Outward(nearFrom));
            }

            AddEnd(builder, near, far, -nearEnd, -farEnd, -Tangent(-halfAngle), pivot, down);
            AddEnd(builder, near, far, nearEnd, farEnd, Tangent(halfAngle), pivot, down);
            return builder.ToMesh("Arena Block");
        }

        private static void AddEnd(FloorMeshBuilder builder, float near, float far, float nearAngle, float farAngle,
            Vector3 normal, Vector3 origin, Vector3 down)
        {
            var a = Point(near, nearAngle) - origin;
            var b = Point(far, farAngle) - origin;
            builder.AddQuad(a, b, b + down, a + down, normal, normal, normal, normal);
        }

        // The direction along the circle at an angle, the way the angle grows.
        private static Vector3 Tangent(float angle)
        {
            return new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        private sealed class Ring
        {
            public readonly float Inner;
            public readonly float Outer;

            public Ring(float inner, float outer)
            {
                this.Inner = inner;
                this.Outer = outer;
            }
        }
    }
}
