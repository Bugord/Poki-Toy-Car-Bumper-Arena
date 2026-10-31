using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Crab mechanic of the sandbox's molds variant: a big wind-up toy crab (key turning on its back) walks sideways
    /// across the sand along a chord, slowly, stops at the far side, snaps its claws, turns, and sets out on a new chord, for
    /// the rest of the round. A trail of footprints shows its path <see cref="GameTuning.CrabSettings.Warning"/> seconds
    /// ahead and it stands still, snapping, before the first walk, so nothing is hit unannounced. A car within
    /// <see cref="GameTuning.CrabSettings.HitRadius"/> is shoved sideways out of its way once per walk, by nobody
    /// (<see cref="HazardBlow"/>). It turns back before the safe edge; if the floor under it falls anyway it walks off and
    /// falls. The chords and their turns come from the bots' seed, so a simulation repeats them; the bots keep out of its
    /// path through <see cref="IArenaHazard"/>.
    /// </summary>
    public sealed class ArenaCrab : ArenaObject, IArenaHazard
    {
        private enum Stage
        {
            Idle,
            Waiting,
            Walking,
            Pausing,
            Falling,
            Gone,
        }

        private const int Footprints = 10;
        private const float FootprintSpacing = 0.55f;
        // Metres ahead of the crab's middle where its trail of footprints starts, past its own feet.
        private const float TrailStart = 1.3f;
        private const float FallGravity = 22f;
        private const float GoneHeight = -14f;
        private const float AppearTime = 0.4f;
        // The crab is a big toy: its model is this many times the size it is built at, to read from the phone's camera.
        private const float ModelScale = 1.5f;

        private readonly GameTuning.CrabSettings settings;
        private readonly MechanicArt art;
        private readonly GameTuning tuning;
        private readonly RoundDifficulty difficulty;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly List<Car> hitCars = new();
        private readonly Transform[] footprints = new Transform[Footprints];
        private readonly Transform[] legs = new Transform[6];
        private readonly float[] legSide = new float[6];

        private HazardBlow blow;
        private System.Random random;
        private Transform crab;
        private Transform rig;
        private Transform key;
        private Transform shadow;
        private readonly Transform[] jaws = new Transform[4];
        private readonly Transform[] eyes = new Transform[2];

        private Stage stage;
        private Vector3 position;
        private Vector3 legStart;
        private Vector3 target;
        private Vector3 direction = Vector3.right;
        private Vector3 facing = Vector3.forward;
        private float walked;
        private float legLength;
        private float pauseTime;
        private float phase;
        private float appearTime;
        private float fallSpeed;
        private float height;

        public ArenaCrab(GameTuning tuning, ArenaMap map, ArenaRound round, IArenaEdge edge, ArenaHazards hazards,
            MechanicArt art, RoundDifficulty difficulty, ArenaCamera arenaCamera, IAudioService audioService, Transform parent)
            : base(GameTuning.ArenaMechanic.Crab, map, round, edge, hazards, parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Crab;
            this.art = art;
            this.difficulty = difficulty;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
        }

        public int Version { get; private set; }

        /// <summary>The crab stands announced, walks or turns: it is on the floor and shows its path.</summary>
        public bool IsAnnounced => stage == Stage.Waiting || stage == Stage.Walking || stage == Stage.Pausing;

        public bool IsWalking => stage == Stage.Walking;

        /// <summary>The crab has walked off the floor and fallen, or is falling.</summary>
        public bool HasFallen => stage == Stage.Falling || stage == Stage.Gone;

        /// <summary>Where the crab is, flat.</summary>
        public Vector3 Position => position;

        /// <summary>The way along its current walk, flat and of length 1.</summary>
        public Vector3 Direction => direction;

        /// <summary>How many footprints of the trail show now.</summary>
        public int VisibleFootprints
        {
            get {
                var count = 0;
                foreach (var print in footprints) {
                    if (print != null && print.gameObject.activeSelf) {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool TryAvoid(Vector3 position, float margin, IArenaEdge edge, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            if (stage != Stage.Waiting && stage != Stage.Walking && stage != Stage.Pausing) {
                return false;
            }

            var remaining = Mathf.Max(0f, Vector3.Dot(target - this.position, direction));
            var reachAhead = Mathf.Min(settings.Speed * (settings.Warning + 0.5f), remaining) + settings.HitRadius;
            return HazardBand.TryAvoid(position, this.position, direction, Across, settings.HitRadius, margin,
                -settings.HitRadius * 0.8f, reachAhead, edge, edgeMargin, out heading);
        }

        public override void Dispose()
        {
            Hazards.Remove(this);
            base.Dispose();
        }

        private Vector3 Across => new(-direction.z, 0f, direction.x);

        protected override void Build()
        {
            random = new System.Random(Hazards.Seed ^ 0x3C7A11);
            blow = new HazardBlow(tuning, difficulty, arenaCamera, audioService);
            Hazards.Add(this);
            BuildLook();
            crab.gameObject.SetActive(false);
        }

        protected override void Step(float deltaTime)
        {
            switch (stage) {
                case Stage.Idle:
                    if (IsClockRunning && Round.Elapsed >= settings.Delay - settings.Warning) {
                        Appear();
                    }

                    break;
                case Stage.Waiting:
                    appearTime += deltaTime;
                    if (Round.Elapsed >= settings.Delay && IsClockRunning) {
                        StartWalk();
                    }

                    break;
                case Stage.Walking:
                    Walk(deltaTime);
                    break;
                case Stage.Pausing:
                    pauseTime += deltaTime;
                    if (pauseTime >= settings.TurnPause) {
                        StartWalk();
                    }

                    break;
                case Stage.Falling:
                    Fall(deltaTime);
                    break;
            }

            if (stage == Stage.Idle || stage == Stage.Gone) {
                return;
            }

            if ((stage == Stage.Walking || stage == Stage.Pausing) && IsClockRunning) {
                HitCars();
            }

            Animate(deltaTime);
            ShowFootprints();
        }

        /// <summary>
        /// The crab pops up at the rim at the far end of a chord through the middle, a little before it may walk, and stands
        /// there snapping its claws while its footprints show the way.
        /// </summary>
        private void Appear()
        {
            var angle = (float)random.NextDouble() * Mathf.PI * 2f;
            var along = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Edge.SolidChord(Vector3.zero, along, out var behind, out var ahead);
            var inset = settings.EdgeInset;
            position = along * -Mathf.Max(0f, behind - inset);
            SetLeg(position, along * Mathf.Max(0f, ahead - inset));
            facing = FacingFor(direction);
            crab.rotation = Quaternion.LookRotation(facing);
            height = 0f;
            appearTime = 0f;
            stage = Stage.Waiting;
            Version++;
            crab.gameObject.SetActive(true);
            shadow.gameObject.SetActive(true);
            Animate(0f);
        }

        private void SetLeg(Vector3 from, Vector3 to)
        {
            legStart = from;
            target = to;
            var offset = to - from;
            offset.y = 0f;
            legLength = offset.magnitude;
            direction = legLength > 0.01f ? offset / legLength : direction;
            walked = 0f;
        }

        /// <summary>The way the crab's face looks: across its path, towards the middle of the arena.</summary>
        private Vector3 FacingFor(Vector3 path)
        {
            var side = new Vector3(-path.z, 0f, path.x);
            return Vector3.Dot(side, -position) >= 0f ? side : -side;
        }

        private void StartWalk()
        {
            // The turn happens before the walk, on the spot, while the claws snap and the new footprints show.
            stage = Stage.Walking;
            hitCars.Clear();
            Version++;
        }

        private void Walk(float deltaTime)
        {
            var step = settings.Speed * deltaTime;
            position += direction * step;
            walked += step;
            if (!Edge.IsSolid(position)) {
                stage = Stage.Falling;
                fallSpeed = 0f;
                return;
            }

            var isAtEnd = Vector3.Dot(target - position, direction) <= 0f;
            if (isAtEnd || Edge.DistanceToEdge(position + direction * 1.2f, settings.EdgeInset * 0.4f) < 0f) {
                stage = Stage.Pausing;
                pauseTime = 0f;
                PlanNextLeg();
            }
        }

        /// <summary>
        /// Picks the next chord from where the crab stands: towards a random point of the inner floor, as far as the safe
        /// floor goes less the inset, or back towards the middle if no chord is long enough.
        /// </summary>
        private void PlanNextLeg()
        {
            var inset = settings.EdgeInset;
            var extent = Edge.SafeExtent;
            for (var attempt = 0; attempt < 8; attempt++) {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var radius = Mathf.Sqrt((float)random.NextDouble()) * extent * 0.5f;
                var aim = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius - position;
                aim.y = 0f;
                if (aim.sqrMagnitude < 4f) {
                    continue;
                }

                aim.Normalize();
                Edge.SolidChord(position, aim, out _, out var ahead);
                for (var length = ahead - inset; length >= settings.MinWalk; length -= 0.5f) {
                    var end = position + aim * length;
                    if (Edge.DistanceToEdge(end, inset) >= 0f) {
                        SetLeg(position, end);
                        return;
                    }
                }
            }

            var home = -position;
            home.y = 0f;
            home = home.sqrMagnitude > 0.01f ? home.normalized : Vector3.right;
            SetLeg(position, position + home * settings.MinWalk);
        }

        private void Fall(float deltaTime)
        {
            fallSpeed += FallGravity * deltaTime;
            height -= fallSpeed * deltaTime;
            position += direction * (settings.Speed * 0.5f * deltaTime);
            shadow.gameObject.SetActive(false);
            if (height < GoneHeight) {
                stage = Stage.Gone;
                crab.gameObject.SetActive(false);
                HideFootprints();
            }
        }

        private void HitCars()
        {
            var reach = settings.HitRadius;
            for (var i = 0; i < Round.Cars.Count; i++) {
                var car = Round.Cars[i];
                if (car.IsOut || car.Position.y > settings.HitHeight || hitCars.Contains(car)) {
                    continue;
                }

                var offset = Flat(car.Position) - position;
                if (offset.sqrMagnitude > reach * reach) {
                    continue;
                }

                hitCars.Add(car);
                var side = Vector3.Dot(offset, Across);
                if (Mathf.Abs(side) < 0.1f) {
                    // Dead on its line: the side the middle of the arena is on, so it is not shoved at the edge.
                    side = -Vector3.Dot(position, Across);
                }

                var push = Across * (side >= 0f ? 1f : -1f) * 0.85f + direction * 0.15f;
                blow.Apply(car, push, settings.Push, 0f, settings.Stagger, settings.EasyPlayerShare);
            }
        }

        // ---- Look. ----

        private void Animate(float deltaTime)
        {
            var isWalking = stage == Stage.Walking || stage == Stage.Falling;
            phase += deltaTime * (isWalking ? 9f : 2.5f);
            var appear = Mathf.Clamp01(appearTime / AppearTime);
            // A pop with a little overshoot.
            var pop = appear < 1f ? Mathf.Sin(appear * Mathf.PI * 0.5f) * (1f + 0.2f * Mathf.Sin(appear * Mathf.PI)) : 1f;

            if (stage == Stage.Pausing) {
                facing = Vector3.RotateTowards(facing, FacingFor(direction), 6f * deltaTime, 0f);
            }

            var bob = isWalking ? Mathf.Abs(Mathf.Sin(phase)) * 0.06f : 0f;
            crab.position = new Vector3(position.x, height, position.z);
            crab.rotation = Quaternion.LookRotation(facing);
            crab.localScale = Vector3.one * (pop * ModelScale);
            rig.localPosition = new Vector3(0f, bob, 0f);
            rig.localRotation = stage == Stage.Falling
                ? Quaternion.Euler(fallSpeed * 4f, 0f, fallSpeed * 6f)
                : Quaternion.Euler(0f, 0f, isWalking ? Mathf.Sin(phase) * 3f : 0f);

            for (var i = 0; i < legs.Length; i++) {
                var swing = Mathf.Sin(phase + i * 1.3f) * (isWalking ? 16f : 3f);
                legs[i].localRotation = Quaternion.Euler(0f, swing * legSide[i], 0f);
            }

            // The claws snap hard while it stands announcing itself, lightly as it walks.
            var open = stage == Stage.Walking ? 14f : 30f;
            var snap = Mathf.Abs(Mathf.Sin(phase * (stage == Stage.Walking ? 1.2f : 2.4f))) * open;
            jaws[0].localRotation = Quaternion.Euler(0f, snap, 0f);
            jaws[1].localRotation = Quaternion.Euler(0f, -snap, 0f);
            jaws[2].localRotation = Quaternion.Euler(0f, snap, 0f);
            jaws[3].localRotation = Quaternion.Euler(0f, -snap, 0f);
            key.localRotation = Quaternion.Euler(0f, phase * 40f * (isWalking ? 1f : 0.4f), 0f);
            eyes[0].localRotation = Quaternion.Euler(Mathf.Sin(phase * 0.7f) * 8f, 0f, 0f);
            eyes[1].localRotation = Quaternion.Euler(Mathf.Sin(phase * 0.7f + 1f) * 8f, 0f, 0f);

            shadow.position = new Vector3(position.x, 0.02f, position.z);
            shadow.rotation = Quaternion.LookRotation(facing);
            shadow.localScale = new Vector3(3.9f, 1f, 2.9f) * pop;
        }

        private void ShowFootprints()
        {
            // Only while the crab is on its way: the trail stays on the sand where it was laid, and the crab walks over it.
            var showing = stage == Stage.Waiting || stage == Stage.Walking || stage == Stage.Pausing;
            if (!showing) {
                HideFootprints();
                return;
            }

            var from = walked + TrailStart;
            var to = Mathf.Min(walked + TrailStart + settings.Speed * settings.Warning, legLength);
            var first = Mathf.CeilToInt(from / FootprintSpacing);
            var side = Across;
            for (var j = 0; j < Footprints; j++) {
                var index = first + j;
                var along = index * FootprintSpacing;
                var print = footprints[index % Footprints];
                if (along > to) {
                    print.gameObject.SetActive(false);
                    continue;
                }

                var lean = index % 2 == 0 ? 0.5f : -0.5f;
                print.gameObject.SetActive(true);
                print.position = legStart + direction * along + side * lean + Vector3.up * 0.01f;
                print.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void HideFootprints()
        {
            foreach (var print in footprints) {
                print.gameObject.SetActive(false);
            }
        }

        private void BuildLook()
        {
            var shell = art.Get("crab_shell");
            var claw = art.Get("crab_claw");
            var leg = art.Get("crab_leg");
            var white = art.Get("crab_eye");
            var navy = art.Get("crab_pupil");
            var gold = art.Get("crab_key");

            crab = new GameObject("Crab").transform;
            crab.SetParent(Root, false);
            rig = new GameObject("Rig").transform;
            rig.SetParent(crab, false);

            ToyMeshes.Part(rig, "Shell", Own(ToyMeshes.Ellipsoid(new Vector3(1.25f, 0.55f, 0.9f))), shell, new Vector3(0f, 0.65f, 0f));
            var stalk = Own(ToyMeshes.Cylinder(0.07f, 0.07f, 0.4f, 8));
            var ball = Own(ToyMeshes.Ellipsoid(Vector3.one * 0.2f, 12, 8));
            var pupil = Own(ToyMeshes.Ellipsoid(Vector3.one * 0.1f, 10, 6));
            for (var i = 0; i < 2; i++) {
                var x = i == 0 ? -0.45f : 0.45f;
                var eye = ToyMeshes.Part(rig, "Eye", stalk, leg, new Vector3(x, 1.05f, 0.6f));
                eyes[i] = eye;
                ToyMeshes.Part(eye, "Ball", ball, white, new Vector3(0f, 0.25f, 0f));
                ToyMeshes.Part(eye, "Pupil", pupil, navy, new Vector3(0f, 0.27f, 0.15f));
            }

            var arm = Own(ToyMeshes.Box(new Vector3(0.2f, 0.2f, 0.75f)));
            var hand = Own(ToyMeshes.Ellipsoid(new Vector3(0.42f, 0.3f, 0.45f), 14, 8));
            var jaw = Own(ToyMeshes.Cylinder(0.15f, 0f, 0.62f, 6));
            for (var i = 0; i < 2; i++) {
                var sign = i == 0 ? -1f : 1f;
                ToyMeshes.Part(rig, "Arm", arm, claw, new Vector3(sign * 1.1f, 0.7f, 0.75f), new Vector3(0f, sign * 12f, 0f));
                ToyMeshes.Part(rig, "Hand", hand, claw, new Vector3(sign * 1.2f, 0.72f, 1.3f));
                for (var k = 0; k < 2; k++) {
                    var pivot = new GameObject("Jaw").transform;
                    pivot.SetParent(rig, false);
                    pivot.localPosition = new Vector3(sign * 1.2f + (k == 0 ? -0.1f : 0.1f), 0.72f, 1.55f);
                    jaws[i * 2 + k] = pivot;
                    ToyMeshes.Part(pivot, "Tip", jaw, claw, new Vector3(0f, 0f, 0.3f), new Vector3(90f, 0f, 0f));
                }
            }

            var limb = Own(ToyMeshes.Box(new Vector3(0.85f, 0.13f, 0.17f)));
            for (var i = 0; i < 6; i++) {
                var sign = i < 3 ? -1f : 1f;
                var pivot = new GameObject("Leg").transform;
                pivot.SetParent(rig, false);
                pivot.localPosition = new Vector3(sign * 1.0f, 0.5f, (i % 3 - 1) * 0.45f);
                legs[i] = pivot;
                legSide[i] = sign;
                ToyMeshes.Part(pivot, "Limb", limb, leg, new Vector3(sign * 0.4f, -0.1f, 0f), new Vector3(0f, 0f, -sign * 24f));
            }

            key = new GameObject("Key").transform;
            key.SetParent(rig, false);
            key.localPosition = new Vector3(0f, 1.2f, -0.1f);
            ToyMeshes.Part(key, "Stem", Own(ToyMeshes.Cylinder(0.07f, 0.07f, 0.4f, 8)), gold, new Vector3(0f, 0.15f, 0f));
            var wing = Own(ToyMeshes.Ellipsoid(new Vector3(0.38f, 0.09f, 0.26f), 12, 6));
            ToyMeshes.Part(key, "WingA", wing, gold, new Vector3(-0.36f, 0.4f, 0f));
            ToyMeshes.Part(key, "WingB", wing, gold, new Vector3(0.36f, 0.4f, 0f));

            ToyMeshes.Outlined(crab.gameObject);

            shadow = ToyMeshes.Part(Root, "Shadow", Own(ToyMeshes.Disc(0.5f, 24)), art.Get("crab_shadow"), Vector3.zero);
            shadow.GetComponent<MeshRenderer>().sortingOrder = -2;

            var print = Own(ToyMeshes.Disc(1f, 12));
            var printMaterial = art.Get("crab_print");
            for (var i = 0; i < Footprints; i++) {
                var decal = ToyMeshes.Decal(Root, "Footprint", print, printMaterial, -3);
                decal.transform.localScale = new Vector3(0.3f, 1f, 0.45f);
                footprints[i] = decal.transform;
                decal.gameObject.SetActive(false);
            }

            shadow.gameObject.SetActive(false);
        }
    }
}
