using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The fence of the HOT POTATO mode (<see cref="RoundRules.IsHotPotato"/>): nobody falls off there, so a low red and
    /// white bumper runs round the rim of the floor and round every hole and pit in it (the candy arena's hole, the
    /// playroom's pits), and a tall, unseen wall stands on it, so no hit or blast lifts a car over. The walls sit just off
    /// the floor, on the default layer: the steering ray looks for the floor on the arena's layer and never sees them.
    /// They have no friction, so a car driven into the fence slides along it. In any other round it builds nothing.
    /// <para>
    /// Built once, as the scene starts: the floor does not crumble in this mode, so the outline it fences stays as it
    /// is. The look is two meshes (the red and the white stripes, merged), so the fence costs two draws and their
    /// outlines, however long it is. The stripes are prisms cut square across an edge and mitred at its corners, so they
    /// meet end to end: no two of them overlap (overlapping red and white faces flicker) and no corner is left open.
    /// </para>
    /// </summary>
    public sealed class ArenaFence : IStartable, IDisposable
    {
        // Sides of the circle fenced round a round floor: off its true rim by under 2 cm at the sandbox's 15 m.
        private const int CircleSides = 64;
        // Metres the stripes are long, about; each edge is cut into stripes of nearly this length.
        private const float StripeLength = 1.6f;
        // Metres the bumper sinks below the floor's top, so no gap shows under it.
        private const float Sink = 0.05f;
        // The longest a mitred corner may stretch the bumper's thickness: a sharper corner is cut off at this.
        private const float MaxMitre = 3f;

        private readonly GameTuning.HotPotatoSettings settings;
        private readonly ArenaMap map;
        private readonly MechanicArt art;
        private readonly Transform parent;
        private readonly bool isActive;

        public ArenaFence(GameTuning tuning, RoundRules rules, ArenaMap map, MechanicArt art, Transform parent)
        {
            this.settings = tuning.SpecialRounds.HotPotato;
            this.map = map;
            this.art = art;
            this.parent = parent;
            this.isActive = rules.IsHotPotato;
        }

        /// <summary>The fence's root, null in a round without one.</summary>
        public Transform Root { get; private set; }

        /// <summary>The walls the cars stop at, one per edge of every fenced loop; empty without a fence.</summary>
        public IReadOnlyList<Collider> Walls => walls;

        private readonly List<Collider> walls = new();
        // Made here, so freed here: the scene takes the objects with it, not the meshes and the material.
        private readonly List<Object> made = new();

        public void Start()
        {
            if (!isActive) {
                return;
            }

            Root = new GameObject("Fence").transform;
            Root.SetParent(parent, false);
            var physics = new PhysicsMaterial("Fence") {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0.15f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            made.Add(physics);

            var red = new FloorMeshBuilder();
            var white = new FloorMeshBuilder();
            var stripe = 0;
            foreach (var (loop, isRim) in Loops()) {
                Fence(Clean(loop), isRim, physics, red, white, ref stripe);
            }

            Merge("Red", red, art.Get("train_red"));
            Merge("White", white, art.Get("train_white"));
            Outline.Apply(Root.gameObject);
        }

        /// <summary>
        /// The loops to fence, each counter-clockwise from above, and whether it is the rim (the floor inside it) or a hole
        /// (the floor outside it): the circle of a round floor, else the shape's outline and its voids.
        /// </summary>
        private IEnumerable<(Vector2[], bool)> Loops()
        {
            var shape = map.Shape;
            if (shape == null) {
                var radius = map.Settings.Radius;
                var circle = new Vector2[CircleSides];
                for (var i = 0; i < CircleSides; i++) {
                    var angle = i * Mathf.PI * 2f / CircleSides;
                    circle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }

                yield return (circle, true);
                yield break;
            }

            yield return (ToArray(shape.Outline), true);
            foreach (var hole in shape.Voids) {
                yield return (hole, false);
            }
        }

        /// <summary>
        /// One wall per edge of the loop, on the side away from the floor, and the stripes of the bumper along it. The
        /// side is told by the loop's own winding, so a loop given either way round fences the right side.
        /// </summary>
        private void Fence(Vector2[] loop, bool isRim, PhysicsMaterial physics, FloorMeshBuilder red,
            FloorMeshBuilder white, ref int stripe)
        {
            if (loop.Length < 3) {
                return;
            }

            // Counter-clockwise, the inside is on the left: the rim's floor is inside it, a hole's floor outside.
            var isLeftFloor = Area(loop) > 0f == isRim;
            var thickness = settings.FenceThickness;
            var count = loop.Length;
            var away = new Vector2[count];
            for (var i = 0; i < count; i++) {
                var along = (loop[(i + 1) % count] - loop[i]).normalized;
                // Right of the way along is (y, -x); away from the floor is right when the floor is on the left.
                away[i] = isLeftFloor ? new Vector2(along.y, -along.x) : new Vector2(-along.y, along.x);
            }

            // The bumper's outer line: every corner pushed out along the middle of its two edges' outward sides, so far
            // that both edges keep the bumper's thickness.
            var outer = new Vector2[count];
            for (var i = 0; i < count; i++) {
                var mitre = (away[(i + count - 1) % count] + away[i]).normalized;
                var stretch = Mathf.Min(1f / Mathf.Max(Vector2.Dot(mitre, away[i]), 0.01f), MaxMitre);
                outer[i] = loop[i] + mitre * (thickness * stretch);
            }

            for (var i = 0; i < count; i++) {
                var next = (i + 1) % count;
                var a = loop[i];
                var b = loop[next];
                var length = Vector2.Distance(a, b);
                var rotation = Quaternion.LookRotation(new Vector3(away[i].x, 0f, away[i].y));
                Wall(a, b, away[i], rotation, length, physics);

                var stripes = Mathf.Max(1, Mathf.RoundToInt(length / StripeLength));
                for (var part = 0; part < stripes; part++) {
                    var from = (float)part / stripes;
                    var to = (float)(part + 1) / stripes;
                    Stripe(stripe++ % 2 == 0 ? red : white, Vector2.Lerp(a, b, from), Vector2.Lerp(a, b, to),
                        Vector2.Lerp(outer[i], outer[next], from), Vector2.Lerp(outer[i], outer[next], to), away[i]);
                }
            }
        }

        /// <summary>
        /// One stripe: a prism from the floor's edge (inner) to the bumper's outer line, its ends left open, as the next
        /// stripe of the loop closes them.
        /// </summary>
        private void Stripe(FloorMeshBuilder builder, Vector2 innerA, Vector2 innerB, Vector2 outerA, Vector2 outerB,
            Vector2 away)
        {
            var bottom = -Sink;
            var top = settings.FenceHeight - Sink;
            var outward = new Vector3(away.x, 0f, away.y);
            builder.AddQuad(At(innerA, top), At(innerB, top), At(outerB, top), At(outerA, top),
                Vector3.up, Vector3.up, Vector3.up, Vector3.up);
            builder.AddQuad(At(innerA, bottom), At(innerB, bottom), At(outerB, bottom), At(outerA, bottom),
                Vector3.down, Vector3.down, Vector3.down, Vector3.down);
            builder.AddQuad(At(outerA, bottom), At(outerB, bottom), At(outerB, top), At(outerA, top),
                outward, outward, outward, outward);
            builder.AddQuad(At(innerA, bottom), At(innerB, bottom), At(innerB, top), At(innerA, top),
                -outward, -outward, -outward, -outward);
        }

        private static Vector3 At(Vector2 point, float height)
        {
            return new Vector3(point.x, height, point.y);
        }

        private void Wall(Vector2 a, Vector2 b, Vector2 away, Quaternion rotation, float length, PhysicsMaterial physics)
        {
            var thickness = settings.FenceThickness;
            var height = settings.WallHeight;
            var middle = (a + b) * 0.5f + away * (thickness * 0.5f);
            var wall = new GameObject("Wall");
            wall.transform.SetParent(Root, false);
            wall.transform.localPosition = new Vector3(middle.x, height * 0.5f - 0.5f, middle.y);
            wall.transform.localRotation = rotation;
            var collider = wall.AddComponent<BoxCollider>();
            // As long as the edge and a little more, so the walls of a corner overlap and leave no gap to slip through.
            collider.size = new Vector3(length + thickness, height, thickness);
            collider.sharedMaterial = physics;
            walls.Add(collider);
        }

        private void Merge(string name, FloorMeshBuilder builder, Material material)
        {
            var mesh = builder.ToMesh("Fence " + name);
            if (mesh.vertexCount == 0) {
                Object.Destroy(mesh);
                return;
            }

            mesh.RecalculateBounds();
            Outline.BakeNormals(mesh);
            made.Add(mesh);
            ToyMeshes.Part(Root, name, mesh, material, Vector3.zero);
        }

        public void Dispose()
        {
            foreach (var thing in made) {
                if (thing != null) {
                    Object.Destroy(thing);
                }
            }

            made.Clear();
        }

        private static Vector2[] ToArray(IReadOnlyList<Vector2> points)
        {
            var array = new Vector2[points.Count];
            for (var i = 0; i < array.Length; i++) {
                array[i] = points[i];
            }

            return array;
        }

        // The loop without points that repeat the one before them, so every edge has a direction.
        private static Vector2[] Clean(Vector2[] loop)
        {
            var points = new List<Vector2>();
            if (loop == null) {
                return points.ToArray();
            }

            foreach (var point in loop) {
                if (points.Count == 0 || Vector2.Distance(points[^1], point) >= 0.01f) {
                    points.Add(point);
                }
            }

            while (points.Count > 1 && Vector2.Distance(points[^1], points[0]) < 0.01f) {
                points.RemoveAt(points.Count - 1);
            }

            return points.ToArray();
        }

        private static float Area(Vector2[] loop)
        {
            var area = 0f;
            for (var i = 0; i < loop.Length; i++) {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Length];
                area += a.x * b.y - b.x * a.y;
            }

            return area * 0.5f;
        }
    }
}
