using System;
using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Jack mechanic of the Playroom arena: a handful of yellow lids marked with a "?" lie on the floor. Every few
    /// seconds one or two of them are announced: the lid rattles and a pulsing circle on the floor shows how far the box
    /// will reach. After the warning a jack-in-the-box springs up out of the floor (a coil and a clown's head) and tosses
    /// every car inside the circle up and away from itself, then stays up for a moment and folds back down.
    /// <para>
    /// Spots are laid once, at random places that are whole and clear of the cars' starting places
    /// (<see cref="PropPlacement"/>), from the seed the bots share. A spot whose floor crumbles is gone. A hazard for the
    /// bots: a bot inside the circle of an announced spot drives straight out of it. The boxes are checks of position
    /// with no colliders. Registered in every scope; does nothing off its variant.
    /// </para>
    /// </summary>
    public sealed class JackBoxes : IArenaHazard, ITickable, IDisposable
    {
        /// <summary>Where a spot is in its cycle.</summary>
        public enum SpotState
        {
            Idle,
            Warning,
            Open,
            Closing,
            Gone,
        }

        private const float LidHalf = 0.8f;
        private const float LidThickness = 0.05f;
        private const float SpringHeight = 1.7f;
        private const float HeadRadius = 0.72f;
        private const float OutlineThickness = 0.075f;
        private const float CloseTime = 0.35f;
        private const float PopTime = 0.18f;
        private const float RattleRate = 70f;
        private const float PulseRate = 5f;

        // The floor nearer than this to a lid's middle takes the spot away.
        private const float VanishDistance = 1.0f;

        private sealed class Spot
        {
            public Vector3 Centre;
            public Transform Root;
            public Transform LidHinge;
            public Transform Spring;
            public Transform Head;
            public GameObject Hole;
            public GameObject Ring;
            public MeshRenderer RingRenderer;
            public SpotState State;
            public float Since;
            public float Presence = 1f;
            public int Beat;
        }

        private readonly GameTuning.JackSettings settings;
        private readonly GameTuning tuning;
        private readonly ArenaMap map;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly RoundDifficulty difficulty;
        private readonly MechanicArt art;
        private readonly ArenaHazards hazards;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Transform parent;
        private readonly List<Spot> spots = new();
        private readonly List<Mesh> meshes = new();
        private readonly List<int> idle = new();
        private Material[] ringBeatA;
        private Material[] ringBeatB;
        private GameObject root;
        private System.Random random;
        private EffectPlayer effects;
        private bool built;
        private float clock;
        private float nextAnnounce = -1f;

        public JackBoxes(GameTuning tuning, ArenaMap map, ArenaRound round, IArenaEdge edge, RoundDifficulty difficulty,
            MechanicArt art, ArenaHazards hazards, ArenaCamera arenaCamera, IAudioService audioService, Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Jack;
            this.map = map;
            this.round = round;
            this.edge = edge;
            this.difficulty = difficulty;
            this.art = art;
            this.hazards = hazards;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
            if (map.Mechanic == GameTuning.ArenaMechanic.Jack) {
                hazards.Add(this);
            }
        }

        /// <summary>Whether the spots are laid out; false off the Jack variant.</summary>
        public bool Exists => built;

        public int SpotCount => spots.Count;

        public Vector3 CentreOf(int index) => spots[index].Centre;

        public SpotState StateOf(int index) => spots[index].State;

        /// <summary>Seconds since the round began.</summary>
        public float Clock => clock;

        /// <summary>How many times a box has sprung up this round.</summary>
        public int Pops { get; private set; }

        /// <summary>How many cars the boxes have tossed this round.</summary>
        public int Tosses { get; private set; }

        public int Version { get; private set; }

        /// <summary>Announces a spot now, as the schedule does; for tests.</summary>
        public void Announce(int index)
        {
            if (spots[index].State == SpotState.Idle) {
                Enter(spots[index], SpotState.Warning);
                Version++;
            }
        }

        public bool TryAvoid(Vector3 position, float margin, IArenaEdge edge, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            if (!built) {
                return false;
            }

            foreach (var spot in spots) {
                if (spot.State != SpotState.Warning) {
                    continue;
                }

                var away = new Vector3(position.x - spot.Centre.x, 0f, position.z - spot.Centre.z);
                var distance = away.magnitude;
                var reach = settings.HitRadius + margin;
                if (distance > reach) {
                    continue;
                }

                var direction = distance > 0.3f ? away / distance : edge.Inward(position).normalized;
                if (direction.sqrMagnitude < 0.01f) {
                    direction = Vector3.back;
                }

                // Straight out of the circle if there is floor there, else the nearest way round it.
                var travel = reach - distance + 0.8f;
                for (var attempt = 0; attempt < 5; attempt++) {
                    var turn = attempt == 0 ? 0f : (attempt % 2 == 1 ? 1f : -1f) * 50f * ((attempt + 1) / 2);
                    var candidate = Quaternion.Euler(0f, turn, 0f) * direction;
                    if (edge.DistanceToEdge(position + candidate * travel, edgeMargin) >= 0f) {
                        heading = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        public void Tick()
        {
            if (map.Mechanic != GameTuning.ArenaMechanic.Jack) {
                return;
            }

            if (!built) {
                if (!round.IsPlaced) {
                    return;
                }

                Build();
            }

            var delta = Time.deltaTime;
            if (round.HasBegun && !round.IsFinished) {
                clock += delta;
                Schedule();
            }

            foreach (var spot in spots) {
                Advance(spot, delta);
            }
        }

        public void Dispose()
        {
            hazards.Remove(this);
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

        private void Schedule()
        {
            if (nextAnnounce < 0f) {
                nextAnnounce = settings.Delay;
            }

            if (clock < nextAnnounce) {
                return;
            }

            idle.Clear();
            for (var i = 0; i < spots.Count; i++) {
                if (spots[i].State == SpotState.Idle) {
                    idle.Add(i);
                }
            }

            var wanted = random.NextDouble() < settings.DoubleChance ? 2 : 1;
            for (var n = 0; n < wanted && idle.Count > 0; n++) {
                var pick = random.Next(idle.Count);
                Announce(idle[pick]);
                idle.RemoveAt(pick);
            }

            var jitter = ((float)random.NextDouble() * 2f - 1f) * settings.Jitter;
            nextAnnounce = clock + Mathf.Max(settings.Warning + 1f, settings.Interval + jitter);
        }

        private void Enter(Spot spot, SpotState state)
        {
            spot.State = state;
            spot.Since = 0f;
        }

        private void Advance(Spot spot, float delta)
        {
            spot.Since += delta;
            if (spot.State != SpotState.Gone && edge.DistanceToEdge(spot.Centre) < VanishDistance) {
                Enter(spot, SpotState.Gone);
            }

            var gone = spot.State == SpotState.Gone;
            spot.Presence = Mathf.MoveTowards(spot.Presence, gone ? 0f : 1f, 4f * delta);
            spot.Root.gameObject.SetActive(spot.Presence > 0.01f);
            if (spot.Presence <= 0.01f) {
                return;
            }

            var lidAngle = 0f;
            var extension = 0f;
            var tilt = 0f;
            var rattle = Vector3.zero;
            switch (spot.State) {
                case SpotState.Warning:
                    var shake = Mathf.Sin(spot.Since * RattleRate);
                    lidAngle = 6f + 5f * Mathf.Abs(Mathf.Sin(spot.Since * 23f));
                    rattle = new Vector3(shake * 0.03f, 0f, 0f);
                    if (spot.Since >= settings.Warning) {
                        Pop(spot);
                    }

                    break;
                case SpotState.Open:
                    lidAngle = 105f;
                    var open = spot.Since;
                    extension = 1f - Mathf.Exp(-9f * open) * Mathf.Cos(16f * open);
                    tilt = 9f * Mathf.Sin(open * 14f) * Mathf.Exp(-open * 2.5f);
                    if (spot.Since >= settings.Hold) {
                        Enter(spot, SpotState.Closing);
                    }

                    break;
                case SpotState.Closing:
                    var back = Mathf.Clamp01(spot.Since / CloseTime);
                    extension = 1f - back * back;
                    lidAngle = Mathf.Lerp(105f, 0f, Mathf.Clamp01((back - 0.6f) / 0.4f));
                    if (spot.Since >= CloseTime) {
                        Enter(spot, SpotState.Idle);
                    }

                    break;
            }

            spot.Root.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, spot.Presence);
            spot.LidHinge.localPosition = new Vector3(0f, 0f, -LidHalf) + rattle;
            spot.LidHinge.localRotation = Quaternion.Euler(-lidAngle, rattle.x * 40f, 0f);
            var isUp = extension > 0.01f;
            spot.Hole.SetActive(isUp || spot.State == SpotState.Warning);
            spot.Spring.gameObject.SetActive(isUp);
            spot.Head.gameObject.SetActive(isUp);
            if (isUp) {
                var height = Mathf.Max(0.02f, extension);
                spot.Spring.localScale = new Vector3(1f, height, 1f);
                spot.Head.localPosition = new Vector3(0f, 0.05f + SpringHeight * height + HeadRadius * 0.8f, 0f);
                spot.Head.localRotation = Quaternion.Euler(0f, 0f, tilt);
            }

            var warning = spot.State == SpotState.Warning;
            spot.Ring.SetActive(warning);
            if (warning) {
                var beat = Mathf.FloorToInt(spot.Since * PulseRate) % 2;
                if (beat != spot.Beat) {
                    spot.Beat = beat;
                    spot.RingRenderer.sharedMaterials = beat == 0 ? ringBeatA : ringBeatB;
                }
            }
        }

        /// <summary>The box springs up: every car inside the circle is thrown up and away from it.</summary>
        private void Pop(Spot spot)
        {
            Enter(spot, SpotState.Open);
            Pops++;
            effects ??= UnityEngine.Object.FindAnyObjectByType<EffectPlayer>();
            if (effects != null) {
                effects.PlayHit(spot.Centre, Vector3.up, 0.5f, false, false);
            }

            if (audioService != null) {
                audioService.Play(audioService.Library.HitLight, 0.6f);
            }

            if (!round.IsRunning || round.IsFinished) {
                return;
            }

            foreach (var car in round.Cars) {
                if (!car.IsTargetable || car.Position.y > settings.HitHeight) {
                    continue;
                }

                var away = new Vector3(car.Position.x - spot.Centre.x, 0f, car.Position.z - spot.Centre.z);
                if (away.sqrMagnitude > settings.HitRadius * settings.HitRadius) {
                    continue;
                }

                Toss(car, away);
            }
        }

        private void Toss(Car car, Vector3 away)
        {
            Vector3 direction;
            if (away.sqrMagnitude > 0.04f) {
                direction = away.normalized;
            }
            else {
                // Right on top of the box: whichever way the car was going, or the middle's way.
                var flat = new Vector3(car.Movement.Velocity.x, 0f, car.Movement.Velocity.z);
                direction = flat.sqrMagnitude > 1f ? flat.normalized : edge.Inward(car.Position).normalized;
                if (direction.sqrMagnitude < 0.01f) {
                    direction = Vector3.back;
                }
            }

            // Only the player's blow is eased, by the round's difficulty: the bots take all of it.
            var share = car.IsPlayer ? Mathf.Lerp(settings.EasyPlayerShare, 1f, difficulty.Level) : 1f;
            var push = settings.Push * share;
            var speedAway = Vector3.Dot(car.Movement.Velocity, direction);
            push = Mathf.Min(push, Mathf.Max(0f, tuning.Car.MaxBumpSpeed - speedAway));
            car.Movement.Push(direction * push + Vector3.up * Mathf.Max(0f, settings.Lift - car.Movement.Velocity.y));
            car.Movement.Stagger(settings.Stagger * share);
            car.HitByHazard();
            Tosses++;

            if (effects != null) {
                effects.PlayHit(car.Position, direction, 0.9f, true, false);
            }

            if (arenaCamera != null) {
                arenaCamera.Shake(car.IsPlayer ? 0.5f : 0.1f);
            }

            if (audioService != null) {
                audioService.Play(audioService.Library.HitHeavy, car.IsPlayer ? 0.8f : 0.4f);
            }
        }

        // ---- Look: built once, from code. ----

        private void Build()
        {
            built = true;
            random = new System.Random(unchecked(hazards.Seed ^ 0x3AC41D));
            var places = PropPlacement.Scatter(random, edge, round.Cars, settings.SpotCount, settings.EdgeClearance,
                settings.SpotSpacing, settings.SpawnClearance);
            root = new GameObject("Jack Boxes");
            root.transform.SetParent(parent, false);

            var outline = art.Get("jack_outline");

            // The lid, hinged at one edge so it can flip up: its middle is half a lid beyond the hinge.
            var lid = new PropMesh();
            lid.Box(new Vector3(0f, LidThickness, LidHalf), new Vector3(LidHalf, LidThickness, LidHalf), Quaternion.identity);
            lid.Submesh = 1;
            // The "?": a hook, a stem and a dot, in navy, just above the lid.
            const float mark = LidThickness * 2f + 0.006f;
            var hook = new Vector3(0f, mark, LidHalf + 0.28f);
            lid.FlatRing(hook, 0.17f, 0.33f, 14, -80f * Mathf.Deg2Rad, 200f * Mathf.Deg2Rad);
            var hookEnd = hook + new Vector3(Mathf.Cos(-80f * Mathf.Deg2Rad), 0f, Mathf.Sin(-80f * Mathf.Deg2Rad)) * 0.25f;
            lid.FlatQuad(hookEnd + new Vector3(-0.08f, 0f, 0f), hookEnd + new Vector3(0.08f, 0f, 0f),
                hookEnd + new Vector3(0.08f, 0f, -0.28f), hookEnd + new Vector3(-0.08f, 0f, -0.28f));
            lid.FlatDisc(new Vector3(hookEnd.x, mark, LidHalf - 0.5f), 0.1f, 10);
            var lidMesh = Keep(lid.ToMesh("Jack Lid"));
            var lidHull = new PropMesh();
            lidHull.Box(new Vector3(0f, LidThickness, LidHalf), new Vector3(LidHalf, LidThickness, LidHalf), Quaternion.identity);
            var lidOutline = Keep(lidHull.ToHull("Jack Lid Outline", OutlineThickness));

            var hole = new PropMesh();
            hole.FlatDisc(new Vector3(0f, 0.011f, 0f), 0.85f, 16);
            var holeMesh = Keep(hole.ToMesh("Jack Hole"));

            var spring = new PropMesh();
            const int coils = 7;
            for (var i = 0; i < coils; i++) {
                spring.Submesh = i % 2;
                spring.Cylinder(new Vector3(0f, (i + 0.5f) * SpringHeight / coils, 0f), Quaternion.identity, 0.36f, 0.36f,
                    SpringHeight / coils * 0.5f, 12);
            }

            var springMesh = Keep(spring.ToMesh("Jack Spring"));
            var springHull = new PropMesh();
            springHull.Cylinder(new Vector3(0f, SpringHeight * 0.5f, 0f), Quaternion.identity, 0.36f, 0.36f,
                SpringHeight * 0.5f, 12);
            var springOutline = Keep(springHull.ToHull("Jack Spring Outline", OutlineThickness));

            var head = new PropMesh();
            head.Sphere(Vector3.zero, Quaternion.identity, Vector3.one * HeadRadius, 18, 12, false);
            head.Submesh = 1;
            head.Sphere(new Vector3(0f, -0.04f, -HeadRadius), Quaternion.identity, Vector3.one * 0.19f, 10, 6, false);
            head.Submesh = 2;
            head.Sphere(new Vector3(-0.26f, 0.18f, -HeadRadius * 0.92f), Quaternion.identity, Vector3.one * 0.11f, 8, 5, false);
            head.Sphere(new Vector3(0.26f, 0.18f, -HeadRadius * 0.92f), Quaternion.identity, Vector3.one * 0.11f, 8, 5, false);
            head.Submesh = 3;
            head.Cylinder(new Vector3(0f, HeadRadius + 0.3f, 0f), Quaternion.identity, 0.62f, 0.04f, 0.5f, 14);
            head.Submesh = 1;
            head.Sphere(new Vector3(0f, HeadRadius + 0.88f, 0f), Quaternion.identity, Vector3.one * 0.15f, 8, 5, false);
            var headMesh = Keep(head.ToMesh("Jack Head"));
            var headHull = new PropMesh();
            headHull.Sphere(Vector3.zero, Quaternion.identity, Vector3.one * HeadRadius, 18, 12, false);
            headHull.Cylinder(new Vector3(0f, HeadRadius + 0.3f, 0f), Quaternion.identity, 0.62f, 0.04f, 0.5f, 14);
            var headOutline = Keep(headHull.ToHull("Jack Head Outline", OutlineThickness));

            var ring = new PropMesh();
            ring.FlatDisc(new Vector3(0f, 0.0125f, 0f), settings.HitRadius, 28);
            ring.Submesh = 1;
            ring.FlatRing(new Vector3(0f, 0.0135f, 0f), settings.HitRadius - 0.14f, settings.HitRadius, 28, 0f,
                Mathf.PI * 2f);
            var ringMesh = Keep(ring.ToMesh("Jack Warning"));

            var glow = art.Get("jack_glow");
            var glowBright = art.Get("jack_glow_bright");
            ringBeatA = new[] { glow, glowBright };
            ringBeatB = new[] { glowBright, glow };
            for (var i = 0; i < places.Count; i++) {
                var spot = new Spot { Centre = places[i] };
                spot.Root = new GameObject("Spot " + i).transform;
                spot.Root.SetParent(root.transform, false);
                spot.Root.position = new Vector3(spot.Centre.x, 0.002f, spot.Centre.z);

                spot.LidHinge = new GameObject("Lid").transform;
                spot.LidHinge.SetParent(spot.Root, false);
                Add(spot.LidHinge.gameObject, lidMesh, art.Get("jack_lid"), outline);
                Add(Child(spot.LidHinge, "Outline"), lidOutline, outline);

                spot.Hole = Child(spot.Root, "Hole");
                Add(spot.Hole, holeMesh, outline);

                spot.Spring = Child(spot.Root, "Spring").transform;
                spot.Spring.localPosition = new Vector3(0f, 0.05f, 0f);
                Add(spot.Spring.gameObject, springMesh, art.Get("jack_spring"), art.Get("jack_white"));
                Add(Child(spot.Spring, "Outline"), springOutline, outline);

                spot.Head = Child(spot.Root, "Head").transform;
                Add(spot.Head.gameObject, headMesh, art.Get("jack_skin"), art.Get("jack_red"), outline, art.Get("jack_hat"));
                Add(Child(spot.Head, "Outline"), headOutline, outline);

                spot.Ring = Child(spot.Root, "Warning");
                spot.RingRenderer = Add(spot.Ring, ringMesh, ringBeatA);
                spot.RingRenderer.sortingOrder = -3;
                spot.Ring.SetActive(false);
                spot.Hole.SetActive(false);
                spot.Spring.gameObject.SetActive(false);
                spot.Head.gameObject.SetActive(false);
                spots.Add(spot);
            }
        }

        private Mesh Keep(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }

        private static GameObject Child(Transform of, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(of, false);
            return child;
        }

        private static GameObject Child(GameObject of, string name) => Child(of.transform, name);

        private static MeshRenderer Add(GameObject target, Mesh mesh, params Material[] materials)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
