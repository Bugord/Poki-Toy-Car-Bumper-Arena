using System.Collections.Generic;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;

namespace ToyCarArena.Gameplay.Effects
{
    /// <summary>
    /// Draws the cartoon effects of the arena: star bursts and impact rings on hits, puffs and speed lines on a boost,
    /// a big poof where a car goes over the edge, dust where the edge crumbles. Everything is a flat unlit shape with a
    /// navy outline (<see cref="EffectShapes"/>), the toy models' and the stickers' look, and goes away by shrinking to
    /// nothing rather than fading: no transparency to sort, and it pops the way a cartoon does.
    /// <para>
    /// Not a ParticleSystem: the effects are a fixed buffer of plain structs moved here and drawn with
    /// <see cref="Graphics.DrawMesh(Mesh, Matrix4x4, Material, int, Camera, int, MaterialPropertyBlock, bool, bool)"/>,
    /// so a burst allocates nothing, makes no objects, and needs no module set up in a scene. The buffer is full at
    /// <see cref="Capacity"/> shapes; a burst beyond it is cut short, which never shows in a scrum of eight cars.
    /// </para>
    /// <para>
    /// The effects run on unscaled time: under a hit-stop the game holds still while the stars of the hit fly, as in a
    /// fighting game. Everything here is looks only; <see cref="ArenaFeedback"/> says when and how strong.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EffectPlayer : MonoBehaviour
    {
        private const int Capacity = 384;

        // The impact ring thins as it grows: it steps through rings with ever larger holes.
        private static readonly float[] RingHoles = { 0.5f, 0.62f, 0.72f, 0.8f, 0.86f, 0.91f };

        // Each shape is built with these outlines, in its own units, and drawn with the one that comes closest to
        // outlinePixels on screen for its size and its distance: one line width for every effect, with no shader.
        private static readonly float[] OutlineLevels = { 0.015f, 0.025f, 0.04f, 0.06f, 0.09f, 0.13f, 0.19f, 0.28f };

        // The hit burst's spikes, the HUD's knock-out burst's.
        private static readonly float[] BurstReach = { 1f, 0.86f, 0.97f, 0.8f, 1f, 0.9f, 0.82f, 1f, 0.88f, 0.95f, 0.84f };

        // The longest a frame may move the effects, in seconds, so a frame after a stall does not throw them away.
        private const float MaxStep = 0.1f;

        // Share of its life a shape takes to pop up to its size, overshooting a little.
        private const float PopShare = 0.15f;

        [Header("Materials")]
        [Tooltip("The outline of every shape: unlit navy, the stickers' outline")]
        [SerializeField]
        private Material outline;

        [Tooltip("Puffs, impact rings and speed lines: unlit white")]
        [SerializeField]
        private Material white;

        [Tooltip("Stars: unlit gold")]
        [SerializeField]
        private Material gold;

        [Tooltip("Dust and chunks of the floor: unlit, a little lighter than the floor")]
        [SerializeField]
        private Material dust;

        [Header("Shapes")]
        [Tooltip("Every shape's size is multiplied by this, so all the effects grow or shrink together; speeds and " +
            "spreads stay as they are")]
        [Min(0.1f)]
        [SerializeField]
        private float sizeScale = 1.25f;

        [Tooltip("Width of every shape's outline on screen, in pixels at 1080 on the short side: the toy models' " +
            "outline (_Width of mat_outline), so the effects and the cars are drawn with one line")]
        [Min(0f)]
        [SerializeField]
        private float outlinePixels = 4f;

        [Tooltip("Downward pull on stars, hopping dust and chunks, in metres per second squared; above the real " +
            "one, so they drop with a cartoon snap")]
        [Min(0f)]
        [SerializeField]
        private float gravity = 18f;

        [Header("Hits")]
        [Tooltip("Stars of the weakest and of the strongest hit")]
        [SerializeField]
        private Vector2Int hitStars = new(3, 6);

        [Tooltip("Size of a star in the weakest and in the strongest hit, in metres")]
        [SerializeField]
        private Vector2 hitStarSize = new(0.55f, 0.95f);

        [Tooltip("Speed the stars fly off at in the weakest and in the strongest hit, in metres per second")]
        [SerializeField]
        private Vector2 hitStarSpeed = new(3f, 7f);

        [Tooltip("Seconds a star of a hit lasts")]
        [Min(0.05f)]
        [SerializeField]
        private float hitStarLife = 0.5f;

        [Tooltip("Size of the comic burst at the point of the weakest and of the strongest hit, in metres; it stands " +
            "in the air, so it is never taken for the player's ring on the floor")]
        [SerializeField]
        private Vector2 hitBurstSize = new(1.3f, 2.3f);

        [Tooltip("Seconds the burst lasts")]
        [Min(0.05f)]
        [SerializeField]
        private float hitBurstLife = 0.2f;

        [Tooltip("Colour of the burst")]
        [SerializeField]
        private Color hitBurstColor = new(1f, 0.93f, 0.55f);

        [Tooltip("Speed lines flying out of the burst in the weakest and in the strongest hit")]
        [SerializeField]
        private Vector2Int hitLines = new(4, 7);

        [Tooltip("How far a hit's effect is pulled toward the camera, in metres: it shows in the same place, in front " +
            "of the cars that made it")]
        [Min(0f)]
        [SerializeField]
        private float hitLift = 2.5f;

        [Tooltip("Puffs of the weakest and of the strongest hit")]
        [SerializeField]
        private Vector2Int hitPuffs = new(1, 4);

        [Tooltip("Size of a puff in the weakest and in the strongest hit, in metres")]
        [SerializeField]
        private Vector2 hitPuffSize = new(0.45f, 0.85f);

        [Tooltip("Size of the white flash at the point of a heavy hit, in metres")]
        [Min(0f)]
        [SerializeField]
        private float heavyFlashSize = 1.8f;

        [Tooltip("How much bigger everything of a boosted hit is, with this many times the stars")]
        [Min(1f)]
        [SerializeField]
        private float boostedScale = 1.35f;

        [Header("Boost")]
        [Tooltip("Puffs blown out of the back of a car when it boosts")]
        [Min(0)]
        [SerializeField]
        private int boostPuffs = 5;

        [Tooltip("Least and most size of a boost puff, in metres")]
        [SerializeField]
        private Vector2 boostPuffSize = new(0.45f, 0.8f);

        [Tooltip("Speed a boost puff is blown back at, in metres per second")]
        [Min(0f)]
        [SerializeField]
        private float boostPuffSpeed = 5f;

        [Header("Wheel smoke")]
        [Tooltip("Least and most size of a puff of smoke from under a wheel, in metres, from a light skid to a full one")]
        [SerializeField]
        private Vector2 wheelPuffSize = new(0.35f, 0.7f);

        [Tooltip("How long a puff of wheel smoke lasts, in seconds")]
        [Min(0.05f)]
        [SerializeField]
        private float wheelPuffLife = 0.45f;

        [Tooltip("Puffs thrown out from under the wheels when a car lands after a jump")]
        [Min(0)]
        [SerializeField]
        private int landingPuffs = 6;

        [Tooltip("How far behind the car's middle its back is, in metres; the car is about 2.6 m long")]
        [Min(0f)]
        [SerializeField]
        private float carRear = 1.3f;

        [Tooltip("A boosting car leaves a pair of speed lines each time it covers this many metres")]
        [Min(0.2f)]
        [SerializeField]
        private float streakSpacing = 1.4f;

        [Tooltip("Least and most length of a speed line, in metres")]
        [SerializeField]
        private Vector2 streakLength = new(1.4f, 2.2f);

        [Tooltip("Width of a speed line at its head, as a share of its length")]
        [Range(0.05f, 0.5f)]
        [SerializeField]
        private float streakWidth = 0.16f;

        [Tooltip("How far out to each side of the car the speed lines run, in metres, and how high, in metres")]
        [SerializeField]
        private Vector2 streakOffset = new(0.6f, 0.5f);

        [Tooltip("Seconds a speed line lasts")]
        [Min(0.05f)]
        [SerializeField]
        private float streakLife = 0.25f;

        [Tooltip("Least and most size of the dust a boosting car kicks up, in metres")]
        [SerializeField]
        private Vector2 trailDustSize = new(0.35f, 0.55f);

        [Header("Knockout")]
        [Tooltip("Puffs of the splash where a car goes over the edge")]
        [Min(0)]
        [SerializeField]
        private int knockoutPuffs = 9;

        [Tooltip("Least and most size of a splash puff, in metres")]
        [SerializeField]
        private Vector2 knockoutPuffSize = new(0.8f, 1.5f);

        [Tooltip("Least and most speed a splash puff is thrown up at, in metres per second")]
        [SerializeField]
        private Vector2 knockoutSpeed = new(4f, 8f);

        [Tooltip("Stars of the splash")]
        [Min(0)]
        [SerializeField]
        private int knockoutStars = 6;

        [Tooltip("Size of a splash star, in metres")]
        [Min(0f)]
        [SerializeField]
        private float knockoutStarSize = 1f;

        [Tooltip("How much bigger the splash is when the player's car goes over")]
        [Min(1f)]
        [SerializeField]
        private float playerKnockoutScale = 1.3f;

        [Tooltip("Gold sparkles round the splash of a car the player knocked out")]
        [Min(0)]
        [SerializeField]
        private int creditSparkles = 7;

        [Header("Crumble")]
        [Tooltip("A shaking ring throws up a clump of dust about every this many metres round it")]
        [Min(0.5f)]
        [SerializeField]
        private float clumpSpacing = 2.5f;

        [Tooltip("Least and most size of a clump, in metres")]
        [SerializeField]
        private Vector2 clumpSize = new(0.35f, 0.7f);

        [Tooltip("Least and most speed a clump hops up at, in metres per second")]
        [SerializeField]
        private Vector2 clumpHop = new(2f, 4f);

        [Tooltip("A falling ring bursts into a cloud of dust and a chunk about every this many metres round it")]
        [Min(0.5f)]
        [SerializeField]
        private float fallSpacing = 2.2f;

        [Tooltip("Least and most size of a cloud of a falling ring, in metres")]
        [SerializeField]
        private Vector2 fallPuffSize = new(0.9f, 1.5f);

        [Tooltip("Least and most size of a chunk of a falling ring, in metres")]
        [SerializeField]
        private Vector2 fallChunkSize = new(0.3f, 0.6f);

        [Header("Floor coming back")]
        [Tooltip("A ring of the floor coming back is marked about every this many metres round it")]
        [Min(0.5f)]
        [SerializeField]
        private float returnSpacing = 3f;

        [Tooltip("Radius a gold ring closing on a spot the floor comes back to starts at and ends at, in metres")]
        [SerializeField]
        private Vector2 returnRingSize = new(1.8f, 0.5f);

        [Tooltip("Least and most size of a puff of dust a landing tile throws up, in metres")]
        [SerializeField]
        private Vector2 landPuffSize = new(0.7f, 1.1f);

        [Header("Pickups")]
        [Tooltip("Stars thrown up when a car takes a pickup, and their size in metres")]
        [Min(0)]
        [SerializeField]
        private int pickupStars = 6;

        [Tooltip("Size of a star of a pickup being taken, in metres")]
        [Min(0f)]
        [SerializeField]
        private float pickupStarSize = 0.75f;

        [Tooltip("Seconds the shockwave's ring takes to go out to its full reach")]
        [Min(0.05f)]
        [SerializeField]
        private float shockwaveLife = 0.45f;

        [Tooltip("Least and most size of a flame puff a car with a turbo leaves, in metres")]
        [SerializeField]
        private Vector2 flamePuffSize = new(0.4f, 0.7f);

        private enum Shape
        {
            Puff,
            Chunk,
            Star,
            Sparkle,
            Ring,
            Streak,
            Burst,
        }

        /// <summary>
        /// One shape of an effect in flight. Gravity pulls it down (a negative one floats it up), drag slows it, and it
        /// keeps its full size for the first <see cref="Hold"/> of its life before it shrinks away.
        /// </summary>
        private struct Particle
        {
            public Shape Shape;
            public Material Fill;
            public Vector3 Position;
            public Vector3 Velocity;
            // A speed line's direction; a chunk's tumble axis.
            public Vector3 Axis;
            public float Gravity;
            public float Drag;
            public float Angle;
            public float Spin;
            public float Size;
            // A ring grows from this radius to its Size.
            public float StartSize;
            public float Delay;
            public float Age;
            public float Life;
            public float Hold;
        }

        /// <summary>
        /// A boosting car leaving speed lines, and where it left the last ones.
        /// </summary>
        private struct Trail
        {
            public Car Car;
            public Vector3 LastPosition;
        }

        private readonly Particle[] particles = new Particle[Capacity];
        private readonly List<Trail> trails = new();
        private readonly List<Mesh> meshes = new();
        private int count;
        // Where a shape goes when the buffer is full; never drawn.
        private Particle overflow;

        // By outline level, and the rings by hole first.
        private Mesh[] puffs;
        private Mesh[] chunks;
        private Mesh[] stars;
        private Mesh[] sparkles;
        private Mesh[] streaks;
        private Mesh[] bursts;
        private Mesh[][] rings;
        private Material burstFill;
        private Camera view;

        private void Awake()
        {
            // Drawing with a missing material logs an error every frame; without effects the game still plays.
            if (outline == null || white == null || gold == null || dust == null) {
                Debug.LogError($"{nameof(EffectPlayer)} in {gameObject.scene.name} is missing a material; no effects.");
                enabled = false;
                return;
            }

            var levels = OutlineLevels.Length;
            puffs = new Mesh[levels];
            chunks = new Mesh[levels];
            stars = new Mesh[levels];
            sparkles = new Mesh[levels];
            streaks = new Mesh[levels];
            bursts = new Mesh[levels];
            rings = new Mesh[RingHoles.Length][];
            for (var hole = 0; hole < rings.Length; hole++) {
                rings[hole] = new Mesh[levels];
            }

            for (var i = 0; i < levels; i++) {
                // A solid's outline is a larger copy, so its visible width is half the growth of its 1 unit across.
                var level = OutlineLevels[i];
                puffs[i] = Keep(EffectShapes.Puff(12, 8, level * 2f));
                chunks[i] = Keep(EffectShapes.Chunk(level * 2f));
                stars[i] = Keep(EffectShapes.Star(5, 0.5f, level));
                sparkles[i] = Keep(EffectShapes.Star(4, 0.3f, level));
                streaks[i] = Keep(EffectShapes.Streak(streakWidth, level));
                bursts[i] = Keep(EffectShapes.Burst(BurstReach, 0.62f, level));
                for (var hole = 0; hole < rings.Length; hole++) {
                    rings[hole][i] = Keep(EffectShapes.Ring(RingHoles[hole], level, 40));
                }
            }

            burstFill = new Material(white) { name = "Effect Burst" };
            burstFill.SetColor("_BaseColor", hitBurstColor);
            burstFill.SetColor("_Color", hitBurstColor);
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) {
                Destroy(mesh);
            }

            if (burstFill != null) {
                Destroy(burstFill);
            }
        }

