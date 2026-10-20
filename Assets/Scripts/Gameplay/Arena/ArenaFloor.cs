using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The round floor the cars drive on, centred on the world origin with its top at height 0, built when the scene
    /// starts so its size is tuned in one place (<see cref="GameTuning.ArenaSettings"/>) with the camera's fit and the
    /// spawn circle. It is a solid disc of <see cref="GameTuning.ArenaSettings.MinRadius"/> inside rings about
    /// <see cref="GameTuning.ArenaSettings.CrumbleStep"/> wide out to the full radius, each ring cut into blocks, so the
    /// edge can crumble away a ring at a time from the outside in; <see cref="ArenaCrumble"/> says when.
    /// The blocks are only seen. The one collider is a plain disc as wide as the rings still in place, swapped for a
    /// smaller one when a ring falls: a car never catches on a seam between two blocks, no collider moves while a block
    /// shakes, and a car on a falling ring has nothing under it and falls with it.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class ArenaFloor : MonoBehaviour
    {
        // Sides of the whole circle; enough that the rim reads as a circle from the camera's distance.
        private const int Segments = 96;

        // Depth of the floor below the driving surface, in metres; seen on the rim and on falling blocks.
        private const float Thickness = 0.5f;

        [Header("Blocks")]
        [Tooltip("Length a block aims for along its ring, in metres; each ring is cut into equal blocks about this long")]
        [Min(0.5f)]
        [SerializeField]
        private float blockLength = 7f;

        [Tooltip("Fewest blocks a ring is cut into")]
        [Min(2)]
        [SerializeField]
        private int minBlocks = 6;

        [Tooltip("Width of the groove drawn between two blocks, in metres, so the rings read as blocks that can come " +
            "apart; the collider has no groove")]
        [Min(0f)]
        [SerializeField]
        private float seam = 0.08f;

        [Header("Shaking")]
        [Tooltip("Farthest a block of a crumbling ring rattles from its place, in metres, just before it falls")]
        [Min(0f)]
        [SerializeField]
        private float shakeDistance = 0.06f;

        [Tooltip("Farthest a block of a crumbling ring tilts, in degrees, just before it falls")]
        [Min(0f)]
        [SerializeField]
        private float shakeAngle = 2.5f;

        [Tooltip("How fast a block rattles, in jolts per second")]
        [Min(0f)]
        [SerializeField]
        private float shakeFrequency = 14f;

        [Tooltip("Share of the full shake a ring starts its warning with; it grows to the full shake as the fall nears")]
        [Range(0f, 1f)]
        [SerializeField]
        private float shakeStart = 0.25f;

        [Header("Falling")]
        [Tooltip("Least and most a falling block drifts outwards, in metres per second")]
        [SerializeField]
        private Vector2 fallDrift = new(0.5f, 2f);

        [Tooltip("Most a falling block is pushed down as it breaks off, in metres per second, so the blocks of a ring " +
            "come apart instead of dropping as one")]
        [Min(0f)]
        [SerializeField]
        private float fallKick = 1.5f;

        [Tooltip("Least and most a falling block tumbles, in degrees per second; it tips over its outer edge")]
        [SerializeField]
        private Vector2 fallSpin = new(60f, 200f);

        [Tooltip("A fallen block is put away this far below the floor, in metres")]
        [Min(0f)]
        [SerializeField]
        private float hideDepth = 25f;

        private readonly List<Mesh> meshes = new();

        private GameTuning.ArenaSettings settings;
        private MeshCollider floorCollider;
        private Ring[] rings = new Ring[0];

        // [i] is a disc out to ring i's outer edge, the last one the solid disc in the middle; the collider is the one
        // for the outermost ring still in place.
        private Mesh[] colliderMeshes;
        private int colliderRing;
        private float innerRadius;

        // Rings shaking or falling, which Update has to move.
        private int movingRings;

        private enum RingState
        {
            Standing,
            Warning,
            Falling,
            Gone,
        }

        /// <summary>
        /// How far from the middle the floor is safe now, in metres: out to the outer edge of the rings still standing,
        /// not counting a ring that has started to crumble, whose blocks are still there but about to fall.
        /// </summary>
        public float Radius { get; private set; }

        /// <summary>
        /// How far from the middle the floor still is, in metres: out to the outer edge of the rings that have not
        /// fallen, a shaking one too. The camera frames this, so it closes in only once a ring has gone.
        /// </summary>
        public float SolidRadius { get; private set; }

        /// <summary>
        /// Rings that can crumble, numbered from the outside in; none when the edge does not crumble.
        /// </summary>
        public int RingCount => rings.Length;

        [Inject]
        private void Construct(GameTuning tuning)
        {
            this.settings = tuning.Arena;
            this.Radius = settings.Radius;
            this.SolidRadius = settings.Radius;
        }

        private void Awake()
        {
            floorCollider = GetComponent<MeshCollider>();

            // The band between the solid middle and the full radius divides into whole rings close to the step, so no
            // ring is left a sliver. No step, or no band, leaves the whole floor solid.
            var band = settings.Radius - settings.MinRadius;
            var ringCount = band > 0f && settings.CrumbleStep > 0f
                ? Mathf.Max(1, Mathf.RoundToInt(band / settings.CrumbleStep))
                : 0;
            innerRadius = ringCount > 0 ? settings.MinRadius : settings.Radius;
            var width = ringCount > 0 ? band / ringCount : 0f;
            var halfSeam = ringCount > 0 ? seam * 0.5f : 0f;

            var template = GetComponent<MeshRenderer>();
            rings = new Ring[ringCount];
            colliderMeshes = new Mesh[ringCount + 1];
            for (var i = 0; i < ringCount; i++) {
                var outer = settings.Radius - i * width;
                rings[i] = BuildRing(i, outer - width, outer, halfSeam, template);
                colliderMeshes[i] = Keep(BuildDisc(outer, "Arena Floor Collider"));
            }

            colliderMeshes[ringCount] = Keep(BuildDisc(innerRadius, "Arena Floor Collider"));
            GetComponent<MeshFilter>().sharedMesh = Keep(BuildDisc(innerRadius - halfSeam, "Arena Floor"));
            colliderRing = 0;
            floorCollider.sharedMesh = colliderMeshes[0];
            UpdateRadius();
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) {
                Destroy(mesh);
            }
        }

        private void Update()
        {
            if (movingRings == 0) {
                return;
            }

            var time = Time.time;
            var deltaTime = Time.deltaTime;
            foreach (var ring in rings) {
                if (ring.State == RingState.Warning) {
                    Shake(ring, time);
                }
                else if (ring.State == RingState.Falling && Fall(ring, deltaTime)) {
                    ring.State = RingState.Gone;
                    movingRings--;
                }
            }
        }

        public float RingInnerRadius(int ring)
        {
            return rings[ring].Inner;
        }

        public float RingOuterRadius(int ring)
        {
            return rings[ring].Outer;
        }

        /// <summary>
        /// Starts a ring shaking for <see cref="GameTuning.ArenaSettings.CrumbleWarning"/>, harder as it goes. It no
        /// longer counts towards <see cref="Radius"/>, but still holds the cars up until <see cref="Drop"/>.
        /// </summary>
        public void Warn(int ring)
        {
            var target = rings[ring];
            if (target.State != RingState.Standing) {
                return;
            }

            target.State = RingState.Warning;
            target.WarnedAt = Time.time;
            movingRings++;
            UpdateRadius();
        }

        /// <summary>
        /// Lets a ring fall: its blocks drop and tumble away, and the collider shrinks to the rings inside it.
        /// </summary>
        public void Drop(int ring)
        {
            var target = rings[ring];
            if (target.State == RingState.Falling || target.State == RingState.Gone) {
                return;
            }

            if (target.State == RingState.Standing) {
                movingRings++;
            }

            target.State = RingState.Falling;
            foreach (var block in target.Blocks) {
                var outward = block.RestRotation * Vector3.right;
                block.Velocity = outward * Random.Range(fallDrift.x, fallDrift.y)
                    + Vector3.down * Random.Range(0f, fallKick);
                // Spinning backwards about the ring's direction tips the outer edge down, as a block breaking off would.
                block.SpinAxis = (block.RestRotation * Vector3.back + Random.insideUnitSphere * 0.5f).normalized;
                block.SpinSpeed = Random.Range(fallSpin.x, fallSpin.y);
            }

            UpdateRadius();
            UpdateCollider();
        }

        /// <summary>
        /// Rattles each block of a warning ring round its place with its own noise, from a light tremor to the full
        /// shake as the fall nears.
        /// </summary>
        private void Shake(Ring ring, float time)
        {
            var warning = settings.CrumbleWarning;
            var progress = warning > 0f ? Mathf.Clamp01((time - ring.WarnedAt) / warning) : 1f;
            var strength = Mathf.Lerp(shakeStart, 1f, progress * progress);
            var distance = shakeDistance * strength;
            var angle = shakeAngle * strength;
            var phase = time * shakeFrequency;
            foreach (var block in ring.Blocks) {
                var x = Noise(block.Seed.x, phase);
                var y = Noise(block.Seed.x + block.Seed.y, phase);
                var z = Noise(block.Seed.y, phase);
                block.Transform.SetLocalPositionAndRotation(
                    block.RestPosition + new Vector3(x, y * 0.5f, z) * distance,
                    block.RestRotation * Quaternion.Euler(z * angle, 0f, -x * angle));
            }
        }

        /// <summary>
        /// Moves the blocks of a falling ring under gravity, tumbling, and puts each away once it is far below.
        /// </summary>
        /// <returns>True when every block of the ring is put away.</returns>
        private bool Fall(Ring ring, float deltaTime)
        {
            var gravity = Physics.gravity;
            var isGone = true;
            foreach (var block in ring.Blocks) {
                var body = block.Transform;
                if (!body.gameObject.activeSelf) {
                    continue;
                }

                block.Velocity += gravity * deltaTime;
                body.SetLocalPositionAndRotation(body.localPosition + block.Velocity * deltaTime,
                    Quaternion.AngleAxis(block.SpinSpeed * deltaTime, block.SpinAxis) * body.localRotation);
                if (body.localPosition.y < -hideDepth) {
                    body.gameObject.SetActive(false);
                }
                else {
                    isGone = false;
                }
            }

            return isGone;
        }

        /// <summary>
        /// The safe radius reaches out over the unbroken run of standing rings from the middle.
        /// </summary>
        private void UpdateRadius()
        {
            var radius = innerRadius;
            for (var i = rings.Length - 1; i >= 0 && rings[i].State == RingState.Standing; i--) {
                radius = rings[i].Outer;
            }

            Radius = radius;
        }

        /// <summary>
        /// The collider reaches out over the unbroken run of rings from the middle that have not fallen, shaking or not.
        /// </summary>
        private void UpdateCollider()
        {
            var ring = rings.Length;
            while (ring > 0 && rings[ring - 1].State is RingState.Standing or RingState.Warning) {
                ring--;
            }

            if (ring != colliderRing) {
                colliderRing = ring;
                floorCollider.sharedMesh = colliderMeshes[ring];
                SolidRadius = ring < rings.Length ? rings[ring].Outer : innerRadius;
            }
        }

        /// <summary>
        /// Cuts a ring into equal blocks about <see cref="blockLength"/> long, each its own object under the floor so it
        /// can shake and fall on its own. The blocks of a ring are alike, so they share one mesh, turned into place.
        /// </summary>
        private Ring BuildRing(int index, float inner, float outer, float halfSeam, MeshRenderer template)
        {
            var middle = (inner + outer) * 0.5f;
            var count = Mathf.Max(minBlocks, Mathf.RoundToInt(2f * Mathf.PI * middle / blockLength));
            var halfAngle = Mathf.PI / count;
            // Blocks turn about their own middle as they tumble, so the mesh is built round it.
            var pivot = new Vector3(middle, -Thickness * 0.5f, 0f);
            var mesh = Keep(BuildBlock(inner + halfSeam, outer - halfSeam, halfAngle, halfSeam, pivot));

            var ring = new Ring(inner, outer, count);
            for (var j = 0; j < count; j++) {
                // A turn about the vertical by minus the angle takes the block's +X, its outward direction, to that angle.
                var rotation = Quaternion.Euler(0f, -j * 2f * halfAngle * Mathf.Rad2Deg, 0f);
                var block = new GameObject($"Ring {index} Block {j}") { layer = gameObject.layer };
                block.transform.SetParent(transform, false);
                block.transform.SetLocalPositionAndRotation(rotation * pivot, rotation);
                block.AddComponent<MeshFilter>().sharedMesh = mesh;
                var look = block.AddComponent<MeshRenderer>();
                look.sharedMaterial = template.sharedMaterial;
                look.shadowCastingMode = template.shadowCastingMode;
                look.receiveShadows = template.receiveShadows;
                ring.Blocks[j] = new Block(block.transform, new Vector2(Random.value * 100f, Random.value * 100f));
            }

            return ring;
        }

        private Mesh Keep(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }

        private static float Noise(float seed, float phase)
        {
            return Mathf.PerlinNoise(seed, phase) * 2f - 1f;
        }

        private static Vector3 Point(float radius, float angle)
        {
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private static Vector3 Outward(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        /// <summary>
        /// The top as a fan round the centre and the rim as a band facing out, each with its own vertices so the edge
        /// between them stays sharp.
        /// </summary>
        private static Mesh BuildDisc(float radius, string name)
        {
            var builder = new MeshBuilder();
            var down = Vector3.down * Thickness;
            for (var i = 0; i < Segments; i++) {
                var from = i * Mathf.PI * 2f / Segments;
                var to = (i + 1) * Mathf.PI * 2f / Segments;
                var a = Point(radius, from);
                var b = Point(radius, to);
                builder.AddTriangle(Vector3.zero, a, b, Vector3.up);
                builder.AddQuad(a, b, b + down, a + down, Outward(from), Outward(to), Outward(to), Outward(from));
            }

            return builder.ToMesh(name);
        }

        /// <summary>
        /// One block of a ring, centred on +X and spanning twice the half angle, round the given pivot: a top, a bottom,
        /// the outer and inner rims and the two cut ends, all solid so it reads as a block while it tumbles. The cut ends
        /// are drawn the inset in from the true cut, parallel to it, so two blocks side by side leave an even seam.
        /// </summary>
        private static Mesh BuildBlock(float near, float far, float halfAngle, float inset, Vector3 pivot)
        {
            // The same inset is a wider angle on the inner arc than on the outer one.
            var nearEnd = halfAngle - Mathf.Asin(Mathf.Clamp01(inset / near));
            var farEnd = halfAngle - Mathf.Asin(Mathf.Clamp01(inset / far));
            var steps = Mathf.Max(1, Mathf.CeilToInt(Segments * halfAngle / Mathf.PI));
            var down = Vector3.down * Thickness;

            var builder = new MeshBuilder();
            for (var i = 0; i < steps; i++) {
                var from = (float)i / steps;
                var to = (float)(i + 1) / steps;
                var nearFrom = Mathf.Lerp(-nearEnd, nearEnd, from);
                var nearTo = Mathf.Lerp(-nearEnd, nearEnd, to);
                var farFrom = Mathf.Lerp(-farEnd, farEnd, from);
                var farTo = Mathf.Lerp(-farEnd, farEnd, to);
                var n0 = Point(near, nearFrom) - pivot;
                var n1 = Point(near, nearTo) - pivot;
                var f0 = Point(far, farFrom) - pivot;
                var f1 = Point(far, farTo) - pivot;

                builder.AddQuad(n0, n1, f1, f0, Vector3.up, Vector3.up, Vector3.up, Vector3.up);
                builder.AddQuad(n0 + down, n1 + down, f1 + down, f0 + down,
                    Vector3.down, Vector3.down, Vector3.down, Vector3.down);
                builder.AddQuad(f0, f1, f1 + down, f0 + down,
                    Outward(farFrom), Outward(farTo), Outward(farTo), Outward(farFrom));
                builder.AddQuad(n0, n1, n1 + down, n0 + down,
                    -Outward(nearFrom), -Outward(nearTo), -Outward(nearTo), -Outward(nearFrom));
            }

            AddEnd(builder, near, far, -nearEnd, -farEnd, -Tangent(-halfAngle), pivot, down);
            AddEnd(builder, near, far, nearEnd, farEnd, Tangent(halfAngle), pivot, down);
            return builder.ToMesh("Arena Block");
        }

        private static void AddEnd(MeshBuilder builder, float near, float far, float nearAngle, float farAngle,
            Vector3 normal, Vector3 origin, Vector3 down)
        {
            var a = Point(near, nearAngle) - origin;
            var b = Point(far, farAngle) - origin;
            builder.AddQuad(a, b, b + down, a + down, normal, normal, normal, normal);
        }

        // The direction along the circle at an angle, the way the angle grows.
        private static Vector3 Tangent(float angle)
        {
            return new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        private sealed class Ring
        {
            public readonly float Inner;
            public readonly float Outer;
            public readonly Block[] Blocks;
            public RingState State;
            public float WarnedAt;

            public Ring(float inner, float outer, int blocks)
            {
                this.Inner = inner;
                this.Outer = outer;
                this.Blocks = new Block[blocks];
            }
        }

        private sealed class Block
        {
            public readonly Transform Transform;
            public readonly Vector3 RestPosition;
            public readonly Quaternion RestRotation;

            // Where in the noise this block's shake reads from, so no two blocks rattle alike.
            public readonly Vector2 Seed;

            public Vector3 Velocity;
            public Vector3 SpinAxis;
            public float SpinSpeed;

            public Block(Transform transform, Vector2 seed)
            {
                this.Transform = transform;
                this.RestPosition = transform.localPosition;
                this.RestRotation = transform.localRotation;
                this.Seed = seed;
            }
        }

        /// <summary>
        /// Gathers faces with their own vertices, so every edge between two faces stays sharp, and winds each face so
        /// it faces its normal; the shapes above then only have to list the corners in order round the face.
        /// </summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<Vector3> normals = new();
            private readonly List<int> triangles = new();

            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                var start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                // Unity draws a triangle whose corners go clockwise seen from the front, where this cross product points.
                var isFacing = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f;
                triangles.Add(start);
                triangles.Add(isFacing ? start + 1 : start + 2);
                triangles.Add(isFacing ? start + 2 : start + 1);
            }

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normalA, Vector3 normalB,
                Vector3 normalC, Vector3 normalD)
            {
                var start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                normals.Add(normalA);
                normals.Add(normalB);
                normals.Add(normalC);
                normals.Add(normalD);
                var facing = normalA + normalB + normalC + normalD;
                var isFacing = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) >= 0f;
                if (isFacing) {
                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + 2);
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 3);
                }
                else {
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 1);
                    triangles.Add(start);
                    triangles.Add(start + 3);
                    triangles.Add(start + 2);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                return mesh;
            }
        }
    }
}
