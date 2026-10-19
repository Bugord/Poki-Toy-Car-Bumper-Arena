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
        private ControlSettings controls = new();

        [SerializeField]
        private BoostSettings boost = new();

        [SerializeField]
        private BotSettings bots = new();

        [SerializeField]
        private RoundSettings round = new();

        [SerializeField]
        private ArenaSettings arena = new();

        public CarSettings Car => car;
        public ControlSettings Controls => controls;
        public BoostSettings Boost => boost;
        public BotSettings Bots => bots;
        public RoundSettings Round => round;
        public ArenaSettings Arena => arena;

        /// <summary>
        /// How a car drives and how hard it pushes. A car's own data may scale these later, when the cars differ. The car
        /// is about 2.6 m long.
        /// </summary>
        [Serializable]
        public sealed class CarSettings
        {
            [Tooltip("Top speed on the flat, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float maxSpeed = 10f;

            [Tooltip("Speed gained per second at full throttle, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float acceleration = 20f;

            [Tooltip("Speed lost per second off the throttle, or above the speed the throttle asks for, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float braking = 12f;

            [Tooltip("Fastest turn, in degrees per second")]
            [Min(0f)]
            [SerializeField]
            private float turnSpeed = 300f;

            [Tooltip("How fast the turn rate builds up and dies down, in degrees per second squared; it also eases the " +
                "car onto its heading without overshooting")]
            [Min(1f)]
            [SerializeField]
            private float turnAcceleration = 2000f;

            [Tooltip("Rigidbody mass; the heavier car of two moves less when they hit")]
            [Min(0.01f)]
            [SerializeField]
            private float mass = 1f;

            [Tooltip("Height of the centre of mass above the bottom of the car, in metres; low keeps it on its wheels")]
            [SerializeField]
            private float centerOfMassHeight = 0.3f;

            [Tooltip("How fast sideways sliding dies down, per second; low drifts, high grips")]
            [Min(0f)]
            [SerializeField]
            private float grip = 6f;

            [Tooltip("Extra impulse on a hit, per metre per second of closing speed, on top of the physics")]
            [Min(0f)]
            [SerializeField]
            private float bumpForce = 1f;

            public float MaxSpeed => maxSpeed;
            public float Acceleration => acceleration;
            public float Braking => braking;
            public float TurnSpeed => turnSpeed;
            public float TurnAcceleration => turnAcceleration;
            public float Mass => mass;
            public float CenterOfMassHeight => centerOfMassHeight;
            public float Grip => grip;
            public float BumpForce => bumpForce;
        }

        /// <summary>
        /// How the mouse and a finger steer. The keys and a gamepad stick point where the car should go, up being away
        /// from the camera; the mouse drives the car towards the cursor while its button is held.
        /// </summary>
        [Serializable]
        public sealed class ControlSettings
        {
            [Tooltip("Joystick: the finger is a stick that appears where it touched. Follow Finger: the car drives " +
                "towards the finger, as it does towards the mouse")]
            [SerializeField]
            private TouchSteering touchSteering = TouchSteering.Joystick;

            [Tooltip("How far the finger moves from where it touched for full throttle, as a share of the screen's " +
                "shorter side; dragged further, the stick's centre follows the finger")]
            [Range(0.02f, 0.5f)]
            [SerializeField]
            private float joystickRadius = 0.1f;

            [Tooltip("Share of the joystick radius the finger moves before the car starts")]
            [Range(0f, 0.9f)]
            [SerializeField]
            private float joystickDeadZone = 0.2f;

            [Tooltip("Following the mouse or a finger, the car stops this close to it, in metres")]
            [Min(0f)]
            [SerializeField]
            private float pointerStopRadius = 1f;

            [Tooltip("Following the mouse or a finger, the car slows down from this far, in metres")]
            [Min(0f)]
            [SerializeField]
            private float pointerSlowRadius = 4f;

            public TouchSteering TouchSteering => touchSteering;
            public float JoystickRadius => joystickRadius;
            public float JoystickDeadZone => joystickDeadZone;
            public float PointerStopRadius => pointerStopRadius;
            public float PointerSlowRadius => pointerSlowRadius;
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

            [Tooltip("Room the camera leaves around the arena, in metres, so a car at the edge is in view in any " +
                "screen shape")]
            [Min(0f)]
            [SerializeField]
            private float viewMargin = 1.5f;

            public float Radius => radius;
            public float MinRadius => minRadius;
            public float CrumbleDelay => crumbleDelay;
            public float CrumbleInterval => crumbleInterval;
            public float CrumbleStep => crumbleStep;
            public float CrumbleWarning => crumbleWarning;
            public float ViewMargin => viewMargin;
        }

        public enum TouchSteering
        {
            Joystick,
            FollowFinger,
        }
    }
}
