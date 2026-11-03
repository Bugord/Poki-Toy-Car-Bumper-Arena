using System;
using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// The oil slicks of a match and the oily tyre tracks they leave. A slick is spilled on the floor behind the car that
    /// used the item and lies there for <see cref="GameTuning.OilSettings.Lifetime"/>: as a layer of the floor
    /// (<see cref="ISurfaceLayer"/>, added to the <see cref="ArenaSurface"/> the cars ask) it takes most of the grip of
    /// every car on it, the spiller's too, so a car slides on with what speed it had and a hit there carries further. A
    /// car on someone else's slick counts as hit by the spiller for as long as it is on it, so sliding off the edge is
    /// the spiller's knock-out. A car that drove through a slick has oily tyres for
    /// <see cref="GameTuning.OilSettings.OilyTime"/>: they keep sliding off the slick, less as the oil wears off
    /// (<see cref="CarMovement.Tyres"/>), and lay two dark tracks behind its rear wheels, thinning as it does; every track narrows away a few seconds later. A slick whose floor goes is gone, and so are the tracks
    /// over floor that fell.
    /// <para>
    /// The tracks are one mesh rebuilt in place while any is there, a ring of quads, so however many there are they cost
    /// one draw and nothing is made each frame. They fade by narrowing, as the effects do, since the unlit material reads
    /// no vertex colour.
    /// </para>
    /// </summary>
    internal sealed class ItemOil : ISurfaceLayer, IDisposable
    {
        private const int PoolSize = 4;
        private const float SpreadTime = 0.3f;
        private const float DryTime = 0.6f;
        // Metres above the floor a car may be and still be on a slick.
        private const float OnHeight = 1f;

        // The tracks: how many quads at most, how far apart they are laid, how wide, how long they last and over how long
        // at the end they narrow away; where the rear wheels are on a car of its own size; how high over the floor.
        private const int TrackCount = 256;
        private const float TrackStep = 0.35f;
        private const float TrackWidth = 0.34f;
        private const float TrackLife = 5f;
        private const float TrackFade = 1.6f;
        private const float WheelSide = 0.56f;
        private const float WheelBack = 0.85f;
        private const float TrackHeight = 0.016f;
        // Below this, in the last seconds of the oil on the tyres, a track is laid thinner.
        private const float OilyFade = 1.2f;

        private sealed class Slick
        {
            public bool IsLive;
            public Car Owner;
            public Vector3 Position;
            public float Age;
            public Transform Root;
        }

        private struct Track
        {
            public Vector3 From;
            public Vector3 To;
            public float Width;
            public float Born;
        }

        private readonly GameTuning.OilSettings settings;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly EffectPlayer effects;
        private readonly IAudioService audioService;
        private readonly Slick[] slicks = new Slick[PoolSize];
        private readonly Track[] tracks = new Track[TrackCount];
        private readonly SurfaceFeel oily;
        private readonly Vector3[] vertices = new Vector3[TrackCount * 4];
        private readonly Mesh trackMesh;
        private readonly MeshRenderer trackRenderer;
        // Per car, by its index in the round: until when its tyres are oily, and where its rear wheels last laid a track.
        private readonly List<float> oilyUntil = new();
        private readonly List<Vector3> lastLeft = new();
        private readonly List<Vector3> lastRight = new();
        private readonly List<bool> isLaying = new();
        private readonly List<bool> isOnSlick = new();
        private int nextTrack;
        private bool isTracksDirty;
        private bool hasSlick;

        public ItemOil(GameTuning tuning, ArenaRound round, IArenaEdge edge, EffectPlayer effects,
            IAudioService audioService, PickupArt art, Transform parent)
        {
            this.settings = tuning.Pickups.Oil;
            this.round = round;
            this.edge = edge;
            this.effects = effects;
            this.audioService = audioService;
            this.oily = new SurfaceFeel(settings.Speed, settings.Acceleration, settings.Grip, settings.Push, 1f);
            for (var i = 0; i < slicks.Length; i++) {
                slicks[i] = new Slick { Root = art.BuildOilSlick(parent) };
            }

            trackMesh = new Mesh { name = "Oil Tracks" };
            trackMesh.MarkDynamic();
            var triangles = new int[TrackCount * 6];
            for (var i = 0; i < TrackCount; i++) {
                triangles[i * 6] = i * 4;
                triangles[i * 6 + 1] = i * 4 + 2;
                triangles[i * 6 + 2] = i * 4 + 1;
                triangles[i * 6 + 3] = i * 4 + 1;
                triangles[i * 6 + 4] = i * 4 + 2;
                triangles[i * 6 + 5] = i * 4 + 3;
            }

            trackMesh.vertices = vertices;
            trackMesh.triangles = triangles;
            var up = new Vector3[vertices.Length];
            for (var i = 0; i < up.Length; i++) {
                up[i] = Vector3.up;
            }

            trackMesh.normals = up;
            // The quads move every frame; the bounds are the whole arena's once, not worked out again each time.
            trackMesh.bounds = new Bounds(Vector3.zero, new Vector3(200f, 2f, 200f));
            trackRenderer = art.BuildOilTracks(parent, trackMesh);
            trackRenderer.enabled = false;
            edge.Changed += OnEdgeChanged;
        }

        /// <summary>
        /// Spills a slick on the floor just ahead of the nearest other car in play within
        /// <see cref="GameTuning.OilSettings.TargetRange"/>, where it is driving, or behind <paramref name="spiller"/>
        /// when there is none; false when there is no floor there.
        /// </summary>
        public bool Spill(Car spiller)
        {
            if (!TryAhead(spiller, out var spot)) {
                var back = Flat(-spiller.transform.forward);
                back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back;
                spot = Flat(spiller.Position) + back * (settings.DropBehind * spiller.Growth);
                if (!edge.IsSolid(spot)) {
                    spot = Flat(spiller.Position);
                    if (!edge.IsSolid(spot)) {
                        return false;
                    }
                }
            }

            // Every slick lying there is taken first; with all of them, the oldest is spilled again.
            Slick slick = null;
            foreach (var candidate in slicks) {
                if (!candidate.IsLive) {
                    slick = candidate;
                    break;
                }

                if (slick == null || candidate.Age > slick.Age) {
                    slick = candidate;
                }
            }

            slick.IsLive = true;
            slick.Owner = spiller;
            slick.Position = spot;
            slick.Age = 0f;
            slick.Root.gameObject.SetActive(true);
            // Turned by where it lies, not by the dice: a simulation repeats the round without drawing for looks.
            var turn = Mathf.Repeat(spot.x * 37f + spot.z * 91f, 360f);
            slick.Root.SetPositionAndRotation(spot, Quaternion.Euler(0f, turn, 0f));
            slick.Root.localScale = Vector3.zero;
            hasSlick = true;
            if (effects != null) {
                effects.PlayPickupFade(spot);
            }

            audioService?.Play(audioService.Library.HitLight, spiller.IsPlayer ? 0.6f : 0.3f);
            return true;
        }

        public SurfaceFeel Sample(Vector3 position)
        {
            if (!hasSlick) {
                return SurfaceFeel.Neutral;
            }

            var feel = SurfaceFeel.Neutral;
            foreach (var slick in slicks) {
                var share = ShareOn(slick, position);
                if (share > 0f) {
                    feel = feel.Combine(SurfaceFeel.Neutral.Lerp(oily, share));
                }
            }

            return feel;
        }

        public void Tick(float deltaTime)
        {
            if (hasSlick) {
                TickSlicks(deltaTime);
            }

            TickTyres();
            if (isTracksDirty) {
                RebuildTracks();
            }
        }

        /// <summary>Takes every slick away at once, the tracks stay to fade: the floor's peak or the match's end.</summary>
        public void Clear()
        {
            foreach (var slick in slicks) {
                if (slick.IsLive) {
                    slick.Age = Mathf.Max(slick.Age, settings.Lifetime - DryTime);
                }
            }
        }

        public void Dispose()
        {
            edge.Changed -= OnEdgeChanged;
            if (trackMesh != null) {
                Object.Destroy(trackMesh);
            }
        }

        /// <summary>
        /// How much of a slick's feel the floor at <paramref name="position"/> has: all of it inside, fading over the
        /// blend at the rim, none off it; less while it spreads and dries up.
        /// </summary>
        private float ShareOn(Slick slick, Vector3 position)
        {
            if (!slick.IsLive) {
                return 0f;
            }

            var radius = settings.Radius * SizeOf(slick);
            var offset = Flat(position) - slick.Position;
            var distance = offset.magnitude;
            if (distance >= radius) {
                return 0f;
            }

            return Mathf.Clamp01((radius - distance) / settings.BlendWidth);
        }

        private float SizeOf(Slick slick)
        {
            var spread = Mathf.Clamp01(slick.Age / SpreadTime);
            var dry = Mathf.Clamp01((settings.Lifetime - slick.Age) / DryTime);
            return Mathf.Min(1f - (1f - spread) * (1f - spread), dry);
        }

        /// <summary>
        /// Ages the slicks, takes the ones whose time is up or whose floor went, and marks the spiller's hit on every
        /// other car on one, which also oils its tyres.
        /// </summary>
        private void TickSlicks(float deltaTime)
        {
            hasSlick = false;
            var cars = round.Cars;
            for (var i = 0; i < isOnSlick.Count; i++) {
                isOnSlick[i] = false;
            }

            foreach (var slick in slicks) {
                if (!slick.IsLive) {
                    continue;
                }

                slick.Age += deltaTime;
                if (slick.Age >= settings.Lifetime || !edge.IsSolid(slick.Position)) {
                    slick.IsLive = false;
                    slick.Owner = null;
                    slick.Root.gameObject.SetActive(false);
                    continue;
                }

                hasSlick = true;
                slick.Root.localScale = Vector3.one * (settings.Radius * SizeOf(slick));
                for (var i = 0; i < cars.Count; i++) {
                    var car = cars[i];
                    if (!car.IsTargetable || car.Position.y > OnHeight || ShareOn(slick, car.Position) <= 0f) {
                        continue;
                    }

                    EnsureCar(i);
                    oilyUntil[i] = Time.time + settings.OilyTime;
                    isOnSlick[i] = true;
                    // A slide off the edge from here is the spiller's, as long as the car is on the oil.
                    if (slick.Owner != null && car != slick.Owner && !slick.Owner.IsOut) {
                        car.HitByItem(slick.Owner);
                    }
                }
            }
        }

        /// <summary>
        /// Gives the cars whose tyres are oily their slide off the slick, and lays their tracks, a quad behind each rear
        /// wheel each step it drives. On a slick the floor already slides, so the tyres add nothing there.
        /// </summary>
        private void TickTyres()
        {
            var cars = round.Cars;
            for (var i = 0; i < cars.Count && i < oilyUntil.Count; i++) {
                var car = cars[i];
                var left = oilyUntil[i] - Time.time;
                if (car.IsOut) {
                    oilyUntil[i] = float.NegativeInfinity;
                }

                var share = car.IsOut || isOnSlick[i] ? 0f : Mathf.Clamp01(left / settings.OilyTime);
                car.Movement.Tyres = SurfaceFeel.Neutral.Lerp(oily, share);
                if (left <= 0f || car.IsOut || !car.Movement.IsGrounded) {
                    isLaying[i] = false;
                    continue;
                }

                var forward = Flat(car.transform.forward).normalized;
                var right = Vector3.Cross(Vector3.up, forward);
                var middle = Flat(car.Position) - forward * (WheelBack * car.Growth);
                var leftWheel = middle - right * (WheelSide * car.Growth);
                var rightWheel = middle + right * (WheelSide * car.Growth);
                if (!isLaying[i]) {
                    isLaying[i] = true;
                    lastLeft[i] = leftWheel;
                    lastRight[i] = rightWheel;
                    continue;
                }

                if ((leftWheel - lastLeft[i]).sqrMagnitude < TrackStep * TrackStep
                    && (rightWheel - lastRight[i]).sqrMagnitude < TrackStep * TrackStep) {
                    continue;
                }

                var width = TrackWidth * car.Growth * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(left / OilyFade));
                AddTrack(lastLeft[i], leftWheel, width);
                AddTrack(lastRight[i], rightWheel, width);
                lastLeft[i] = leftWheel;
                lastRight[i] = rightWheel;
            }
        }

        private void AddTrack(Vector3 from, Vector3 to, float width)
        {
            ref var track = ref tracks[nextTrack];
            track.From = from;
            track.To = to;
            track.Width = width;
            track.Born = Time.time;
            nextTrack = (nextTrack + 1) % TrackCount;
            isTracksDirty = true;
        }

        /// <summary>
        /// Puts every quad where its track is, as wide as it has left; the ones gone are folded to nothing. Once none
        /// is left, the mesh stops being drawn and rebuilt.
        /// </summary>
        private void RebuildTracks()
        {
            var now = Time.time;
            var liveTracks = 0;
            for (var i = 0; i < TrackCount; i++) {
                ref var track = ref tracks[i];
                var share = track.Width > 0f ? Mathf.Clamp01((TrackLife - (now - track.Born)) / TrackFade) : 0f;
                var index = i * 4;
                if (share <= 0f) {
                    track.Width = 0f;
                    vertices[index] = vertices[index + 1] = vertices[index + 2] = vertices[index + 3] = Vector3.zero;
                    continue;
                }

                liveTracks++;
                var along = track.To - track.From;
                var side = Vector3.Cross(Vector3.up, along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.forward)
                    * (track.Width * share * 0.5f);
                var lift = Vector3.up * TrackHeight;
                vertices[index] = track.From - side + lift;
                vertices[index + 1] = track.From + side + lift;
                vertices[index + 2] = track.To - side + lift;
                vertices[index + 3] = track.To + side + lift;
            }

            trackMesh.vertices = vertices;
            trackRenderer.enabled = liveTracks > 0;
            isTracksDirty = liveTracks > 0;
        }

        /// <summary>Tracks over floor that fell go with it.</summary>
        private void OnEdgeChanged()
        {
            for (var i = 0; i < TrackCount; i++) {
                ref var track = ref tracks[i];
                if (track.Width > 0f && !edge.IsSolid((track.From + track.To) * 0.5f)) {
                    track.Width = 0f;
                    isTracksDirty = true;
                }
            }
        }

        private void EnsureCar(int index)
        {
            while (oilyUntil.Count <= index) {
                oilyUntil.Add(float.NegativeInfinity);
                lastLeft.Add(Vector3.zero);
                lastRight.Add(Vector3.zero);
                isLaying.Add(false);
                isOnSlick.Add(false);
            }
        }

        // The nearest other car in play within reach, and the floor just ahead of it, where it drives (its nose when it
        // stands): the slick is spilled where it is about to be, not where it was.
        private bool TryAhead(Car spiller, out Vector3 spot)
        {
            spot = default;
            var origin = Flat(spiller.Position);
            var best = settings.TargetRange * settings.TargetRange;
            Car target = null;
            foreach (var car in round.Cars) {
                if (car == spiller || !car.IsTargetable) {
                    continue;
                }

                var distance = (Flat(car.Position) - origin).sqrMagnitude;
                if (distance <= best) {
                    best = distance;
                    target = car;
                }
            }

            if (target == null) {
                return false;
            }

            var heading = Flat(target.Movement.Velocity);
            if (heading.sqrMagnitude < 1f) {
                heading = Flat(target.transform.forward);
            }

            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            var position = Flat(target.Position);
            spot = position + heading * (settings.DropAhead * target.Growth);
            if (edge.IsSolid(spot)) {
                return true;
            }

            spot = position;
            return edge.IsSolid(spot);
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
