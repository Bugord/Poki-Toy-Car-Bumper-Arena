using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The floor the cars drive on, centred on the world origin with its top at height 0, built when the scene starts so
    /// its size is tuned in one place (<see cref="GameTuning.ArenaSettings"/>) with the camera's fit and the spawn circle.
    /// What it is made of comes from an <see cref="ArenaLayout"/>: a set of blocks, each in a wave that crumbles together
    /// (a ring of a round floor), and a part that never crumbles. <see cref="ArenaCrumble"/> says when a wave shakes and
    /// falls.
    /// The blocks are only seen. The one collider is the layout's, as wide as the waves still in place, swapped for a
    /// smaller one when a wave falls: a car never catches on a seam between two blocks, no collider moves while a block
    /// shakes, and a car on a falling block has nothing under it and falls with it.
    /// The floor is also the <see cref="IArenaEdge"/> of the arena, answered by the layout's edge field.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class ArenaFloor : MonoBehaviour, IArenaEdge
    {
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

        [Tooltip("A fallen block is put away this far below the floor, in metres; deeper than the ground of every arena, so " +
            "the block drops out of sight into the fog or behind the ground instead of vanishing on the way")]
        [Min(0f)]
        [SerializeField]
        private float hideDepth = 110f;

        private ArenaMap map;
        private GameTuning.ArenaSettings settings;
        private MeshCollider floorCollider;
        private CircleEdge edge;
        private ArenaLayout layout;
        private WaveBlocks[] waves = new WaveBlocks[0];
        private WaveState[] states = new WaveState[0];

        // Waves shaking or falling, which Update has to move.
        private int movingWaves;

        /// <summary>
        /// Waves that can crumble, numbered in the order they fall; none when the edge does not crumble.
        /// </summary>
        public int WaveCount => layout?.WaveCount ?? 0;

        public event Action Changed
        {
            add => edge.Changed += value;
            remove => edge.Changed -= value;
        }

        public float FullExtent => edge.FullExtent;
        public float SafeExtent => edge.SafeExtent;
        public float SolidExtent => edge.SolidExtent;
        public float Span => edge.Span;

        public float DistanceToEdge(Vector3 position, float inset = 0f) => edge.DistanceToEdge(position, inset);
        public float DistanceToSolidEdge(Vector3 position) => edge.DistanceToSolidEdge(position);
        public bool IsSolid(Vector3 position) => edge.IsSolid(position);
        public Vector3 Inward(Vector3 position) => edge.Inward(position);
        public float Proximity(Vector3 position, float near, float far) => edge.Proximity(position, near, far);
        public float Nearness(Vector3 position) => edge.Nearness(position);
        public Vector3 ClampToFloor(Vector3 position) => edge.ClampToFloor(position);
        public float SolidHalfChord(Vector3 origin, Vector3 direction) => edge.SolidHalfChord(origin, direction);

        [Inject]
        private void Construct(ArenaMap map)
        {
            this.map = map;
            this.settings = map.Settings;
            // Made here, not in Awake: the arena's readers hold the floor from the moment the scope is built.
            this.edge ??= new CircleEdge(settings.Radius);
        }

        private void Awake()
        {
            floorCollider = GetComponent<MeshCollider>();
            layout = new CircleLayout(settings, edge, blockLength, minBlocks, seam);

            var template = GetComponent<MeshRenderer>();
            // Before the blocks are built: they copy the template's material.
            if (map.HasTheme) {
                template.sharedMaterial = map.FloorMaterial;
            }

            waves = new WaveBlocks[layout.WaveCount];
            states = new WaveState[layout.WaveCount];
            var blocks = new List<Block>[layout.WaveCount];
            for (var i = 0; i < blocks.Length; i++) {
                blocks[i] = new List<Block>();
            }

            foreach (var tile in layout.Tiles) {
                var block = new GameObject(tile.Name) { layer = gameObject.layer };
                block.transform.SetParent(transform, false);
                block.transform.SetLocalPositionAndRotation(tile.Position, tile.Rotation);
                block.AddComponent<MeshFilter>().sharedMesh = tile.Mesh;
                var look = block.AddComponent<MeshRenderer>();
                look.sharedMaterial = template.sharedMaterial;
                look.shadowCastingMode = template.shadowCastingMode;
                look.receiveShadows = template.receiveShadows;
                blocks[tile.Wave].Add(new Block(block.transform, new Vector2(UnityEngine.Random.value * 100f,
                    UnityEngine.Random.value * 100f)));
            }

            for (var i = 0; i < waves.Length; i++) {
                waves[i] = new WaveBlocks(blocks[i].ToArray());
            }

            GetComponent<MeshFilter>().sharedMesh = layout.CoreMesh;
            floorCollider.sharedMesh = layout.ColliderMesh;
        }

        private void OnDestroy()
        {
            if (layout == null) {
                return;
            }

            foreach (var mesh in layout.Meshes) {
                Destroy(mesh);
            }
        }

        private void Update()
        {
            if (movingWaves == 0) {
                return;
            }

            var time = Time.time;
            var deltaTime = Time.deltaTime;
            for (var i = 0; i < waves.Length; i++) {
                var wave = waves[i];
                if (states[i] == WaveState.Warning) {
                    Shake(wave, time);
                }
                else if (states[i] == WaveState.Falling && Fall(wave, deltaTime)) {
                    states[i] = WaveState.Gone;
                    movingWaves--;
                }
            }
        }

        public CrumbleWave Wave(int wave)
        {
            return layout.Wave(wave);
        }

        /// <summary>
        /// Whether a point is over the blocks of a wave, which a car there falls with.
        /// </summary>
        public bool Contains(int wave, Vector3 position)
        {
            return layout.Contains(wave, position);
        }

        /// <summary>
        /// Starts a wave shaking for <see cref="GameTuning.ArenaSettings.CrumbleWarning"/>, harder as it goes. It no
        /// longer counts as safe floor, but still holds the cars up until <see cref="Drop"/>.
        /// </summary>
        public void Warn(int wave)
        {
            if (states[wave] != WaveState.Standing) {
                return;
            }

            states[wave] = WaveState.Warning;
            waves[wave].WarnedAt = Time.time;
            movingWaves++;
            Refresh();
        }

        /// <summary>
        /// Lets a wave fall: its blocks drop and tumble away, and the collider shrinks to the floor inside it.
        /// </summary>
        public void Drop(int wave)
        {
            var target = waves[wave];
            if (states[wave] == WaveState.Falling || states[wave] == WaveState.Gone) {
                return;
            }

            if (states[wave] == WaveState.Standing) {
                movingWaves++;
            }

            states[wave] = WaveState.Falling;
            foreach (var block in target.Blocks) {
                var outward = block.RestRotation * Vector3.right;
                block.Velocity = outward * UnityEngine.Random.Range(fallDrift.x, fallDrift.y)
                    + Vector3.down * UnityEngine.Random.Range(0f, fallKick);
                // Spinning backwards about the wave's direction tips the outer edge down, as a block breaking off would.
                block.SpinAxis = (block.RestRotation * Vector3.back + UnityEngine.Random.insideUnitSphere * 0.5f)
                    .normalized;
                block.SpinSpeed = UnityEngine.Random.Range(fallSpin.x, fallSpin.y);
            }

            Refresh();
        }

        /// <summary>
        /// The layout works out the safe and solid floor from the waves' states and moves the collider.
        /// </summary>
        private void Refresh()
        {
            if (layout.Refresh(states)) {
                floorCollider.sharedMesh = layout.ColliderMesh;
            }
        }

        /// <summary>
        /// Rattles each block of a warning wave round its place with its own noise, from a light tremor to the full
        /// shake as the fall nears.
        /// </summary>
        private void Shake(WaveBlocks wave, float time)
        {
            var warning = settings.CrumbleWarning;
            var progress = warning > 0f ? Mathf.Clamp01((time - wave.WarnedAt) / warning) : 1f;
            var strength = Mathf.Lerp(shakeStart, 1f, progress * progress);
            var distance = shakeDistance * strength;
            var angle = shakeAngle * strength;
            var phase = time * shakeFrequency;
            foreach (var block in wave.Blocks) {
                var x = Noise(block.Seed.x, phase);
                var y = Noise(block.Seed.x + block.Seed.y, phase);
                var z = Noise(block.Seed.y, phase);
                block.Transform.SetLocalPositionAndRotation(
                    block.RestPosition + new Vector3(x, y * 0.5f, z) * distance,
                    block.RestRotation * Quaternion.Euler(z * angle, 0f, -x * angle));
            }
        }

        /// <summary>
        /// Moves the blocks of a falling wave under gravity, tumbling, and puts each away once it is far below.
        /// </summary>
        /// <returns>True when every block of the wave is put away.</returns>
        private bool Fall(WaveBlocks wave, float deltaTime)
        {
            var gravity = Physics.gravity;
            var isGone = true;
            foreach (var block in wave.Blocks) {
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

        private static float Noise(float seed, float phase)
        {
            return Mathf.PerlinNoise(seed, phase) * 2f - 1f;
        }

        private sealed class WaveBlocks
        {
            public readonly Block[] Blocks;
            public float WarnedAt;

            public WaveBlocks(Block[] blocks)
            {
                this.Blocks = blocks;
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
    }
}
