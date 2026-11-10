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
    /// outlines, however long it is.
    /// </para>
    /// </summary>
    public sealed class ArenaFence : IStartable, IDisposable
    {
        // Sides of the circle fenced round a round floor: off its true rim by under 2 cm at the sandbox's 15 m.
        private const int CircleSides = 64;
        // Metres the stripes are long, about; each edge is cut into stripes of nearly this length.
        private const float StripeLength = 1.6f;

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

            var red = new List<CombineInstance>();
            var white = new List<CombineInstance>();
            var box = ToyMeshes.Box(Vector3.one);
            foreach (var (loop, isRim) in Loops()) {
                Fence(loop, isRim, physics, box, red, white);
            }

            Merge("Red", red, art.Get("train_red"));
            Merge("White", white, art.Get("train_white"));
            Object.Destroy(box);
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
        private void Fence(Vector2[] loop, bool isRim, PhysicsMaterial physics, Mesh box, List<CombineInstance> red,
            List<CombineInstance> white)
        {
            if (loop == null || loop.Length < 3) {
                return;
            }

            // Counter-clockwise, the inside is on the left: the rim's floor is inside it, a hole's floor outside.
            var isLeftFloor = Area(loop) > 0f == isRim;
            var thickness = settings.FenceThickness;
            var stripe = red.Count + white.Count;
            for (var i = 0; i < loop.Length; i++) {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Length];
                var along = b - a;
                var length = along.magnitude;
                if (length < 0.01f) {
                    continue;
                }

                along /= length;
                // Right of the way along is (y, -x); away from the floor is right when the floor is on the left.
                var away = isLeftFloor ? new Vector2(along.y, -along.x) : new Vector2(-along.y, along.x);
                var rotation = Quaternion.LookRotation(new Vector3(away.x, 0f, away.y));
                Wall(a, b, away, rotation, length, physics);

                // The bumper: a run of stripes, a little longer than they are spaced, so the corners close.
                var count = Mathf.Max(1, Mathf.RoundToInt(length / StripeLength));
                var step = length / count;
                for (var s = 0; s < count; s++) {
                    var middle = a + along * (step * (s + 0.5f)) + away * (thickness * 0.5f);
                    var matrix = Matrix4x4.TRS(new Vector3(middle.x, settings.FenceHeight * 0.5f - 0.05f, middle.y),
                        rotation, new Vector3(step + thickness * 0.6f, settings.FenceHeight, thickness));
                    (stripe++ % 2 == 0 ? red : white).Add(new CombineInstance { mesh = box, transform = matrix });
                }
            }
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

        private void Merge(string name, List<CombineInstance> parts, Material material)
        {
            if (parts.Count == 0) {
                return;
            }

            var mesh = new Mesh { name = "Fence " + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
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
