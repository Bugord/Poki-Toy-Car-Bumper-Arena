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
    /// The Carousel arena's mechanic: the middle of the floor is a turntable. It starts to turn a few seconds into the
    /// round, and every so often eases to a stop and turns the other way, with its arrows flashing just before. A car
    /// on it is carried round like one standing on a platform (see <see cref="IArenaPlatform"/>: the car's own steering
    /// works in the turntable's frame), so it is the ground that moves, not the car that is shoved. It is registered in
    /// every scope and does nothing off the Carousel arena.
    /// <para>
    /// The disc is wider than the final solid disc, so it follows the floor: as soon as the floor no longer reaches its
    /// rim, the disc shrinks to what is left rather than hang its picture over a gap. The cars start out beyond it, so
    /// nobody begins the round on the turntable. The first direction is drawn from <see cref="BotDriver.Seed"/>, so it
    /// is not memorised yet a simulation repeats it.
    /// </para>
    /// </summary>
    public sealed class CarouselDisc : IArenaPlatform, ITickable, IDisposable
    {
        private const int WedgeSteps = 6;
        private const int RimSegments = 64;

        private readonly GameTuning.CarouselSettings settings;
        private readonly ArenaMap map;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly CarouselDecor decor;
        private readonly IObjectResolver resolver;
        private readonly List<Mesh> meshes = new();
        private readonly MeshRenderer[] arrowRenderers = new MeshRenderer[2];
        private readonly GameObject[] arrowSets = new GameObject[2];
        private GameObject root;
        private Transform disc;
        private bool built;
        private float firstDirection = 1f;
        private float clock;
        private int flashed = -1;
        private int shownSet = -1;

        public CarouselDisc(GameTuning tuning, ArenaMap map, ArenaRound round, IArenaEdge edge, CarouselDecor decor,
            IObjectResolver resolver)
        {
            this.settings = tuning.Carousel;
            this.map = map;
            this.round = round;
            this.edge = edge;
            this.decor = decor;
            this.resolver = resolver;
        }

        /// <summary>Whether the turntable is laid on the floor; false off the Carousel arena.</summary>
        public bool Exists => built;

        /// <summary>The radius the turntable reaches now, in metres: its own, or less once the floor has shrunk.</summary>
        public float Radius { get; private set; }

        /// <summary>The turn right now, in radians per second, positive as Unity's Y rotation.</summary>
        public float AngularVelocity { get; private set; }

        /// <summary>How far it has turned, in radians.</summary>
        public float Angle { get; private set; }

        /// <summary>Whether the arrows are flashing, a moment before the turn reverses.</summary>
        public bool IsWarning { get; private set; }

        /// <summary>The turntable's progress through its schedule, in seconds; it runs once the round has begun.</summary>
        public float Clock => clock;

        public PlatformMotion Sample(Vector3 position)
        {
            if (AngularVelocity == 0f || Radius <= 0f) {
                return PlatformMotion.None;
            }

            var distance = Mathf.Sqrt(position.x * position.x + position.z * position.z);
            var inside = Mathf.Clamp01((Radius - distance) / settings.RimBlend);
            if (inside <= 0f) {
                return PlatformMotion.None;
            }

            // Eased, so the carry fades smoothly across the rim.
            var share = inside * inside * (3f - 2f * inside);
            var omega = AngularVelocity * share;
            // v = omega x r about the vertical axis, with Unity's sense of a positive turn.
            return new PlatformMotion(new Vector3(position.z * omega, 0f, -position.x * omega), omega);
        }

        public void Tick()
        {
            if (map.Mechanic != GameTuning.ArenaMechanic.Carousel) {
                return;
            }

            if (!built) {
                if (!round.IsPlaced) {
                    return;
                }

                Build();
            }

            var delta = Time.deltaTime;
            if (round.HasBegun) {
                clock += delta;
            }

            AngularVelocity = Speed(clock) * settings.AngularSpeed * Mathf.Deg2Rad;
            Angle += AngularVelocity * delta;
            IsWarning = round.IsRunning && Warning(clock);

            // The floor around the middle shrinks the moment a ring starts to shake; the disc follows it down, fast but not
            // at once.
            var target = Mathf.Max(0f, Mathf.Min(settings.Radius, edge.DistanceToEdge(Vector3.zero) - decor.RimWidth));
            Radius = Mathf.MoveTowards(Radius, target, settings.ShrinkSpeed * delta);
            Show();
        }

        public void Dispose()
        {
            if (root != null) {
                UnityEngine.Object.Destroy(root);
            }

            foreach (var mesh in meshes) {
                if (mesh != null) {
                    UnityEngine.Object.Destroy(mesh);
                }
            }

            meshes.Clear();
        }

        /// <summary>
        /// The turn as a share of the full speed, from -1 to 1 (positive is the first direction): still until the start
        /// delay, then eased up to speed, reversing through a stop every interval.
        /// </summary>
        private float Speed(float at)
        {
            var since = at - settings.StartDelay;
            if (since <= 0f) {
                return 0f;
            }

            var leg = (int)(since / settings.ReverseInterval);
            var into = since - leg * settings.ReverseInterval;
            var direction = leg % 2 == 0 ? firstDirection : -firstDirection;
            var previous = leg == 0 ? 0f : -direction;
            var ease = Mathf.Clamp01(into / settings.EaseTime);
            ease = ease * ease * (3f - 2f * ease);
            return Mathf.Lerp(previous, direction, ease);
        }

        private bool Warning(float at)
        {
            var since = at - settings.StartDelay;
            if (since <= 0f) {
                return false;
            }

            var into = since % settings.ReverseInterval;
            return settings.ReverseInterval - into <= settings.Telegraph;
        }

        private void Show()
        {
            disc.localRotation = Quaternion.Euler(0f, Angle * Mathf.Rad2Deg, 0f);
            var scale = Radius / settings.Radius;
            disc.localScale = new Vector3(scale, 1f, scale);
            root.SetActive(Radius > 0.05f);

            // The arrows that point the way it turns now; before it starts, the way it will.
            int set;
            if (AngularVelocity > 0.001f) {
                set = 0;
            }
            else if (AngularVelocity < -0.001f) {
                set = 1;
            }
            else {
                set = shownSet >= 0 ? shownSet : firstDirection > 0f ? 0 : 1;
            }

            if (set != shownSet) {
                shownSet = set;
                arrowSets[0].SetActive(set == 0);
                arrowSets[1].SetActive(set == 1);
            }

            var flash = IsWarning && Mathf.FloorToInt(clock * decor.FlashRate) % 2 == 0 ? 1 : 0;
            if (flash != flashed) {
                flashed = flash;
                var material = flash == 1 ? decor.ArrowWarning : decor.Arrow;
                arrowRenderers[0].sharedMaterial = material;
                arrowRenderers[1].sharedMaterial = material;
            }
        }

        /// <summary>
        /// Lays the turntable out. Done when the cars are placed, not when the scope is built: a simulation sets the
        /// bots' seed after the scene is loaded, and the first direction comes from it.
        /// </summary>
        private void Build()
        {
            built = true;
            var seed = resolver.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Select(driver => driver.Seed)
                .DefaultIfEmpty(0).First();
            var random = new System.Random(unchecked(seed ^ 0x5C0A71D));
            firstDirection = random.Next(2) == 0 ? 1f : -1f;
            Radius = Mathf.Max(0f, Mathf.Min(settings.Radius, edge.DistanceToEdge(Vector3.zero) - decor.RimWidth));

            root = new GameObject("Carousel");
            root.transform.SetParent(decor.transform, false);
            root.transform.localPosition = new Vector3(0f, decor.Lift, 0f);
            disc = new GameObject("Turntable").transform;
            disc.SetParent(root.transform, false);

            var radius = settings.Radius;
            AddLayer(disc.gameObject, "Rim", decor.Outline, -3, RimMesh(radius));
            AddLayer(disc.gameObject, "Wedges A", decor.WedgeA, -2, WedgeMesh(radius, 0));
            AddLayer(disc.gameObject, "Wedges B", decor.WedgeB, -2, WedgeMesh(radius, 1));
            for (var set = 0; set < 2; set++) {
                var sign = set == 0 ? 1f : -1f;
                arrowSets[set] = new GameObject(set == 0 ? "Arrows Forward" : "Arrows Back");
                arrowSets[set].transform.SetParent(disc, false);
                arrowRenderers[set] = AddLayer(arrowSets[set], "Arrows", decor.Arrow, -1, ArrowMesh(radius, sign));
            }
        }

        /// <summary>One flat layer of the turntable, transparent like the Candy patches and sorted the same way.</summary>
        private static MeshRenderer AddLayer(GameObject parent, string name, Material material, int order, Mesh mesh)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(parent.transform, false);
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = layer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>The navy ring just outside the disc.</summary>
        private Mesh RimMesh(float radius)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var outer = radius + decor.RimWidth;
            for (var i = 0; i < RimSegments; i++) {
                var a = i * Mathf.PI * 2f / RimSegments;
                var b = (i + 1) * Mathf.PI * 2f / RimSegments;
                AddQuad(vertices, triangles, Polar(radius - 0.02f, a), Polar(outer, a), Polar(outer, b),
                    Polar(radius - 0.02f, b));
            }

            return ToMesh("Carousel Rim", vertices, triangles);
        }

        /// <summary>Every other wedge of the pattern, <paramref name="parity"/> picking which half.</summary>
        private Mesh WedgeMesh(float radius, int parity)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var count = decor.Wedges;
            var width = Mathf.PI * 2f / count;
            for (var wedge = parity; wedge < count; wedge += 2) {
                var start = vertices.Count;
                vertices.Add(Vector3.zero);
                for (var step = 0; step <= WedgeSteps; step++) {
                    vertices.Add(Polar(radius, (wedge + step / (float)WedgeSteps) * width));
                }

                for (var step = 0; step < WedgeSteps; step++) {
                    AddUpwards(vertices, triangles, start, start + 1 + step, start + 2 + step);
                }
            }

            return ToMesh("Carousel Wedges", vertices, triangles);
        }

        /// <summary>
        /// Chevrons along the rim, each pointing round the turntable: the way a point there goes when the disc turns by a
        /// positive angle (<paramref name="sign"/> 1), or the other way (-1).
        /// </summary>
        private Mesh ArrowMesh(float radius, float sign)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var middle = radius - 0.95f;
            for (var i = 0; i < decor.Arrows; i++) {
                var angle = (i + 0.5f) * Mathf.PI * 2f / decor.Arrows;
                var outward = Polar(1f, angle);
                var along = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * sign;
                var centre = outward * middle;
                var start = vertices.Count;
                vertices.Add(centre + along * 0.65f);
                vertices.Add(centre - along * 0.4f + outward * 0.72f);
                vertices.Add(centre - along * 0.03f);
                vertices.Add(centre - along * 0.4f - outward * 0.72f);
                AddUpwards(vertices, triangles, start, start + 1, start + 2);
                AddUpwards(vertices, triangles, start, start + 2, start + 3);
            }

            return ToMesh("Carousel Arrows", vertices, triangles);
        }

        private static Vector3 Polar(float radius, float angle)
        {
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c,
            Vector3 d)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
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
    }
}