        /// <summary>
        /// Two cars hitting at <paramref name="point"/>: a comic burst in the air with speed lines flying out of it, stars
        /// and a few puffs, all bigger the stronger the hit (<paramref name="power"/> 0 to 1), and a white flash for a
        /// heavy one. No ring on the floor: the player's own marker is a ring there.
        /// <paramref name="normal"/> is the way the car that was hit is pushed, on the floor.
        /// </summary>
        public void PlayHit(Vector3 point, Vector3 normal, float power, bool isHeavy, bool isBoosted)
        {
            power = Mathf.Clamp01(power);
            var scale = isBoosted ? boostedScale : 1f;
            // Pulled toward the camera along its view of the point, so it stays where it was on screen but stands in
            // front of the two cars: a puff's outline is its far side, which a car behind it would hide.
            var origin = TowardView(point + Vector3.up * 0.3f, hitLift);

            var stars = Mathf.RoundToInt(Mathf.Lerp(hitStars.x, hitStars.y, power) * scale);
            var starSize = Mathf.Lerp(hitStarSize.x, hitStarSize.y, power) * scale;
            var starSpeed = Mathf.Lerp(hitStarSpeed.x, hitStarSpeed.y, power) * scale;
            for (var i = 0; i < stars; i++) {
                // Spread round the point, up and out, the way of the push a little more often.
                var outward = Around((i + Random.value * 0.6f) / stars) + normal * 0.5f;
                var direction = (outward.normalized + Vector3.up * Random.Range(0.6f, 1.3f)).normalized;
                ref var shape = ref Add(isBoosted && i % 2 == 1 ? Shape.Sparkle : Shape.Star, gold, origin,
                    starSize * Random.Range(0.75f, 1.1f), hitStarLife * Random.Range(0.8f, 1.2f));
                shape.Velocity = direction * (starSpeed * Random.Range(0.7f, 1.1f));
                shape.Gravity = gravity;
                shape.Drag = 1.5f;
                shape.Angle = Random.Range(0f, 360f);
                shape.Spin = Random.Range(-540f, 540f);
                shape.Hold = 0.55f;
            }

            // The burst stands behind the stars, turned a little each time; the lines fly out of it in the view's plane.
            var burstSize = Mathf.Lerp(hitBurstSize.x, hitBurstSize.y, power) * scale;
            var burstAt = TowardView(origin, 0.3f);
            ref var burst = ref Add(Shape.Burst, burstFill, burstAt, burstSize, hitBurstLife);
            burst.Angle = Random.Range(-25f, 25f);
            burst.Hold = 0.45f;
            var facing = view != null ? view.transform.rotation : Quaternion.identity;
            var lines = Mathf.RoundToInt(Mathf.Lerp(hitLines.x, hitLines.y, power));
            for (var i = 0; i < lines; i++) {
                var angle = (i + Random.Range(-0.25f, 0.25f)) / lines * Mathf.PI * 2f;
                var axis = facing * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                ref var line = ref Add(Shape.Streak, white, burstAt + axis * (burstSize * sizeScale * 0.45f),
                    burstSize * Random.Range(0.45f, 0.65f), hitBurstLife * Random.Range(0.9f, 1.3f));
                line.Axis = axis;
                line.Velocity = axis * (burstSize * Random.Range(5f, 7f));
                line.Drag = 9f;
                line.Hold = 0.3f;
            }

            var puffs = Mathf.RoundToInt(Mathf.Lerp(hitPuffs.x, hitPuffs.y, power));
            var puffSize = Mathf.Lerp(hitPuffSize.x, hitPuffSize.y, power) * scale;
            // In front of the burst, whose face would hide the far side that is a puff's outline.
            var puffsAt = TowardView(origin, 0.7f);
            for (var i = 0; i < puffs; i++) {
                var side = Around(Random.value);
                ref var shape = ref Add(Shape.Puff, white, puffsAt + side * 0.3f, puffSize * Random.Range(0.7f, 1.1f),
                    Random.Range(0.35f, 0.5f));
                shape.Velocity = side * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(0.5f, 1.5f);
                shape.Gravity = -1f;
                shape.Drag = 4f;
                shape.Hold = 0.3f;
            }

            if (isHeavy) {
                ref var flash = ref Add(Shape.Sparkle, white, TowardView(origin, 0.5f), heavyFlashSize * scale, 0.16f);
                flash.Angle = Random.Range(-20f, 20f);
                flash.Hold = 0.35f;
            }
        }

