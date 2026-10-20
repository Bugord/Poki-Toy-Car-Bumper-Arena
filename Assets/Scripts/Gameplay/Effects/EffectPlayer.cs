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

        [Tooltip("Outline of a puff or a chunk, as a share of its size")]
        [Range(0f, 0.5f)]
        [SerializeField]
        private float solidOutline = 0.34f;

        [Tooltip("Outline of a star or a speed line, as a share of its size")]
        [Range(0f, 0.2f)]
        [SerializeField]
        private float flatOutline = 0.11f;

        [Tooltip("Outline of an impact ring, as a share of its radius")]
        [Range(0f, 0.2f)]
        [SerializeField]
        private float ringOutline = 0.08f;

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

        [Tooltip("Radius the impact ring grows to in the weakest and in the strongest hit, in metres")]
        [SerializeField]
        private Vector2 hitRingRadius = new(1.1f, 2.6f);

        [Tooltip("Seconds the impact ring lasts")]
        [Min(0.05f)]
        [SerializeField]
        private float hitRingLife = 0.3f;

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

        private enum Shape
        {
            Puff,
            Chunk,
            Star,
            Sparkle,
            Ring,
            Streak,
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

        private Mesh puff;
        private Mesh chunk;
        private Mesh star;
        private Mesh sparkle;
        private Mesh streak;
        private Mesh[] rings;
        private Transform viewpoint;

        private void Awake()
        {
            // Drawing with a missing material logs an error every frame; without effects the game still plays.
            if (outline == null || white == null || gold == null || dust == null) {
                Debug.LogError($"{nameof(EffectPlayer)} in {gameObject.scene.name} is missing a material; no effects.");
                enabled = false;
                return;
            }

            puff = Keep(EffectShapes.Puff(12, 8, solidOutline));
            chunk = Keep(EffectShapes.Chunk(solidOutline));
            star = Keep(EffectShapes.Star(5, 0.5f, flatOutline));
            sparkle = Keep(EffectShapes.Star(4, 0.3f, flatOutline));
            streak = Keep(EffectShapes.Streak(streakWidth, flatOutline));
            rings = new Mesh[RingHoles.Length];
            for (var i = 0; i < rings.Length; i++) {
                rings[i] = Keep(EffectShapes.Ring(RingHoles[i], ringOutline, 40));
            }
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) {
                Destroy(mesh);
            }
        }

        /// <summary>
        /// Two cars hitting at <paramref name="point"/>: a burst of stars, an impact ring on the floor and a few puffs,
        /// all bigger the stronger the hit (<paramref name="power"/> 0 to 1), and a white flash for a heavy one.
        /// <paramref name="normal"/> is the way the car that was hit is pushed, on the floor.
        /// </summary>
        public void PlayHit(Vector3 point, Vector3 normal, float power, bool isHeavy, bool isBoosted)
        {
            power = Mathf.Clamp01(power);
            var scale = isBoosted ? boostedScale : 1f;
            var origin = point + Vector3.up * 0.3f;

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

            var ringRadius = Mathf.Lerp(hitRingRadius.x, hitRingRadius.y, power) * scale;
            ref var ring = ref Add(Shape.Ring, white, OnFloor(point), ringRadius, hitRingLife);
            ring.StartSize = ring.Size * 0.25f;

            var puffs = Mathf.RoundToInt(Mathf.Lerp(hitPuffs.x, hitPuffs.y, power));
            var puffSize = Mathf.Lerp(hitPuffSize.x, hitPuffSize.y, power) * scale;
            for (var i = 0; i < puffs; i++) {
                var side = Around(Random.value);
                ref var shape = ref Add(Shape.Puff, white, origin + side * 0.3f, puffSize * Random.Range(0.7f, 1.1f),
                    Random.Range(0.35f, 0.5f));
                shape.Velocity = side * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(0.5f, 1.5f);
                shape.Gravity = -1f;
                shape.Drag = 4f;
                shape.Hold = 0.3f;
            }

            if (isHeavy) {
                ref var flash = ref Add(Shape.Sparkle, white, origin + Vector3.up * 0.2f, heavyFlashSize * scale, 0.16f);
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

        private void LateUpdate()
        {
            var deltaTime = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            LeaveTrails();

            if (viewpoint == null) {
                var main = Camera.main;
                viewpoint = main != null ? main.transform : null;
            }

            var view = viewpoint != null ? viewpoint.rotation : Quaternion.identity;
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
                Draw(shape, view);
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

        private void Draw(in Particle shape, Quaternion view)
        {
            var progress = shape.Age / shape.Life;
            Mesh mesh;
            Quaternion rotation;
            Vector3 scale;
            if (shape.Shape == Shape.Ring) {
                // Out fast, then easing; it thins as it goes and is gone at its widest.
                var grow = 1f - Cube(1f - progress);
                var radius = Mathf.Lerp(shape.StartSize, shape.Size, grow);
                mesh = rings[Mathf.Min((int)(progress * rings.Length), rings.Length - 1)];
                rotation = Quaternion.identity;
                scale = new Vector3(radius, 1f, radius);
            }
            else {
                var size = shape.Size * Pop(progress, shape.Hold);
                if (size <= 0.001f) {
                    return;
                }

                scale = new Vector3(size, size, size);
                switch (shape.Shape) {
                    case Shape.Chunk:
                        mesh = chunk;
                        rotation = Quaternion.AngleAxis(shape.Angle, shape.Axis);
                        break;
                    case Shape.Star:
                    case Shape.Sparkle:
                        // Facing the camera like the player's arrow, turned about the view.
                        mesh = shape.Shape == Shape.Star ? star : sparkle;
                        rotation = view * Quaternion.Euler(0f, 0f, shape.Angle);
                        break;
                    case Shape.Streak:
                        // Along its way, its face turned to the camera as far as that lets it.
                        mesh = streak;
                        rotation = Quaternion.LookRotation(shape.Axis, view * Vector3.back);
                        break;
                    default:
                        mesh = puff;
                        rotation = Quaternion.identity;
                        break;
                }
            }

            var matrix = Matrix4x4.TRS(shape.Position, rotation, scale);
            Graphics.DrawMesh(mesh, matrix, outline, 0, null, EffectShapes.OutlinePart, null, false, false);
            Graphics.DrawMesh(mesh, matrix, shape.Fill, 0, null, EffectShapes.FillPart, null, false, false);
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
