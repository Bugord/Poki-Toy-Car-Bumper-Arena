using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Ball arena's twist: from <see cref="GameTuning.BallSettings.Delay"/> seconds into the round, and every
    /// <see cref="GameTuning.BallSettings.Interval"/> after, a big toy ball is announced and then rolls across the arena
    /// along a straight chord. First a wide lane lights up on the floor, with chevrons pointing the way, and the ball
    /// wobbles at the edge (<see cref="GameTuning.BallSettings.Warning"/>); then it rolls across and off the far side,
    /// where it falls away. Each car it touches is knocked aside once per pass and off balance for a moment, and the hit
    /// is nobody's (<see cref="Car.HitByHazard"/>), so a car it sends over the edge is no one's knockout.
    /// <para>
    /// The ball is moved by this class alone and touches cars by distance, not through physics: it is never pushed or
    /// stopped by a car and needs no collision layer, and a fast ball cannot skip a car between two frames since the
    /// whole stretch it covered is tested. It lives in the scene only for the materials of its look; on any other arena
    /// it switches itself off. The chords come from the seed the bots share (<see cref="BallLane.Seed"/>), the lane
    /// state for the bots goes through <see cref="BallLane"/>.
    /// </para>
    /// <para>
    /// In a match whose cycles turn the variants (<see cref="ArenaVariants"/>) it is on whenever some cycle plays the
    /// Ball variant, and the balls run only in those cycles: the first one <see cref="GameTuning.BallSettings.Delay"/>
    /// after the cycle starts, from a seed turned for the cycle, and a ball on the floor as the cycle ends shrinks away
    /// and hits nobody.
    /// </para>
    /// </summary>
    public sealed class ArenaBall : MonoBehaviour
    {
        private enum Stage
        {
            Idle,
            Warning,
            Rolling,
            Falling,
        }

        // Half the car's width as the ball sees it, in metres: a car whose middle is within the ball's radius and this
        // of the ball's path is touched.
        private const float CarReach = 0.9f;

        // Seconds-squared gravity of the ball once it is over the edge.
        private const float FallGravity = 22f;

        // Height below the floor at which the fallen ball is put away.
        private const float GoneHeight = -14f;

        // The lane's chevrons: spacing along the lane, half width, half length and thickness, in metres.
        private const float ChevronSpacing = 3f;
        private const float ChevronWidth = 0.75f;
        private const float ChevronHalfLength = 0.55f;
        private const float ChevronThickness = 0.45f;

        [Header("Look")]
        [Tooltip("The ball's four segments, in order round it: red, yellow, blue, white")]
        [SerializeField]
        private Material[] segmentMaterials = new Material[4];

        [Tooltip("Navy shell drawn round the ball for its outline")]
        [SerializeField]
        private Material outlineMaterial;

        [Tooltip("The translucent lane on the floor")]
        [SerializeField]
        private Material laneMaterial;

        [Tooltip("The chevrons on the lane that point the way the ball rolls")]
        [SerializeField]
        private Material chevronMaterial;

        [Tooltip("The soft spot under the ball")]
        [SerializeField]
        private Material shadowMaterial;

        [Tooltip("Effects of the scene, for the burst of a hit")]
        [SerializeField]
        private EffectPlayer effects;

        [Tooltip("Share the outline shell is bigger than the ball")]
        [Min(0f)]
        [SerializeField]
        private float outlineScale = 0.07f;

        [Tooltip("Colour of the lane at its dimmest and brightest while the ball is announced")]
        [SerializeField]
        private Color laneDim = new(1f, 0.78f, 0.2f, 0.18f);

        [SerializeField]
        private Color laneBright = new(1f, 0.78f, 0.2f, 0.42f);

        [Tooltip("Seconds the dimmed and the bright lane take to swap")]
        [Min(0.05f)]
        [SerializeField]
        private float lanePulse = 0.3f;

        [Tooltip("Sideways shake and hop of the ball while it is announced, in metres")]
        [SerializeField]
        private Vector2 wobble = new(0.1f, 0.25f);

        [Header("Feeling")]
        [Tooltip("Camera shake of a hit on the player and on a bot, from 0 to 1")]
        [SerializeField]
        private Vector2 hitShake = new(0.6f, 0.15f);

        [Tooltip("Volume of the hit sound on the player and on a bot")]
        [SerializeField]
        private Vector2 hitVolume = new(0.9f, 0.45f);

        [Tooltip("Volume of the low rumble as the ball sets off")]
        [Range(0f, 1f)]
        [SerializeField]
        private float rollVolume = 0.35f;

        private readonly List<Vector3> chevronVertices = new();
        private readonly List<int> chevronTriangles = new();
        private readonly List<Car> hitCars = new();
        private readonly int colorId = Shader.PropertyToID("_BaseColor");

        private GameTuning.BallSettings settings;
        private GameTuning tuning;
        private ArenaRound round;
        private IArenaEdge edge;
        private RoundDifficulty difficulty;
        private BallLane lane;
        private ArenaCamera arenaCamera;
        private IAudioService audioService;

        private Transform ball;
        private Transform spin;
        private Transform shadow;
        private Transform laneRoot;
        private Transform laneQuad;
        private MeshRenderer laneRenderer;
        private Mesh chevronMesh;
        private MaterialPropertyBlock laneBlock;
        private System.Random random;
        private MechanicRun run;

        private Stage stage;
        private float nextLaunch = -1f;
        private float stageTime;
        private float speed;
        private float edgeLength;

        // Half the length of the lane drawn on the floor and how far its middle is along the line from the origin.
        private float laneHalf;
        private float laneShift;
        private Vector3 position;
        private float height;
        private float fallSpeed;
        private bool hasEntered;
        private float angle;

        /// <summary>
        /// Whether a ball is announced, rolling or falling away.
        /// </summary>
        public bool IsActive => stage != Stage.Idle;

        /// <summary>
        /// Where the ball is, with its height; meaningful while <see cref="IsActive"/>.
        /// </summary>
        public Vector3 Position => new(position.x, height, position.z);

        [Inject]
        private void Construct(GameTuning tuning, ArenaRound round, IArenaEdge edge,
            RoundDifficulty difficulty, BallLane lane, ArenaCamera arenaCamera, IAudioService audioService,
            ArenaVariants variants)
        {
            this.tuning = tuning;
            this.settings = tuning.Ball;
            this.round = round;
            this.edge = edge;
            this.difficulty = difficulty;
            this.lane = lane;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;

            this.run = new MechanicRun(variants, GameTuning.ArenaMechanic.Ball);

            // An arena no cycle of which plays the ball has none: nothing of it is built, drawn or run.
            if (!run.IsUsed) {
                gameObject.SetActive(false);
                return;
            }

            if (ball == null) {
                BuildLook();
            }
        }

        private void Update()
        {
            switch (run.Next(round.IsPlaced, Time.deltaTime)) {
                case MechanicStep.Idle:
                    return;
                case MechanicStep.Leave:
                    // In nobody's way from now on; the ball and its lane shrink where they are.
                    lane.IsActive = false;
                    run.Shrink(transform);
                    return;
                case MechanicStep.Clear:
                    Clear();
                    return;
                case MechanicStep.Start:
                    Clear();
                    break;
            }

            if (stage == Stage.Idle) {
                TryLaunch();
                return;
            }

            var deltaTime = Time.deltaTime;
            stageTime += deltaTime;
            switch (stage) {
                case Stage.Warning:
                    TickWarning();
                    break;
                case Stage.Rolling:
                case Stage.Falling:
                    TickRolling(deltaTime);
                    break;
            }
        }

        private void TryLaunch()
        {
            if (!round.IsRunning || round.IsFinished) {
                return;
            }

            if (nextLaunch < 0f) {
                nextLaunch = settings.Delay;
            }

            if (run.Clock < nextLaunch) {
                return;
            }

            random ??= new System.Random(run.SeedOf(lane.Seed));
            Launch();
        }

        /// <summary>
        /// Picks the chord and shows the lane and the ball at the edge.
        /// </summary>
        private void Launch()
        {
            var extent = edge.SolidExtent;
            var directionAngle = (float)random.NextDouble() * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(directionAngle), 0f, Mathf.Sin(directionAngle));
            var normal = new Vector3(-direction.z, 0f, direction.x);
            var offset = ((float)random.NextDouble() * 2f - 1f) * settings.MaxOffset * extent;
            var origin = normal * offset;
            // The line through the floor runs from its farthest solid point behind the origin to the farthest ahead. They are
            // the same on a round floor; on a square one the foot of the perpendicular is not the middle of the chord. A gap
            // in the chord (a fallen corner) is no matter: the ball drops through it, as it does off any edge.
            edge.SolidChord(origin, direction, out var behind, out var ahead);
            edgeLength = Mathf.Max(behind, ahead);
            laneHalf = (behind + ahead) * 0.5f;
            laneShift = (ahead - behind) * 0.5f;

            speed = Mathf.Lerp(settings.EasySpeed, settings.Speed, difficulty.Level);
            stage = Stage.Warning;
            stageTime = 0f;
            hasEntered = false;
            hitCars.Clear();
            fallSpeed = 0f;
            angle = 0f;
            position = origin - direction * (edgeLength + 0.3f);
            height = settings.Radius;

            lane.Direction = direction;
            lane.Normal = normal;
            lane.Origin = origin;
            lane.HalfWidth = settings.Radius;
            lane.BallPosition = position;
            lane.Pass++;
            lane.IsActive = true;

            // The next ball counts from this one's announcement, whatever it does meanwhile.
            var jitter = ((float)random.NextDouble() * 2f - 1f) * settings.Jitter;
            nextLaunch = run.Clock + Mathf.Max(settings.Warning + 2f, settings.Interval + jitter);

            ShowLane(origin, direction, normal);
            ball.gameObject.SetActive(true);
            shadow.gameObject.SetActive(true);
            Place(0f);
        }

        private void TickWarning()
        {
            // Sways and hops on the spot, as a ball held back.
            var sway = Mathf.Sin(stageTime * 38f) * wobble.x;
            var hop = Mathf.Abs(Mathf.Sin(stageTime * 13f)) * wobble.y;
            height = settings.Radius + hop;
            Place(sway);
            lane.BallPosition = position;
            PulseLane();

            if (stageTime >= settings.Warning) {
                stage = Stage.Rolling;
                stageTime = 0f;
                height = settings.Radius;
                if (audioService != null) {
                    audioService.Play(audioService.Library.HitLight, rollVolume);
                }
            }
        }

        private void TickRolling(float deltaTime)
        {
            var previous = position;
            position += lane.Direction * (speed * deltaTime);
            angle += speed * deltaTime / settings.Radius * Mathf.Rad2Deg;

            if (stage == Stage.Rolling) {
                if (!hasEntered && edge.DistanceToSolidEdge(position) > 0.1f) {
                    hasEntered = true;
                }

                // Over the far edge, or over a ring that has fallen away, it drops.
                if (hasEntered && !edge.IsSolid(position)) {
                    stage = Stage.Falling;
                    fallSpeed = 0f;
                }
            }

            if (stage == Stage.Falling) {
                fallSpeed += FallGravity * deltaTime;
                height -= fallSpeed * deltaTime;
                if (height < GoneHeight) {
                    Finish();
                    return;
                }
            }

            lane.BallPosition = position;
            if (stage == Stage.Rolling) {
                lane.IsActive = true;
            }
            else {
                // Falling away it is no longer in anybody's way.
                lane.IsActive = false;
            }

            Place(0f);
            PulseLane();
            if (round.IsRunning && height > -1f) {
                HitCars(previous, position);
            }
        }

        /// <summary>
        /// Puts the ball away and its schedule back to the start, its look back to full size, for the next cycle that
        /// plays it.
        /// </summary>
        private void Clear()
        {
            if (ball != null) {
                Finish();
                ball.localScale = Vector3.one;
                laneRoot.localScale = Vector3.one;
            }

            stage = Stage.Idle;
            nextLaunch = -1f;
            random = null;
            hitCars.Clear();
        }

        private void Finish()
        {
            stage = Stage.Idle;
            lane.IsActive = false;
            ball.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
            laneRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Knocks aside every car the ball swept through since the last frame that it has not hit this pass.
        /// </summary>
        private void HitCars(Vector3 from, Vector3 to)
        {
            var reach = settings.Radius + CarReach;
            foreach (var car in round.Cars) {
                if (!car.IsTargetable || car.Position.y > settings.HitHeight || hitCars.Contains(car)) {
                    continue;
                }

                var flat = new Vector3(car.Position.x, 0f, car.Position.z);
                if (DistanceToSegment(flat, from, to) > reach) {
                    continue;
                }

                hitCars.Add(car);
                Knock(car, flat);
            }
        }

        private void Knock(Car car, Vector3 flat)
        {
            var sideOffset = Vector3.Dot(flat - position, lane.Normal);
            if (Mathf.Abs(sideOffset) < 0.05f) {
                // Dead on the ball's line: the side the middle of the arena is on, so it is not thrown at the edge.
                sideOffset = -Vector3.Dot(position, lane.Normal);
            }

            var side = lane.Normal * (sideOffset >= 0f ? 1f : -1f);
            var direction = (side * settings.SideShare + lane.Direction * (1f - settings.SideShare)).normalized;

            // Only the player's blow is eased, by the round's difficulty: the bots take all of it.
            var share = car.IsPlayer ? Mathf.Lerp(settings.EasyPlayerShare, 1f, difficulty.Level) : 1f;
            var push = settings.Push * share;
            // Never past the most a hit may leave a car going, as with a car's own hit.
            var speedAway = Vector3.Dot(car.Movement.Velocity, direction);
            push = Mathf.Min(push, Mathf.Max(0f, tuning.Car.MaxBumpSpeed - speedAway));
            if (push > 0f) {
                car.Movement.Push(direction * push);
            }

            car.Movement.Stagger(settings.Stagger * share);
            car.HitByHazard();

            if (effects != null) {
                effects.PlayHit(car.Position, direction, 0.85f, true, false);
            }

            if (arenaCamera != null) {
                arenaCamera.Shake(car.IsPlayer ? hitShake.x : hitShake.y);
            }

            if (audioService != null) {
                audioService.Play(audioService.Library.HitHeavy, car.IsPlayer ? hitVolume.x : hitVolume.y);
            }
        }

        private void Place(float sway)
        {
            var place = new Vector3(position.x, height, position.z) + lane.Normal * sway;
            ball.position = place;
            var axis = Vector3.Cross(Vector3.up, lane.Direction);
            spin.rotation = Quaternion.AngleAxis(angle, axis);

            var onFloor = Mathf.Max(0f, height);
            var fade = Mathf.Clamp01(1f - (height - settings.Radius) / 3f);
            shadow.position = new Vector3(place.x, 0.05f, place.z);
            shadow.localScale = Vector3.one * (settings.Radius * 2.1f * fade);
            shadow.gameObject.SetActive(onFloor > 0f && stage != Stage.Idle && stage != Stage.Falling);
        }

        private void PulseLane()
        {
            if (stage == Stage.Falling) {
                laneRoot.gameObject.SetActive(false);
                return;
            }

            // Brighter while announced and pulsing; steady once the ball rolls.
            var mix = stage == Stage.Warning ? 0.5f + 0.5f * Mathf.Sin(stageTime * Mathf.PI * 2f / lanePulse) : 1f;
            laneBlock.SetColor(colorId, Color.Lerp(laneDim, laneBright, mix));
            laneRenderer.SetPropertyBlock(laneBlock);
        }

        private void ShowLane(Vector3 origin, Vector3 direction, Vector3 normal)
        {
            laneRoot.gameObject.SetActive(true);
            laneRoot.SetPositionAndRotation(origin + direction * laneShift + Vector3.up * 0.04f,
                Quaternion.LookRotation(direction, Vector3.up));
            laneQuad.localScale = new Vector3(settings.Radius * 2f, 1f, laneHalf * 2f);

            chevronVertices.Clear();
            chevronTriangles.Clear();
            var count = Mathf.Max(1, Mathf.FloorToInt((laneHalf * 2f - 1f) / ChevronSpacing));
            for (var i = 0; i < count; i++) {
                var z = -laneHalf + ChevronSpacing * (i + 0.5f) + (laneHalf * 2f - count * ChevronSpacing) * 0.5f;
                AddChevron(z);
            }

            chevronMesh.Clear();
            chevronMesh.SetVertices(chevronVertices);
            chevronMesh.SetTriangles(chevronTriangles, 0);
            chevronMesh.RecalculateBounds();
            PulseLane();
        }

        /// <summary>
        /// One chevron pointing along +Z of the lane, centred on <paramref name="z"/>: two slanted bars meeting at the
        /// tip, a little above the lane so they do not flicker against it.
        /// </summary>
        private void AddChevron(float z)
        {
            var tip = new Vector3(0f, 0.02f, z + ChevronHalfLength);
            var right = new Vector3(ChevronWidth, 0.02f, z - ChevronHalfLength);
            var left = new Vector3(-ChevronWidth, 0.02f, z - ChevronHalfLength);
            var drop = new Vector3(0f, 0f, -ChevronThickness);
            AddQuad(tip, right, right + drop, tip + drop);
            AddQuad(left, tip, tip + drop, left + drop);
        }

        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var start = chevronVertices.Count;
            chevronVertices.Add(a);
            chevronVertices.Add(b);
            chevronVertices.Add(c);
            chevronVertices.Add(d);
            // Wound so it faces up.
            chevronTriangles.Add(start);
            chevronTriangles.Add(start + 1);
            chevronTriangles.Add(start + 2);
            chevronTriangles.Add(start);
            chevronTriangles.Add(start + 2);
            chevronTriangles.Add(start + 3);
        }

        private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            var segment = to - from;
            var length = segment.sqrMagnitude;
            var along = length > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - from, segment) / length) : 0f;
            return (point - (from + segment * along)).magnitude;
        }

        // ---- Look: built once, from code, over the materials of the scene object. ----

        private void BuildLook()
        {
            laneBlock = new MaterialPropertyBlock();

            ball = new GameObject("Ball").transform;
            ball.SetParent(transform, false);
            spin = new GameObject("Spin").transform;
            spin.SetParent(ball, false);
            var radius = settings.Radius;

            var body = spin.gameObject.AddComponent<MeshFilter>();
            body.sharedMesh = BuildSphere(radius, 4, false);
            spin.gameObject.AddComponent<MeshRenderer>().sharedMaterials = segmentMaterials;

            var shell = new GameObject("Outline").transform;
            shell.SetParent(ball, false);
            shell.gameObject.AddComponent<MeshFilter>().sharedMesh = BuildSphere(radius * (1f + outlineScale), 1, true);
            shell.gameObject.AddComponent<MeshRenderer>().sharedMaterial = outlineMaterial;

            shadow = new GameObject("BallShadow").transform;
            shadow.SetParent(transform, false);
            shadow.gameObject.AddComponent<MeshFilter>().sharedMesh = BuildDisc(0.5f, 24);
            shadow.gameObject.AddComponent<MeshRenderer>().sharedMaterial = shadowMaterial;

            laneRoot = new GameObject("Lane").transform;
            laneRoot.SetParent(transform, false);
            laneQuad = new GameObject("LaneFloor").transform;
            laneQuad.SetParent(laneRoot, false);
            laneQuad.gameObject.AddComponent<MeshFilter>().sharedMesh = BuildQuad();
            laneRenderer = laneQuad.gameObject.AddComponent<MeshRenderer>();
            laneRenderer.sharedMaterial = laneMaterial;

            var chevrons = new GameObject("Chevrons").transform;
            chevrons.SetParent(laneRoot, false);
            chevronMesh = new Mesh { name = "BallChevrons" };
            chevronMesh.MarkDynamic();
            chevrons.gameObject.AddComponent<MeshFilter>().sharedMesh = chevronMesh;
            chevrons.gameObject.AddComponent<MeshRenderer>().sharedMaterial = chevronMaterial;

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true)) {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            ball.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
            laneRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// A UV-less sphere of <paramref name="segments"/> wedges from pole to pole, one submesh each, like a beach ball's;
        /// turned inside out for the outline shell, so only its far side shows round the ball.
        /// </summary>
        private static Mesh BuildSphere(float radius, int segments, bool isInside)
        {
            const int Slices = 24;
            const int Stacks = 14;
            var vertices = new Vector3[(Slices + 1) * (Stacks + 1)];
            var normals = new Vector3[vertices.Length];
            for (var stack = 0; stack <= Stacks; stack++) {
                var phi = Mathf.PI * stack / Stacks;
                for (var slice = 0; slice <= Slices; slice++) {
                    var theta = Mathf.PI * 2f * slice / Slices;
                    var normal = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));
                    var index = stack * (Slices + 1) + slice;
                    vertices[index] = normal * radius;
                    normals[index] = isInside ? -normal : normal;
                }
            }

            var mesh = new Mesh { name = "BallSphere" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.subMeshCount = segments;
            var triangles = new List<int>[segments];
            for (var i = 0; i < segments; i++) {
                triangles[i] = new List<int>();
            }

            for (var stack = 0; stack < Stacks; stack++) {
                for (var slice = 0; slice < Slices; slice++) {
                    var a = stack * (Slices + 1) + slice;
                    var b = a + Slices + 1;
                    var list = triangles[slice * segments / Slices];
                    // Unity faces are clockwise from the front.
                    if (isInside) {
                        list.Add(a); list.Add(b); list.Add(a + 1);
                        list.Add(a + 1); list.Add(b); list.Add(b + 1);
                    }
                    else {
                        list.Add(a); list.Add(a + 1); list.Add(b);
                        list.Add(a + 1); list.Add(b + 1); list.Add(b);
                    }
                }
            }

            for (var i = 0; i < segments; i++) {
                mesh.SetTriangles(triangles[i], i);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildDisc(float radius, int sides)
        {
            var vertices = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            for (var i = 0; i < sides; i++) {
                var angle = Mathf.PI * 2f * i / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % sides + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "BallShadowDisc", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh {
                name = "BallLane",
                vertices = new[] {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
