using System;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Stirrer mechanic of the Candy arena: a striped candy stick that pivots over the hole in the middle of the
    /// donut and sweeps round the ring. After a few seconds it sets off, and every few seconds more it slows to a stop,
    /// flashes for a moment and then goes back the other way. A car it meets is pushed along its sweep.
    /// <para>
    /// The warning is in the thing itself and on the floor: the arm is big and moves slowly, a faint wedge on the floor
    /// shows the sector it will cover in the next second (also while it stands still, for the way it will go), and the
    /// arm and the wedge flash for the second it is stopped. It is a hazard for the bots (<see cref="IArenaHazard"/>): a
    /// bot the arm will reach within a moment runs along the sweep, faster than the arm, until it is clear of it.
    /// The arm shortens with the floor that is left, so after the rim has crumbled it does not hang over the void.
    /// Registered in every scope; does nothing off its variant. In a match whose cycles turn the variants it is built
    /// afresh, its schedule from the start, each cycle that plays it, and leaves as the cycle ends (<see cref="MechanicRun"/>).
    /// </para>
    /// </summary>
    public sealed class CandyStirrer : IArenaHazard, ITickable, IDisposable
    {
        private const float ArmHalfWidth = 0.3f;
        private const float ArmHalfHeight = 0.27f;
        private const float ArmLift = 0.34f;
        private const float BandLength = 0.9f;
        private const float TipRadius = 0.75f;
        private const float HubRadius = 1.3f;
        private const float OutlineThickness = 0.09f;
        private const float WedgeInner = 3.2f;
        private const int WedgeSteps = 10;

        // A sweep slower than this, in radians per second, is a standing arm: it hits nobody.
        private const float MovingSpeed = 0.08f;

        // Metres per second the arm shortens at as the floor crumbles away under it.
        private const float ShrinkSpeed = 5f;

        // Seconds ahead of the arm a bot starts to get out of its way (see GameTuning.StirrerSettings.BotLookAhead).

        private const float FlashRate = 8f;

        private readonly GameTuning.StirrerSettings settings;
        private readonly GameTuning tuning;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly RoundDifficulty difficulty;
        private readonly MechanicArt art;
        private readonly ArenaHazards hazards;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Transform parent;
        private readonly System.Collections.Generic.List<Mesh> meshes = new();
        private Material[] normalMaterials;
        private Material[] flashMaterials;
        private bool isOn;
        private float[] nextHit = Array.Empty<float>();
        private GameObject root;
        private Transform arm;
        private Transform wedge;
        private MeshRenderer armRenderer;
        private MeshRenderer wedgeRenderer;
        private MeshRenderer tipRenderer;
        private Transform tip;
        private Material red;
        private Material white;
        private Material flash;
        private Material wedgeMaterial;
        private Material wedgeWarning;
        private EffectPlayer effects;
        private readonly MechanicRun run;
        private bool isFlashing;
        private int leg = -1;
        private bool wasPaused;
        private float clock;
        private float firstDirection = 1f;

        public CandyStirrer(GameTuning tuning, ArenaRound round, IArenaEdge edge, RoundDifficulty difficulty,
            MechanicArt art, ArenaHazards hazards, ArenaCamera arenaCamera, IAudioService audioService,
            ArenaVariants variants, Transform parent)
        {
            this.run = new MechanicRun(variants, GameTuning.ArenaMechanic.Stirrer);
            this.tuning = tuning;
            this.settings = tuning.Stirrer;
            this.round = round;
            this.edge = edge;
            this.difficulty = difficulty;
            this.art = art;
            this.hazards = hazards;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
        }

        /// <summary>Whether the arm is laid out; false off the Stirrer variant.</summary>
        public bool Exists => run.IsBuilt;

        /// <summary>Where the arm points, in radians as Unity's Y rotation: (sin, cos) is its flat direction.</summary>
        public float Angle { get; private set; }

        /// <summary>The sweep right now, in radians per second, positive as Unity's Y rotation.</summary>
        public float AngularVelocity { get; private set; }

        /// <summary>Metres the arm reaches now from the middle.</summary>
        public float Length { get; private set; }

        /// <summary>Whether the arm is stopped and flashing for the sweep to come.</summary>
        public bool IsWarning { get; private set; }

        /// <summary>The way the arm sweeps, or the way it will, as 1 or -1.</summary>
        public float Direction { get; private set; } = 1f;

        /// <summary>Seconds since the round began.</summary>
        public float Clock => clock;

        /// <summary>How many cars the arm has hit this round.</summary>
        public int Hits { get; private set; }

        public int Version { get; private set; }

        public bool TryAvoid(Vector3 position, float margin, IArenaEdge edge, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            var radius = Mathf.Sqrt(position.x * position.x + position.z * position.z);
            if (!run.IsRunning || Length <= settings.HubRadius || radius < settings.HubRadius || radius > Length + margin) {
                return false;
            }

            // How far ahead of the arm, along its sweep, the car is: negative once the arm has passed it.
            var yaw = Mathf.Atan2(position.x, position.z) * Mathf.Rad2Deg;
            var ahead = Direction * Mathf.DeltaAngle(Angle * Mathf.Rad2Deg, yaw) * Mathf.Deg2Rad;
            var half = Mathf.Atan2(settings.Reach + margin, radius);
            var span = settings.AngularSpeed * Mathf.Deg2Rad * settings.BotLookAhead;
            if (ahead < -half || ahead > span + half) {
                return false;
            }

            // Out along the sweep: the car is faster than the arm, except at the very rim. Tried bent in from the rim first.
            var along = Direction * new Vector3(position.z, 0f, -position.x) / radius;
            var inward = edge.Inward(position);
            inward = inward.sqrMagnitude > 0.01f ? inward.normalized : -new Vector3(position.x, 0f, position.z) / radius;
            for (var attempt = 0; attempt < 3; attempt++) {
                var candidate = (along + inward * (attempt == 0 ? 0.25f : attempt == 1 ? 0.7f : 1.6f)).normalized;
                if (edge.DistanceToEdge(position + candidate * 3f, edgeMargin) >= 0f) {
                    heading = candidate;
                    return true;
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
            }

            Schedule(delta);
            Show(delta);
            if (round.IsRunning && !round.IsFinished && Mathf.Abs(AngularVelocity) > MovingSpeed) {
                HitCars();
            }
        }

        public void Dispose()
        {
            Clear();
        }

        /// <summary>Takes the arm off the floor and out of the bots' way, its schedule back to the start.</summary>
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
            isOn = false;
            isFlashing = false;
            leg = -1;
            wasPaused = false;
            clock = 0f;
            firstDirection = 1f;
            Angle = 0f;
            AngularVelocity = 0f;
            Length = 0f;
            IsWarning = false;
            Direction = 1f;
        }

        /// <summary>
        /// The arm's turn at this moment of the round: still until the start (flashing for the last second), then legs of
        /// <see cref="GameTuning.StirrerSettings.ReverseInterval"/> that ease up, sweep, ease down and stand still and
        /// flashing for the pause, the next leg going the other way.
        /// </summary>
        private void Schedule(float delta)
        {
            var since = clock - settings.Delay;
            var paused = false;
            float share;
            if (since < 0f) {
                share = 0f;
                IsWarning = round.HasBegun && since > -1f;
                Direction = firstDirection;
            }
            else {
                var length = settings.ReverseInterval;
                var index = (int)(since / length);
                var into = since - index * length;
                var sweeping = length - settings.Pause;
                Direction = index % 2 == 0 ? firstDirection : -firstDirection;
                var up = Smooth(into / settings.EaseTime);
                var down = Smooth((sweeping - into) / settings.EaseTime);
                share = into < sweeping ? up * down : 0f;
                paused = into >= sweeping;
                if (paused) {
                    // The flash and the next leg's way are shown while it stands still.
                    Direction = -Direction;
                }

                IsWarning = paused;
                if (index != leg) {
                    leg = index;
                    Version++;
                }
            }

            if (paused && !wasPaused || since < 0f && IsWarning && !isFlashing) {
                Version++;
                if (audioService != null) {
                    audioService.Play(audioService.Library.CrumbleWarning, 0.3f);
                }
            }

            wasPaused = paused;
            isFlashing = IsWarning;
            AngularVelocity = share * settings.AngularSpeed * Mathf.Deg2Rad * Direction;
            Angle += AngularVelocity * delta;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void HitCars()
        {
            var direction = new Vector3(Mathf.Sin(Angle), 0f, Mathf.Cos(Angle));
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (!car.IsTargetable || car.Position.y > settings.HitHeight || clock < nextHit[i]) {
                    continue;
                }

                var flat = new Vector3(car.Position.x, 0f, car.Position.z);
                var radius = flat.magnitude;
                if (radius < settings.HubRadius || radius > Length + 0.4f) {
                    continue;
                }

                var along = Mathf.Clamp(Vector3.Dot(flat, direction), 0f, Length);
                if ((flat - direction * along).sqrMagnitude > settings.Reach * settings.Reach) {
                    continue;
                }

                nextHit[i] = clock + settings.HitCooldown;
                Knock(car, flat, radius);
            }
        }

        private void Knock(Car car, Vector3 flat, float radius)
        {
            var sweep = Mathf.Sign(AngularVelocity) * new Vector3(flat.z, 0f, -flat.x) / radius;
            var inward = edge.Inward(car.Position);
            inward = inward.sqrMagnitude > 0.01f ? inward.normalized : -flat / radius;
            var direction = (sweep * settings.SweepShare + inward * (1f - settings.SweepShare)).normalized;

            // Only the player's blow is eased, by the round's difficulty: the bots take all of it.
            var share = car.IsPlayer ? Mathf.Lerp(settings.EasyPlayerShare, 1f, difficulty.Level) : 1f;
            var push = settings.Push * share;
            var speedAway = Vector3.Dot(car.Movement.Velocity, direction);
            push = Mathf.Min(push, Mathf.Max(0f, tuning.Car.MaxBumpSpeed - speedAway));
            if (push > 0f) {
                car.Movement.Push(direction * push);
            }

            car.Movement.Stagger(settings.Stagger * share);
            car.HitByHazard();
            Hits++;

            effects ??= UnityEngine.Object.FindAnyObjectByType<EffectPlayer>();
            if (effects != null) {
                effects.PlayHit(car.Position, direction, 0.8f, true, false);
            }

            if (arenaCamera != null) {
                arenaCamera.Shake(car.IsPlayer ? 0.5f : 0.12f);
            }

            if (audioService != null) {
                audioService.Play(audioService.Library.HitHeavy, car.IsPlayer ? 0.8f : 0.4f);
            }
        }

        private void Show(float delta)
        {
            // The arm reaches as far as the floor does, no further.
            var target = Mathf.Clamp(edge.SolidExtent - 0.3f, 0f, settings.Length);
            Length = Mathf.MoveTowards(Length, target, ShrinkSpeed * delta);
            var scale = Length / settings.Length;
            root.SetActive(Length > settings.HubRadius);
            var turn = Quaternion.Euler(0f, Angle * Mathf.Rad2Deg, 0f);
            arm.localRotation = turn;
            arm.localScale = new Vector3(1f, 1f, scale);
            tip.localPosition = turn * new Vector3(0f, 0.7f, Length);
            tip.localRotation = turn;

            wedge.localRotation = turn;
            wedge.localScale = new Vector3(Direction * Length, 1f, Length);

            // The arm blinks while it stands stopped; the sector stays lit the whole time.
            var on = IsWarning && Mathf.FloorToInt(clock * FlashRate) % 2 == 0;
            if (on != isOn) {
                isOn = on;
                armRenderer.sharedMaterials = on ? flashMaterials : normalMaterials;
                tipRenderer.sharedMaterials = on ? flashMaterials : normalMaterials;
            }

            wedgeRenderer.sharedMaterial = IsWarning ? wedgeWarning : wedgeMaterial;
        }

        // ---- Look: built once, from code. ----

        private void Build()
        {
            hazards.Add(this);
            var random = new System.Random(run.SeedOf(unchecked(hazards.Seed ^ 0x57112E5)));
            firstDirection = random.Next(2) == 0 ? 1f : -1f;
            Direction = firstDirection;
            nextHit = new float[round.Cars.Count];
            Length = Mathf.Clamp(edge.SolidExtent - 0.3f, 0f, settings.Length);

            // The arm starts where the cars and the sector it will cover first are clearest of them.
            var best = 0f;
            var bestClearance = -1f;
            var span = settings.AngularSpeed * Mathf.Deg2Rad * settings.WedgeAhead;
            for (var candidate = 0; candidate < 24; candidate++) {
                var yaw = candidate * Mathf.PI * 2f / 24f;
                var clearance = float.MaxValue;
                for (var k = 0; k <= 2; k++) {
                    var a = yaw + firstDirection * span * k * 0.5f;
                    clearance = Mathf.Min(clearance, Clearance(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a))));
                }

                if (clearance > bestClearance) {
                    bestClearance = clearance;
                    best = yaw;
                }
            }

            Angle = best;

            root = new GameObject("Candy Stirrer");
            root.transform.SetParent(parent, false);
            red = art.Get("stirrer_red");
            white = art.Get("stirrer_white");
            flash = art.Get("stirrer_flash");
            wedgeMaterial = art.Get("stirrer_wedge");
            wedgeWarning = art.Get("stirrer_wedge_warn");
            var outline = art.Get("stirrer_outline");

            // The arm: bands of two colours along +Z, a ball at its tip, hull round all of it.
            var armShape = new PropMesh();
            var bands = Mathf.CeilToInt(settings.Length / BandLength);
            for (var i = 0; i < bands; i++) {
                armShape.Submesh = i % 2;
                armShape.Box(new Vector3(0f, ArmLift, (i + 0.5f) * BandLength),
                    new Vector3(ArmHalfWidth, ArmHalfHeight, BandLength * 0.5f), Quaternion.identity);
            }

            var armMesh = Keep(armShape.ToMesh("Stirrer Arm"));
            var armHull = new PropMesh();
            for (var i = 0; i < bands; i++) {
                armHull.Box(new Vector3(0f, ArmLift, (i + 0.5f) * BandLength),
                    new Vector3(ArmHalfWidth, ArmHalfHeight, BandLength * 0.5f), Quaternion.identity);
            }

            normalMaterials = new[] { red, white };
            flashMaterials = new[] { flash, flash };
            arm = new GameObject("Arm").transform;
            arm.SetParent(root.transform, false);
            armRenderer = Add(arm.gameObject, armMesh, normalMaterials);
            Add(Child(arm, "Outline"), Keep(armHull.ToHull("Stirrer Arm Outline", OutlineThickness)), outline);

            // The lollipop ball at the tip, with a white band round it.
            var ball = new PropMesh();
            ball.Sphere(Vector3.zero, Quaternion.identity, Vector3.one * TipRadius, 16, 10, false);
            ball.Submesh = 1;
            ball.Cylinder(Vector3.zero, Quaternion.identity, TipRadius * 1.02f, TipRadius * 1.02f, 0.09f, 16);
            var ballHull = new PropMesh();
            ballHull.Sphere(Vector3.zero, Quaternion.identity, Vector3.one * TipRadius, 16, 10, false);
            tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            tipRenderer = Add(tip.gameObject, Keep(ball.ToMesh("Stirrer Tip")), normalMaterials);
            Add(Child(tip, "Outline"), Keep(ballHull.ToHull("Stirrer Tip Outline", OutlineThickness)), outline);

            // The hub over the hole.
            var hub = new PropMesh();
            hub.Cylinder(new Vector3(0f, 0.32f, 0f), Quaternion.identity, HubRadius, HubRadius * 0.9f, 0.26f, 20);
            Add(Child(root.transform, "Hub"), Keep(hub.ToMesh("Stirrer Hub")), art.Get("stirrer_hub"));
            Add(Child(root.transform, "Hub Outline"), Keep(hub.ToHull("Stirrer Hub Outline", OutlineThickness)), outline);

            wedge = new GameObject("Wedge").transform;
            wedge.SetParent(root.transform, false);
            wedge.localPosition = new Vector3(0f, 0.013f, 0f);
            var fan = new PropMesh();
            var fromRadius = WedgeInner;
            for (var i = 0; i < WedgeSteps; i++) {
                var a = span * i / WedgeSteps;
                var b = span * (i + 1) / WedgeSteps;
                // Unit sector at the full length; the transform scales it with the arm.
                var scaleIn = fromRadius / settings.Length;
                fan.FlatQuad(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * scaleIn,
                    new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 1f, new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)) * 1f,
                    new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)) * scaleIn);
            }

            wedgeRenderer = Add(wedge.gameObject, Keep(fan.ToMesh("Stirrer Wedge")), wedgeMaterial);
            // Under the cars' shadows, as the Candy patches are.
            wedgeRenderer.sortingOrder = -3;
            wedge.localScale = new Vector3(settings.Length, 1f, settings.Length);
        }

        private Mesh Keep(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }

        private float Clearance(Vector3 direction)
        {
            var nearest = float.MaxValue;
            foreach (var car in round.Cars) {
                var flat = new Vector3(car.Position.x, 0f, car.Position.z);
                var along = Mathf.Clamp(Vector3.Dot(flat, direction), 0f, settings.Length);
                nearest = Mathf.Min(nearest, (flat - direction * along).magnitude);
            }

            return nearest;
        }

        private static GameObject Child(Transform of, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(of, false);
            return child;
        }

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
