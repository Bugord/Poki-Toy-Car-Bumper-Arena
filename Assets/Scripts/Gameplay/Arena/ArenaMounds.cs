using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Mounds mechanic of the sandbox's bricks variant: from <see cref="GameTuning.MoundsSettings.Delay"/> seconds in,
    /// and every <see cref="GameTuning.MoundsSettings.Interval"/> after, one or two spots of the floor are announced
    /// (<see cref="GameTuning.MoundsSettings.Warning"/>): a ring the mound's size whose inside fills from the middle out as
    /// the time runs down, and a low bulge of sand trembling up in its middle. Then the bulge pops up into a mound: a car over it at that moment is tossed up and outwards (nobody's hit, <see cref="HazardBlow"/>), and
    /// for <see cref="GameTuning.MoundsSettings.Stay"/> seconds the mound is slow sand, a layer of the arena's
    /// <see cref="ArenaSurface"/>. Then it sinks. A mound whose floor crumbles sinks at once. The mound is a picture and has
    /// no collider, so cars drive through it, slowed. The spots come from the bots' seed, so a simulation repeats them.
    /// </summary>
    public sealed class ArenaMounds : ArenaObject, IArenaHazard, ISurfaceLayer
    {
        private enum Stage
        {
            Free,
            Warning,
            Risen,
            Sinking,
        }

        private const int Pool = 4;
        private const float PopTime = 0.28f;
        private const float SinkTime = 0.4f;
        // Half the car's width as the mound sees it, in metres, on top of the mound's radius.
        private const float CarReach = 0.7f;
        // Metres over which the sand's effect fades in at the rim.
        private const float BlendWidth = 0.4f;
        private const float DecalHeight = 0.01f;
        // The share of the mound's height the bulge reaches by the end of the warning, where the pop takes over.
        private const float BulgeShare = 0.3f;
        // How much wider than the mound the spill of sand round its foot is.
        private const float SpillShare = 1.14f;

        // The dune from its foot to its top, as shares of the radius and height: a short lip at the foot keeps the
        // outline from sinking into the floor, then a soft rounded slope. It is damp, darker sand.
        private static readonly Vector2[] Profile =
        {
            new(1f, 0f), new(0.99f, 0.07f), new(0.94f, 0.15f), new(0.83f, 0.3f), new(0.69f, 0.49f), new(0.53f, 0.68f),
            new(0.37f, 0.84f), new(0.21f, 0.95f), new(0.09f, 0.99f), new(0f, 1f),
        };

        // A cap of dry, light sand over the dune's upper part, a hair proud of it so that its edge throws the outline: the
        // arena's light falls from about where the camera is, so a smooth dune alone shades flat and reads as a disc.
        private static readonly Vector2[] Cap =
        {
            new(0.883f, 0.3f), new(0.817f, 0.36f), new(0.757f, 0.44f), new(0.685f, 0.53f), new(0.607f, 0.62f),
            new(0.525f, 0.71f), new(0.432f, 0.8f), new(0.332f, 0.88f), new(0.242f, 0.94f), new(0.135f, 0.98f),
            new(0f, 1.012f),
        };

        private sealed class Mound
        {
            public Stage Stage;
            public Vector3 Position;
            public float Time;
            public Transform Root;
            public Transform Dome;
            public Transform Disc;
            public Transform Ring;
            public MaterialPropertyBlock Block;
            public MeshRenderer DiscRenderer;
            public float Scale;
        }

        private readonly GameTuning.MoundsSettings settings;
        private readonly GameTuning tuning;
        private readonly MechanicArt art;
        private readonly RoundDifficulty difficulty;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Mound[] mounds = new Mound[Pool];
        private readonly int colorId = Shader.PropertyToID("_BaseColor");
        // The countdown inside the ring, from faint to strong, the ring itself, and the damp sand round a risen mound.
        private readonly Color fillFaint = new(0.42f, 0.25f, 0.1f, 0.15f);
        private readonly Color fillFull = new(0.42f, 0.25f, 0.1f, 0.4f);
        private readonly Color ringColour = new(0.42f, 0.25f, 0.1f, 0.9f);
        private readonly Color spill = new(0.5f, 0.33f, 0.15f, 0.3f);

        private HazardBlow blow;
        private System.Random random;
        private float nextAnnounce = -1f;
        private int version;

        public ArenaMounds(GameTuning tuning, ArenaMap map, ArenaRound round, IArenaEdge edge, ArenaHazards hazards,
            MechanicArt art, RoundDifficulty difficulty, ArenaCamera arenaCamera, IAudioService audioService,
            ArenaSurface surface, ArenaVariants variants, Transform parent)
            : base(GameTuning.ArenaMechanic.Mounds, map, round, edge, hazards, variants, parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Mounds;
            this.art = art;
            this.difficulty = difficulty;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            surface.Add(this);
        }

        public int Version => version;

        /// <summary>How many mounds are announced or risen now.</summary>
        public int ActiveCount
        {
            get {
                var count = 0;
                foreach (var mound in mounds) {
                    if (mound != null && mound.Stage != Stage.Free) {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>The position of mound <paramref name="index"/> of the pool and whether it is announced, risen or neither.</summary>
        public bool TryGetMound(int index, out Vector3 position, out bool isRisen)
        {
            var mound = mounds[index];
            position = mound != null ? mound.Position : default;
            isRisen = mound != null && mound.Stage == Stage.Risen;
            return mound != null && (mound.Stage == Stage.Warning || mound.Stage == Stage.Risen);
        }

        public SurfaceFeel Sample(Vector3 position)
        {
            // A mound left behind by a cycle that is over shrinks away as plain floor.
            if (!IsRunning) {
                return SurfaceFeel.Neutral;
            }

            foreach (var mound in mounds) {
                if (mound == null || mound.Stage == Stage.Free || mound.Stage == Stage.Warning || mound.Scale <= 0.1f) {
                    continue;
                }

                var offset = Flat(position) - mound.Position;
                var distance = offset.magnitude;
                var radius = settings.Radius * mound.Scale;
                if (distance > radius + BlendWidth) {
                    continue;
                }

                // Half the effect at the rim itself, all of it half a blend inside.
                var inside = Mathf.Clamp01(0.5f + (radius - distance) / BlendWidth);
                var sand = new SurfaceFeel(settings.Speed, settings.Acceleration, settings.Grip, 1f, 1f);
                return SurfaceFeel.Neutral.Lerp(sand, inside);
            }

            return SurfaceFeel.Neutral;
        }

        public bool TryAvoid(Vector3 position, float margin, IArenaEdge edge, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            foreach (var mound in mounds) {
                if (mound == null || mound.Stage != Stage.Warning) {
                    continue;
                }

                var offset = Flat(position) - mound.Position;
                var reach = settings.Radius + CarReach + margin;
                var distance = offset.magnitude;
                if (distance > reach) {
                    continue;
                }

                // Straight out of the circle; from its very middle, towards the middle of the arena.
                var away = distance > 0.2f ? offset / distance : -Flat(mound.Position);
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.right;
                for (var attempt = 0; attempt < 3; attempt++) {
                    var turned = Quaternion.AngleAxis(attempt == 0 ? 0f : (attempt == 1 ? 60f : -60f), Vector3.up) * away;
                    var spot = position + turned * (reach + 0.8f - distance);
                    if (edge.DistanceToEdge(spot, edgeMargin) >= 0f) {
                        heading = turned;
                        return true;
                    }
                }
            }

            return false;
        }

        protected override void ResetState()
        {
            System.Array.Clear(mounds, 0, mounds.Length);
            nextAnnounce = -1f;
        }

        protected override void Build()
        {
            random = new System.Random(SeedOf(Hazards.Seed ^ 0x5B2D));
            blow = new HazardBlow(tuning, difficulty, arenaCamera, audioService);
            Hazards.Add(this);
            BuildLook();
        }

        protected override void Step(float deltaTime)
        {
            if (IsClockRunning) {
                TryAnnounce();
            }

            foreach (var mound in mounds) {
                if (mound.Stage == Stage.Free) {
                    continue;
                }

                mound.Time += deltaTime;
                switch (mound.Stage) {
                    case Stage.Warning:
                        TickWarning(mound);
                        break;
                    case Stage.Risen:
                        TickRisen(mound);
                        break;
                    case Stage.Sinking:
                        TickSinking(mound);
                        break;
                }
            }
        }

        private void TryAnnounce()
        {
            if (nextAnnounce < 0f) {
                nextAnnounce = settings.Delay;
            }

            if (Clock < nextAnnounce) {
                return;
            }

            var jitter = ((float)random.NextDouble() * 2f - 1f) * settings.Jitter;
            nextAnnounce = Clock + Mathf.Max(settings.Warning + 1f, settings.Interval + jitter);
            var count = random.Next(settings.MinCount, Mathf.Max(settings.MinCount, settings.MaxCount) + 1);
            var announced = 0;
            for (var i = 0; i < count; i++) {
                if (TryPick(out var spot) && Announce(spot)) {
                    announced++;
                }
            }

            if (announced > 0) {
                version++;
            }
        }

        /// <summary>
        /// A random spot of the floor that is safe with room round it, clear of the other mounds, and, early in the round,
        /// clear of the cars' starting contour.
        /// </summary>
        private bool TryPick(out Vector3 spot)
        {
            var extent = Edge.SafeExtent;
            var room = settings.Radius + settings.EdgeMargin;
            for (var attempt = 0; attempt < 24; attempt++) {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var radius = Mathf.Sqrt((float)random.NextDouble()) * extent;
                spot = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (Edge.DistanceToEdge(spot, room) < 0f) {
                    continue;
                }

                if (IsNearAnother(spot) || (Clock < settings.SpawnClearTime && IsNearStartContour(spot))) {
                    continue;
                }

                return true;
            }

            spot = default;
            return false;
        }

        private bool IsNearAnother(Vector3 spot)
        {
            foreach (var mound in mounds) {
                if (mound.Stage != Stage.Free && (mound.Position - spot).sqrMagnitude < 4f * settings.Radius * settings.Radius) {
                    return true;
                }
            }

            return false;
        }

        private bool IsNearStartContour(Vector3 spot)
        {
            var count = Round.Cars.Count;
            if (Edge.TryGetSpawn(0, count, out _)) {
                for (var i = 0; i < count; i++) {
                    Edge.TryGetSpawn(i, count, out var start);
                    if ((Flat(start) - spot).sqrMagnitude < (settings.SpawnClear + settings.Radius) * (settings.SpawnClear + settings.Radius)) {
                        return true;
                    }
                }

                return false;
            }

            var contour = Map.Settings.Radius * Map.Settings.SpawnRadius;
            return Mathf.Abs(spot.magnitude - contour) < settings.SpawnClear + settings.Radius;
        }

        private bool Announce(Vector3 spot)
        {
            foreach (var mound in mounds) {
                if (mound.Stage != Stage.Free) {
                    continue;
                }

                mound.Stage = Stage.Warning;
                mound.Position = spot;
                mound.Time = 0f;
                mound.Scale = 0f;
                mound.Root.position = spot;
                mound.Root.gameObject.SetActive(true);
                mound.Ring.gameObject.SetActive(true);
                mound.Ring.localScale = new Vector3(settings.Radius, 1f, settings.Radius);
                mound.Disc.localScale = Vector3.zero;
                SetDome(mound);
                return true;
            }

            return false;
        }

        private void TickWarning(Mound mound)
        {
            var progress = Mathf.Clamp01(mound.Time / settings.Warning);
            mound.Block.SetColor(colorId, Color.Lerp(fillFaint, fillFull, progress));
            mound.DiscRenderer.SetPropertyBlock(mound.Block);
            // The fill spreads from the middle to the ring as the time runs down; the bulge swells and trembles harder.
            var fill = settings.Radius * Mathf.Sqrt(progress);
            mound.Disc.localScale = new Vector3(fill, 1f, fill);
            var tremble = 1f + Mathf.Sin(mound.Time * 45f) * 0.12f * progress;
            mound.Scale = BulgeShare * progress * progress * tremble;
            SetDome(mound);

            if (!Edge.IsSolid(mound.Position)) {
                Release(mound);
                return;
            }

            if (mound.Time >= settings.Warning) {
                Pop(mound);
            }
        }

        private void Pop(Mound mound)
        {
            mound.Stage = Stage.Risen;
            mound.Time = 0f;
            mound.Ring.gameObject.SetActive(false);
            mound.Block.SetColor(colorId, spill);
            mound.DiscRenderer.SetPropertyBlock(mound.Block);
            if (audioService != null) {
                audioService.Play(audioService.Library.HitLight, 0.5f);
            }

            if (!IsClockRunning) {
                return;
            }

            var reach = settings.Radius + CarReach;
            foreach (var car in Round.Cars) {
                if (!car.IsTargetable || car.Position.y > 1.5f) {
                    continue;
                }

                var offset = Flat(car.Position) - mound.Position;
                if (offset.sqrMagnitude > reach * reach) {
                    continue;
                }

                // Out from the mound's middle; from the very middle, towards the middle of the arena.
                var away = offset.sqrMagnitude > 0.04f ? offset.normalized : -Flat(mound.Position).normalized;
                blow.Apply(car, away, settings.BounceOut, settings.BounceUp, settings.Stagger, settings.EasyPlayerShare, 0.6f);
            }
        }

        private void TickRisen(Mound mound)
        {
            var pop = Mathf.Clamp01(mound.Time / PopTime);
            // Up from the bulge with an overshoot, then settling.
            mound.Scale = pop < 1f
                ? Mathf.Lerp(BulgeShare, 1f, Mathf.Sin(pop * Mathf.PI * 0.5f)) * (1f + 0.25f * Mathf.Sin(pop * Mathf.PI))
                : 1f;
            SetDome(mound);
            // The sand goes when its time is up or the floor no longer reaches its edge.
            if (mound.Time >= settings.Stay || Edge.DistanceToEdge(mound.Position) < settings.Radius * 0.6f) {
                mound.Stage = Stage.Sinking;
                mound.Time = 0f;
            }
        }

        private void TickSinking(Mound mound)
        {
            mound.Scale = Mathf.Max(0f, 1f - mound.Time / SinkTime);
            SetDome(mound);
            if (mound.Scale <= 0f) {
                Release(mound);
            }
        }

        private void SetDome(Mound mound)
        {
            // Narrow while it is a bulge, out to the full radius as it rises; the spill of damp sand follows the foot.
            var width = settings.Radius * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(mound.Scale));
            mound.Dome.localScale = new Vector3(width, Mathf.Max(0.001f, settings.Height * mound.Scale), width);
            if (mound.Stage != Stage.Warning) {
                var foot = width * SpillShare;
                mound.Disc.localScale = new Vector3(foot, 1f, foot);
            }
        }

        private static void Release(Mound mound)
        {
            mound.Stage = Stage.Free;
            mound.Scale = 0f;
            mound.Root.gameObject.SetActive(false);
        }

        private void BuildLook()
        {
            var sand = art.Get("mound_sand");
            var damp = art.Get("mound_sand_dark");
            var decal = art.Get("mound_ring");
            var dune = Own(ToyMeshes.Revolved(Profile, 36));
            var cap = Own(ToyMeshes.Revolved(Cap, 36));
            var disc = Own(ToyMeshes.Disc(1f, 40));
            var ring = Own(ToyMeshes.Ring(0.9f, 1f, 48));
            var ringBlock = new MaterialPropertyBlock();
            ringBlock.SetColor(colorId, ringColour);
            for (var i = 0; i < Pool; i++) {
                var mound = new Mound {
                    Root = new GameObject("Mound").transform,
                    Block = new MaterialPropertyBlock(),
                };
                mound.Root.SetParent(Root, false);
                mound.Dome = new GameObject("Sand").transform;
                mound.Dome.SetParent(mound.Root, false);
                ToyMeshes.Part(mound.Dome, "Dune", dune, damp, Vector3.zero);
                ToyMeshes.Part(mound.Dome, "Cap", cap, sand, Vector3.zero);
                // Only the sand is outlined; the flat fill and ring carry their own colour.
                ToyMeshes.Outlined(mound.Dome.gameObject);
                var fill = ToyMeshes.Decal(mound.Root, "Fill", disc, decal, -5);
                fill.transform.localPosition = Vector3.up * DecalHeight;
                mound.Disc = fill.transform;
                mound.DiscRenderer = fill;
                var band = ToyMeshes.Decal(mound.Root, "Ring", ring, decal, -4);
                band.transform.localPosition = Vector3.up * (DecalHeight + 0.002f);
                band.SetPropertyBlock(ringBlock);
                mound.Ring = band.transform;
                mound.Root.gameObject.SetActive(false);
                mounds[i] = mound;
            }
        }
    }
}
