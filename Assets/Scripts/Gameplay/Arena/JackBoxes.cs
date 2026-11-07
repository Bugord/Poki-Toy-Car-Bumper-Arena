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
    /// The Jack mechanic of the Playroom arena: a handful of low toy boxes (a red box, a blue lid with a white star) stand
    /// on the floor. Every few seconds one or two of them are announced: the lid rattles, and a ring on the floor shows how
    /// far the box will reach while its inside fills from the middle out as the time runs down. After the warning the lid
    /// flips open, a jack (a coil and a clown's head) springs up and tosses every car inside the ring up and away from
    /// the box, then stays up for a moment and folds back in. The box has no "?" so that it is never taken for the
    /// boxes of items.
    /// <para>
    /// Spots are laid once, at random places that are whole and clear of the cars' starting places
    /// (<see cref="PropPlacement"/>), from the seed the bots share. A spot whose floor crumbles is gone. A hazard for the
    /// bots: a bot inside the circle of an announced spot drives straight out of it. The boxes are checks of position
    /// with no colliders. Registered in every scope; does nothing off its variant.
    /// In a match whose cycles turn the variants it is built afresh, its schedule from the start, each cycle that plays
    /// it, and leaves as the cycle ends (<see cref="MechanicRun"/>).
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

        private const float BoxHalf = 0.75f;
        private const float BoxHeight = 0.45f;
        private const float LidHalf = 0.78f;
        private const float LidThickness = 0.1f;
        private const float SpringHeight = 1.6f;
        private const float SpringRadius = 0.3f;
        private const int Coils = 6;
        private const float HeadRadius = 0.6f;
        private const float RingWidth = 0.24f;
        private const float CloseTime = 0.35f;
        private const float RattleRate = 70f;

        // The floor nearer than this to a lid's middle takes the spot away.
        private const float VanishDistance = 1.0f;

        private sealed class Spot
        {
            public Vector3 Centre;
            public Transform Root;
            public Transform LidHinge;
            public Transform Spring;
            public Transform Head;
            public GameObject Ring;
            public Transform Fill;
            public SpotState State;
            public float Since;
            public float Presence = 1f;
        }

        private readonly GameTuning.JackSettings settings;
        private readonly GameTuning tuning;
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
        private GameObject root;
        private System.Random random;
        private EffectPlayer effects;
        private readonly MechanicRun run;
        private float clock;
        private float nextAnnounce = -1f;

        public JackBoxes(GameTuning tuning, ArenaRound round, IArenaEdge edge, RoundDifficulty difficulty,
            MechanicArt art, ArenaHazards hazards, ArenaCamera arenaCamera, IAudioService audioService,
            ArenaVariants variants, Transform parent)
        {
            this.run = new MechanicRun(variants, GameTuning.ArenaMechanic.Jack);
            this.tuning = tuning;
            this.settings = tuning.Jack;
            this.round = round;
            this.edge = edge;
            this.difficulty = difficulty;
            this.art = art;
            this.hazards = hazards;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
        }

        /// <summary>Whether the spots are laid out; false off the Jack variant.</summary>
        public bool Exists => run.IsBuilt;

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
            if (!run.IsRunning) {
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
            switch (run.Next(round.IsPlaced, Time.deltaTime)) {
                case MechanicStep.Idle:
                    return;
                case MechanicStep.Leave:
                    hazards.Remove(this);
                    run.Shrink(root != null ? root.transform : null);
                    return;
                case MechanicStep.Clear:
                    Clear();
                    return;
                case MechanicStep.Start:
                    Clear();
                    Build();
                    break;
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
            Clear();
        }

        /// <summary>Takes the boxes off the floor and out of the bots' way, their schedule back to the start.</summary>
        private void Clear()
        {
            hazards.Remove(this);
            if (root != null) {
                UnityEngine.Object.Destroy(root);
                root = null;
            }

            foreach (var mesh in meshes) {
                if (mesh != null) {
                    UnityEngine.Object.Destroy(mesh);
                }
            }

            meshes.Clear();
            spots.Clear();
            idle.Clear();
            clock = 0f;
            nextAnnounce = -1f;
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
            spot.LidHinge.localPosition = new Vector3(0f, BoxHeight, LidHalf) + rattle;
            spot.LidHinge.localRotation = Quaternion.Euler(lidAngle, rattle.x * 40f, 0f);
            var isUp = extension > 0.01f;
            spot.Spring.gameObject.SetActive(isUp);
            spot.Head.gameObject.SetActive(isUp);
            if (isUp) {
                var height = Mathf.Max(0.02f, extension);
                spot.Spring.localScale = new Vector3(1f, height, 1f);
                spot.Head.localPosition = new Vector3(0f, BoxHeight + SpringHeight * height + HeadRadius * 0.8f, 0f);
                spot.Head.localRotation = Quaternion.Euler(0f, 0f, tilt);
            }

            var warning = spot.State == SpotState.Warning;
            spot.Ring.SetActive(warning);
            if (warning) {
                // The inside of the ring fills from the middle out, full when the box springs.
                var fill = settings.HitRadius * Mathf.Sqrt(Mathf.Clamp01(spot.Since / settings.Warning));
                spot.Fill.localScale = new Vector3(fill, 1f, fill);
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
            hazards.Add(this);
            random = new System.Random(run.SeedOf(unchecked(hazards.Seed ^ 0x3AC41D)));
            var places = PropPlacement.Scatter(random, edge, round.Cars, settings.SpotCount, settings.EdgeClearance,
                settings.SpotSpacing, settings.SpawnClearance);
            root = new GameObject("Jack Boxes");
            root.transform.SetParent(parent, false);

            var navy = art.Get("jack_outline");
            var red = art.Get("jack_red");
            var blue = art.Get("jack_lid");
            var white = art.Get("jack_white");
            var yellow = art.Get("jack_spring");
            var skin = art.Get("jack_skin");

            var box = Keep(ToyMeshes.Box(new Vector3(BoxHalf * 2f, BoxHeight, BoxHalf * 2f)));
            var lid = Keep(ToyMeshes.Box(new Vector3(LidHalf * 2f, LidThickness, LidHalf * 2f)));
            var star = Keep(Star(0.42f, 0.19f));
            var coil = Keep(ToyMeshes.Cylinder(SpringRadius, SpringRadius, SpringHeight / Coils * 0.62f, 16));
            var face = Keep(ToyMeshes.Ellipsoid(Vector3.one * HeadRadius, 20, 12));
            var eye = Keep(ToyMeshes.Ellipsoid(new Vector3(0.085f, 0.11f, 0.06f), 8, 5));
            var nose = Keep(ToyMeshes.Ellipsoid(Vector3.one * 0.15f, 10, 6));
            var hat = Keep(ToyMeshes.Cylinder(0.46f, 0f, 0.62f, 16));
            var bobble = Keep(ToyMeshes.Ellipsoid(Vector3.one * 0.13f, 8, 5));
            var band = Keep(ToyMeshes.Ring(settings.HitRadius - RingWidth, settings.HitRadius, 56));
            var disc = Keep(ToyMeshes.Disc(1f, 56));

            var glow = art.Get("jack_glow");
            var glowBright = art.Get("jack_glow_bright");
            for (var i = 0; i < places.Count; i++) {
                var spot = new Spot { Centre = places[i] };
                spot.Root = new GameObject("Spot " + i).transform;
                spot.Root.SetParent(root.transform, false);
                spot.Root.position = new Vector3(spot.Centre.x, 0.002f, spot.Centre.z);

                var body = ToyMeshes.Part(spot.Root, "Box", box, red, new Vector3(0f, BoxHeight * 0.5f, 0f));
                ToyMeshes.Outlined(body.gameObject);

                // The lid is hinged at its far edge, so it flips up and away from the camera.
                spot.LidHinge = new GameObject("Lid").transform;
                spot.LidHinge.SetParent(spot.Root, false);
                ToyMeshes.Part(spot.LidHinge, "Cover", lid, blue, new Vector3(0f, LidThickness * 0.5f, -LidHalf));
                ToyMeshes.Outlined(spot.LidHinge.gameObject);
                ToyMeshes.Part(spot.LidHinge, "Star", star, art.Get("jack_star"), new Vector3(0f, LidThickness + 0.006f, -LidHalf));

                spot.Spring = new GameObject("Spring").transform;
                spot.Spring.SetParent(spot.Root, false);
                spot.Spring.localPosition = new Vector3(0f, BoxHeight, 0f);
                for (var c = 0; c < Coils; c++) {
                    ToyMeshes.Part(spot.Spring, "Coil", coil, c % 2 == 0 ? yellow : white,
                        new Vector3(0f, (c + 0.5f) * SpringHeight / Coils, 0f));
                }

                ToyMeshes.Outlined(spot.Spring.gameObject);

                spot.Head = new GameObject("Head").transform;
                spot.Head.SetParent(spot.Root, false);
                ToyMeshes.Part(spot.Head, "Face", face, skin, Vector3.zero);
                ToyMeshes.Part(spot.Head, "Nose", nose, red, new Vector3(0f, -0.06f, -HeadRadius * 0.97f));
                ToyMeshes.Part(spot.Head, "Hat", hat, blue, new Vector3(0f, HeadRadius * 0.82f + 0.26f, 0f),
                    new Vector3(-8f, 0f, 0f));
                ToyMeshes.Part(spot.Head, "Bobble", bobble, white, new Vector3(0f, HeadRadius * 0.82f + 0.6f, 0.05f));
                ToyMeshes.Outlined(spot.Head.gameObject);
                // The eyes are navy already: an outline would only make them blots.
                ToyMeshes.Part(spot.Head, "Eye", eye, navy, new Vector3(-0.22f, 0.16f, -HeadRadius * 0.93f));
                ToyMeshes.Part(spot.Head, "Eye", eye, navy, new Vector3(0.22f, 0.16f, -HeadRadius * 0.93f));

                spot.Ring = new GameObject("Warning");
                spot.Ring.transform.SetParent(spot.Root, false);
                var fill = ToyMeshes.Decal(spot.Ring.transform, "Fill", disc, glow, -4);
                fill.transform.localPosition = Vector3.up * 0.011f;
                spot.Fill = fill.transform;
                var edgeRing = ToyMeshes.Decal(spot.Ring.transform, "Ring", band, glowBright, -3);
                edgeRing.transform.localPosition = Vector3.up * 0.013f;
                spot.Ring.SetActive(false);
                spot.Spring.gameObject.SetActive(false);
                spot.Head.gameObject.SetActive(false);
                spots.Add(spot);
            }
        }

        /// <summary>A flat five-pointed star facing up, its first point away from the camera.</summary>
        private static Mesh Star(float outer, float inner)
        {
            var star = new PropMesh();
            for (var k = 0; k < 10; k++) {
                var a = Mathf.PI * 0.5f + k * Mathf.PI / 5f;
                var b = Mathf.PI * 0.5f + (k + 1) * Mathf.PI / 5f;
                var ra = k % 2 == 0 ? outer : inner;
                var rb = k % 2 == 0 ? inner : outer;
                star.FlatTriangle(Vector3.zero, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ra,
                    new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * rb);
            }

            return star.ToMesh("Jack Star");
        }

        private Mesh Keep(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }
    }
}
