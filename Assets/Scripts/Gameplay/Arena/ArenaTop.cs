using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Top mechanic of the tabletop's cloth variant: a big spinning top stands on the table from the start, wobbling,
    /// and a second before <see cref="GameTuning.TopSettings.Delay"/> its ring on the floor starts to flash and it spins up.
    /// Then it wanders over the floor on a smooth curving path, turning softly away from the safe edge, so it never falls
    /// unless its floor does. The ring on the floor round it is always there, pulsing, and faint dots show where it will be
    /// over the next <see cref="GameTuning.TopSettings.LookAhead"/> seconds. A car it touches is flung round with its spin and
    /// out from it (<see cref="HazardBlow"/>, nobody's hit), the same car not more often than
    /// <see cref="GameTuning.TopSettings.Retrigger"/>. The wander is a fixed function of the round's clock and a phase drawn
    /// from the bots' seed, so a simulation repeats it, and the prediction replays the same steering.
    /// </summary>
    public sealed class ArenaTop : ArenaObject, IArenaHazard
    {
        private enum Stage
        {
            Standing,
            Wandering,
            Falling,
            Gone,
        }

        private const int Dots = 6;
        private const float FallGravity = 22f;
        private const float GoneHeight = -14f;
        private const float DotHeight = 0.012f;
        // Smaller steps each dot of the prediction takes, for the curve.
        private const int SubSteps = 2;
        // Seconds ahead the steering looks for the edge.
        private const float EdgeLook = 0.9f;
        private const float VersionPeriod = 3f;

        private readonly GameTuning.TopSettings settings;
        private readonly GameTuning tuning;
        private readonly MechanicArt art;
        private readonly RoundDifficulty difficulty;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Vector3[] path = new Vector3[Dots];
        private readonly Transform[] dots = new Transform[Dots];
        private readonly float[] lastFling = new float[8];
        private readonly int colorId = Shader.PropertyToID("_BaseColor");

        private HazardBlow blow;
        private Transform top;
        private Transform spin;
        private Transform tilt;
        private Transform ring;
        private Transform shadow;
        private MeshRenderer ringRenderer;
        private MaterialPropertyBlock ringBlock;
        private Stage stage;
        private Vector3 position;
        private float heading;
        private float phaseA;
        private float phaseB;
        private float clock;
        private float spinAngle;
        private float spinSpeed = 3f;
        private float height;
        private float fallSpeed;
        private float nextVersion;
        private int version = 1;

        public ArenaTop(GameTuning tuning, ArenaMap map, ArenaRound round, IArenaEdge edge, ArenaHazards hazards,
            MechanicArt art, RoundDifficulty difficulty, ArenaCamera arenaCamera, IAudioService audioService, Transform parent)
            : base(GameTuning.ArenaMechanic.Top, map, round, edge, hazards, parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Top;
            this.art = art;
            this.difficulty = difficulty;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
        }

        public int Version => version;

        /// <summary>Where the top is, flat; meaningful once the mechanic is built.</summary>
        public Vector3 Position => position;

        public bool IsWandering => stage == Stage.Wandering;

        public bool TryAvoid(Vector3 position, float margin, IArenaEdge edge, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            if (stage != Stage.Wandering) {
                return false;
            }

            // The nearest of where the top is and where it will be: a car steers away from that one.
            var reach = settings.HitRadius + margin;
            var best = reach * reach;
            var found = false;
            var from = this.position;
            for (var i = -1; i < Dots; i++) {
                var spot = i < 0 ? this.position : path[i];
                var offset = position - spot;
                offset.y = 0f;
                if (offset.sqrMagnitude < best) {
                    best = offset.sqrMagnitude;
                    from = spot;
                    found = true;
                }
            }

            if (!found) {
                return false;
            }

            var away = position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.04f ? away.normalized : Vector3.right;
            var clear = reach + 1f - Mathf.Sqrt(best);
            for (var attempt = 0; attempt < 3; attempt++) {
                var turned = Quaternion.AngleAxis(attempt == 0 ? 0f : (attempt == 1 ? 70f : -70f), Vector3.up) * away;
                if (edge.DistanceToEdge(position + turned * clear, edgeMargin) >= 0f) {
                    heading = turned;
                    return true;
                }
            }

            return false;
        }

        public override void Dispose()
        {
            Hazards.Remove(this);
            base.Dispose();
        }

        protected override void Build()
        {
            var random = new System.Random(Hazards.Seed ^ 0x7E1F);
            phaseA = (float)random.NextDouble() * Mathf.PI * 2f;
            phaseB = (float)random.NextDouble() * Mathf.PI * 2f;
            heading = (float)random.NextDouble() * Mathf.PI * 2f;
            blow = new HazardBlow(tuning, difficulty, arenaCamera, audioService);
            Hazards.Add(this);
            for (var i = 0; i < lastFling.Length; i++) {
                lastFling[i] = float.NegativeInfinity;
            }

            // It starts on the middle of the table, clear of the cars' starting contour.
            position = Edge.ClampToFloor(Vector3.zero);
            BuildLook();
            Animate(0f);
        }

        protected override void Step(float deltaTime)
        {
            var elapsed = Round.Elapsed;
            switch (stage) {
                case Stage.Standing:
                    if (IsClockRunning && elapsed >= settings.Delay) {
                        stage = Stage.Wandering;
                        nextVersion = elapsed;
                    }

                    break;
                case Stage.Wandering:
                    Wander(deltaTime);
                    break;
                case Stage.Falling:
                    Fall(deltaTime);
                    break;
            }

            if (stage == Stage.Gone) {
                return;
            }

            if (stage == Stage.Wandering) {
                if (elapsed >= nextVersion) {
                    nextVersion = elapsed + VersionPeriod;
                    version++;
                }

                Predict();
                if (IsClockRunning) {
                    FlingCars();
                }
            }

            Animate(deltaTime);
        }

        private bool IsSpinningUp => stage == Stage.Standing && IsClockRunning && Round.Elapsed >= settings.Delay - settings.SpinUp;

        /// <summary>
        /// The top's turn at <paramref name="time"/> from <paramref name="at"/> heading <paramref name="angle"/>: a smooth
        /// wobble of the wander, overridden by a soft turn away from the safe edge when it is near. One function for the
        /// real path and its prediction.
        /// </summary>
        private float TurnAt(float time, Vector3 at, float angle)
        {
            var wobble = Mathf.Sin(time * 0.75f + phaseA) * 0.6f + Mathf.Sin(time * 1.7f + phaseB) * 0.4f;
            var turn = wobble * settings.TurnRate;
            var forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var look = at + forward * (settings.Speed * EdgeLook);
            if (Edge.DistanceToEdge(look, settings.EdgeMargin) < 0f) {
                // Towards the middle of the table. The edge's own inward direction would do too, but it flips along the
                // ridge between two edges (a square's corner), and the top would shake on the spot instead of turning.
                var inward = -at;
                inward.y = 0f;
                if (inward.sqrMagnitude < 0.25f) {
                    inward = Edge.Inward(look);
                }

                var want = Mathf.Atan2(inward.z, inward.x);
                var error = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                turn = Mathf.Clamp(error * 3f, -3f, 3f);
            }

            return turn;
        }

        private void Wander(float deltaTime)
        {
            clock += deltaTime;
            heading += TurnAt(clock, position, heading) * deltaTime;
            position += new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading)) * (settings.Speed * deltaTime);
            if (!Edge.IsSolid(position)) {
                stage = Stage.Falling;
                fallSpeed = 0f;
            }
        }

        private void Fall(float deltaTime)
        {
            fallSpeed += FallGravity * deltaTime;
            height -= fallSpeed * deltaTime;
            ring.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
            foreach (var dot in dots) {
                dot.gameObject.SetActive(false);
            }

            if (height < GoneHeight) {
                stage = Stage.Gone;
                top.gameObject.SetActive(false);
            }
        }

        private void Predict()
        {
            var at = position;
            var angle = heading;
            var time = clock;
            var dt = settings.LookAhead / Dots / SubSteps;
            for (var i = 0; i < Dots; i++) {
                // Each dot a sixth of the look-ahead further on.
                for (var s = 0; s < SubSteps; s++) {
                    angle += TurnAt(time, at, angle) * dt;
                    at += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (settings.Speed * dt);
                    time += dt;
                }

                path[i] = at;
            }
        }

        private void FlingCars()
        {
            var reach = settings.HitRadius;
            for (var i = 0; i < Round.Cars.Count; i++) {
                var car = Round.Cars[i];
                if (!car.IsTargetable || car.Position.y > settings.HitHeight || i >= lastFling.Length
                    || Time.time - lastFling[i] < settings.Retrigger) {
                    continue;
                }

                var offset = Flat(car.Position) - position;
                if (offset.sqrMagnitude > reach * reach) {
                    continue;
                }

                lastFling[i] = Time.time;
                // Round with the spin, which turns clockwise seen from above like the model, and out from the top.
                var outward = offset.sqrMagnitude > 0.04f ? offset.normalized : new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
                var around = Vector3.Cross(Vector3.up, outward);
                var direction = around * settings.TangentShare + outward * (1f - settings.TangentShare);
                blow.Apply(car, direction, settings.Push, 0f, settings.Stagger, settings.EasyPlayerShare);
            }
        }

        // ---- Look. ----

        private void Animate(float deltaTime)
        {
            var spinning = stage == Stage.Wandering || stage == Stage.Falling;
            var target = spinning ? 22f : (IsSpinningUp ? 14f : 3f);
            spinSpeed = Mathf.MoveTowards(spinSpeed, target, 30f * deltaTime);
            spinAngle += spinSpeed * deltaTime * Mathf.Rad2Deg;

            var time = Time.time;
            // The axis circles as the top precesses; more when it stands and slows, steady when it runs.
            var lean = spinning ? 6f : 10f;
            tilt.localRotation = Quaternion.Euler(Mathf.Sin(time * 5f) * lean, 0f, Mathf.Cos(time * 5f) * lean);
            spin.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
            top.position = new Vector3(position.x, height, position.z);

            // The ring breathes always; it flashes in the spin-up, a second before the top can touch anything.
            var flashing = IsSpinningUp;
            var pulse = 0.5f + 0.5f * Mathf.Sin(time * (flashing ? 18f : 4f));
            var scale = settings.HitRadius * (1f + 0.04f * pulse);
            ring.position = new Vector3(position.x, DotHeight, position.z);
            ring.localScale = new Vector3(scale, 1f, scale);
            ringBlock.SetColor(colorId, new Color(1f, flashing ? 0.55f : 0.9f, flashing ? 0.3f : 0.95f,
                Mathf.Lerp(0.18f, flashing ? 0.7f : 0.38f, pulse)));
            ringRenderer.SetPropertyBlock(ringBlock);

            shadow.position = new Vector3(position.x, 0.02f, position.z);
            shadow.localScale = Vector3.one * (settings.Radius * 2.2f);

            var showDots = stage == Stage.Wandering;
            for (var i = 0; i < Dots; i++) {
                dots[i].gameObject.SetActive(showDots);
                if (showDots) {
                    dots[i].position = new Vector3(path[i].x, DotHeight, path[i].z);
                }
            }
        }

        private void BuildLook()
        {
            var body = art.Get("top_body");
            var stripe = art.Get("top_stripe");
            var gold = art.Get("top_tip");
            var white = art.Get("top_stud");
            var radius = settings.Radius;

            top = new GameObject("Top").transform;
            top.SetParent(Root, false);
            tilt = new GameObject("Tilt").transform;
            tilt.SetParent(top, false);
            spin = new GameObject("Spin").transform;
            spin.SetParent(tilt, false);

            // A cone point down under a round shoulder, a stripe round the rim, a stem and a knob on top.
            ToyMeshes.Part(spin, "Cone", Own(ToyMeshes.Cylinder(0.04f, radius, 1.1f, 20)), body, new Vector3(0f, 0.65f, 0f));
            ToyMeshes.Part(spin, "Shoulder", Own(ToyMeshes.Ellipsoid(new Vector3(radius, 0.5f, radius), 20, 8, Mathf.PI * 0.5f)),
                body, new Vector3(0f, 1.2f, 0f));
            ToyMeshes.Part(spin, "Stripe", Own(ToyMeshes.Cylinder(radius * 1.04f, radius * 1.04f, 0.2f, 20)), stripe,
                new Vector3(0f, 1.15f, 0f));
            ToyMeshes.Part(spin, "Tip", Own(ToyMeshes.Cylinder(0f, 0.12f, 0.2f, 8)), gold, new Vector3(0f, 0.1f, 0f));
            ToyMeshes.Part(spin, "Stem", Own(ToyMeshes.Cylinder(0.11f, 0.11f, 0.7f, 10)), gold, new Vector3(0f, 1.9f, 0f));
            ToyMeshes.Part(spin, "Knob", Own(ToyMeshes.Ellipsoid(Vector3.one * 0.22f, 12, 8)), gold, new Vector3(0f, 2.3f, 0f));
            var stud = Own(ToyMeshes.Box(new Vector3(0.28f, 0.28f, 0.28f)));
            for (var i = 0; i < 4; i++) {
                var angle = i * 90f;
                var offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(radius * 0.98f, 1.15f, 0f);
                ToyMeshes.Part(spin, "Stud", stud, white, offset, new Vector3(0f, -angle, 0f));
            }

            ToyMeshes.Outlined(top.gameObject);

            shadow = ToyMeshes.Part(Root, "Shadow", Own(ToyMeshes.Disc(0.5f, 24)), art.Get("top_shadow"), Vector3.zero);
            shadow.GetComponent<MeshRenderer>().sortingOrder = -2;

            ringBlock = new MaterialPropertyBlock();
            ringRenderer = ToyMeshes.Decal(Root, "Ring", Own(ToyMeshes.Ring(0.82f, 1f, 40)), art.Get("top_ring"), -4);
            ring = ringRenderer.transform;

            var dot = Own(ToyMeshes.Disc(0.16f, 10));
            var dotMaterial = art.Get("top_dot");
            for (var i = 0; i < Dots; i++) {
                var decal = ToyMeshes.Decal(Root, "Dot", dot, dotMaterial, -3);
                dots[i] = decal.transform;
                dots[i].gameObject.SetActive(false);
            }
        }
    }
}