        /// <summary>
        /// A car firing its boost: puffs blown out of its back, and speed lines and dust behind it while the boost
        /// lasts.
        /// </summary>
        public void PlayBoost(Car car)
        {
            var direction = MoveDirection(car);
            var side = Vector3.Cross(Vector3.up, direction);
            var rear = car.Position - direction * carRear + Vector3.up * 0.35f;
            for (var i = 0; i < boostPuffs; i++) {
                var spread = Random.Range(-1f, 1f);
                ref var shape = ref Add(Shape.Puff, white, rear + side * (spread * 0.4f),
                    Random.Range(boostPuffSize.x, boostPuffSize.y), Random.Range(0.35f, 0.5f));
                shape.Velocity = -direction * (boostPuffSpeed * Random.Range(0.6f, 1f)) + side * (spread * 2f)
                    + Vector3.up * Random.Range(0.5f, 1.5f);
                shape.Gravity = -2f;
                shape.Drag = 5f;
                shape.Hold = 0.35f;
            }

            for (var i = 0; i < trails.Count; i++) {
                if (trails[i].Car == car) {
                    trails[i] = new Trail { Car = car, LastPosition = car.Position };
                    return;
                }
            }

            trails.Add(new Trail { Car = car, LastPosition = car.Position });
        }

        /// <summary>
        /// A puff of smoke from under a wheel at <paramref name="point"/>, left behind as the car slides on: bigger the
        /// harder the skid (<paramref name="share"/> 0 to 1). <paramref name="carVelocity"/> is the car's, a little of
        /// which the puff keeps, so a trail curls off the way the car went.
        /// </summary>
        public void PlayWheelSmoke(Vector3 point, Vector3 carVelocity, float share)
        {
            share = Mathf.Clamp01(share);
            ref var shape = ref Add(Shape.Puff, white, point, Mathf.Lerp(wheelPuffSize.x, wheelPuffSize.y, share)
                * Random.Range(0.8f, 1.1f), wheelPuffLife * Random.Range(0.8f, 1.15f));
            shape.Velocity = carVelocity * 0.15f + Around(Random.value) * Random.Range(0.3f, 0.8f)
                + Vector3.up * Random.Range(0.6f, 1.2f);
            shape.Gravity = -1.5f;
            shape.Drag = 4f;
            shape.Hold = 0.25f;
        }

