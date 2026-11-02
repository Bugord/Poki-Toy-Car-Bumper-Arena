using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// Every physics and gameplay number of the prototype in one asset (<c>Data/Gameplay/data_game_tuning</c>), as the
    /// brief asks, so the feel is tuned in one place. The values are starting points; each step of the brief tunes its
    /// own section. Code reads them through the properties and never keeps its own copy of a number.
    /// </summary>
    [CreateAssetMenu(fileName = "data_game_tuning", menuName = "Bonk Arena/Game Tuning")]
    public sealed partial class GameTuning : ScriptableObject
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

        [Header("Maps")]
        [Tooltip("Arenas in rotation order; the first is the visit's first round, the rest open with the player's level")]
        [SerializeField]
        private MapSettings[] maps = Array.Empty<MapSettings>();

        [Header("Progress")]
        [Tooltip("Round rewards and the experience needed for each next player level")]
        [SerializeField]
        private ProgressSettings progress = new();

        [Header("Players")]
        [Tooltip("Visit roster turnover between rounds; cosmetics only")]
        [SerializeField]
        private PlayerSettings players = new();

        [Header("Ball arena")]
        [Tooltip("The big toy ball of the Ball mechanic: when it rolls, how fast, and how hard it knocks a car aside")]
        [SerializeField]
        private BallSettings ball = new();

        [Header("Candy arena")]
        [Tooltip("Caramel and icing patches of the Candy mechanic: how they feel under a car and where they lie")]
        [SerializeField]
        private CandySettings candy = new();

        [Header("Carousel arena")]
        [Tooltip("The turntable of the Carousel mechanic: how big it is, how fast it turns and when it reverses")]
        [SerializeField]
        private CarouselSettings carousel = new();

        [Header("Crab arena")]
        [Tooltip("The wind-up crab of the Crab mechanic: how fast it walks and how hard it shoves a car aside")]
        [SerializeField]
        private CrabSettings crab = new();

        [Header("Mounds arena")]
        [Tooltip("The sand mounds of the Mounds mechanic: when they rise, how big, how they toss and slow a car")]
        [SerializeField]
        private MoundsSettings mounds = new();

        [Header("Top arena")]
        [Tooltip("The spinning top of the Top mechanic: how fast it wanders and how hard it flings a car")]
        [SerializeField]
        private TopSettings top = new();

        [Header("Pencil arena")]
        [Tooltip("The rolling pencil of the Pencil mechanic: how often it rolls, how long it is and how hard it knocks a car")]
        [SerializeField]
        private PencilSettings pencil = new();

        public CarSettings Car => car;
        public ControlSettings Controls => controls;
        public BoostSettings Boost => boost;
        public BotSettings Bots => bots;
        public RoundSettings Round => round;
        public IReadOnlyList<MapSettings> Maps => maps;
        public ProgressSettings Progress => progress;
        public PlayerSettings Players => players;
        public BallSettings Ball => ball;
        public CandySettings Candy => candy;
        public CarouselSettings Carousel => carousel;
        public CrabSettings Crab => crab;
        public MoundsSettings Mounds => mounds;
        public TopSettings Top => top;
        public PencilSettings Pencil => pencil;

        [Serializable]
        public sealed class PlayerSettings
        {
            [Tooltip("Chance for each player from the finished round to leave, except the revenge target")]
            [Range(0f, 1f)]
            [SerializeField]
            private float leaveChance = 0.25f;

            [Tooltip("Maximum departures after one round")]
            [Range(0, 6)]
            [SerializeField]
            private int maxLeaves = 2;

            public float LeaveChance => leaveChance;
            public int MaxLeaves => maxLeaves;
        }

        /// <summary>
        /// Every round advances the garage; all reward amounts and level prices are tuned together here.
        /// </summary>
        [Serializable]
        public sealed class ProgressSettings
        {
            [Tooltip("XP for every completed round, whatever its outcome")]
            [Min(0)]
            [SerializeField]
            private int roundXp = 10;

            [Tooltip("XP for each car placed behind the player (car count minus place)")]
            [Min(0)]
            [SerializeField]
            private int placeXp = 5;

            [Tooltip("Extra XP for being the last car standing")]
            [Min(0)]
            [SerializeField]
            private int winXp = 15;

            [Tooltip("XP for each knockout credited to the player")]
            [Min(0)]
            [SerializeField]
            private int knockoutXp = 10;

            [Tooltip("XP from level 1 to 2, 2 to 3 and so on; the last price repeats for all later levels")]
            [Min(1)]
            [SerializeField]
            private int[] levelXp = { 40, 60, 80, 100, 120, 140 };

            public int RoundXp => roundXp;
            public int PlaceXp => placeXp;
            public int WinXp => winXp;
            public int KnockoutXp => knockoutXp;
            public int[] LevelXp => levelXp;
        }

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

            [Tooltip("Extra push a car gets from a hit, on top of the physics: metres per second for every metre per " +
                "second the other car drove into it (no more than the two closed in at), times the other's mass over " +
                "its own, so the faster and heavier car wins; a boosted hitter multiplies it by the boost's bump " +
                "multiplier")]
            [Min(0f)]
            [SerializeField]
            private float bumpForce = 0.5f;

            [Tooltip("Fastest the extra push of a hit leaves a car going away from the hitter, in metres per second, " +
                "whatever the speeds and masses, so no hit flings a car across the arena; the physics' own bounce is " +
                "never cut. A car hit at this speed along its nose slides about 13 m, across it about 3 m")]
            [Min(0f)]
            [SerializeField]
            private float maxBumpSpeed = 18f;

            [Tooltip("Seconds a car is knocked off balance by a hit at full speed (shorter for a slower hit, no longer " +
                "for a boosted one): it lets go of the throttle and its grip drops, so the push carries it instead of " +
                "dying down at once and the hit car cannot drive straight back; a boost ends it")]
            [Min(0f)]
            [SerializeField]
            private float hitStagger = 0.5f;

            [Tooltip("Share of the grip a car keeps while knocked off balance")]
            [Range(0f, 1f)]
            [SerializeField]
            private float staggerGrip = 0.2f;

            public float MaxSpeed => maxSpeed;
            public float Acceleration => acceleration;
            public float Braking => braking;
            public float TurnSpeed => turnSpeed;
            public float TurnAcceleration => turnAcceleration;
            public float Mass => mass;
            public float CenterOfMassHeight => centerOfMassHeight;
            public float Grip => grip;
            public float BumpForce => bumpForce;
            public float MaxBumpSpeed => maxBumpSpeed;
            public float HitStagger => hitStagger;
            public float StaggerGrip => staggerGrip;
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

            [Tooltip("A steering touch released within this many seconds can boost, if it stayed within Tap Boost Slop")]
            [Min(0f)]
            [SerializeField]
            private float tapBoostTime = 0.2f;

            [Tooltip("Maximum distance from the touch's start for a tap boost, as a share of the screen's shorter side; " +
                "moving out and back still counts as steering")]
            [Range(0f, 0.5f)]
            [SerializeField]
            private float tapBoostSlop = 0.03f;

            [Tooltip("Seconds a tap boost remembers the last nonzero steering direction after the finger lets go; " +
                "afterwards it boosts along the car's nose")]
            [Min(0f)]
            [SerializeField]
            private float tapBoostMemory = 0.5f;

            [Tooltip("Seconds without steering in the visit's first round before the touch joystick reminder returns")]
            [Min(0f)]
            [SerializeField]
            private float idleHintDelay = 3f;

            [Tooltip("Maximum joystick reminders during the visit's first round, excluding the start demonstration")]
            [Min(0)]
            [SerializeField]
            private int idleHintMax = 2;

            [Tooltip("Visit rounds, from the first, in which a touch screen keeps a ghost joystick wherever no finger " +
                "steers")]
            [Min(0)]
            [SerializeField]
            private int ghostStickRounds = 5;

            [Tooltip("Distance to another car, centre to centre in metres, within which to suggest the first boost")]
            [Min(0f)]
            [SerializeField]
            private float boostHintDistance = 6f;

            [Tooltip("Following the mouse or a finger, the car stops this close to it, in metres")]
            [Min(0f)]
            [SerializeField]
            private float pointerStopRadius = 1f;

            [Tooltip("Following the mouse or a finger, the car slows down from this far, in metres")]
            [Min(0f)]
            [SerializeField]
            private float pointerSlowRadius = 4f;

            [Tooltip("From this far inside the edge, in metres, the round's edge assist takes the steer off the arena " +
                "away, more the closer the car gets; far enough for a car at top speed to brake to a stop")]
            [Min(0f)]
            [SerializeField]
            private float edgeAssistMargin = 5f;

            [Tooltip("This far inside the edge, in metres, the edge assist takes all of the steer off the arena it " +
                "takes: a car steered straight off creeps up to here and stops, so it must be short of the margin and " +
                "keep the car's nose (half its length, about 1.3 m) on the arena")]
            [Min(0f)]
            [SerializeField]
            private float edgeAssistStop = 2f;

            [Tooltip("Distance inside the safe arena radius of the player's edge holding line, in metres; " +
                "keeps half the car's length and a little reserve on the floor")]
            [Min(0f)]
            [SerializeField]
            private float edgeHoldMargin = 1.5f;

            [Tooltip("Braking in metres per second squared used to limit outward speed to " +
                "sqrt(2 * brake * distance to the holding line), including during a boost")]
            [Min(0f)]
            [SerializeField]
            private float edgeHoldBrake = 12f;

            [Tooltip("The most outward speed the hold takes away per second, in metres per second squared, scaled " +
                "by the round's edge assist: full on Easy, a fraction on Normal, so a hard car still slides off there")]
            [Min(0f)]
            [SerializeField]
            private float edgeHoldMaxBrake = 80f;

            [Tooltip("Seconds after another car last drove into or kept pushing the player during which edge " +
                "holding yields to the shove; holding also yields while staggered")]
            [Min(0f)]
            [SerializeField]
            private float edgeHoldContactGrace = 0.5f;

            [Header("Auto boost")]
            [Tooltip("A ram fires the player's boost on its own when the player drives at a car this close, centre to " +
                "centre in metres; how often is the round's auto boost")]
            [Min(0f)]
            [SerializeField]
            private float autoRamRange = 4.5f;

            [Tooltip("Most the car the player drives at may be off the player's steer, in degrees, for a ram")]
            [Range(0f, 90f)]
            [SerializeField]
            private float autoRamAngle = 25f;

            [Tooltip("Least throttle the player drives with for a ram: a player who rolls or brakes does not mean to hit")]
            [Range(0f, 1f)]
            [SerializeField]
            private float autoRamThrottle = 0.5f;

            [Tooltip("Least speed, in metres per second, at which the player closes on the car for a ram")]
            [Min(0f)]
            [SerializeField]
            private float autoRamClosing = 1f;

            [Tooltip("Seconds the player has to keep driving at the car before a ram fires, so a car merely passed by " +
                "in front is not rammed")]
            [Min(0f)]
            [SerializeField]
            private float autoRamHold = 0.12f;

            [Tooltip("Centre to centre distance, in metres, at which the player's car meets the one it rams: a ram is " +
                "fired only if the car stops there on the floor, short of the edge holding line")]
            [Min(0f)]
            [SerializeField]
            private float autoRamContact = 2.6f;

            [Tooltip("A rescue fires the player's boost inward on its own when a hit pushes the car outward this close " +
                "to the edge, in metres")]
            [Min(0f)]
            [SerializeField]
            private float autoRescueMargin = 2.5f;

            [Tooltip("Least outward speed, in metres per second, of a pushed car for a rescue")]
            [Min(0f)]
            [SerializeField]
            private float autoRescueSpeed = 4f;

            [Tooltip("Seconds after a hit during which a rescue may fire")]
            [Min(0f)]
            [SerializeField]
            private float autoRescueWindow = 1f;

            [Tooltip("A rescue fires only if the car's outward speed would carry it to the edge within this many " +
                "seconds: a bump that the car rides out on the floor is left to the player")]
            [Min(0f)]
            [SerializeField]
            private float autoRescueLookAhead = 0.5f;

            [Tooltip("Least seconds between two rescues")]
            [Min(0f)]
            [SerializeField]
            private float autoRescueCooldown = 4f;

            public TouchSteering TouchSteering => touchSteering;
            public float JoystickRadius => joystickRadius;
            public float JoystickDeadZone => joystickDeadZone;
            public float TapBoostTime => tapBoostTime;
            public float TapBoostSlop => tapBoostSlop;
            public float TapBoostMemory => tapBoostMemory;
            public float IdleHintDelay => idleHintDelay;
            public int IdleHintMax => idleHintMax;
            public int GhostStickRounds => ghostStickRounds;
            public float BoostHintDistance => boostHintDistance;
            public float PointerStopRadius => pointerStopRadius;
            public float PointerSlowRadius => pointerSlowRadius;
            public float EdgeAssistMargin => edgeAssistMargin;
            public float EdgeAssistStop => edgeAssistStop;
            public float EdgeHoldMargin => edgeHoldMargin;
            public float EdgeHoldBrake => edgeHoldBrake;
            public float EdgeHoldMaxBrake => edgeHoldMaxBrake;
            public float EdgeHoldContactGrace => edgeHoldContactGrace;
            public float AutoRamRange => autoRamRange;
            public float AutoRamAngle => autoRamAngle;
            public float AutoRamThrottle => autoRamThrottle;
            public float AutoRamClosing => autoRamClosing;
            public float AutoRamHold => autoRamHold;
            public float AutoRamContact => autoRamContact;
            public float AutoRescueMargin => autoRescueMargin;
            public float AutoRescueSpeed => autoRescueSpeed;
            public float AutoRescueWindow => autoRescueWindow;
            public float AutoRescueLookAhead => autoRescueLookAhead;
            public float AutoRescueCooldown => autoRescueCooldown;
        }

        /// <summary>
        /// The boost: the player's one decision, to attack or to dodge.
        /// </summary>
        [Serializable]
        public sealed class BoostSettings
        {
            [Tooltip("Speed added at once, in metres per second, towards where the car is steering (along its nose " +
                "when it is not); the top speed is raised by as much while the boost lasts")]
            [Min(0f)]
            [SerializeField]
            private float impulse = 10f;

            [Tooltip("Seconds the boost lasts, during which the car holds its line at the raised top speed and its hits " +
                "push harder. It cannot be steered meanwhile and is braked off afterwards, so keep it short: the boost " +
                "back from the edge of the last, 5 m disc must stop short of the far edge")]
            [Min(0f)]
            [SerializeField]
            private float duration = 0.3f;

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
        /// The bots: pick a target, speed up at it, stay away from the edge. How hard they play the player comes from
        /// <see cref="Easy"/> and <see cref="Normal"/>, blended by the round of the visit and the player's losing streak,
        /// so a new player wins early and keeps pressing "play again"; each bot's character is one of four
        /// personalities, a few multipliers over the numbers here, so the bots do not all drive alike.
        /// </summary>
        [Serializable]
        public sealed class BotSettings
        {
            [Tooltip("Seconds between picks of a new target")]
            [Min(0f)]
            [SerializeField]
            private float retargetInterval = 2f;

            [Tooltip("Share of the retarget interval each pick lengthens or shortens it by at random, so the bots do " +
                "not change their minds together")]
            [Range(0f, 1f)]
            [SerializeField]
            private float retargetJitter = 0.5f;

            [Tooltip("A bot this close to the edge, in metres, turns back towards the middle")]
            [Min(0f)]
            [SerializeField]
            private float edgeMargin = 1.5f;

            [Header("Pits")]
            [Tooltip("How far ahead along its line to the target, in metres, a bot looks for a pit or a hole that would " +
                "swallow it; 0 never goes round")]
            [Min(0f)]
            [SerializeField]
            private float pitProbe = 9f;

            [Tooltip("How many points along that line a bot samples the floor at")]
            [Range(2, 8)]
            [SerializeField]
            private int pitSamples = 5;

            [Tooltip("Seconds a bot keeps the side it chose to go round a pit, so it does not dither at the middle")]
            [Min(0f)]
            [SerializeField]
            private float pitSideHold = 1f;

            [Tooltip("How much of the way round a pit goes inward, away from the pit, beside along its rim: 0 hugs the " +
                "rim, 1 as much inward as along")]
            [Min(0f)]
            [SerializeField]
            private float pitInward = 0.5f;

            [Header("Hazards")]
            [Tooltip("Seconds, from and to, a bot takes to notice a new hazard of the arena's mechanic (a crab, a top, " +
                "a pencil, a train) before it steers out of its way; a personality's caution divides it")]
            [SerializeField]
            private Vector2 hazardReaction = new(0.15f, 0.45f);

            [Tooltip("Metres beyond a hazard's own reach a bot still counts as in its way")]
            [Min(0f)]
            [SerializeField]
            private float hazardMargin = 1.2f;

            [Header("Aim")]
            [Tooltip("Seconds ahead a bot foresees where its speed takes it, times its personality's caution; heading " +
                "past the edge margin by then, it turns back towards the middle before it gets there")]
            [Min(0f)]
            [SerializeField]
            private float lookAhead = 0.25f;

            [Tooltip("A bot with its target right in front of its nose and this close, in metres (centre to centre), " +
                "is pushing it: it stops looking ahead and keeps pushing up to the edge margin; 0 never commits")]
            [Min(0f)]
            [SerializeField]
            private float pushDistance = 3.5f;

            [Tooltip("Widest angle off a bot's nose, in degrees, its target counts as right in front at")]
            [Range(0f, 90f)]
            [SerializeField]
            private float pushAngle = 45f;

            [Tooltip("A bot aims this far behind its target, in metres, on the side facing the middle, so the hit " +
                "pushes the target out towards the edge")]
            [Min(0f)]
            [SerializeField]
            private float aimBehind = 1.5f;

            [Tooltip("Seconds of its target's motion a bot aims ahead of it")]
            [Min(0f)]
            [SerializeField]
            private float aimLead = 0.3f;

            [Header("Target picks")]
            [Tooltip("How much a target's distance counts against it in a pick, per arena diameter away")]
            [Min(0f)]
            [SerializeField]
            private float distanceWeight = 1f;

            [Tooltip("How much a target counts for being out near the edge, where it is easy to push off: this for a " +
                "car right at the edge, nothing for one in the middle; times the personality's edge weight")]
            [Min(0f)]
            [SerializeField]
            private float edgeWeight = 0.6f;

            [Tooltip("Most the player's car can count for in a pick, whatever the difficulty and the personality add " +
                "up to, so even the bully does not hunt the player alone")]
            [SerializeField]
            private float maxPlayerWeight = 0.25f;

            [Tooltip("What a target counts against it for each other bot already after it, so the bots spread out " +
                "instead of mobbing one car")]
            [Min(0f)]
            [SerializeField]
            private float crowdPenalty = 0.4f;

            [Tooltip("Most a random amount added to each target's score in a pick can be, so the same bot does not " +
                "always pick the same car")]
            [Min(0f)]
            [SerializeField]
            private float pickNoise = 0.5f;

            [Tooltip("What the bot's current target counts for in a pick, so a bot keeps after one car long enough " +
                "for its intent to be read and dodged")]
            [Min(0f)]
            [SerializeField]
            private float stickWeight = 0.3f;

            [Tooltip("What a car hit within the opportunity time and sliding outward counts for in a pick, times the " +
                "personality's opportunism: a car on its way off is the easiest to finish")]
            [Min(0f)]
            [SerializeField]
            private float opportunityWeight = 0.35f;

            [Tooltip("Seconds after a car is hit during which it counts as an opportunity")]
            [Min(0f)]
            [SerializeField]
            private float opportunityTime = 1f;

            [Tooltip("What the car that last hit a bot counts for in that bot's picks; only one bot at a time holds " +
                "such a grudge against the player")]
            [Min(0f)]
            [SerializeField]
            private float grudgeWeight = 0.4f;

            [Tooltip("Seconds a bot holds a grudge against the car that hit it")]
            [Min(0f)]
            [SerializeField]
            private float grudgeTime = 4f;

            [Header("Difficulty")]
            [Tooltip("The first round of a visit: few bots, slow at the player, slow to react and to boost, and the " +
                "player's car kept from driving off the edge")]
            [SerializeField]
            private DifficultySettings easy = new(bots: 3, playerPace: 0.6f, playerWeight: -0.6f, playerGrace: 10f,
                maxPlayerHunters: 1, reactionTime: new Vector2(0.4f, 0.6f), aimError: 15f,
                boostDelay: new Vector2(1.2f, 2.5f), escapeBoostChance: 0.3f, edgeAssist: 1f,
                playerBump: 1.3f, playerStaggerScale: 0.4f, staggerFromPlayer: 1f, playerKnockoutWindow: 2.5f,
                duelPace: 0.6f, duelEscapeBoostChance: 0.3f);

            [Tooltip("Where the difficulty settles after the ramp")]
            [SerializeField]
            private DifficultySettings normal = new(bots: 6, playerPace: 1f, playerWeight: 0.05f, playerGrace: 2f,
                maxPlayerHunters: 2, reactionTime: new Vector2(0.12f, 0.2f), aimError: 3f,
                boostDelay: new Vector2(0.3f, 1.2f), escapeBoostChance: 0.5f, edgeAssist: 0.3f,
                playerBump: 1.3f, playerStaggerScale: 0.4f, staggerFromPlayer: 1f, playerKnockoutWindow: 2.5f,
                duelPace: 1f, duelEscapeBoostChance: 0.5f);

            [Tooltip("Rounds from easy to normal: round 1 is easy, round 1 + this is normal, the ones between blend")]
            [Min(0)]
            [SerializeField]
            private int rampRounds = 6;

            [Tooltip("Rounds the help against driving off the edge (EdgeAssist, and with it the edge hold) takes from " +
                "easy to normal, apart from the bots' ramp: driving off on one's own is no fun at any round, so it stays " +
                "longer; a losing streak steps it back the same way")]
            [Min(0)]
            [SerializeField]
            private int edgeAssistRounds = 12;

            [Tooltip("Rounds lost in a row from which the bots ease off")]
            [Min(1)]
            [SerializeField]
            private int streakStart = 2;

            [Tooltip("Share of the way back to easy the bots step for each round lost in a row, counting from the " +
                "streak start")]
            [Range(0f, 1f)]
            [SerializeField]
            private float streakEase = 0.25f;

            [Tooltip("Furthest a losing streak steps the bots back towards easy, as a share of the way")]
            [Range(0f, 1f)]
            [SerializeField]
            private float maxStreakEase = 0.75f;

            [Header("Personalities")]
            [Tooltip("The personalities of a round's bots: the first as many as there are bots this round, shuffled " +
                "among them each round; a short list repeats from its start")]
            [SerializeField]
            private BotPersonality[] mix =
            {
                BotPersonality.Bully,
                BotPersonality.Careful,
                BotPersonality.Opportunist,
                BotPersonality.Clumsy,
                BotPersonality.Bully,
                BotPersonality.Careful,
                BotPersonality.Opportunist,
            };

            [Tooltip("Comes for the player and boosts early, bold at the edge")]
            [SerializeField]
            private PersonalitySettings bully = new(caution: 0.75f, playerWeight: 0.35f, edgeWeight: 1f,
                opportunism: 1f, boostDelay: 0.5f, aimError: 1f, escapeBoost: 1f);

            [Tooltip("Keeps to the middle, picks cars near it and saves itself on the edge")]
            [SerializeField]
            private PersonalitySettings careful = new(caution: 1.3f, playerWeight: 0f, edgeWeight: 0.6f,
                opportunism: 0.5f, boostDelay: 1.3f, aimError: 0.7f, escapeBoost: 1.5f);

            [Tooltip("Goes for cars that were just hit and slide outward, the moment any car is hit")]
            [SerializeField]
            private PersonalitySettings opportunist = new(caution: 1f, playerWeight: -0.1f, edgeWeight: 1.4f,
                opportunism: 3f, boostDelay: 0.8f, aimError: 1f, escapeBoost: 1f);

            [Tooltip("Aims wide and is bold at the edge, now and then going off on its own")]
            [SerializeField]
            private PersonalitySettings clumsy = new(caution: 0.6f, playerWeight: 0f, edgeWeight: 1f,
                opportunism: 1f, boostDelay: 1f, aimError: 2.5f, escapeBoost: 0.6f);

            public float RetargetInterval => retargetInterval;
            public float RetargetJitter => retargetJitter;
            public float EdgeMargin => edgeMargin;
            public float PitProbe => pitProbe;
            public int PitSamples => pitSamples;
            public float PitSideHold => pitSideHold;
            public float PitInward => pitInward;
            public Vector2 HazardReaction => hazardReaction;
            public float HazardMargin => hazardMargin;
            public float LookAhead => lookAhead;
            public float PushDistance => pushDistance;
            public float PushAngle => pushAngle;
            public float AimBehind => aimBehind;
            public float AimLead => aimLead;
            public float DistanceWeight => distanceWeight;
            public float EdgeWeight => edgeWeight;
            public float MaxPlayerWeight => maxPlayerWeight;
            public float CrowdPenalty => crowdPenalty;
            public float PickNoise => pickNoise;
            public float StickWeight => stickWeight;
            public float OpportunityWeight => opportunityWeight;
            public float OpportunityTime => opportunityTime;
            public float GrudgeWeight => grudgeWeight;
            public float GrudgeTime => grudgeTime;
            public DifficultySettings Easy => easy;
            public DifficultySettings Normal => normal;
            public int RampRounds => rampRounds;
            public int EdgeAssistRounds => edgeAssistRounds;
            public int StreakStart => streakStart;
            public float StreakEase => streakEase;
            public float MaxStreakEase => maxStreakEase;
            public BotPersonality[] Mix => mix;

            public PersonalitySettings Personality(BotPersonality personality)
            {
                return personality switch
                {
                    BotPersonality.Bully => bully,
                    BotPersonality.Careful => careful,
                    BotPersonality.Opportunist => opportunist,
                    _ => clumsy,
                };
            }
        }

        /// <summary>
        /// How hard a round is at one end of the difficulty ramp. Only what the player feels changes: how many bots
        /// there are, how fast and how often they come for the player's car, how quickly they see a dodge, how well they
        /// aim, how readily they boost, how the player's hits carry cars off and earn knockouts, and how much the
        /// player's car is kept from driving off the edge. Staying on the arena never gets slower for the bots, or they
        /// would fall off on their own and the rounds swing wildly; and the bots fight each other at full pace, so the
        /// arena stays busy.
        /// </summary>
        [Serializable]
        public sealed class DifficultySettings
        {
            [Tooltip("Bots in the round, beside the player; fewer early on, so a new player lasts long enough to push " +
                "a car off and has fewer cars to beat")]
            [Range(1, 6)]
            [SerializeField]
            private int bots = 6;

            [Tooltip("Throttle a bot drives at the player's car with, from 0 to 1; below 1 the player outruns it and " +
                "its hits push less")]
            [Range(0f, 1f)]
            [SerializeField]
            private float playerPace = 1f;

            [Tooltip("What the player's car counts for in a pick, beside the others; below 0 the bots pick on the " +
                "player less, so a new player is not mobbed")]
            [SerializeField]
            private float playerWeight = -0.15f;

            [Tooltip("Seconds from the start of a round during which no bot picks the player's car, so a new player " +
                "has time to find their car and the controls before anyone comes for them")]
            [Min(0f)]
            [SerializeField]
            private float playerGrace = 6f;

            [Tooltip("Most bots after the player's car at once")]
            [Min(0)]
            [SerializeField]
            private int maxPlayerHunters = 2;

            [Tooltip("Shortest and longest time, in seconds, between two looks of a bot at its target: in between it " +
                "steers by where it last saw the target going, so a sharp turn dodges it")]
            [SerializeField]
            private Vector2 reactionTime = new(0.12f, 0.2f);

            [Tooltip("Most a bot's heading at its target is off, in degrees, picked again at each look; times the " +
                "personality's aim error")]
            [Range(0f, 45f)]
            [SerializeField]
            private float aimError = 3f;

            [Tooltip("Shortest and longest delay before a bot boosts once its target is ahead, in seconds; times the " +
                "personality's boost delay")]
            [SerializeField]
            private Vector2 boostDelay = new(0.3f, 1.2f);

            [Tooltip("Chance a bot pushed out past the edge margin and still sliding outward boosts back towards the " +
                "middle, rolled once each time, times the personality's escape boost; 0 never saves itself, 1 always " +
                "does when the boost is ready")]
            [Range(0f, 1f)]
            [SerializeField]
            private float escapeBoostChance = 0.5f;

            [Tooltip("Share of the player's steer off the arena taken away near the edge (from the controls' edge " +
                "assist margin in), so a new player who steers too far stops at the edge instead of driving off it; " +
                "hits still push the car off. 0 leaves the steer alone, 1 never drives off")]
            [Range(0f, 1f)]
            [SerializeField]
            private float edgeAssist;

            [Tooltip("Chance the game boosts for the player at a moment a boost wins: a ram into a car the player drives " +
                "at, or a rescue back from the edge after a hit; rolled once each moment. 0 leaves the boost to the " +
                "player alone; the button and keys work the same either way")]
            [Range(0f, 1f)]
            [SerializeField]
            private float autoBoost;

            [Tooltip("How much harder the player's hits push than a bot's, so the player's shove is the one that " +
                "sends cars flying and the knock-outs read as the player's")]
            [Min(0f)]
            [SerializeField]
            private float playerBump = 1.3f;

            [Tooltip("Share of the time off balance the player's car gets, so a hit on the player still pushes it but " +
                "takes the controls away only briefly")]
            [Range(0f, 1f)]
            [SerializeField]
            private float playerStaggerScale = 0.4f;

            [Tooltip("Times the time off balance after all other hit multipliers when the player drives into a car, " +
                "up to the car's hit stagger times this; longer lets the player's shove carry the car further")]
            [Min(0f)]
            [SerializeField]
            private float staggerFromPlayer = 1f;

            [Tooltip("Seconds after the player's last hit during which a falling car counts as the player's knockout; " +
                "bot hits keep the round's knockout credit window")]
            [Min(0f)]
            [SerializeField]
            private float playerKnockoutWindow = 2.5f;

            [Tooltip("Throttle the last bot drives at the player with when only those two cars remain; replaces " +
                "player pace so the final duel can be gentler without slowing earlier fights")]
            [Range(0f, 1f)]
            [SerializeField]
            private float duelPace = 1f;

            [Tooltip("Chance the last bot boosts back from the edge when only it and the player remain; replaces " +
                "escape boost chance, still times the personality's escape boost, rolled once each escape")]
            [Range(0f, 1f)]
            [SerializeField]
            private float duelEscapeBoostChance = 0.5f;

            public DifficultySettings()
            {
            }

            public DifficultySettings(int bots, float playerPace, float playerWeight, float playerGrace,
                int maxPlayerHunters, Vector2 reactionTime, float aimError, Vector2 boostDelay, float escapeBoostChance,
                float edgeAssist, float playerBump, float playerStaggerScale, float staggerFromPlayer,
                float playerKnockoutWindow, float duelPace, float duelEscapeBoostChance)
            {
                this.bots = bots;
                this.playerPace = playerPace;
                this.playerWeight = playerWeight;
                this.playerGrace = playerGrace;
                this.maxPlayerHunters = maxPlayerHunters;
                this.reactionTime = reactionTime;
                this.aimError = aimError;
                this.boostDelay = boostDelay;
                this.escapeBoostChance = escapeBoostChance;
                this.edgeAssist = edgeAssist;
                this.playerBump = playerBump;
                this.playerStaggerScale = playerStaggerScale;
                this.staggerFromPlayer = staggerFromPlayer;
                this.playerKnockoutWindow = playerKnockoutWindow;
                this.duelPace = duelPace;
                this.duelEscapeBoostChance = duelEscapeBoostChance;
            }

            public int Bots => bots;
            public float PlayerPace => playerPace;
            public float PlayerWeight => playerWeight;
            public float PlayerGrace => playerGrace;
            public int MaxPlayerHunters => maxPlayerHunters;
            public Vector2 ReactionTime => reactionTime;
            public float AimError => aimError;
            public Vector2 BoostDelay => boostDelay;
            public float EscapeBoostChance => escapeBoostChance;
            public float EdgeAssist => edgeAssist;
            public float AutoBoost => autoBoost;
            public float PlayerBump => playerBump;
            public float PlayerStaggerScale => playerStaggerScale;
            public float StaggerFromPlayer => staggerFromPlayer;
            public float PlayerKnockoutWindow => playerKnockoutWindow;
            public float DuelPace => duelPace;
            public float DuelEscapeBoostChance => duelEscapeBoostChance;
        }

        /// <summary>
        /// One bot personality, as multipliers over <see cref="BotSettings"/> and the difficulty (1 changes nothing),
        /// so the character stays the same while the difficulty ramps, and a player notices "that one always comes for
        /// me".
        /// </summary>
        [Serializable]
        public sealed class PersonalitySettings
        {
            [Tooltip("Times the look-ahead at the edge and the room a boost is given: low is bold and pushes cars " +
                "further out, but now and then follows them off; high stays well inside")]
            [Min(0.1f)]
            [SerializeField]
            private float caution = 1f;

            [Tooltip("Added to what the player's car counts for in a pick, up to the most it can count for")]
            [SerializeField]
            private float playerWeight;

            [Tooltip("Times the edge weight of a pick: low picks cars near the bot in the middle, high goes for cars " +
                "out by the edge")]
            [Min(0f)]
            [SerializeField]
            private float edgeWeight = 1f;

            [Tooltip("Times the weight of a car just hit and sliding outward; above 1 the bot also looks for a new " +
                "target the moment any car is hit")]
            [Min(0f)]
            [SerializeField]
            private float opportunism = 1f;

            [Tooltip("Times the delay before a bot boosts into a car it pushes")]
            [Min(0f)]
            [SerializeField]
            private float boostDelay = 1f;

            [Tooltip("Times the aim error")]
            [Min(0f)]
            [SerializeField]
            private float aimError = 1f;

            [Tooltip("Times the chance of a boost back from the edge, up to always")]
            [Min(0f)]
            [SerializeField]
            private float escapeBoost = 1f;

            public PersonalitySettings()
            {
            }

            public PersonalitySettings(float caution, float playerWeight, float edgeWeight, float opportunism,
                float boostDelay, float aimError, float escapeBoost)
            {
                this.caution = caution;
                this.playerWeight = playerWeight;
                this.edgeWeight = edgeWeight;
                this.opportunism = opportunism;
                this.boostDelay = boostDelay;
                this.aimError = aimError;
                this.escapeBoost = escapeBoost;
            }

            public float Caution => caution;
            public float PlayerWeight => playerWeight;
            public float EdgeWeight => edgeWeight;
            public float Opportunism => opportunism;
            public float BoostDelay => boostDelay;
            public float AimError => aimError;
            public float EscapeBoost => escapeBoost;
        }

        /// <summary>
        /// One round, from the start to the result screen.
        /// </summary>
        [Serializable]
        public sealed class RoundSettings
        {
            [Tooltip("Seconds a round lasts at most (60 to 90 in the brief); the edge has crumbled to its smallest by then")]
            [Range(60f, 90f)]
            [SerializeField]
            private float duration = 75f;

            [Tooltip("Seconds between the last car falling and the result screen, after a win or the time running out")]
            [Min(0f)]
            [SerializeField]
            private float resultDelay = 1.5f;

            [Tooltip("Seconds between the player's fall and the result screen: shorter than after a win, since a fall " +
                "reads at once and the brief wants a lost round back in play within about 3 seconds")]
            [Min(0f)]
            [SerializeField]
            private float outResultDelay = 1f;

            [Tooltip("A car that falls off within this many seconds of another car driving into it was knocked out by " +
                "that car: the player's KNOCKOUT! and knock-out count, and who knocked the player out")]
            [Min(0f)]
            [SerializeField]
            private float knockoutCreditWindow = 2.5f;

            [Tooltip("Seconds after the floor ring under the player falls away that a fall without knockout credit " +
                "is attributed to that ring; measured at the last grounded step, not elimination")]
            [Min(0f)]
            [SerializeField]
            private float ringFallWindow = 2f;

            [Tooltip("Seconds after a successful boost that a fall without knockout credit or a recent ring fall " +
                "is attributed to the boost; measured at the last grounded step, not elimination")]
            [Min(0f)]
            [SerializeField]
            private float boostFallWindow = 1f;

            [Tooltip("Seconds after another car's hit that a fall without knockout credit, a recent ring fall or " +
                "a boost is attributed to sliding after the hit; measured at the last grounded step, not elimination")]
            [Min(0f)]
            [SerializeField]
            private float slideWindow = 6f;

            [Tooltip("The kick-off counts down from this number (3, 2, 1, GO!) before every round or match; the clock, " +
                "the bots and the floor start at GO. 0 begins at once")]
            [Range(0, 5)]
            [SerializeField]
            private int kickoffFrom = 3;

            [Tooltip("Seconds each number of the kick-off stays on the screen, unscaled")]
            [Min(0.1f)]
            [SerializeField]
            private float kickoffStep = 0.7f;

            [Tooltip("Seconds GO! stays on the screen once the round has begun under it, unscaled")]
            [Min(0.1f)]
            [SerializeField]
            private float kickoffGo = 0.8f;

            [Tooltip("A car below this height, in metres, is out")]
            [SerializeField]
            private float fallHeight = -3f;

            [Tooltip("A car out of the round keeps falling in view and is put away below this height, in metres; deeper than " +
                "the ground of every arena (its catalog entry's ground depth), so a car is hidden by the ground or the fog " +
                "long before it is put away")]
            [SerializeField]
            private float removeHeight = -100f;

            public float Duration => duration;
            public float ResultDelay => resultDelay;
            public float OutResultDelay => outResultDelay;
            public float KnockoutCreditWindow => knockoutCreditWindow;
            public float RingFallWindow => ringFallWindow;
            public float BoostFallWindow => boostFallWindow;
            public float SlideWindow => slideWindow;
            public int KickoffFrom => kickoffFrom;
            public float KickoffStep => kickoffStep;
            public float KickoffGo => kickoffGo;
            public float FallHeight => fallHeight;
            public float RemoveHeight => removeHeight;
        }

        /// <summary>
        /// One arena of the rotation: the numbers of its round and the level that opens it. Its look is in the
        /// <c>ArenaCatalog</c>, by the same id.
        /// </summary>
        [Serializable]
        public sealed class MapSettings
        {
            [Tooltip("Permanent identity shared with the arena catalog, storage and analytics; letters only")]
            [SerializeField]
            private string id;

            [Tooltip("First player level at which this arena comes up in the rotation")]
            [Min(1)]
            [SerializeField]
            private int unlockLevel = 1;

            [Tooltip("The variants of this arena, first the one every player meets first. A round plays one of them: " +
                "its own floor look and its own twist; the first arena's first variant has none, so the first round of " +
                "a visit teaches only the basics")]
            [SerializeField]
            private VariantSettings[] variants = Array.Empty<VariantSettings>();

            [SerializeField]
            private ArenaSettings arena = new();

            public string Id => id;
            public int UnlockLevel => unlockLevel;
            public IReadOnlyList<VariantSettings> Variants => variants;
            public ArenaSettings Arena => arena;

            /// <summary>The variant with this id, or null.</summary>
            public VariantSettings FindVariant(string variantId)
            {
                foreach (var variant in variants) {
                    if (variant.Id == variantId) {
                        return variant;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// One variant of an arena: what makes a round on it play differently. Its floor look is in the
        /// <c>ArenaCatalog</c>, by the arena's id and this one.
        /// </summary>
        [Serializable]
        public sealed class VariantSettings
        {
            [Tooltip("Permanent identity shared with the arena catalog, storage and analytics; lowercase letters only, no digits")]
            [SerializeField]
            private string id;

            [Tooltip("What makes a round on this variant play differently")]
            [SerializeField]
            private ArenaMechanic mechanic;

            public string Id => id;
            public ArenaMechanic Mechanic => mechanic;
        }

        /// <summary>
        /// The arena's own twist on a round. Its numbers are in the section of the same name.
        /// </summary>
        public enum ArenaMechanic
        {
            None,
            /// <summary>Sticky caramel and slippery icing patches on the floor.</summary>
            Candy,
            /// <summary>The middle of the floor turns and carries the cars on it.</summary>
            Carousel,
            /// <summary>A big toy ball rolls across the arena now and then and knocks cars aside.</summary>
            Ball,
            /// <summary>A wind-up crab walks across the arena and shoves what it meets.</summary>
            Crab,
            /// <summary>Sand mounds rise at random places and slow and toss the cars.</summary>
            Mounds,
            /// <summary>A spinning top wanders over the table and flings what it touches.</summary>
            Top,
            /// <summary>A pencil rolls across the page and sweeps it.</summary>
            Pencil,
            /// <summary>Jelly springs toss the cars that drive onto them.</summary>
            Jelly,
            /// <summary>A candy stirrer sweeps round from the middle of the hole and changes direction.</summary>
            Stirrer,
            /// <summary>A toy train circles the arena on rails.</summary>
            Train,
            /// <summary>Jack-in-the-boxes pop up at marked places and toss the cars.</summary>
            Jack,
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

            [Tooltip("The cars start on a circle this share of the radius out from the middle, facing it")]
            [Range(0f, 0.95f)]
            [SerializeField]
            private float spawnRadius = 0.6f;

            [Tooltip("Height above the floor the cars start at, in metres, so they settle onto it instead of starting " +
                "inside it")]
            [Min(0f)]
            [SerializeField]
            private float spawnHeight = 0.05f;

            [Tooltip("The edge stops crumbling at this radius, in metres: the solid middle of the floor")]
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
            private float crumbleInterval = 11f;

            [Tooltip("Metres of radius each crumble takes, about: the floor between the radius and the smallest radius " +
                "is cut into whole rings as close to this wide as fit; 0 never crumbles")]
            [Min(0f)]
            [SerializeField]
            private float crumbleStep = 2f;

            [Tooltip("Seconds a crumbling piece shakes before it falls, so a car on it can get away")]
            [Min(0f)]
            [SerializeField]
            private float crumbleWarning = 2f;

            [Tooltip("Room the camera leaves around the arena, in metres, so a car at the edge is in view in any " +
                "screen shape")]
            [Min(0f)]
            [SerializeField]
            private float viewMargin = 1.5f;

            [Header("Shape")]
            [Tooltip("The outline of the floor. Circle is a disc inside rings (Radius and the crumble numbers above); " +
                "the others are cut into tiles from the numbers below, and Radius then says the circle that holds the " +
                "whole floor (props are placed outside it)")]
            [SerializeField]
            private ArenaShapeKind shape = ArenaShapeKind.Circle;

            [Tooltip("How the floor crumbles: Rings from the edge in, Strips side by side with the corners first, " +
                "Scatter tiles at random along the edge (and the hole widens), Segments one by one round the rim. Each " +
                "shape supports one pattern")]
            [SerializeField]
            private CrumblePattern pattern = CrumblePattern.Rings;

            [Tooltip("Half the floor's width (X) and length (Z, away from the camera) in metres: both sides of a square, " +
                "the flats of a hexagon (X, the distance from the middle to a flat), the width and the whole length of " +
                "a stadium")]
            [SerializeField]
            private Vector2 halfSize = new(10.5f, 10.5f);

            [Tooltip("Radius of the rounded corners of a square, in metres")]
            [Min(0f)]
            [SerializeField]
            private float cornerRadius = 3f;

            [Tooltip("The part that never crumbles: half its width in metres (a square's half side, the distance from " +
                "the middle to the outer flat of a hexagon's ring, a stadium's half width)")]
            [Min(1f)]
            [SerializeField]
            private float coreSize = 5.5f;

            [Tooltip("The hole in the middle of a hexagon: its distance to a flat at the start of the round (X) and once " +
                "it has widened to the most (Y), in metres")]
            [SerializeField]
            private Vector2 holeSize = new(2f, 3.5f);

            [Tooltip("The two pits of a stadium, left and right of the middle: distance from the middle to the near edge " +
                "of a pit (X) and width of a pit (Y), in metres; 0 width has none")]
            [SerializeField]
            private Vector2 pits = new(8.5f, 3f);

            [Tooltip("Half the length of a pit along Z, in metres")]
            [Min(0f)]
            [SerializeField]
            private float pitHalfLength = 4.5f;

            [Tooltip("Tiles along one side of the outer layer: blocks along a side strip of a square, tiles along a flat of " +
                "a hexagon, wedges round a cap of a stadium")]
            [Range(1, 12)]
            [SerializeField]
            private int tilesPerSide = 3;

            [Tooltip("How many waves the tiles crumble in when the pattern scatters or cuts them (Scatter, Segments); " +
                "the other patterns have their own")]
            [Range(2, 20)]
            [SerializeField]
            private int waves = 8;

            [Tooltip("The cars start on the contour this far in from the rim, in metres, on a floor that is not a circle")]
            [Min(0f)]
            [SerializeField]
            private float spawnInset = 5.2f;

            [Tooltip("The cars do not start within this distance of the middle, in metres (a turntable there), nor within " +
                "two metres of a pit")]
            [Min(0f)]
            [SerializeField]
            private float spawnClear;

            public float Radius => radius;
            public float SpawnRadius => spawnRadius;
            public float SpawnHeight => spawnHeight;
            public float MinRadius => minRadius;
            public float CrumbleDelay => crumbleDelay;
            public float CrumbleInterval => crumbleInterval;
            public float CrumbleStep => crumbleStep;
            public float CrumbleWarning => crumbleWarning;
            public float ViewMargin => viewMargin;
            public ArenaShapeKind Shape => shape;
            public CrumblePattern Pattern => pattern;
            public Vector2 HalfSize => halfSize;
            public float CornerRadius => cornerRadius;
            public float CoreSize => coreSize;
            public Vector2 HoleSize => holeSize;
            public Vector2 Pits => pits;
            public float PitHalfLength => pitHalfLength;
            public int TilesPerSide => tilesPerSide;
            public int Waves => waves;
            public float SpawnInset => spawnInset;
            public float SpawnClear => spawnClear;
        }

        /// <summary>The outline of an arena's floor.</summary>
        public enum ArenaShapeKind
        {
            /// <summary>A disc inside rings.</summary>
            Circle,
            /// <summary>A square with rounded corners, like a board.</summary>
            RoundedSquare,
            /// <summary>A hexagon with a hole in the middle that widens as the round goes on.</summary>
            HexDonut,
            /// <summary>Two half discs joined by straight sides, with a pit left and right of the middle.</summary>
            Stadium,
        }

        /// <summary>The order the pieces of the floor crumble in.</summary>
        public enum CrumblePattern
        {
            /// <summary>Rings from the edge in.</summary>
            Rings,
            /// <summary>The corners, then the sides one after another.</summary>
            Strips,
            /// <summary>Tiles at random along the edge, and the hole widens.</summary>
            Scatter,
            /// <summary>Pieces one after another round the rim, layer by layer.</summary>
            Segments,
        }

        /// <summary>
        /// The Candy arena's patches. Each feel number multiplies what the car has on plain floor, so 1 changes nothing.
        /// Caramel is a safe, slow, heavy spot; icing is fast and slides. The layout is six patches on a ring of slots,
        /// alternating caramel and icing, turned by a seeded angle each round; see <c>CandyPatches</c>.
        /// </summary>
        [Serializable]
        public sealed class CandySettings
        {
            [Header("Caramel")]
            [Tooltip("Top speed on caramel, against plain floor; a boost is not slowed")]
            [Range(0.1f, 1f)]
            [SerializeField]
            private float caramelSpeed = 0.55f;

            [Tooltip("Acceleration on caramel, against plain floor")]
            [Range(0.1f, 1f)]
            [SerializeField]
            private float caramelAcceleration = 0.6f;

            [Tooltip("Grip on caramel, against plain floor; above 1 the car sticks to its line")]
            [Min(0f)]
            [SerializeField]
            private float caramelGrip = 1.6f;

            [Tooltip("Share of a hit's extra push a car on caramel gets: it resists being shoved")]
            [Range(0f, 1f)]
            [SerializeField]
            private float caramelPush = 0.6f;

            [Tooltip("Share of the stagger time a hit gives a car on caramel")]
            [Range(0f, 1f)]
            [SerializeField]
            private float caramelStagger = 0.6f;

            [Header("Icing")]
            [Tooltip("Grip on icing, against plain floor; low slides and turns badly")]
            [Range(0.02f, 1f)]
            [SerializeField]
            private float icingGrip = 0.2f;

            [Tooltip("Top speed on icing, against plain floor")]
            [Min(0.1f)]
            [SerializeField]
            private float icingSpeed = 1.15f;

            [Tooltip("Acceleration on icing, against plain floor")]
            [Min(0.1f)]
            [SerializeField]
            private float icingAcceleration = 1f;

            [Header("Patches")]
            [Tooltip("Metres across which a patch's effect fades in at its rim, so a car does not jerk on entering")]
            [Min(0.01f)]
            [SerializeField]
            private float blendWidth = 0.5f;

            [Tooltip("Seconds a patch shrinks away in, once its ring is crumbling")]
            [Min(0.01f)]
            [SerializeField]
            private float vanishTime = 0.3f;

            [Tooltip("A patch starts to vanish when the floor's radius is less than the patch's outer edge minus this, " +
                "in metres")]
            [Min(0f)]
            [SerializeField]
            private float vanishMargin = 0.4f;

            [Tooltip("Distance of the two inner patches from the middle, in metres. With their radius they stay inside " +
                "the spawn circle, so the start is fair")]
            [Min(0f)]
            [SerializeField]
            private float innerDistance = 4.2f;

            [Tooltip("Radius of an inner patch, in metres")]
            [Min(0.5f)]
            [SerializeField]
            private float innerRadius = 2.8f;

            [Tooltip("Distance of the four outer patches from the middle, in metres. Their inner edge stays out of the " +
                "spawn circle and their outer edge inside the first ring")]
            [Min(0f)]
            [SerializeField]
            private float outerDistance = 12.6f;

            [Tooltip("Radius of an outer patch, in metres")]
            [Min(0.5f)]
            [SerializeField]
            private float outerRadius = 2.1f;

            [Header("On a donut floor")]
            [Tooltip("Distance of the three inner patches from the middle on a floor with a hole, in metres, along the " +
                "directions of the hexagon's flats. They lie between the hole and the spawn contour")]
            [Min(0f)]
            [SerializeField]
            private float ringInnerDistance = 5.2f;

            [Tooltip("Radius of an inner patch on a floor with a hole, in metres")]
            [Min(0.5f)]
            [SerializeField]
            private float ringInnerRadius = 1.4f;

            [Tooltip("Distance of the three outer patches from the middle on a floor with a hole, in metres. They lie " +
                "beyond the spawn contour and reach into the crumbling edge")]
            [Min(0f)]
            [SerializeField]
            private float ringOuterDistance = 11.4f;

            [Tooltip("Radius of an outer patch on a floor with a hole, in metres")]
            [Min(0.5f)]
            [SerializeField]
            private float ringOuterRadius = 1.6f;

            public float CaramelSpeed => caramelSpeed;
            public float CaramelAcceleration => caramelAcceleration;
            public float CaramelGrip => caramelGrip;
            public float CaramelPush => caramelPush;
            public float CaramelStagger => caramelStagger;
            public float IcingGrip => icingGrip;
            public float IcingSpeed => icingSpeed;
            public float IcingAcceleration => icingAcceleration;
            public float BlendWidth => blendWidth;
            public float VanishTime => vanishTime;
            public float VanishMargin => vanishMargin;
            public float InnerDistance => innerDistance;
            public float InnerRadius => innerRadius;
            public float OuterDistance => outerDistance;
            public float OuterRadius => outerRadius;
            public float RingInnerDistance => ringInnerDistance;
            public float RingInnerRadius => ringInnerRadius;
            public float RingOuterDistance => ringOuterDistance;
            public float RingOuterRadius => ringOuterRadius;
        }

        /// <summary>
        /// The Carousel arena's turntable: a disc in the middle of the floor that turns and carries the cars on it. It
        /// reverses on a fixed schedule, easing through a stop, and flashes before it does; see <c>CarouselDisc</c>.
        /// </summary>
        [Serializable]
        public sealed class CarouselSettings
        {
            [Tooltip("Radius of the turntable, in metres. Wider than the arena's solid middle, so it shrinks with the " +
                "floor in the last stage; the cars start outside it")]
            [Min(1f)]
            [SerializeField]
            private float radius = 7f;

            [Tooltip("Turn at full speed, in degrees per second: at the rim 30 is about 3.7 metres per second")]
            [Min(0f)]
            [SerializeField]
            private float angularSpeed = 30f;

            [Tooltip("Seconds into the round before the turntable starts to turn")]
            [Min(0f)]
            [SerializeField]
            private float startDelay = 3f;

            [Tooltip("Seconds it turns one way before it reverses")]
            [Min(1f)]
            [SerializeField]
            private float reverseInterval = 10f;

            [Tooltip("Seconds it takes to get up to speed, and to stop and turn back")]
            [Min(0.1f)]
            [SerializeField]
            private float easeTime = 1f;

            [Tooltip("Seconds before a reversal that the arrows on the rim flash")]
            [Min(0f)]
            [SerializeField]
            private float telegraph = 0.8f;

            [Tooltip("Metres across which the carry fades out at the rim, so a car is not snatched on or off")]
            [Min(0.05f)]
            [SerializeField]
            private float rimBlend = 0.5f;

            [Tooltip("Metres per second the turntable shrinks at once the floor under its rim is crumbling")]
            [Min(0.5f)]
            [SerializeField]
            private float shrinkSpeed = 6f;

            public float Radius => radius;
            public float AngularSpeed => angularSpeed;
            public float StartDelay => startDelay;
            public float ReverseInterval => reverseInterval;
            public float EaseTime => easeTime;
            public float Telegraph => telegraph;
            public float RimBlend => rimBlend;
            public float ShrinkSpeed => shrinkSpeed;
        }

        /// <summary>
        /// The Ball mechanic: a big toy ball rolls along a chord of the arena now and then, announced by a lane on the
        /// floor, and knocks aside each car it touches. Nobody is credited for a car it knocks out.
        /// </summary>
        [Serializable]
        public sealed class BallSettings
        {
            [Tooltip("Seconds into the round before the first ball is announced")]
            [Min(0f)]
            [SerializeField]
            private float delay = 6f;

            [Tooltip("Seconds from one ball being announced to the next")]
            [Min(1f)]
            [SerializeField]
            private float interval = 9f;

            [Tooltip("Random seconds added to or taken off each interval")]
            [Min(0f)]
            [SerializeField]
            private float jitter = 1.5f;

            [Tooltip("Seconds the lane shows on the floor and the ball wobbles at the edge before it rolls")]
            [Min(0.1f)]
            [SerializeField]
            private float warning = 1.3f;

            [Tooltip("Radius of the ball, in metres; the lane is as wide as the ball")]
            [Min(0.2f)]
            [SerializeField]
            private float radius = 1.4f;

            [Tooltip("Speed of the ball on the easiest round, metres per second: it is slower, easier to read and to dodge")]
            [Min(1f)]
            [SerializeField]
            private float easySpeed = 10f;

            [Tooltip("Speed of the ball on the hardest round, metres per second")]
            [Min(1f)]
            [SerializeField]
            private float speed = 13f;

            [Tooltip("The lane passes this far from the middle at most, as a share of the arena's radius; 0 is always " +
                "through the middle")]
            [Range(0f, 1f)]
            [SerializeField]
            private float maxOffset = 0.4f;

            [Tooltip("Speed, in metres per second, the ball adds to a car it touches")]
            [Min(0f)]
            [SerializeField]
            private float push = 11f;

            [Tooltip("Share of the push that sends the car sideways out of the lane; the rest goes the way the ball rolls")]
            [Range(0f, 1f)]
            [SerializeField]
            private float sideShare = 0.8f;

            [Tooltip("Seconds a hit knocks the car off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.7f;

            [Tooltip("The player's share of the push and stagger on the easiest round; the hardest takes all of it, so " +
                "the ball is rarely what drops a new player")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("A bot this close to the lane's side, in metres, counts as in the lane and steers out of it")]
            [Min(0f)]
            [SerializeField]
            private float botMargin = 1.2f;

            [Tooltip("Seconds a bot takes to notice the lane, times 1 over its caution: the clumsy ones see it late")]
            [SerializeField]
            private Vector2 botReaction = new(0.15f, 0.45f);

            [Tooltip("A car out of the round is out of the ball's way beyond this height above the floor, in metres")]
            [Min(0f)]
            [SerializeField]
            private float hitHeight = 1.5f;

            public float Delay => delay;
            public float Interval => interval;
            public float Jitter => jitter;
            public float Warning => warning;
            public float Radius => radius;
            public float EasySpeed => easySpeed;
            public float Speed => speed;
            public float MaxOffset => maxOffset;
            public float Push => push;
            public float SideShare => sideShare;
            public float Stagger => stagger;
            public float EasyPlayerShare => easyPlayerShare;
            public float BotMargin => botMargin;
            public Vector2 BotReaction => botReaction;
            public float HitHeight => hitHeight;
        }

        /// <summary>
        /// The Crab arena's crab: a wind-up toy that walks sideways across the floor along chords and shoves the cars in its way. Nobody is credited for a car it knocks out.
        /// </summary>
        [Serializable]
        public sealed class CrabSettings
        {
            [Tooltip("Seconds into the round before the crab starts walking")]
            [Min(0f)]
            [SerializeField]
            private float delay = 5f;

            [Tooltip("Walking speed, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float speed = 3f;

            [Tooltip("Seconds ahead of the crab its footprints show on the sand, and the bots start to keep out of its path")]
            [Min(0f)]
            [SerializeField]
            private float warning = 1.5f;

            [Tooltip("A car whose middle is this close to the crab's, in metres, is shoved")]
            [Min(0f)]
            [SerializeField]
            private float hitRadius = 2.4f;

            [Tooltip("Speed, in metres per second, the crab adds to a car it shoves, sideways out of its path")]
            [Min(0f)]
            [SerializeField]
            private float push = 8f;

            [Tooltip("Seconds a shove knocks the car off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.5f;

            [Tooltip("The player's share of the push and stagger on the easiest round; the hardest takes all of it")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("Seconds the crab stands and snaps its claws at the end of a walk before it turns back")]
            [Min(0f)]
            [SerializeField]
            private float turnPause = 1f;

            [Tooltip("The crab turns back this far, in metres, short of the safe edge, so it rarely walks off")]
            [Min(0f)]
            [SerializeField]
            private float edgeInset = 2.2f;

            [Tooltip("The shortest walk the crab sets out on, in metres")]
            [Min(0f)]
            [SerializeField]
            private float minWalk = 6f;

            [Tooltip("A car above this height over the floor, in metres, is out of the crab's reach")]
            [Min(0f)]
            [SerializeField]
            private float hitHeight = 1.5f;

            public float Delay => delay;
            public float Speed => speed;
            public float Warning => warning;
            public float HitRadius => hitRadius;
            public float Push => push;
            public float Stagger => stagger;
            public float EasyPlayerShare => easyPlayerShare;
            public float TurnPause => turnPause;
            public float EdgeInset => edgeInset;
            public float MinWalk => minWalk;
            public float HitHeight => hitHeight;
        }

        /// <summary>
        /// The Mounds arena's sand mounds: each is announced on the floor, then pops up, tosses the cars on it and stays a while as slow sand. Nobody is credited for a car it knocks out.
        /// </summary>
        [Serializable]
        public sealed class MoundsSettings
        {
            [Tooltip("Seconds into the round before the first mounds are announced")]
            [Min(0f)]
            [SerializeField]
            private float delay = 5f;

            [Tooltip("Seconds from one announcement to the next")]
            [Min(0f)]
            [SerializeField]
            private float interval = 6f;

            [Tooltip("Random seconds added to or taken off each interval")]
            [Min(0f)]
            [SerializeField]
            private float jitter = 1f;

            [Tooltip("Fewest mounds announced at once")]
            [Min(1)]
            [SerializeField]
            private int minCount = 1;

            [Tooltip("Most mounds announced at once")]
            [Min(1)]
            [SerializeField]
            private int maxCount = 2;

            [Tooltip("Seconds the circle trembles on the floor before the mound pops up")]
            [Min(0f)]
            [SerializeField]
            private float warning = 1.2f;

            [Tooltip("Seconds a mound stays up before it sinks")]
            [Min(0f)]
            [SerializeField]
            private float stay = 4f;

            [Tooltip("Radius of a mound, in metres")]
            [Min(0f)]
            [SerializeField]
            private float radius = 1.6f;

            [Tooltip("Height of a mound, in metres")]
            [Min(0f)]
            [SerializeField]
            private float height = 0.6f;

            [Tooltip("Speed, in metres per second, a mound throws a car on it outwards from its middle")]
            [Min(0f)]
            [SerializeField]
            private float bounceOut = 6f;

            [Tooltip("Speed, in metres per second, a mound throws a car on it upwards")]
            [Min(0f)]
            [SerializeField]
            private float bounceUp = 4.7f;

            [Tooltip("Seconds the toss knocks the car off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.4f;

            [Tooltip("The player's share of the toss and stagger on the easiest round")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("Share of its top speed a car keeps on a risen mound")]
            [Range(0.1f, 1f)]
            [SerializeField]
            private float speed = 0.6f;

            [Tooltip("Share of its acceleration a car keeps on a risen mound")]
            [Range(0.1f, 1f)]
            [SerializeField]
            private float acceleration = 0.7f;

            [Tooltip("How much more a car grips on a risen mound")]
            [Min(0.1f)]
            [SerializeField]
            private float grip = 1.2f;

            [Tooltip("Seconds into the round during which no mound is announced on the cars' starting contour")]
            [Min(0f)]
            [SerializeField]
            private float spawnClearTime = 8f;

            [Tooltip("Metres from the starting contour that stay free of mounds for that time")]
            [Min(0f)]
            [SerializeField]
            private float spawnClear = 3f;

            [Tooltip("A mound lies at least this far, in metres, inside the safe edge, its own radius apart")]
            [Min(0f)]
            [SerializeField]
            private float edgeMargin = 1f;

            public float Delay => delay;
            public float Interval => interval;
            public float Jitter => jitter;
            public int MinCount => minCount;
            public int MaxCount => maxCount;
            public float Warning => warning;
            public float Stay => stay;
            public float Radius => radius;
            public float Height => height;
            public float BounceOut => bounceOut;
            public float BounceUp => bounceUp;
            public float Stagger => stagger;
            public float EasyPlayerShare => easyPlayerShare;
            public float Speed => speed;
            public float Acceleration => acceleration;
            public float Grip => grip;
            public float SpawnClearTime => spawnClearTime;
            public float SpawnClear => spawnClear;
            public float EdgeMargin => edgeMargin;
        }

        /// <summary>
        /// The Top arena's spinning top: it wanders over the floor on a curving path, turning back from the edge, and flings the cars it touches round with its spin. Nobody is credited for a car it knocks out.
        /// </summary>
        [Serializable]
        public sealed class TopSettings
        {
            [Tooltip("Seconds into the round before the top starts to wander; it spins up for the second before that")]
            [Min(0f)]
            [SerializeField]
            private float delay = 5f;

            [Tooltip("Seconds before it wanders that its ring flashes and it spins faster, so nothing is hit unannounced")]
            [Min(0f)]
            [SerializeField]
            private float spinUp = 1f;

            [Tooltip("Speed of the top over the floor, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float speed = 4f;

            [Tooltip("The most the wander turns the top, radians per second")]
            [Min(0f)]
            [SerializeField]
            private float turnRate = 1.1f;

            [Tooltip("Radius of the top's body, in metres")]
            [Min(0f)]
            [SerializeField]
            private float radius = 1.3f;

            [Tooltip("A car whose middle is this close to the top's, in metres, is flung")]
            [Min(0f)]
            [SerializeField]
            private float hitRadius = 2.1f;

            [Tooltip("Speed, in metres per second, the top adds to a car it flings")]
            [Min(0f)]
            [SerializeField]
            private float push = 10f;

            [Tooltip("Share of the fling that goes round with the spin; the rest goes straight out from the top")]
            [Range(0f, 1f)]
            [SerializeField]
            private float tangentShare = 0.75f;

            [Tooltip("Seconds a fling knocks the car off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.6f;

            [Tooltip("Seconds before the same car can be flung again")]
            [Min(0f)]
            [SerializeField]
            private float retrigger = 1f;

            [Tooltip("The player's share of the push and stagger on the easiest round")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("The top steers off the safe edge when it is closer than this, in metres")]
            [Min(0f)]
            [SerializeField]
            private float edgeMargin = 2.5f;

            [Tooltip("Seconds of its path ahead that the faint dots show and the bots keep out of")]
            [Min(0f)]
            [SerializeField]
            private float lookAhead = 1f;

            [Tooltip("A car above this height over the floor, in metres, is out of the top's reach")]
            [Min(0f)]
            [SerializeField]
            private float hitHeight = 1.5f;

            public float Delay => delay;
            public float SpinUp => spinUp;
            public float Speed => speed;
            public float TurnRate => turnRate;
            public float Radius => radius;
            public float HitRadius => hitRadius;
            public float Push => push;
            public float TangentShare => tangentShare;
            public float Stagger => stagger;
            public float Retrigger => retrigger;
            public float EasyPlayerShare => easyPlayerShare;
            public float EdgeMargin => edgeMargin;
            public float LookAhead => lookAhead;
            public float HitHeight => hitHeight;
        }

        /// <summary>
        /// The Pencil arena's pencil: a long pencil announced by a dashed band on the floor, then rolled across it like a rolling pin, knocking along the cars in its band. Nobody is credited for a car it knocks out.
        /// </summary>
        [Serializable]
        public sealed class PencilSettings
        {
            [Tooltip("Seconds into the round before the first pencil is announced")]
            [Min(0f)]
            [SerializeField]
            private float delay = 6f;

            [Tooltip("Seconds from one announcement to the next")]
            [Min(0f)]
            [SerializeField]
            private float interval = 10f;

            [Tooltip("Random seconds added to or taken off each interval")]
            [Min(0f)]
            [SerializeField]
            private float jitter = 1.5f;

            [Tooltip("Seconds the band shows on the floor and the pencil wobbles at the edge before it rolls")]
            [Min(0f)]
            [SerializeField]
            private float warning = 1.5f;

            [Tooltip("Speed of the pencil across the floor, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float speed = 6f;

            [Tooltip("Radius of the pencil, in metres")]
            [Min(0f)]
            [SerializeField]
            private float radius = 0.5f;

            [Tooltip("Length of the pencil as a share of the floor's width along its line")]
            [Range(0.2f, 1f)]
            [SerializeField]
            private float lengthShare = 0.6f;

            [Tooltip("How far the pencil's middle may sit from the middle of its line, as a share of the floor's width")]
            [Range(0f, 0.5f)]
            [SerializeField]
            private float maxOffset = 0.35f;

            [Tooltip("Speed, in metres per second, the pencil adds to a car it knocks, along the way it rolls")]
            [Min(0f)]
            [SerializeField]
            private float push = 9f;

            [Tooltip("Seconds a knock leaves the car off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.6f;

            [Tooltip("The player's share of the push and stagger on the easiest round")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("A car above this height over the floor, in metres, is out of the pencil's reach")]
            [Min(0f)]
            [SerializeField]
            private float hitHeight = 1.5f;

            public float Delay => delay;
            public float Interval => interval;
            public float Jitter => jitter;
            public float Warning => warning;
            public float Speed => speed;
            public float Radius => radius;
            public float LengthShare => lengthShare;
            public float MaxOffset => maxOffset;
            public float Push => push;
            public float Stagger => stagger;
            public float EasyPlayerShare => easyPlayerShare;
            public float HitHeight => hitHeight;
        }

        public enum TouchSteering
        {
            Joystick,
            FollowFinger,
        }

        /// <summary>
        /// What drives a bot, beside the difficulty: see <see cref="BotSettings.Mix"/>.
        /// </summary>
        public enum BotPersonality
        {
            Bully,
            Careful,
            Opportunist,
            Clumsy,
        }
    }
}
