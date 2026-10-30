using System;
using System.Collections.Generic;
using System.Linq;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Candy arena's mechanic: six flat patches on the floor, caramel (slow, sticky, hard to shove) and icing
    /// (fast, slippery), which change how a car drives and takes a hit while it is over them. It is also the arena's
    /// <see cref="IArenaSurface"/>: registered in every scope and neutral off the Candy arena, so a car asks it without
    /// knowing which arena it is on.
    /// <para>
    /// The layout is four outer patches and two inner ones on a ring of six slots, caramel and icing alternating, so each
    /// kind has two outer and one inner. The inner ones lie wholly inside the spawn circle and the outer ones wholly
    /// outside it, so no car starts on a patch and the start is fair. Every patch reaches past the final solid disc, so
    /// none outlives the crumbling: a patch shrinks away as soon as the floor no longer reaches its outer edge, and stops
    /// acting with it, rather than hang over the gap or decide the last duel alone. The whole layout is turned by an
    /// angle drawn from <see cref="BotDriver.Seed"/> (and the kinds swapped on a coin flip), so it is not memorised yet a
    /// simulation repeats it.
    /// </para>
    /// </summary>
    public sealed class CandyPatches : IArenaSurface, ITickable, IDisposable
    {
        private const int Segments = 40;
        private const int Slots = 6;

        private readonly GameTuning.CandySettings settings;
        private readonly ArenaMap map;
        private readonly ArenaRound round;
        private readonly ArenaFloor floor;
        private readonly CandyDecor decor;
        private readonly IObjectResolver resolver;
        private readonly List<Patch> patches = new();
        private readonly List<Mesh> meshes = new();
        private bool built;

        public CandyPatches(GameTuning tuning, ArenaMap map, ArenaRound round, ArenaFloor floor, CandyDecor decor,
            IObjectResolver resolver)
        {
            this.settings = tuning.Candy;
            this.map = map;
            this.round = round;
            this.floor = floor;
            this.decor = decor;
            this.resolver = resolver;
        }

        /// <summary>How many patches lie on the floor; zero off the Candy arena.</summary>
        public int Count => patches.Count;

        /// <summary>Centre of patch <paramref name="index"/> on the ground plane (x, z).</summary>
        public Vector2 CenterOf(int index) => patches[index].Center;

        /// <summary>Radius of patch <paramref name="index"/>, at most, in metres.</summary>
        public float RadiusOf(int index) => patches[index].Radius;

        public bool IsCaramel(int index) => patches[index].IsCaramel;

        public SurfaceFeel Sample(Vector3 position)
        {
            foreach (var patch in patches) {
                if (patch.Scale <= 0f) {
                    continue;
                }

                var dx = position.x - patch.Center.x;
                var dz = position.z - patch.Center.y;
                var distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance > patch.Radius * patch.Scale + settings.BlendWidth) {
                    continue;
                }

                var rim = patch.RimAt(Mathf.Atan2(dz, dx), decor.Wobble, decor.Bumps) * patch.Scale;
                // Half the effect at the rim itself, all of it half a blend inside.
                var inside = Mathf.Clamp01(0.5f + (rim - distance) / settings.BlendWidth);
                if (inside > 0f) {
                    return SurfaceFeel.Neutral.Lerp(patch.IsCaramel ? Caramel() : Icing(), inside);
                }
            }

            return SurfaceFeel.Neutral;
        }

        public void Tick()
        {
            if (map.Mechanic != GameTuning.ArenaMechanic.Candy) {
                return;
            }

            if (!built) {
                if (!round.IsPlaced) {
                    return;
                }

                Build();
            }

            var step = Time.deltaTime / settings.VanishTime;
            var radius = floor.Radius;
            foreach (var patch in patches) {
                if (radius >= patch.Reach - settings.VanishMargin || patch.Scale <= 0f) {
                    continue;
                }

                patch.Scale = Mathf.Max(0f, patch.Scale - step);
                // Eased, so it shrinks like a sticker peeled off.
                var shown = patch.Scale * patch.Scale * (3f - 2f * patch.Scale);
                patch.Root.transform.localScale = new Vector3(shown, 1f, shown);
                patch.Root.SetActive(patch.Scale > 0f);
            }
        }

        public void Dispose()
        {
            foreach (var patch in patches) {
                if (patch.Root != null) {
                    UnityEngine.Object.Destroy(patch.Root);
                }
            }

            foreach (var mesh in meshes) {
                if (mesh != null) {
                    UnityEngine.Object.Destroy(mesh);
                }
            }

            patches.Clear();
            meshes.Clear();
        }

        private SurfaceFeel Caramel()
        {
            return new SurfaceFeel(settings.CaramelSpeed, settings.CaramelAcceleration, settings.CaramelGrip,
                settings.CaramelPush, settings.CaramelStagger);
        }

        private SurfaceFeel Icing()
        {
            return new SurfaceFeel(settings.IcingSpeed, settings.IcingAcceleration, settings.IcingGrip, 1f, 1f);
        }

        /// <summary>
        /// Lays the patches out. Done when the cars are placed, not when the scope is built: a simulation sets the bots'
        /// seed after the scene is loaded, and the layout comes from it.
        /// </summary>
        private void Build()
        {
            built = true;
            var seed = resolver.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Select(driver => driver.Seed)
                .DefaultIfEmpty(0).First();
            var random = new System.Random(unchecked(seed ^ 0x2A5F1C3));
            var turn = (float)(random.NextDouble() * Mathf.PI * 2d);
            var flip = random.Next(2);
            var parent = new GameObject("Candy Patches").transform;
            parent.SetParent(decor.transform, false);

            for (var slot = 0; slot < Slots; slot++) {
                var inner = slot % 3 == 1;
                var distance = inner ? settings.InnerDistance : settings.OuterDistance;
                var radius = inner ? settings.InnerRadius : settings.OuterRadius;
                var angle = turn + slot * Mathf.PI * 2f / Slots;
                var center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var patch = new Patch(center, radius, distance + radius, (slot + flip) % 2 == 0,
                    (float)(random.NextDouble() * Mathf.PI * 2d));
                patches.Add(patch);
                BuildVisual(patch, parent, random);
            }
        }

        private void BuildVisual(Patch patch, Transform parent, System.Random random)
        {
            var root = new GameObject(patch.IsCaramel ? "Caramel" : "Icing");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(patch.Center.x, decor.Lift, patch.Center.y);
            patch.Root = root;

            AddLayer(root, "Rim", decor.Outline, -3, BlobMesh(patch, decor.OutlineWidth));
            if (patch.IsCaramel) {
                AddLayer(root, "Body", decor.Caramel, -2, BlobMesh(patch, 0f));
                AddLayer(root, "Shine", decor.CaramelShine, -1, ShineMesh(patch.Radius));
                return;
            }

            AddLayer(root, "Body", decor.Icing, -2, BlobMesh(patch, 0f));
            var vertices = new[] { new List<Vector3>(), new List<Vector3>() };
            var triangles = new[] { new List<int>(), new List<int>() };
            for (var i = 0; i < decor.Sprinkles; i++) {
                // Inside two thirds of the patch, so a dash never reaches the rim whatever the wobble.
                var spot = patch.Radius * 0.62f * Mathf.Sqrt((float)random.NextDouble());
                var around = (float)(random.NextDouble() * Mathf.PI * 2d);
                var turn = (float)(random.NextDouble() * Mathf.PI);
                AddDash(vertices[i % 2], triangles[i % 2], new Vector2(Mathf.Cos(around), Mathf.Sin(around)) * spot,
                    new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)), 0.28f, 0.065f);
            }

            AddLayer(root, "Sprinkles A", decor.SprinkleA, -1, ToMesh("Sprinkles A", vertices[0], triangles[0]));
            AddLayer(root, "Sprinkles B", decor.SprinkleB, -1, ToMesh("Sprinkles B", vertices[1], triangles[1]));
        }

        /// <summary>
        /// One flat layer of a patch. The layers are transparent and lie in one plane, so the order they are drawn in is
        /// the sorting order, below zero to stay under the cars' shadow spots, which are drawn at zero.
        /// </summary>
        private static void AddLayer(GameObject root, string name, Material material, int order, Mesh mesh)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(root.transform, false);
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = layer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = order;
        }

        /// <summary>The wobbly blob of a patch, <paramref name="grow"/> metres wider all round.</summary>
        private Mesh BlobMesh(Patch patch, float grow)
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var triangles = new List<int>();
            for (var i = 0; i < Segments; i++) {
                var angle = i * Mathf.PI * 2f / Segments;
                var rim = patch.RimAt(angle, decor.Wobble, decor.Bumps) + grow;
                vertices.Add(new Vector3(Mathf.Cos(angle) * rim, 0f, Mathf.Sin(angle) * rim));
            }

            for (var i = 0; i < Segments; i++) {
                AddUpwards(vertices, triangles, 0, 1 + i, 1 + (i + 1) % Segments);
            }

            return ToMesh("Candy Blob", vertices, triangles);
        }

        /// <summary>A tilted glossy oval toward the light, up and to the left of the middle.</summary>
        private Mesh ShineMesh(float radius)
        {
            const int sides = 20;
            var centre = new Vector2(-0.3f, 0.28f) * radius;
            var vertices = new List<Vector3> { new(centre.x, 0f, centre.y) };
            var triangles = new List<int>();
            var tilt = Mathf.Deg2Rad * -35f;
            var cos = Mathf.Cos(tilt);
            var sin = Mathf.Sin(tilt);
            for (var i = 0; i < sides; i++) {
                var angle = i * Mathf.PI * 2f / sides;
                var x = Mathf.Cos(angle) * radius * 0.3f;
                var z = Mathf.Sin(angle) * radius * 0.14f;
                vertices.Add(new Vector3(centre.x + x * cos - z * sin, 0f, centre.y + x * sin + z * cos));
            }

            for (var i = 0; i < sides; i++) {
                AddUpwards(vertices, triangles, 0, 1 + i, 1 + (i + 1) % sides);
            }

            return ToMesh("Candy Shine", vertices, triangles);
        }

        private static void AddDash(List<Vector3> vertices, List<int> triangles, Vector2 centre, Vector2 along,
            float length, float width)
        {
            var across = new Vector2(-along.y, along.x);
            var start = vertices.Count;
            for (var i = 0; i < 4; i++) {
                var lengthSign = i == 0 || i == 3 ? -1f : 1f;
                var widthSign = i < 2 ? -1f : 1f;
                var point = centre + along * (lengthSign * length * 0.5f) + across * (widthSign * width * 0.5f);
                vertices.Add(new Vector3(point.x, 0f, point.y));
            }

            AddUpwards(vertices, triangles, start, start + 1, start + 2);
            AddUpwards(vertices, triangles, start, start + 2, start + 3);
        }

        /// <summary>A triangle wound so that it faces up, whichever way round its corners were given.</summary>
        private static void AddUpwards(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
        {
            var up = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).y >= 0f;
            triangles.Add(a);
            triangles.Add(up ? b : c);
            triangles.Add(up ? c : b);
        }

        private Mesh ToMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            return mesh;
        }

        private sealed class Patch
        {
            public readonly Vector2 Center;
            public readonly float Radius;
            public readonly float Reach;
            public readonly bool IsCaramel;
            private readonly float phase;

            /// <summary>1 whole, 0 gone.</summary>
            public float Scale = 1f;

            public GameObject Root;

            public Patch(Vector2 center, float radius, float reach, bool isCaramel, float phase)
            {
                Center = center;
                Radius = radius;
                Reach = reach;
                IsCaramel = isCaramel;
                this.phase = phase;
            }

            /// <summary>
            /// Distance from the middle to the rim at an angle: the radius, bulged and dipped by sines, scaled so that
            /// no bulge goes past <see cref="Radius"/>. The mesh and the effect share it, so a car feels what it sees.
            /// </summary>
            public float RimAt(float angle, float wobble, int bumps)
            {
                return Radius * (1f + wobble * Mathf.Sin(bumps * angle + phase)) / (1f + wobble);
            }
        }
    }
}
