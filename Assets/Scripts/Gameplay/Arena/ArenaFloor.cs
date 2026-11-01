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
    /// The floor is also the <see cref="IArenaEdge"/> of the arena: the exact formulas of <see cref="CircleEdge"/> for a
    /// round floor, a <see cref="GridEdge"/> of signed distances for any other shape, which its layout keeps up to date.
    /// When the floor goes in cycles a fallen wave comes back (<see cref="Raise"/>, <see cref="Restore"/>): its blocks
    /// appear hanging over their places, then drop into them, and only as they land do the collider and the edge take
    /// them back, so nothing on the floor collides with a block in the air.
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

        [Header("Coming back")]
        [Tooltip("Height a returning block hangs at over its place before it drops, in metres; it eases down to it from " +
            "half again as high as it appears")]
        [Min(0f)]
        [SerializeField]
        private float returnHeight = 3f;

        [Tooltip("Seconds a returning block takes to drop from where it hangs into its place; it falls faster and faster, " +
            "as a dropped block would")]
        [Min(0.05f)]
        [SerializeField]
        private float returnDrop = 0.35f;

        [Tooltip("Farthest a hanging block bobs from its height, in metres (x), and tilts, in degrees (y), so it reads as " +
            "held up and about to come down")]
        [SerializeField]
        private Vector2 returnBob = new(0.15f, 3f);

        [Tooltip("Seconds a returning block takes to sink from where it appears to the height it hangs at")]
        [Min(0.01f)]
        [SerializeField]
        private float returnSettle = 0.4f;

        private ArenaMap map;
        private GameTuning.ArenaSettings settings;
        private MeshCollider floorCollider;
        private IArenaEdge edge;
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

        public void SolidChord(Vector3 origin, Vector3 direction, out float behind, out float ahead) =>
            edge.SolidChord(origin, direction, out behind, out ahead);

        public IReadOnlyList<Vector3> Outline(bool solid) => edge.Outline(solid);

        public bool TryGetSpawn(int index, int count, out Vector3 position) => edge.TryGetSpawn(index, count, out position);

        [Inject]
        private void Construct(ArenaMap map)
        {
            this.map = map;
            this.settings = map.Settings;
            // Made here, not in Awake: the arena's readers hold the floor from the moment the scope is built.
            this.edge ??= map.Shape == null ? new CircleEdge(settings.Radius) : new GridEdge(map.Shape);
        }

        private void Awake()
        {
            floorCollider = GetComponent<MeshCollider>();
            layout = map.Shape == null
                ? new CircleLayout(settings, (CircleEdge)edge, blockLength, minBlocks, seam,
                    FloorUv.ForCircle(settings.Radius))
                : new TiledLayout(map.Shape, (GridEdge)edge, seam, FloorUv.For(map.Shape));

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
                else if (states[i] == WaveState.Returning) {
                    Hover(wave, time);
                }
            }
        }

        public CrumbleWave Wave(int wave)
        {
            return layout.Wave(wave);
        }

        /// <summary>Where a wave is in its crumbling and coming back.</summary>
        public WaveState StateOf(int wave)
        {
            return states[wave];
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
            Warn(wave, settings.CrumbleWarning);
        }

        /// <summary>
        /// Starts a wave shaking for <paramref name="duration"/> seconds, harder as it goes: the floor in cycles may shake
        /// its waves for less than the arena's warning, when its shrink is short.
        /// </summary>
        public void Warn(int wave, float duration)
        {
            if (states[wave] != WaveState.Standing) {
                return;
            }

            states[wave] = WaveState.Warning;
            waves[wave].WarnedAt = Time.time;
            waves[wave].WarningTime = duration;
            movingWaves++;
            Refresh();
        }

        /// <summary>
        /// Lets a wave fall: its blocks drop and tumble away, and the collider shrinks to the floor inside it.
        /// </summary>
        public void Drop(int wave)
        {
            var target = waves[wave];
            if (states[wave] is WaveState.Falling or WaveState.Gone or WaveState.Returning) {
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
        /// Brings a fallen wave back over its place, to land in <paramref name="landIn"/> seconds: its blocks appear
        /// hanging over their places, bob there and drop into them as the time runs out. It stays no floor at all,
        /// neither safe nor solid and with no collider, until <see cref="Restore"/>, so a block in the air touches no car.
        /// </summary>
        public void Raise(int wave, float landIn)
        {
            if (states[wave] is not (WaveState.Falling or WaveState.Gone)) {
                return;
            }

            if (states[wave] == WaveState.Gone) {
                movingWaves++;
            }

            states[wave] = WaveState.Returning;
            var target = waves[wave];
            var time = Time.time;
            target.RaisedAt = time;
            target.LandAt = time + Mathf.Max(0f, landIn);
            foreach (var block in target.Blocks) {
                block.Velocity = Vector3.zero;
                block.Transform.gameObject.SetActive(true);
            }

            Hover(target, time);
        }

        /// <summary>
        /// Puts a wave back in its place, whatever it was doing: its blocks at rest, and the collider and the edge with it
        /// again, which tells the readers of the edge (the camera opens out again).
        /// </summary>
        public void Restore(int wave)
        {
            if (states[wave] == WaveState.Standing) {
                return;
            }

            if (states[wave] is WaveState.Warning or WaveState.Falling or WaveState.Returning) {
                movingWaves--;
            }

            states[wave] = WaveState.Standing;
            foreach (var block in waves[wave].Blocks) {
                block.Velocity = Vector3.zero;
                block.Transform.gameObject.SetActive(true);
                block.Transform.SetLocalPositionAndRotation(block.RestPosition, block.RestRotation);
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
            var warning = wave.WarningTime;
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

        /// <summary>
        /// Holds the blocks of a returning wave over their places: sinking from where they appear to where they hang,
        /// bobbing and tilting there with their own noise, and in the last moment dropping faster and faster, straight
        /// into place.
        /// </summary>
        private void Hover(WaveBlocks wave, float time)
        {
            var left = wave.LandAt - time;
            float height;
            float sway;
            if (left <= returnDrop) {
                // A fall from rest: the height goes with the square of the time left.
                var share = Mathf.Clamp01(left / returnDrop);
                height = returnHeight * share * share;
                sway = share;
            }
            else {
                var settle = Mathf.Clamp01((time - wave.RaisedAt) / returnSettle);
                var ease = 1f - (1f - settle) * (1f - settle);
                height = returnHeight * Mathf.Lerp(1.5f, 1f, ease);
                sway = 1f;
            }

            // Slower than the shake of a crumbling block: a sway, not a rattle.
            var phase = time * shakeFrequency * 0.15f;
            foreach (var block in wave.Blocks) {
                var x = Noise(block.Seed.x, phase);
                var y = Noise(block.Seed.x + block.Seed.y, phase);
                var z = Noise(block.Seed.y, phase);
                block.Transform.SetLocalPositionAndRotation(
                    block.RestPosition + Vector3.up * (height + y * returnBob.x * sway),
                    block.RestRotation * Quaternion.Euler(z * returnBob.y * sway, 0f, -x * returnBob.y * sway));
            }
        }

        private static float Noise(float seed, float phase)
        {
            return Mathf.PerlinNoise(seed, phase) * 2f - 1f;
        }

        private sealed class WaveBlocks
        {
            public readonly Block[] Blocks;
            public float WarnedAt;
            public float WarningTime;
            public float RaisedAt;
            public float LandAt;

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