        /// <summary>
        /// A car landing on the floor at <paramref name="point"/> after a jump: a ring of puffs thrown out from under it,
        /// bigger the harder it came down (<paramref name="share"/> 0 to 1).
        /// </summary>
        public void PlayLanding(Vector3 point, float share)
        {
            share = Mathf.Clamp01(share);
            var size = Mathf.Lerp(wheelPuffSize.x, wheelPuffSize.y * 1.3f, share);
            for (var i = 0; i < landingPuffs; i++) {
                var outward = Around((i + Random.value * 0.5f) / landingPuffs);
                ref var shape = ref Add(Shape.Puff, white, point + outward * 1.3f, size * Random.Range(0.8f, 1.1f),
                    wheelPuffLife * Random.Range(0.9f, 1.2f));
                shape.Velocity = outward * Random.Range(2f, 3.5f) * (0.6f + share) + Vector3.up * Random.Range(0.3f, 0.8f);
                shape.Gravity = -1f;
                shape.Drag = 5f;
                shape.Hold = 0.25f;
            }
        }

        /// <summary>
        /// A car going over the edge at <paramref name="point"/>: a splash of puffs and stars thrown up, bigger for the
        /// player's car.
        /// </summary>
        public void PlayKnockout(Vector3 point, bool isPlayer)
        {
            var scale = isPlayer ? playerKnockoutScale : 1f;

            ref var poof = ref Add(Shape.Puff, white, point, knockoutPuffSize.y * 1.4f * scale, 0.5f);
            poof.Gravity = -2f;
            poof.Hold = 0.3f;

            for (var i = 0; i < knockoutPuffs; i++) {
                var outward = Around((i + Random.value * 0.5f) / knockoutPuffs);
                var speed = Random.Range(knockoutSpeed.x, knockoutSpeed.y) * scale;
                ref var shape = ref Add(Shape.Puff, white, point + outward * 0.5f,
                    Random.Range(knockoutPuffSize.x, knockoutPuffSize.y) * scale, Random.Range(0.6f, 0.9f));
                shape.Velocity = outward * (speed * 0.45f) + Vector3.up * speed;
                shape.Gravity = gravity * 0.5f;
                shape.Drag = 1.5f;
                shape.Hold = 0.45f;
            }

            for (var i = 0; i < knockoutStars; i++) {
                var outward = Around((i + Random.value * 0.5f) / knockoutStars);
                var speed = Random.Range(knockoutSpeed.x, knockoutSpeed.y) * scale;
                ref var shape = ref Add(Shape.Star, gold, point + Vector3.up * 0.5f,
                    knockoutStarSize * scale * Random.Range(0.8f, 1.1f), Random.Range(0.7f, 1f));
                shape.Velocity = outward * (speed * 0.6f) + Vector3.up * (speed * 1.2f);
                shape.Gravity = gravity * 0.6f;
                shape.Drag = 1f;
                shape.Angle = Random.Range(0f, 360f);
                shape.Spin = Random.Range(-360f, 360f);
                shape.Hold = 0.6f;
            }

        }

