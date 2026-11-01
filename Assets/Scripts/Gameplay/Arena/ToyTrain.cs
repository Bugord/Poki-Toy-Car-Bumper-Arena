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
    /// The Train mechanic of the Playroom arena: a toy train (an engine and some carriages) circles the floor on a rail
    /// loop painted near the rim, between the rim and the two pits, shoving every car on the rail out of its way. A few
    /// seconds into the round it whistles and sets off, and ahead of it a line of chevrons lights up on the rail for the
    /// next second or more, so a car has time to leave the track; it laps for the rest of the round. A car on the rail
    /// is thrown to the side of it that it is on (the middle side if it is right on the line), and a little ahead.
    /// <para>
    /// The loop is a stadium, the rim's shape pulled in by <see cref="GameTuning.TrainSettings.RailInset"/>, found from
    /// the arena's own numbers the way the cars' starting contour is, and checked against <see cref="IArenaEdge"/> when
    /// it is laid, so it never runs over a pit. The rail is drawn piece by piece; a piece whose floor has crumbled is
    /// not drawn, and the train, which cannot cross the gap, derails there: it tips and falls off. A few seconds later a
    /// new one is put on a stretch of rail that still has floor, while there is one. A hazard for the bots, who step off the rail inwards (or run on ahead of the
    /// train where a pit is in the way). Registered in every scope; does nothing off its variant.
    /// </para>
    /// </summary>
    public sealed class ToyTrain : IArenaHazard, ITickable, IDisposable
    {
        private const float PieceLength = 1.8f;
        private const int ChevronPool = 16;
        private const float ChevronStep = 1f;
        private const float OutlineThickness = 0.09f;
        private const float FlashRate = 6f;

        // Rail layers over the floor, in metres: below the car shadows (0.015) and above the candy patches (0.008).
        private const float OutlineY = 0.006f;
        private const float BedY = 0.009f;
        private const float SleeperY = 0.012f;
        private const float RailY = 0.0135f;
        private const float GlowY = 0.0145f;

        private const float FallGravity = 22f;
        private const float GoneHeight = -14f;
        private const float TipRate = 140f;

        private sealed class Part
        {
            public Transform Root;
            public float Length;
            public float Offset;
            public Vector3 Velocity;
            public float Height;
            public float Fall;
            public float Roll;
        }

        private readonly GameTuning.TrainSettings settings;
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
        private readonly List<Mesh> meshes = new();
        private readonly List<Part> parts = new();
        private readonly List<Vector3> railVertices = new();
        private readonly List<int[][]> pieceTriangles = new();
        private readonly List<int>[] railIndices = { new(), new(), new(), new() };
        private readonly MeshRenderer[] chevrons = new MeshRenderer[ChevronPool];
        private float[] nextHit = Array.Empty<float>();
        private GameObject root;
        private Mesh railMesh;
        private Material glowBright;
        private Material glowDim;
        private EffectPlayer effects;
        private bool built;
        private bool isDirty;
        private float clock;
        private float travelled;
        private System.Random random;
        private float departAt;
        private float goneAt;
        private bool whistled;
        private bool isGone;
        private int lap;
        private float direction = 1f;
        private float trainLength;

        // The loop: a stadium with straights of half-length straightHalf and caps of this radius round (0, ±straightHalf).
        private float straightHalf;
        private float loopRadius;
        private float loopLength;

        public ToyTrain(GameTuning tuning, ArenaMap map, ArenaRound round, IArenaEdge edge, RoundDifficulty difficulty,
            MechanicArt art, ArenaHazards hazards, ArenaCamera arenaCamera, IAudioService audioService, Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Train;
            this.map = map;
            this.round = round;
            this.edge = edge;
            this.difficulty = difficulty;
            this.art = art;
            this.hazards = hazards;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
            if (map.Mechanic == GameTuning.ArenaMechanic.Train) {
                hazards.Add(this);
            }
        }

        /// <summary>Whether the train and its rail are laid; false off the Train variant.</summary>
        public bool Exists => built;

        /// <summary>The train has fallen off the rail and is gone or falling.</summary>
        public bool IsDerailed { get; private set; }

        /// <summary>Whether the train is under way now.</summary>
        public bool IsMoving => Speed > 0.5f && !IsDerailed;

        /// <summary>Speed along the rail, in metres per second.</summary>
        public float Speed { get; private set; }

        /// <summary>Whether the rail ahead is lit, a warning before the train sets off and while it runs.</summary>
        public bool IsWarning { get; private set; }

        /// <summary>Metres along the rail the front of the engine is, from the start of the loop.</summary>
        public float Head { get; private set; }

        /// <summary>The way the train runs along the loop's distance: 1 or -1.</summary>
        public float Direction => direction;

        /// <summary>The length of the rail loop, in metres.</summary>
        public float RailLength => loopLength;

        /// <summary>The length of the train from the nose to the end of the last carriage, in metres.</summary>
        public float TrainLength => trainLength;

        /// <summary>How many times a new train has been put on the rail after one came off it.</summary>
        public int Respawns { get; private set; }

        /// <summary>How many cars the train has hit this round.</summary>
        public int Hits { get; private set; }

        /// <summary>Seconds since the round began.</summary>
        public float Clock => clock;

        public int Version { get; private set; }

        /// <summary>The rail's middle line at a distance along the loop, flat.</summary>
        public Vector3 RailPosition(float distance)
        {
            Sample(distance, out var position, out _);
            return position;
        }

        public bool TryAvoid(Vector3 position, float margin, IArenaEdge edge, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            if (!built || IsDerailed || !IsWarning) {
                return false;
            }

            Project(position, out var along, out var lateral, out var outward);
            if (Mathf.Abs(lateral) > settings.SideReach + margin) {
                return false;
            }

            // How far in front of the nose the car is, along the rail: negative along the train's own length.
            var ahead = Wrap(direction * (along - Head));
            var reach = Mathf.Max(Speed, 0.5f * tuning.Train.Speed) * settings.BotLookAhead + 1f;
            if (ahead < -trainLength - 1f || ahead > reach) {
                return false;
            }

            // Off the rail inwards if there is room (the pits are in the way along their length), else on ahead of it.
            var inward = -outward;
            var forward = Tangent(along) * direction;
            for (var attempt = 0; attempt < 3; attempt++) {
                var candidate = attempt switch {
                    0 => inward,
                    1 => (inward + forward).normalized,
                    _ => forward,
                };
                if (edge.DistanceToEdge(position + candidate * 3f, edgeMargin) >= 0f) {
                    heading = candidate;
                    return true;
                }
            }

            return false;
        }

        public void Tick()
        {
            if (map.Mechanic != GameTuning.ArenaMechanic.Train) {
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
            }

            if (isDirty) {
                isDirty = false;
                RebuildRail();
            }

            if (IsDerailed) {
                Fall(delta);
                if (isGone && settings.RespawnDelay > 0f && round.IsRunning && !round.IsFinished
                    && clock >= goneAt + settings.RespawnDelay) {
                    TryRespawn();
                }

                return;
            }

            Run(delta);
            Place();
            Light();
            if (round.IsRunning && !round.IsFinished && Speed > 0.5f) {
                HitCars();
            }
        }

        public void Dispose()
        {
            edge.Changed -= OnFloorChanged;
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

        // ---- The loop ----

        private float CapLength => Mathf.PI * loopRadius;

        /// <summary>Position and unit direction of travel (counter-clockwise from above) at a distance along the loop.</summary>
        private void Sample(float distance, out Vector3 position, out Vector3 tangent)
        {
            distance = ((distance % loopLength) + loopLength) % loopLength;
            var straight = 2f * straightHalf;
            if (distance < straight) {
                position = new Vector3(loopRadius, 0f, -straightHalf + distance);
                tangent = Vector3.forward;
                return;
            }

            distance -= straight;
            if (distance < CapLength) {
                var angle = distance / loopRadius;
                position = new Vector3(Mathf.Cos(angle) * loopRadius, 0f, straightHalf + Mathf.Sin(angle) * loopRadius);
                tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                return;
            }

            distance -= CapLength;
            if (distance < straight) {
                position = new Vector3(-loopRadius, 0f, straightHalf - distance);
                tangent = Vector3.back;
                return;
            }

            distance -= straight;
            var back = Mathf.PI + distance / loopRadius;
            position = new Vector3(Mathf.Cos(back) * loopRadius, 0f, -straightHalf + Mathf.Sin(back) * loopRadius);
            tangent = new Vector3(-Mathf.Sin(back), 0f, Mathf.Cos(back));
        }

        private Vector3 Tangent(float distance)
        {
            Sample(distance, out _, out var tangent);
            return tangent;
        }

        /// <summary>
        /// Where along the loop a point is nearest, how far off the line (positive towards the rim), and the flat unit
        /// direction towards the rim there. Meaningful near the rail only.
        /// </summary>
        private void Project(Vector3 point, out float along, out float lateral, out Vector3 outward)
        {
            if (point.z > straightHalf) {
                var from = new Vector3(point.x, 0f, point.z - straightHalf);
                var angle = Mathf.Atan2(from.z, from.x);
                along = 2f * straightHalf + Mathf.Clamp(angle, 0f, Mathf.PI) * loopRadius;
                lateral = from.magnitude - loopRadius;
                outward = from.sqrMagnitude > 1e-6f ? from.normalized : Vector3.right;
            }
            else if (point.z < -straightHalf) {
                var from = new Vector3(point.x, 0f, point.z + straightHalf);
                var angle = Mathf.Atan2(from.z, from.x);
                if (angle < 0f) {
                    angle += Mathf.PI * 2f;
                }

                along = 4f * straightHalf + CapLength + Mathf.Clamp(angle - Mathf.PI, 0f, Mathf.PI) * loopRadius;
                lateral = from.magnitude - loopRadius;
                outward = from.sqrMagnitude > 1e-6f ? from.normalized : Vector3.left;
            }
            else if (point.x >= 0f) {
                along = point.z + straightHalf;
                lateral = point.x - loopRadius;
                outward = Vector3.right;
            }
            else {
                along = 2f * straightHalf + CapLength + (straightHalf - point.z);
                lateral = -point.x - loopRadius;
                outward = Vector3.left;
            }
        }

        private float Wrap(float distance)
        {
            distance %= loopLength;
            if (distance > loopLength * 0.5f) {
                distance -= loopLength;
            }
            else if (distance < -loopLength * 0.5f) {
                distance += loopLength;
            }

            return distance;
        }

        // ---- Running ----

        private void Run(float delta)
        {
            var since = clock - departAt;
            var target = Mathf.Lerp(settings.EasySpeed, settings.Speed, difficulty.Level);
            var ease = Mathf.Clamp01(since / settings.StartRamp);
            Speed = round.HasBegun && since >= 0f ? target * ease * ease * (3f - 2f * ease) : 0f;
            IsWarning = round.HasBegun && since >= -settings.Warning;

            // The whistle goes with the first of the lit chevrons, before the train moves, and again on every lap.
            if (IsWarning && !whistled) {
                whistled = true;
                Whistle();
            }

            if (Speed > 0f) {
                travelled += Speed * delta;
                Head = Mathf.Repeat(Head + direction * Speed * delta, loopLength);
                var laps = Mathf.FloorToInt(travelled / loopLength);
                if (laps != lap) {
                    lap = laps;
                    Whistle();
                }

                if (!OnTrack()) {
                    Derail();
                }
            }
        }

        private void Whistle()
        {
            Version++;
            if (audioService != null) {
                audioService.Play(audioService.Library.CrumbleWarning, 0.5f);
            }
        }

        private float HeadStart() => straightHalf + direction * settings.StationOffset;

        /// <summary>Whether the floor holds under the nose and under each part of the train.</summary>
        private bool OnTrack()
        {
            if (!edge.IsSolid(RailPosition(Head + direction * 1.2f))) {
                return false;
            }

            foreach (var part in parts) {
                if (!edge.IsSolid(RailPosition(Head - direction * (part.Offset + part.Length * 0.5f)))) {
                    return false;
                }
            }

            return true;
        }

        private void Place()
        {
            foreach (var part in parts) {
                var distance = Head - direction * (part.Offset + part.Length * 0.5f);
                Sample(distance, out var position, out var tangent);
                part.Root.SetPositionAndRotation(position, Quaternion.LookRotation(tangent * direction, Vector3.up));
            }
        }

        private void Light()
        {
            var showing = IsWarning && !IsDerailed;
            var reach = Mathf.Lerp(settings.EasySpeed, settings.Speed, difficulty.Level) * settings.Warning;
            var count = Mathf.Min(ChevronPool, Mathf.CeilToInt(reach / ChevronStep) + 2);
            var beat = Mathf.FloorToInt(clock * FlashRate);
            for (var i = 0; i < ChevronPool; i++) {
                var renderer = chevrons[i];
                if (!showing || i >= count) {
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                var distance = Head + direction * (0.8f + i * ChevronStep);
                Sample(distance, out var position, out var tangent);
                if (!edge.IsSolid(position)) {
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                renderer.gameObject.SetActive(true);
                renderer.transform.SetPositionAndRotation(new Vector3(position.x, GlowY, position.z),
                    Quaternion.LookRotation(tangent * direction, Vector3.up));
                var glow = (beat + i) % 3 == 0 ? glowBright : glowDim;
                if (renderer.sharedMaterial != glow) {
                    renderer.sharedMaterial = glow;
                }
            }
        }

        private void HitCars()
        {
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (!car.IsTargetable || car.Position.y > settings.HitHeight || clock < nextHit[i]) {
                    continue;
                }

                var flat = new Vector3(car.Position.x, 0f, car.Position.z);
                foreach (var part in parts) {
                    var distance = Head - direction * (part.Offset + part.Length * 0.5f);
                    Sample(distance, out var centre, out var tangent);
                    var forward = tangent * direction;
                    var side = new Vector3(forward.z, 0f, -forward.x);
                    var rel = flat - centre;
                    var lateral = Vector3.Dot(rel, side);
                    if (Mathf.Abs(Vector3.Dot(rel, forward)) > part.Length * 0.5f + settings.EndReach
                        || Mathf.Abs(lateral) > settings.SideReach) {
                        continue;
                    }

                    nextHit[i] = clock + settings.HitCooldown;
                    Knock(car, forward, side, lateral, centre);
                    break;
                }
            }
        }

        private void Knock(Car car, Vector3 forward, Vector3 side, float lateral, Vector3 centre)
        {
            // To the side of the train the car is on; right on the line, to the side of the middle of the arena.
            var way = Mathf.Abs(lateral) > 0.5f ? Mathf.Sign(lateral) : (Vector3.Dot(-centre, side) >= 0f ? 1f : -1f);
            var heading = (side * way * settings.SideShare + forward * (1f - settings.SideShare)).normalized;

            // Only the player's blow is eased, by the round's difficulty: the bots take all of it.
            var share = car.IsPlayer ? Mathf.Lerp(settings.EasyPlayerShare, 1f, difficulty.Level) : 1f;
            var push = settings.Push * share;
            var speedAway = Vector3.Dot(car.Movement.Velocity, heading);
            push = Mathf.Min(push, Mathf.Max(0f, tuning.Car.MaxBumpSpeed - speedAway));
            if (push > 0f) {
                car.Movement.Push(heading * push);
            }

            car.Movement.Stagger(settings.Stagger * share);
            car.HitByHazard();
            Hits++;

            effects ??= UnityEngine.Object.FindAnyObjectByType<EffectPlayer>();
            if (effects != null) {
                effects.PlayHit(car.Position, heading, 0.85f, true, false);
            }

            if (arenaCamera != null) {
                arenaCamera.Shake(car.IsPlayer ? 0.55f : 0.12f);
            }

            if (audioService != null) {
                audioService.Play(audioService.Library.HitHeavy, car.IsPlayer ? 0.85f : 0.4f);
            }
        }

        // ---- Derailing ----

        private void Derail()
        {
            IsDerailed = true;
            IsWarning = false;
            Version++;
            foreach (var part in parts) {
                var distance = Head - direction * (part.Offset + part.Length * 0.5f);
                Sample(distance, out _, out var tangent);
                part.Velocity = tangent * direction * Speed;
                part.Height = 0f;
                part.Fall = 0f;
                part.Roll = 0f;
            }

            foreach (var chevron in chevrons) {
                chevron.gameObject.SetActive(false);
            }

            Speed = 0f;
        }

        private void Fall(float delta)
        {
            var any = false;
            foreach (var part in parts) {
                if (!part.Root.gameObject.activeSelf) {
                    continue;
                }

                part.Fall += FallGravity * delta;
                part.Height -= part.Fall * delta;
                part.Roll += TipRate * delta;
                var position = part.Root.position;
                var flat = part.Velocity * delta;
                part.Root.SetPositionAndRotation(new Vector3(position.x + flat.x, part.Height, position.z + flat.z),
                    part.Root.rotation * Quaternion.Euler(0f, 0f, TipRate * delta));
                if (part.Height < GoneHeight) {
                    part.Root.gameObject.SetActive(false);
                }
                else {
                    any = true;
                }
            }

            if (!any && !isGone) {
                isGone = true;
                goneAt = clock;
            }
        }

        /// <summary>
        /// Puts the train back on the rail where the floor holds for a run ahead and behind it, after the old one has fallen
        /// away, so the arena keeps its train while any stretch of rail is left. False when there is no such stretch, and
        /// then it is tried again on the next tick: the floor only gets worse, but a stretch may be found late.
        /// </summary>
        private bool TryRespawn()
        {
            var behind = trainLength + settings.RespawnRun * 0.25f;
            var count = Mathf.CeilToInt(loopLength);
            var start = random.Next(count);
            for (var i = 0; i < count; i++) {
                var head = (start + i) % count;
                if (!IsRunSolid(head, behind, settings.RespawnRun)) {
                    continue;
                }

                Head = head;
                travelled = 0f;
                lap = 0;
                departAt = clock + settings.Warning;
                whistled = false;
                IsDerailed = false;
                isGone = false;
                Respawns++;
                foreach (var part in parts) {
                    part.Root.gameObject.SetActive(true);
                }

                Place();
                return true;
            }

            return false;
        }

        private bool IsRunSolid(float head, float behind, float ahead)
        {
            for (var d = -behind; d <= ahead; d += 1f) {
                if (!edge.IsSolid(RailPosition(head + direction * d))) {
                    return false;
                }
            }

            return true;
        }

        // ---- Look: built once, from code. ----

        private void Build()
        {
            built = true;
            random = new System.Random(unchecked(hazards.Seed ^ 0x72A1B5));
            departAt = settings.Delay;
            direction = random.Next(2) == 0 ? 1f : -1f;
            var half = map.Settings.HalfSize;
            straightHalf = Mathf.Max(0f, half.y - half.x);
            loopRadius = half.x - settings.RailInset;
            loopLength = 4f * straightHalf + 2f * Mathf.PI * loopRadius;
            nextHit = new float[round.Cars.Count];
            edge.Changed += OnFloorChanged;

            root = new GameObject("Toy Train");
            root.transform.SetParent(parent, false);
            glowBright = art.Get("train_glow");
            glowDim = art.Get("train_glow_dim");

            BuildRail();
            BuildTrain();
            BuildChevrons();
            Head = HeadStart();
            Place();
            ReportRail();
        }

        private void OnFloorChanged()
        {
            isDirty = true;
        }

        /// <summary>The rail's pieces: each a chord of the loop, drawn in four layers (outline, bed, sleepers, rails).</summary>
        private void BuildRail()
        {
            var pieces = Mathf.CeilToInt(loopLength / PieceLength);
            var length = loopLength / pieces;
            for (var piece = 0; piece < pieces; piece++) {
                var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
                var from = piece * length;
                var to = from + length;
                Strip(triangles[0], from, to, 1.1f, OutlineY, 0f);
                Strip(triangles[1], from, to, 0.93f, BedY, 0f);
                Strip(triangles[2], from + length * 0.2f, from + length * 0.2f + 0.3f, 0.82f, SleeperY, 0f);
                Strip(triangles[2], from + length * 0.7f, from + length * 0.7f + 0.3f, 0.82f, SleeperY, 0f);
                Strip(triangles[3], from, to, 0.08f, RailY, 0.52f);
                Strip(triangles[3], from, to, 0.08f, RailY, -0.52f);
                pieceTriangles.Add(new[] {
                    triangles[0].ToArray(), triangles[1].ToArray(), triangles[2].ToArray(), triangles[3].ToArray(),
                });
            }

            railMesh = new Mesh { name = "Toy Train Rail" };
            railMesh.MarkDynamic();
            railMesh.SetVertices(railVertices);
            railMesh.subMeshCount = 4;
            meshes.Add(railMesh);
            var rail = new GameObject("Rail");
            rail.transform.SetParent(root.transform, false);
            rail.AddComponent<MeshFilter>().sharedMesh = railMesh;
            var renderer = rail.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] {
                art.Get("train_outline"), art.Get("train_bed"), art.Get("train_sleeper"), art.Get("train_rail"),
            };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            RebuildRail();
        }

        /// <summary>A strip of half-width <paramref name="half"/> along the loop between two distances, shifted sideways.</summary>
        private void Strip(List<int> triangles, float from, float to, float half, float height, float shift)
        {
            var steps = Mathf.Max(1, Mathf.CeilToInt((to - from) / 0.9f));
            for (var step = 0; step < steps; step++) {
                var a = Mathf.Lerp(from, to, (float)step / steps);
                var b = Mathf.Lerp(from, to, (float)(step + 1) / steps);
                Sample(a, out var pa, out var ta);
                Sample(b, out var pb, out var tb);
                var na = new Vector3(ta.z, 0f, -ta.x);
                var nb = new Vector3(tb.z, 0f, -tb.x);
                var start = railVertices.Count;
                railVertices.Add(new Vector3(pa.x, height, pa.z) + na * (shift - half));
                railVertices.Add(new Vector3(pa.x, height, pa.z) + na * (shift + half));
                railVertices.Add(new Vector3(pb.x, height, pb.z) + nb * (shift + half));
                railVertices.Add(new Vector3(pb.x, height, pb.z) + nb * (shift - half));
                // Facing up whichever way round the loop's side vectors lie.
                var up = Vector3.Cross(railVertices[start + 1] - railVertices[start], railVertices[start + 2] - railVertices[start]).y >= 0f;
                triangles.Add(start);
                triangles.Add(up ? start + 1 : start + 2);
                triangles.Add(up ? start + 2 : start + 1);
                triangles.Add(start);
                triangles.Add(up ? start + 2 : start + 3);
                triangles.Add(up ? start + 3 : start + 2);
            }
        }

        /// <summary>Draws the pieces that still have floor under them, so a rail never hangs over a gap.</summary>
        private void RebuildRail()
        {
            foreach (var list in railIndices) {
                list.Clear();
            }

            var length = loopLength / pieceTriangles.Count;
            for (var piece = 0; piece < pieceTriangles.Count; piece++) {
                if (!edge.IsSolid(RailPosition((piece + 0.5f) * length))) {
                    continue;
                }

                for (var layer = 0; layer < 4; layer++) {
                    railIndices[layer].AddRange(pieceTriangles[piece][layer]);
                }
            }

            for (var layer = 0; layer < 4; layer++) {
                railMesh.SetTriangles(railIndices[layer], layer, false);
            }

            railMesh.RecalculateBounds();
        }

        private void BuildTrain()
        {
            var outline = art.Get("train_outline");
            var dark = art.Get("train_dark");
            var white = art.Get("train_white");
            var engineMaterials = new[] { art.Get("train_red"), white, dark, art.Get("train_yellow") };

            var engine = new PropMesh();
            var enginePart = settings.PartLength.x;
            engine.Submesh = 2;
            engine.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.12f, enginePart * 0.48f), Quaternion.identity);
            engine.Cylinder(new Vector3(0f, 1.38f, enginePart * 0.36f), Quaternion.identity, 0.16f, 0.25f, 0.28f, 12);
            AddWheels(engine, enginePart * 0.31f, 3);
            engine.Submesh = 0;
            engine.Cylinder(new Vector3(0f, 0.74f, enginePart * 0.13f), Quaternion.Euler(90f, 0f, 0f), 0.47f, 0.47f,
                enginePart * 0.37f, 14);
            engine.Box(new Vector3(0f, 1.57f, -enginePart * 0.29f), new Vector3(0.68f, 0.07f, 0.6f), Quaternion.identity);
            engine.Submesh = 1;
            engine.Box(new Vector3(0f, 0.97f, -enginePart * 0.29f), new Vector3(0.58f, 0.52f, 0.5f), Quaternion.identity);
            engine.Submesh = 3;
            engine.Sphere(new Vector3(0f, 0.74f, enginePart * 0.5f + 0.02f), Quaternion.identity, Vector3.one * 0.19f, 10, 6, false);
            var engineMesh = Keep(engine.ToMesh("Train Engine"));
            var engineHull = new PropMesh();
            engineHull.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.12f, enginePart * 0.48f), Quaternion.identity);
            engineHull.Cylinder(new Vector3(0f, 0.74f, enginePart * 0.13f), Quaternion.Euler(90f, 0f, 0f), 0.47f, 0.47f,
                enginePart * 0.37f, 14);
            engineHull.Box(new Vector3(0f, 0.97f, -enginePart * 0.29f), new Vector3(0.58f, 0.52f, 0.5f), Quaternion.identity);
            engineHull.Box(new Vector3(0f, 1.57f, -enginePart * 0.29f), new Vector3(0.68f, 0.07f, 0.6f), Quaternion.identity);
            engineHull.Cylinder(new Vector3(0f, 1.38f, enginePart * 0.36f), Quaternion.identity, 0.16f, 0.25f, 0.28f, 12);
            var engineOutline = Keep(engineHull.ToHull("Train Engine Outline", OutlineThickness));

            var carriagePart = settings.PartLength.y;
            var carriage = new PropMesh();
            carriage.Submesh = 0;
            carriage.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.1f, carriagePart * 0.48f), Quaternion.identity);
            AddWheels(carriage, carriagePart * 0.3f, 2);
            carriage.Submesh = 1;
            carriage.Box(new Vector3(0f, 0.64f, 0f), new Vector3(0.7f, 0.28f, carriagePart * 0.44f), Quaternion.identity);
            carriage.Submesh = 2;
            carriage.Box(new Vector3(0f, 1.0f, 0f), new Vector3(0.36f, 0.36f, 0.36f), Quaternion.Euler(0f, 20f, 0f));
            var carriageMesh = Keep(carriage.ToMesh("Train Carriage"));
            var carriageHull = new PropMesh();
            carriageHull.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.1f, carriagePart * 0.48f), Quaternion.identity);
            carriageHull.Box(new Vector3(0f, 0.64f, 0f), new Vector3(0.7f, 0.28f, carriagePart * 0.44f), Quaternion.identity);
            carriageHull.Box(new Vector3(0f, 1.0f, 0f), new Vector3(0.36f, 0.36f, 0.36f), Quaternion.Euler(0f, 20f, 0f));
            var carriageOutline = Keep(carriageHull.ToHull("Train Carriage Outline", OutlineThickness));

            var offset = 0f;
            var colours = new[] { art.Get("train_blue"), art.Get("train_yellow") };
            for (var i = 0; i <= settings.Carriages; i++) {
                var part = new Part { Length = i == 0 ? enginePart : carriagePart, Offset = offset };
                part.Root = new GameObject(i == 0 ? "Engine" : "Carriage " + i).transform;
                part.Root.SetParent(root.transform, false);
                if (i == 0) {
                    AddMesh(part.Root.gameObject, engineMesh, engineMaterials);
                    AddMesh(Child(part.Root, "Outline"), engineOutline, outline);
                }
                else {
                    AddMesh(part.Root.gameObject, carriageMesh, dark, colours[(i - 1) % colours.Length], white);
                    AddMesh(Child(part.Root, "Outline"), carriageOutline, outline);
                }

                parts.Add(part);
                offset += part.Length + settings.Gap;
            }

            trainLength = offset - settings.Gap;
        }

        private static void AddWheels(PropMesh mesh, float spread, int perSide)
        {
            for (var side = -1; side <= 1; side += 2) {
                for (var i = 0; i < perSide; i++) {
                    var z = perSide == 1 ? 0f : Mathf.Lerp(-spread, spread, (float)i / (perSide - 1));
                    mesh.Cylinder(new Vector3(side * 0.66f, 0.28f, z), Quaternion.Euler(0f, 0f, 90f), 0.28f, 0.28f, 0.08f, 12);
                }
            }
        }

        private void BuildChevrons()
        {
            var shape = new PropMesh();
            const float width = 1.15f;
            const float lengthHalf = 0.5f;
            const float thick = 0.4f;
            // A chevron pointing along +Z: two slanted bars meeting at the tip.
            var tip = new Vector3(0f, 0f, lengthHalf);
            var right = new Vector3(width, 0f, -lengthHalf);
            var left = new Vector3(-width, 0f, -lengthHalf);
            var drop = new Vector3(0f, 0f, -thick);
            shape.FlatQuad(tip, right, right + drop, tip + drop);
            shape.FlatQuad(left, tip, tip + drop, left + drop);
            var mesh = Keep(shape.ToMesh("Train Chevron"));
            for (var i = 0; i < ChevronPool; i++) {
                var chevron = new GameObject("Chevron " + i);
                chevron.transform.SetParent(root.transform, false);
                chevrons[i] = AddMesh(chevron, mesh, glowDim);
                chevrons[i].sortingOrder = -2;
                chevron.SetActive(false);
            }
        }

        /// <summary>Warns in the log when the rail runs over a pit or past the rim, for a tuning change to be caught.</summary>
        private void ReportRail()
        {
            var worst = float.MaxValue;
            for (var i = 0; i < 240; i++) {
                worst = Mathf.Min(worst, edge.DistanceToEdge(RailPosition(loopLength * i / 240f)));
            }

            RailClearance = worst;
        }

        /// <summary>The least distance from the rail's middle line to the edge of the floor (a pit or the rim), in metres.</summary>
        public float RailClearance { get; private set; }

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

        private static MeshRenderer AddMesh(GameObject target, Mesh mesh, params Material[] materials)
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
