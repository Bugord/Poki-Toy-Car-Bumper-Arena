using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// Every physics and gameplay number of the prototype in one asset (<c>Data/Gameplay/data_game_tuning</c>), as the
    /// brief asks, so the feel is tuned in one place. The values are starting points; each step of the brief tunes its
    /// own section. Code reads them through the properties and never keeps its own copy of a number.
    /// </summary>
    [CreateAssetMenu(fileName = "data_game_tuning", menuName = "Toy Car Arena/Game Tuning")]
    public sealed class GameTuning : ScriptableObject
    {
        [SerializeField]
        private CarSettings car = new();

        [SerializeField]
        private BoostSettings boost = new();

        [SerializeField]
        private BotSettings bots = new();

        [SerializeField]
        private RoundSettings round = new();

        [SerializeField]
        private ArenaSettings arena = new();

        public CarSettings Car => car;
        public BoostSettings Boost => boost;
        public BotSettings Bots => bots;
        public RoundSettings Round => round;
        public ArenaSettings Arena => arena;

        /// <summary>
        /// How a car drives and how hard it pushes. A car's own data may scale these later, when the cars differ.
        /// </summary>
        [Serializable]
        public sealed class CarSettings
        {
            [Tooltip("Top speed on the flat, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float maxSpeed = 12f;

            [Tooltip("Speed gained per second at full throttle, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float acceleration = 25f;

            [Tooltip("Turn rate at speed, in degrees per second")]
            [Min(0f)]
            [SerializeField]
            private float turnSpeed = 180f;

            [Tooltip("Rigidbody mass; the heavier car of two moves less when they hit")]
            [Min(0.01f)]
            [SerializeField]
            private float mass = 1f;

            [Tooltip("How fast sideways sliding dies down, per second; low drifts, high grips")]
            [Min(0f)]
            [SerializeField]
            private float grip = 8f;

            [Tooltip("Extra impulse on a hit, per metre per second of closing speed, on top of the physics")]
            [Min(0f)]
            [SerializeField]
            private float bumpForce = 1f;

            public float MaxSpeed => maxSpeed;
            public float Acceleration => acceleration;
            public float TurnSpeed => turnSpeed;
            public float Mass => mass;
            public float Grip => grip;
            public float BumpForce => bumpForce;
        }

        /// <summary>
        /// The boost: the player's one decision, to attack or to dodge.
        /// </summary>
        [Serializable]
        public sealed class BoostSettings
        {
            [Tooltip("Speed added at once, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float impulse = 12f;

            [Tooltip("Seconds the boost lasts, during which the top speed is raised and hits push harder")]
            [Min(0f)]
            [SerializeField]
            private float duration = 0.4f;

            [Tooltip("What a hit's push is multiplied by during the boost")]
            [Min(1f)]
            [SerializeField]
            private float bumpMultiplier = 1.5f;

            [Tooltip("Seconds from one boost to the next")]
            [Min(0f)]
            [SerializeField]
            private float cooldown = 3f;

            public float Impulse => impulse;
            public float Duration => duration;
            public float BumpMultiplier => bumpMultiplier;
            public float Cooldown => cooldown;
        }

        /// <summary>
        /// The bots: pick a target, speed up at it, stay away from the edge.
        /// </summary>
        [Serializable]
        public sealed class BotSettings
        {
            [Tooltip("Bots in a round, beside the player (5 to 7 in the brief)")]
            [Range(5, 7)]
            [SerializeField]
            private int count = 6;

            [Tooltip("Seconds between picks of a new target")]
            [Min(0f)]
            [SerializeField]
            private float retargetInterval = 2f;

            [Tooltip("A bot this close to the edge, in metres, turns back towards the middle")]
            [Min(0f)]
            [SerializeField]
            private float edgeMargin = 2f;

            [Tooltip("Shortest and longest delay before a bot boosts once its target is ahead, in seconds")]
            [SerializeField]
            private Vector2 boostDelay = new(0.3f, 1.2f);

            public int Count => count;
            public float RetargetInterval => retargetInterval;
            public float EdgeMargin => edgeMargin;
            public Vector2 BoostDelay => boostDelay;
        }

        /// <summary>
        /// One round, from the start to the result screen.
        /// </summary>
        [Serializable]
        public sealed class RoundSettings
        {
            [Tooltip("Seconds a round lasts at most (60 to 90 in the brief); the arena is gone by then")]
            [Range(60f, 90f)]
            [SerializeField]
            private float duration = 75f;

            [Tooltip("Seconds between the last car falling and the result screen")]
            [Min(0f)]
            [SerializeField]
            private float resultDelay = 1.5f;

            [Tooltip("A car below this height, in metres, is out")]
            [SerializeField]
            private float fallHeight = -3f;

            public float Duration => duration;
            public float ResultDelay => resultDelay;
            public float FallHeight => fallHeight;
        }

        /// <summary>
        /// The round arena and how its edge crumbles away.
        /// </summary>
        [Serializable]
        public sealed class ArenaSettings
        {
            [Tooltip("Radius at the start of a round, in metres")]
            [Min(1f)]
            [SerializeField]
            private float radius = 15f;

            [Tooltip("The edge stops crumbling at this radius, in metres")]
            [Min(1f)]
            [SerializeField]
            private float minRadius = 5f;

            [Tooltip("Seconds into the round before the edge starts to crumble")]
            [Min(0f)]
            [SerializeField]
            private float crumbleDelay = 15f;

            [Tooltip("Seconds between two crumbles of the edge")]
            [Min(0.1f)]
            [SerializeField]
            private float crumbleInterval = 6f;

            [Tooltip("Metres of radius each crumble takes")]
            [Min(0f)]
            [SerializeField]
            private float crumbleStep = 1.5f;

            [Tooltip("Seconds a crumbling piece shakes before it falls, so a car on it can get away")]
            [Min(0f)]
            [SerializeField]
            private float crumbleWarning = 1.5f;

            public float Radius => radius;
            public float MinRadius => minRadius;
            public float CrumbleDelay => crumbleDelay;
            public float CrumbleInterval => crumbleInterval;
            public float CrumbleStep => crumbleStep;
            public float CrumbleWarning => crumbleWarning;
        }
    }
}