        /// <summary>
        /// The player's knock-out, over the splash at <paramref name="point"/>: a ring of gold sparkles bursting out
        /// one after another.
        /// </summary>
        public void PlayKnockoutCredit(Vector3 point)
        {
            for (var i = 0; i < creditSparkles; i++) {
                var outward = Around((float)i / creditSparkles);
                ref var shape = ref Add(Shape.Sparkle, gold, point + Vector3.up * 1.5f + outward * 0.8f,
                    knockoutStarSize * Random.Range(0.7f, 1f), 0.7f);
                shape.Velocity = outward * 5f + Vector3.up * 2f;
                shape.Drag = 3f;
                shape.Angle = Random.Range(-15f, 15f);
                shape.Delay = 0.05f * i;
                shape.Hold = 0.5f;
            }
        }

        /// <summary>
        /// A car taking a pickup at <paramref name="point"/>: gold stars thrown up and a ring on the floor.
        /// </summary>
        public void PlayPickup(Vector3 point)
        {
            var origin = point + Vector3.up * 0.9f;
            for (var i = 0; i < pickupStars; i++) {
                var outward = Around((i + Random.value * 0.4f) / pickupStars);
                ref var shape = ref Add(i % 2 == 0 ? Shape.Star : Shape.Sparkle, gold, origin,
                    pickupStarSize * Random.Range(0.8f, 1.1f), Random.Range(0.45f, 0.65f));
                shape.Velocity = outward * 4f + Vector3.up * Random.Range(3f, 5f);
                shape.Gravity = gravity * 0.6f;
                shape.Drag = 1.5f;
                shape.Angle = Random.Range(0f, 360f);
                shape.Spin = Random.Range(-360f, 360f);
                shape.Hold = 0.5f;
            }

            ref var ring = ref Add(Shape.Ring, white, OnFloor(point), 2.2f, 0.35f);
            ring.StartSize = ring.Size * 0.3f;
        }

        /// <summary>
        /// A pickup fading off the floor unclaimed: a puff of dust where it lay.
        /// </summary>
        public void PlayPickupFade(Vector3 point)
        {
            ref var cloud = ref Add(Shape.Puff, dust, point + Vector3.up * 0.5f, Random.Range(0.9f, 1.3f), 0.45f);
            cloud.Velocity = Vector3.up * 1.2f;
            cloud.Gravity = -0.5f;
            cloud.Drag = 3f;
            cloud.Hold = 0.3f;
        }

        /// <summary>
        /// The shockwave of an item going out from <paramref name="point"/> to <paramref name="radius"/> metres: a big white
        /// ring that reaches the edge of the push, a gold one and a second white one behind it, and a wall of dust blown
        /// out along the first, so the whole reach reads from a phone's camera.
        /// </summary>
        public void PlayShockwave(Vector3 point, float radius)
        {
            for (var i = 0; i < 3; i++) {
                var reach = i == 0 ? 1f : i == 1 ? 0.8f : 0.6f;
                ref var ring = ref Add(Shape.Ring, i == 1 ? gold : white, OnFloor(point), radius * reach / sizeScale,
                    shockwaveLife * (1f - 0.12f * i));
                ring.StartSize = ring.Size * 0.15f;
                ring.Delay = 0.08f * i;
            }

            const int Puffs = 18;
            for (var i = 0; i < Puffs; i++) {
                var outward = Around((i + Random.value * 0.5f) / Puffs);
                ref var cloud = ref Add(Shape.Puff, dust, OnFloor(point) + outward * 0.8f, Random.Range(1.1f, 1.7f),
                    Random.Range(0.45f, 0.65f));
                cloud.Velocity = outward * (radius / shockwaveLife * 0.8f) + Vector3.up * Random.Range(0.4f, 1.2f);
                cloud.Gravity = -0.5f;
                cloud.Drag = 3.5f;
                cloud.Hold = 0.3f;
            }
        }

        /// <summary>
        /// A shield taking a hit at <paramref name="point"/> and going: a white ring and sparkles thrown out.
        /// </summary>
        public void PlayShieldPop(Vector3 point)
        {
            ref var ring = ref Add(Shape.Ring, white, OnFloor(point), 2.4f, 0.3f);
            ring.StartSize = ring.Size * 0.4f;
            const int Sparkles = 6;
            for (var i = 0; i < Sparkles; i++) {
                var outward = Around((i + Random.value * 0.4f) / Sparkles);
                ref var shape = ref Add(Shape.Sparkle, white, point + Vector3.up * 0.9f + outward * 0.8f,
                    pickupStarSize * Random.Range(0.7f, 1f), 0.5f);
                shape.Velocity = outward * 5f + Vector3.up * 1.5f;
                shape.Drag = 3f;
                shape.Angle = Random.Range(-15f, 15f);
                shape.Hold = 0.5f;
            }
        }

        /// <summary>
        /// A car with a turbo leaving flame puffs behind it; called a few times a second while the turbo lasts.
        /// </summary>
        public void PlayTurboFlame(Car car)
        {
            var direction = MoveDirection(car);
            var side = Vector3.Cross(Vector3.up, direction);
            var rear = car.Position - direction * carRear + Vector3.up * 0.4f;
            for (var i = 0; i < 2; i++) {
                ref var flame = ref Add(Shape.Puff, i == 0 ? gold : white, rear + side * Random.Range(-0.3f, 0.3f),
                    Random.Range(flamePuffSize.x, flamePuffSize.y), Random.Range(0.25f, 0.4f));
                flame.Velocity = -direction * Random.Range(3f, 5f) + Vector3.up * Random.Range(0.2f, 1f);
                flame.Gravity = -1f;
                flame.Drag = 4f;
                flame.Hold = 0.3f;
            }
        }

