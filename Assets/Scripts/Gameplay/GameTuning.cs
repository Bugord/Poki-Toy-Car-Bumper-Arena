using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// Every physics and gameplay number of the prototype in one asset (<c>Data/Gameplay/data_game_tuning</c>), as the
    /// brief asks, so the feel is tuned in one place. The values are starting points; each step of the brief tunes its
    /// own section. Code reads them through the properties and never keeps its own copy of a number.
    /// </summary>
    [CreateAssetMenu(fileName = "data_game_tuning", menuName = "Bonk Arena/Game Tuning")]
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

            [Tooltip("How much harder the player's hits push than a bot's, so the player's shove is the one that " +
                "sends cars flying and the knock-outs read as the player's")]
            [Min(0f)]
            [SerializeField]
            private float playerBumpMultiplier = 1.3f;

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

            [Tooltip("Share of the time off balance the player's car gets, so a hit on the player still pushes it but " +
                "takes the controls away only briefly")]
            [Range(0f, 1f)]
            [SerializeField]
            private float playerStaggerScale = 0.4f;

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
            public float PlayerBumpMultiplier => playerBumpMultiplier;
            public float HitStagger => hitStagger;
            public float StaggerGrip => staggerGrip;
            public float PlayerStaggerScale => playerStaggerScale;
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

            public TouchSteering TouchSteering => touchSteering;
            public float JoystickRadius => joystickRadius;
            public float JoystickDeadZone => joystickDeadZone;
            public float PointerStopRadius => pointerStopRadius;
            public float PointerSlowRadius => pointerSlowRadius;
            public float EdgeAssistMargin => edgeAssistMargin;
            public float EdgeAssistStop => edgeAssistStop;
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
                boostDelay: new Vector2(1.2f, 2.5f), escapeBoostChance: 0.3f, edgeAssist: 1f);

            [Tooltip("Where the difficulty settles after the ramp")]
            [SerializeField]
            private DifficultySettings normal = new(bots: 6, playerPace: 1f, playerWeight: 0.05f, playerGrace: 2f,
                maxPlayerHunters: 2, reactionTime: new Vector2(0.12f, 0.2f), aimError: 3f,
                boostDelay: new Vector2(0.3f, 1.2f), escapeBoostChance: 0.5f, edgeAssist: 0.3f);

            [Tooltip("Rounds from easy to normal: round 1 is easy, round 1 + this is normal, the ones between blend")]
            [Min(0)]
            [SerializeField]
            private int rampRounds = 6;

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
        /// aim, how readily they boost, and how much the player's car is kept from driving off the edge. Staying on the
        /// arena never gets slower for the bots, or they would fall off on their own and the rounds swing wildly; and
        /// the bots fight each other at full pace, so the arena stays busy.
        /// </summary>
        [Serializable]
        public sealed class DifficultySettings
        {
            [Tooltip("Bots in the round, beside the player; fewer early on, so a new player lasts long enough to push " +
                "a car off and has fewer cars to beat")]
            [Range(1, 7)]
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

            public DifficultySettings()
            {
            }

            public DifficultySettings(int bots, float playerPace, float playerWeight, float playerGrace,
                int maxPlayerHunters, Vector2 reactionTime, float aimError, Vector2 boostDelay, float escapeBoostChance,
                float edgeAssist)
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
                "is attributed to that ring")]
            [Min(0f)]
            [SerializeField]
            private float ringFallWindow = 2f;

            [Tooltip("Seconds after a successful boost that a fall without knockout credit or a recent ring fall " +
                "is attributed to the boost")]
            [Min(0f)]
            [SerializeField]
            private float boostFallWindow = 1f;

            [Tooltip("Seconds after another car's hit that a fall without knockout credit, a recent ring fall or " +
                "a boost is attributed to sliding after the hit")]
            [Min(0f)]
            [SerializeField]
            private float slideWindow = 6f;

            [Tooltip("A car below this height, in metres, is out")]
            [SerializeField]
            private float fallHeight = -3f;

            [Tooltip("A car out of the round keeps falling in view and is put away below this height, in metres")]
            [SerializeField]
            private float removeHeight = -25f;

            public float Duration => duration;
            public float ResultDelay => resultDelay;
            public float OutResultDelay => outResultDelay;
            public float KnockoutCreditWindow => knockoutCreditWindow;
            public float RingFallWindow => ringFallWindow;
            public float BoostFallWindow => boostFallWindow;
            public float SlideWindow => slideWindow;
            public float FallHeight => fallHeight;
            public float RemoveHeight => removeHeight;
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

            public float Radius => radius;
            public float SpawnRadius => spawnRadius;
            public float SpawnHeight => spawnHeight;
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