        /// <summary>
        /// A ring of the edge, between <paramref name="inner"/> and <paramref name="outer"/> metres from the middle,
        /// starting to shake: clumps of dust hop up all round it, one after another, for <paramref name="duration"/>
        /// seconds, until it falls.
        /// </summary>
        public void PlayRingWarning(float inner, float outer, float duration)
        {
            var spots = Spots(inner, outer, clumpSpacing);
            for (var i = 0; i < spots; i++) {
                var outward = Around((i + Random.Range(-0.4f, 0.4f)) / spots);
                var position = OnRing(outward, inner, outer);
                var delay = Random.Range(0f, duration * 0.85f);

                ref var clump = ref Add(Shape.Puff, dust, position, Random.Range(clumpSize.x, clumpSize.y),
                    Random.Range(0.45f, 0.7f));
                clump.Velocity = Vector3.up * Random.Range(clumpHop.x, clumpHop.y) + outward * Random.Range(0f, 0.8f);
                clump.Gravity = gravity * 0.5f;
                clump.Delay = delay;
                clump.Hold = 0.5f;

                ref var bit = ref Add(Shape.Chunk, dust, position, clumpSize.x * 0.6f, 0.5f);
                bit.Velocity = Vector3.up * (Random.Range(clumpHop.x, clumpHop.y) * 1.2f)
                    + outward * Random.Range(0.5f, 1.5f);
                bit.Gravity = gravity;
                bit.Axis = Random.onUnitSphere;
                bit.Spin = Random.Range(-500f, 500f);
                bit.Delay = delay + Random.Range(0f, 0.1f);
                bit.Hold = 0.6f;
            }
        }

        /// <summary>
        /// A ring of the edge falling away: a cloud of dust and a tumbling chunk all round it.
        /// </summary>
        public void PlayRingFall(float inner, float outer)
        {
            var spots = Spots(inner, outer, fallSpacing);
            for (var i = 0; i < spots; i++) {
                var outward = Around((i + Random.Range(-0.3f, 0.3f)) / spots);
                var position = OnRing(outward, inner, outer);

                ref var cloud = ref Add(Shape.Puff, dust, position, Random.Range(fallPuffSize.x, fallPuffSize.y),
                    Random.Range(0.6f, 0.9f));
                cloud.Velocity = Vector3.up * Random.Range(1f, 2.5f) + outward * Random.Range(0.5f, 2f);
                cloud.Gravity = -1f;
                cloud.Drag = 2f;
                cloud.Delay = Random.Range(0f, 0.1f);
                cloud.Hold = 0.4f;

                ref var bit = ref Add(Shape.Chunk, dust, position, Random.Range(fallChunkSize.x, fallChunkSize.y), 1f);
                bit.Velocity = outward * Random.Range(1f, 3f) + Vector3.up * Random.Range(2f, 5f);
                bit.Gravity = gravity;
                bit.Axis = Random.onUnitSphere;
                bit.Spin = Random.Range(-400f, 400f);
                bit.Hold = 0.7f;
            }
        }

        /// <summary>
        /// The tiles of a wave about to fall, a floor that is not round: a clump of dust hops up over each spot, one after
        /// another through the warning. Each spot is (x, z, drift x, drift z).
        /// </summary>
        public void PlayTileWarning(Vector4[] spots, float duration)
        {
            foreach (var spot in spots) {
                var outward = new Vector3(spot.z, 0f, spot.w);
                var position = new Vector3(spot.x, 0.1f, spot.y)
                    + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                var delay = Random.Range(0f, duration * 0.85f);

                ref var clump = ref Add(Shape.Puff, dust, position, Random.Range(clumpSize.x, clumpSize.y),
                    Random.Range(0.45f, 0.7f));
                clump.Velocity = Vector3.up * Random.Range(clumpHop.x, clumpHop.y) + outward * Random.Range(0f, 0.8f);
                clump.Gravity = gravity * 0.5f;
                clump.Delay = delay;
                clump.Hold = 0.5f;

                ref var bit = ref Add(Shape.Chunk, dust, position, clumpSize.x * 0.6f, 0.5f);
                bit.Velocity = Vector3.up * (Random.Range(clumpHop.x, clumpHop.y) * 1.2f)
                    + outward * Random.Range(0.5f, 1.5f);
                bit.Gravity = gravity;
                bit.Axis = Random.onUnitSphere;
                bit.Spin = Random.Range(-500f, 500f);
                bit.Delay = delay + Random.Range(0f, 0.1f);
                bit.Hold = 0.6f;
            }
        }

        /// <summary>
        /// The tiles of a wave falling away, a floor that is not round: a cloud of dust and a tumbling chunk over each spot.
        /// </summary>
        public void PlayTileFall(Vector4[] spots)
        {
            foreach (var spot in spots) {
                var outward = new Vector3(spot.z, 0f, spot.w);
                var position = new Vector3(spot.x, 0.1f, spot.y)
                    + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));

                ref var cloud = ref Add(Shape.Puff, dust, position, Random.Range(fallPuffSize.x, fallPuffSize.y),
                    Random.Range(0.6f, 0.9f));
                cloud.Velocity = Vector3.up * Random.Range(1f, 2.5f) + outward * Random.Range(0.5f, 2f);
                cloud.Gravity = -1f;
                cloud.Drag = 2f;
                cloud.Delay = Random.Range(0f, 0.1f);
                cloud.Hold = 0.4f;

                ref var bit = ref Add(Shape.Chunk, dust, position, Random.Range(fallChunkSize.x, fallChunkSize.y), 1f);
                bit.Velocity = outward * Random.Range(1f, 3f) + Vector3.up * Random.Range(2f, 5f);
                bit.Gravity = gravity;
                bit.Axis = Random.onUnitSphere;
                bit.Spin = Random.Range(-400f, 400f);
                bit.Hold = 0.7f;
            }
        }

        /// <summary>
        /// A ring of the floor coming back, between <paramref name="inner"/> and <paramref name="outer"/> metres from the
        /// middle: a gold ring closes on each spot of the empty band over the <paramref name="duration"/> until the ring
        /// lands, and is gone as it does.
        /// </summary>
        public void PlayRingReturn(float inner, float outer, float duration)
        {
            var spots = Spots(inner, outer, returnSpacing);
            for (var i = 0; i < spots; i++) {
                var outward = Around((i + Random.Range(-0.2f, 0.2f)) / spots);
                AddClosingRing(OnFloor(outward * ((inner + outer) * 0.5f)), duration);
            }
        }

        /// <summary>
        /// The tiles of a wave coming back, a floor that is not round: a gold ring closes on each spot over the
        /// <paramref name="duration"/> until the tiles land, and is gone as they do.
        /// </summary>
        public void PlayTileReturn(Vector4[] spots, float duration)
        {
            foreach (var spot in spots) {
                AddClosingRing(OnFloor(new Vector3(spot.x, 0f, spot.y)), duration);
            }
        }

        /// <summary>
        /// A ring of the floor landing back in place: a puff of dust squeezed out round it.
        /// </summary>
        public void PlayRingLand(float inner, float outer)
        {
            var spots = Spots(inner, outer, returnSpacing);
            for (var i = 0; i < spots; i++) {
                var outward = Around((i + Random.Range(-0.3f, 0.3f)) / spots);
                AddLandingPuff(OnRing(outward, inner, outer), outward);
            }
        }

        /// <summary>
        /// The tiles of a wave landing back in place, a floor that is not round: a puff of dust squeezed out over each spot.
        /// </summary>
        public void PlayTileLand(Vector4[] spots)
        {
            foreach (var spot in spots) {
                var outward = new Vector3(spot.z, 0f, spot.w);
                var position = new Vector3(spot.x, 0.1f, spot.y)
                    + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                AddLandingPuff(position, outward);
            }
        }

        private void AddClosingRing(Vector3 position, float duration)
        {
            ref var ring = ref Add(Shape.Ring, gold, position, returnRingSize.y, duration);
            ring.StartSize = returnRingSize.x * sizeScale;
        }

        private void AddLandingPuff(Vector3 position, Vector3 outward)
        {
            ref var cloud = ref Add(Shape.Puff, dust, position, Random.Range(landPuffSize.x, landPuffSize.y),
                Random.Range(0.45f, 0.65f));
            cloud.Velocity = outward * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(0.5f, 1.2f);
            cloud.Gravity = -0.5f;
            cloud.Drag = 3f;
            cloud.Delay = Random.Range(0f, 0.05f);
            cloud.Hold = 0.3f;
        }

        private void LateUpdate()
        {
            var deltaTime = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            LeaveTrails();

            if (view == null) {
                view = Camera.main;
            }

            var facing = view != null ? view.transform.rotation : Quaternion.identity;
            // Removing swaps the last shape in; going backwards, that one has already moved this frame.
            for (var i = count - 1; i >= 0; i--) {
                ref var shape = ref particles[i];
                if (shape.Delay > 0f) {
                    shape.Delay -= deltaTime;
                    continue;
                }

                shape.Age += deltaTime;
                if (shape.Age >= shape.Life) {
                    particles[i] = particles[--count];
                    continue;
                }

                shape.Velocity += Vector3.down * (shape.Gravity * deltaTime);
                shape.Velocity *= Mathf.Exp(-shape.Drag * deltaTime);
                shape.Position += shape.Velocity * deltaTime;
                shape.Angle += shape.Spin * deltaTime;
                Draw(shape, facing);
            }
        }

        /// <summary>
        /// Leaves a pair of speed lines and a puff of dust behind each boosting car every time it has covered the
        /// spacing, so a car held still by a hit leaves no pile of them.
        /// </summary>
        private void LeaveTrails()
        {
            for (var i = trails.Count - 1; i >= 0; i--) {
                var trail = trails[i];
                var car = trail.Car;
                if (car == null || car.IsOut || !car.Movement.IsBoosting) {
                    trails.RemoveAt(i);
                    continue;
                }

                var position = car.Position;
                var travelled = position - trail.LastPosition;
                travelled.y = 0f;
                if (travelled.sqrMagnitude < streakSpacing * streakSpacing) {
                    continue;
                }

                var direction = travelled.normalized;
                var side = Vector3.Cross(Vector3.up, direction);
                var rear = position - direction * carRear;
                for (var edge = -1; edge <= 1; edge += 2) {
                    var offset = side * (edge * streakOffset.x * Random.Range(0.8f, 1.2f))
                        + Vector3.up * (streakOffset.y * Random.Range(0.6f, 1.4f));
                    ref var line = ref Add(Shape.Streak, white, rear + offset,
                        Random.Range(streakLength.x, streakLength.y), streakLife * Random.Range(0.8f, 1.2f));
                    line.Axis = direction;
                    line.Hold = 0.2f;
                }

                ref var cloud = ref Add(Shape.Puff, dust, OnFloor(rear) + side * Random.Range(-0.5f, 0.5f),
                    Random.Range(trailDustSize.x, trailDustSize.y), Random.Range(0.35f, 0.5f));
                cloud.Velocity = -direction + Vector3.up * 0.6f;
                cloud.Gravity = -1.5f;
                cloud.Drag = 3f;
                cloud.Hold = 0.25f;

                trail.LastPosition = position;
                trails[i] = trail;
            }
        }

        private void Draw(in Particle shape, Quaternion facing)
        {
            var progress = shape.Age / shape.Life;
            var line = OutlineWidth(shape.Position);
            Mesh mesh;
            Quaternion rotation;
            Vector3 scale;
            if (shape.Shape == Shape.Ring) {
                // Out fast, then easing; it thins as it goes and is gone at its widest.
                var grow = 1f - Cube(1f - progress);
                var radius = Mathf.Lerp(shape.StartSize, shape.Size, grow);
                var hole = Mathf.Min((int)(progress * rings.Length), rings.Length - 1);
                mesh = rings[hole][Level(line, radius, radius * (1f - RingHoles[hole]))];
                rotation = Quaternion.identity;
                scale = new Vector3(radius, 1f, radius);
            }
            else {
                var size = shape.Size * Pop(progress, shape.Hold);
                if (size <= 0.001f) {
                    return;
                }

                scale = new Vector3(size, size, size);
                var level = Level(line, size, size * 0.5f);
                switch (shape.Shape) {
                    case Shape.Chunk:
                        mesh = chunks[level];
                        rotation = Quaternion.AngleAxis(shape.Angle, shape.Axis);
                        break;
                    case Shape.Star:
                    case Shape.Sparkle:
                    case Shape.Burst:
                        // Facing the camera like the player's arrow, turned about the view.
                        mesh = shape.Shape == Shape.Star ? stars[level] :
                            shape.Shape == Shape.Burst ? bursts[level] : sparkles[level];
                        rotation = facing * Quaternion.Euler(0f, 0f, shape.Angle);
                        break;
                    case Shape.Streak:
                        // Along its way, its face turned to the camera as far as that lets it. Its width, not its
                        // length, is what a thick line would swallow.
                        mesh = streaks[Level(line, size, size * streakWidth)];
                        rotation = Quaternion.LookRotation(shape.Axis, facing * Vector3.back);
                        break;
                    default:
                        mesh = puffs[level];
                        rotation = Quaternion.identity;
                        break;
                }
            }

            var matrix = Matrix4x4.TRS(shape.Position, rotation, scale);
            Graphics.DrawMesh(mesh, matrix, outline, 0, null, EffectShapes.OutlinePart, null, false, false);
            Graphics.DrawMesh(mesh, matrix, shape.Fill, 0, null, EffectShapes.FillPart, null, false, false);
        }

        /// <summary>
        /// <paramref name="point"/> moved <paramref name="distance"/> metres toward the camera along the line it is seen
        /// on, so it shows in the same place but in front of what stands round it.
        /// </summary>
        private Vector3 TowardView(Vector3 point, float distance)
        {
            if (view == null) {
                view = Camera.main;
            }

            if (view == null) {
                return point;
            }

            var toCamera = view.transform.position - point;
            return point + toCamera.normalized * Mathf.Min(distance, toCamera.magnitude * 0.5f);
        }

        /// <summary>
        /// The world width at <paramref name="position"/> that is <see cref="outlinePixels"/> on screen, as the toy
        /// models' outline measures it: pixels at 1080 on the short side.
        /// </summary>
        private float OutlineWidth(Vector3 position)
        {
            if (view == null) {
                return 0.08f;
            }

            var height = Mathf.Max(1f, view.pixelHeight);
            var shortSide = Mathf.Min(view.pixelWidth, view.pixelHeight);
            var camera = view.transform;
            var span = view.orthographic ? view.orthographicSize * 2f :
                2f * Mathf.Max(0.1f, Vector3.Dot(position - camera.position, camera.forward)) *
                Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return outlinePixels * shortSide / 1080f * span / height;
        }

        /// <summary>
        /// The outline level that comes closest to <paramref name="width"/> on a shape scaled by
        /// <paramref name="scale"/>. A shape thinner than about three lines (a puff almost gone) takes a thinner one, so
        /// it never ends as a navy dot.
        /// </summary>
        private static int Level(float width, float scale, float thickness)
        {
            width = Mathf.Min(width, thickness * 0.35f);
            var share = width / Mathf.Max(0.001f, scale);
            var best = 0;
            for (var i = 1; i < OutlineLevels.Length; i++) {
                if (Mathf.Abs(OutlineLevels[i] - share) < Mathf.Abs(OutlineLevels[best] - share)) {
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Takes a free slot for a new shape, set up to sit still at full size for half its life; the caller sets the
        /// rest. When the buffer is full the shape goes to a slot that is never drawn.
        /// </summary>
        private ref Particle Add(Shape shape, Material fill, Vector3 position, float size, float life)
        {
            if (count == particles.Length) {
                overflow = default;
                return ref overflow;
            }

            ref var particle = ref particles[count++];
            particle = new Particle
            {
                Shape = shape,
                Fill = fill,
                Position = position,
                Axis = Vector3.forward,
                Size = size * sizeScale,
                Life = Mathf.Max(0.01f, life),
                Hold = 0.5f,
            };
            return ref particle;
        }

        private Mesh Keep(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }

        /// <summary>
        /// Where a boosting car is going: along its speed, or its nose while it barely moves.
        /// </summary>
        private static Vector3 MoveDirection(Car car)
        {
            var velocity = car.Movement.Velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 1f) {
                return velocity.normalized;
            }

            var forward = car.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        /// <summary>
        /// How many spots round a ring, about <paramref name="spacing"/> apart along its middle, within the buffer.
        /// </summary>
        private static int Spots(float inner, float outer, float spacing)
        {
            var circumference = Mathf.PI * (inner + outer);
            return Mathf.Clamp(Mathf.RoundToInt(circumference / spacing), 6, 48);
        }

        /// <summary>
        /// A point on the floor between two radii, out along <paramref name="outward"/>.
        /// </summary>
        private static Vector3 OnRing(Vector3 outward, float inner, float outer)
        {
            var margin = Mathf.Min(0.3f, (outer - inner) * 0.25f);
            return outward * Random.Range(inner + margin, outer - margin) + Vector3.up * 0.1f;
        }

        /// <summary>
        /// A flat direction a share of the way round a circle.
        /// </summary>
        private static Vector3 Around(float share)
        {
            var angle = share * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        /// <summary>
        /// Just above the floor under a point, so a ring lies on it without flickering.
        /// </summary>
        private static Vector3 OnFloor(Vector3 point)
        {
            return new Vector3(point.x, 0.04f, point.z);
        }

        /// <summary>
        /// The share of its size a shape has: popping up past it and back at first, holding it, then shrinking to
        /// nothing faster and faster.
        /// </summary>
        private static float Pop(float progress, float hold)
        {
            if (progress < PopShare) {
                // Ease out with a little overshoot.
                var x = progress / PopShare - 1f;
                const float Overshoot = 1.70158f;
                return 1f + (Overshoot + 1f) * x * x * x + Overshoot * x * x;
            }

            hold = Mathf.Clamp(hold, PopShare, 0.95f);
            if (progress < hold) {
                return 1f;
            }

            var shrink = (progress - hold) / (1f - hold);
            return 1f - shrink * shrink;
        }

        private static float Cube(float value) => value * value * value;
    }
}
