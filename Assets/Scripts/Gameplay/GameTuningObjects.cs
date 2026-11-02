using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The sections of the interactive arena objects that live apart from the main file, so adding a mechanic does not
    /// touch the rest of <see cref="GameTuning"/>: the Jelly pads, the Stirrer, the Train and the Jack-in-the-boxes.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Jelly arena")]
        [Tooltip("The jelly pads of the Jelly mechanic: where they lie and how far they toss a car")]
        [SerializeField]
        private JellySettings jelly = new();

        [Header("Stirrer arena")]
        [Tooltip("The candy-stick arm of the Stirrer mechanic: how fast it sweeps, when it reverses and how hard it hits")]
        [SerializeField]
        private StirrerSettings stirrer = new();

        [Header("Train arena")]
        [Tooltip("The toy train of the Train mechanic: its rail loop, speed, warning and how hard it hits")]
        [SerializeField]
        private TrainSettings train = new();

        [Header("Jack arena")]
        [Tooltip("The jack-in-the-boxes of the Jack mechanic: how many spots, how often they open and how hard they toss")]
        [SerializeField]
        private JackSettings jack = new();

        public JellySettings Jelly => jelly;
        public StirrerSettings Stirrer => stirrer;
        public TrainSettings Train => train;
        public JackSettings Jack => jack;

        /// <summary>
        /// The Jelly mechanic: wobbly pads on the floor that toss a car driving onto them forward and up, landing it
        /// on the floor again. They hit nobody, so they are no hazard to the bots.
        /// </summary>
        [Serializable]
        public sealed class JellySettings
        {
            [Tooltip("How many pads lie on the floor")]
            [Range(1, 8)]
            [SerializeField]
            private int padCount = 4;

            [Tooltip("Radius of a pad, in metres")]
            [Min(0.5f)]
            [SerializeField]
            private float padRadius = 1.3f;

            [Tooltip("Upward speed a pad gives a car, in metres per second; with gravity it sets how long the car flies")]
            [Min(1f)]
            [SerializeField]
            private float jumpSpeed = 6.5f;

            [Tooltip("How far a car flies forward at least, in metres: it keeps its own speed if that carries it further")]
            [Min(0f)]
            [SerializeField]
            private float range = 7f;

            [Tooltip("A car slower than this, in metres per second, is only tossed straight up")]
            [Min(0f)]
            [SerializeField]
            private float minSpeed = 2f;

            [Tooltip("Metres from the edge of the floor the flight must land at least; the throw is shortened to keep it")]
            [Min(0f)]
            [SerializeField]
            private float landingMargin = 0.8f;

            [Tooltip("Seconds before a pad tosses again")]
            [Min(0f)]
            [SerializeField]
            private float cooldown = 1f;

            [Tooltip("Metres a pad lies at least from the floor's edge, so it is whole when the round starts")]
            [Min(0f)]
            [SerializeField]
            private float edgeClearance = 2.6f;

            [Tooltip("Metres between the middles of two pads at least")]
            [Min(0f)]
            [SerializeField]
            private float padSpacing = 4f;

            [Tooltip("Metres from a car's starting place the middle of a pad lies at least")]
            [Min(0f)]
            [SerializeField]
            private float spawnClearance = 3.8f;

            public int PadCount => padCount;
            public float PadRadius => padRadius;
            public float JumpSpeed => jumpSpeed;
            public float Range => range;
            public float MinSpeed => minSpeed;
            public float LandingMargin => landingMargin;
            public float Cooldown => cooldown;
            public float EdgeClearance => edgeClearance;
            public float PadSpacing => padSpacing;
            public float SpawnClearance => spawnClearance;
        }

        /// <summary>
        /// The Stirrer mechanic: a candy-stick arm that pivots at the middle of the donut's hole and sweeps round the
        /// ring, stopping and flashing before each reversal, and pushes a car it meets along its sweep.
        /// </summary>
        [Serializable]
        public sealed class StirrerSettings
        {
            [Tooltip("Seconds into the round before the arm starts to sweep; it flashes for the last second of them")]
            [Min(0f)]
            [SerializeField]
            private float delay = 5f;

            [Tooltip("Sweep at full speed, in degrees per second: at 35 the tip of a 15 metre arm moves about 9 metres per second")]
            [Min(1f)]
            [SerializeField]
            private float angularSpeed = 35f;

            [Tooltip("Seconds the arm sweeps one way, the stop that warns of the reversal included")]
            [Min(2f)]
            [SerializeField]
            private float reverseInterval = 9f;

            [Tooltip("Seconds the arm stands still and flashes before it reverses")]
            [Min(0.5f)]
            [SerializeField]
            private float pause = 1f;

            [Tooltip("Seconds it takes to get up to speed and to slow to the stop")]
            [Min(0.1f)]
            [SerializeField]
            private float easeTime = 0.5f;

            [Tooltip("Length of the arm from the middle, in metres; it is shortened to the floor that is left")]
            [Min(3f)]
            [SerializeField]
            private float length = 15.5f;

            [Tooltip("Metres from the middle where the arm begins to hit; the hub over the hole is harmless")]
            [Min(0f)]
            [SerializeField]
            private float hubRadius = 1.5f;

            [Tooltip("Half the arm's width plus half a car, in metres: a car this close to the arm is hit")]
            [Min(0.2f)]
            [SerializeField]
            private float reach = 1.3f;

            [Tooltip("Seconds ahead of the arm the floor shows where it is going to sweep")]
            [Min(0.2f)]
            [SerializeField]
            private float wedgeAhead = 1f;

            [Tooltip("Speed, in metres per second, the arm adds to a car it hits")]
            [Min(0f)]
            [SerializeField]
            private float push = 9f;

            [Tooltip("Share of the push that goes along the sweep; the rest sends the car in from the rim")]
            [Range(0f, 1f)]
            [SerializeField]
            private float sweepShare = 0.85f;

            [Tooltip("Seconds a hit knocks the car off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.5f;

            [Tooltip("Seconds before the arm may hit the same car again")]
            [Min(0f)]
            [SerializeField]
            private float hitCooldown = 1.5f;

            [Tooltip("The player's share of the push and stagger on the easiest round; the hardest takes all of it")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("A car above this height, in metres, is out of the arm's way")]
            [Min(0f)]
            [SerializeField]
            private float hitHeight = 1.5f;

            [Tooltip("A bot gets out of the arm's way when the arm will reach it within this many seconds")]
            [Min(0f)]
            [SerializeField]
            private float botLookAhead = 1.3f;

            public float BotLookAhead => botLookAhead;
            public float Delay => delay;
            public float AngularSpeed => angularSpeed;
            public float ReverseInterval => reverseInterval;
            public float Pause => pause;
            public float EaseTime => easeTime;
            public float Length => length;
            public float HubRadius => hubRadius;
            public float Reach => reach;
            public float WedgeAhead => wedgeAhead;
            public float Push => push;
            public float SweepShare => sweepShare;
            public float Stagger => stagger;
            public float HitCooldown => hitCooldown;
            public float EasyPlayerShare => easyPlayerShare;
            public float HitHeight => hitHeight;
        }

        /// <summary>
        /// The Train mechanic: a toy train circles the playroom on a rail loop near the rim and shoves what it meets; the
        /// rail ahead lights up first. When the floor under the rail gives way the train derails and falls off.
        /// </summary>
        [Serializable]
        public sealed class TrainSettings
        {
            [Tooltip("Seconds into the round before the train sets off; its rail starts to light up a warning before")]
            [Min(0f)]
            [SerializeField]
            private float delay = 5f;

            [Tooltip("Speed of the train on the easiest round, metres per second")]
            [Min(1f)]
            [SerializeField]
            private float easySpeed = 5.5f;

            [Tooltip("Speed of the train on the hardest round, metres per second")]
            [Min(1f)]
            [SerializeField]
            private float speed = 7f;

            [Tooltip("Seconds ahead of the train the rail shows lit; the engine's whistle goes with the first of them")]
            [Min(0.5f)]
            [SerializeField]
            private float warning = 1.2f;

            [Tooltip("Metres from the rim to the middle of the rail. The playroom's pits end three metres from the rim, " +
                "so the rail runs between them and the rim")]
            [Min(1f)]
            [SerializeField]
            private float railInset = 1.5f;

            [Tooltip("Length of the engine and of a carriage, in metres")]
            [SerializeField]
            private Vector2 partLength = new(2.6f, 2.2f);

            [Tooltip("Metres between the parts of the train")]
            [Min(0f)]
            [SerializeField]
            private float gap = 0.4f;

            [Tooltip("How many carriages follow the engine")]
            [Range(0, 4)]
            [SerializeField]
            private int carriages = 2;

            [Tooltip("Half the train's width plus half a car, in metres: a car this close to its side is hit")]
            [Min(0.2f)]
            [SerializeField]
            private float sideReach = 1.7f;

            [Tooltip("Metres beyond the ends of a part a car is hit")]
            [Min(0f)]
            [SerializeField]
            private float endReach = 0.8f;

            [Tooltip("Speed, in metres per second, the train adds to a car it hits")]
            [Min(0f)]
            [SerializeField]
            private float push = 10f;

            [Tooltip("Share of the push that sends the car out of the way, to the side it is on; the rest goes the way the train runs")]
            [Range(0f, 1f)]
            [SerializeField]
            private float sideShare = 0.7f;

            [Tooltip("Seconds a hit knocks the car off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.6f;

            [Tooltip("The player's share of the push and stagger on the easiest round; the hardest takes all of it")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("A bot counts the train as in its way when it can reach it in this many seconds of its run")]
            [Min(0f)]
            [SerializeField]
            private float botLookAhead = 1.6f;

            [Tooltip("A car above this height, in metres, is out of the train's way")]
            [Min(0f)]
            [SerializeField]
            private float hitHeight = 1.5f;

            [Tooltip("Seconds the train takes to get up to speed when it sets off")]
            [Min(0.1f)]
            [SerializeField]
            private float startRamp = 1f;

            [Tooltip("Metres from the middle of the straight the nose stands at the start, along the rail")]
            [Min(0f)]
            [SerializeField]
            private float stationOffset = 4f;

            [Tooltip("Seconds before the train may hit the same car again")]
            [Min(0f)]
            [SerializeField]
            private float hitCooldown = 1.2f;

            [Tooltip("Seconds after a train has come off the rail before a new one is put on a stretch of rail that is left; " +
                "0 or less means the arena has only the one train")]
            [SerializeField]
            private float respawnDelay = 3f;

            [Tooltip("Metres of whole rail the new train needs ahead of its nose and behind its tail, so it has a run")]
            [Min(0f)]
            [SerializeField]
            private float respawnRun = 20f;

            public float StartRamp => startRamp;
            public float StationOffset => stationOffset;
            public float HitCooldown => hitCooldown;
            public float RespawnDelay => respawnDelay;
            public float RespawnRun => respawnRun;
            public float Delay => delay;
            public float EasySpeed => easySpeed;
            public float Speed => speed;
            public float Warning => warning;
            public float RailInset => railInset;
            public Vector2 PartLength => partLength;
            public float Gap => gap;
            public int Carriages => carriages;
            public float SideReach => sideReach;
            public float EndReach => endReach;
            public float Push => push;
            public float SideShare => sideShare;
            public float Stagger => stagger;
            public float EasyPlayerShare => easyPlayerShare;
            public float BotLookAhead => botLookAhead;
            public float HitHeight => hitHeight;
        }

        /// <summary>
        /// The Jack mechanic: marked spots on the floor where a jack-in-the-box springs up now and then and tosses the
        /// cars next to it. A spot rattles and glows first.
        /// </summary>
        [Serializable]
        public sealed class JackSettings
        {
            [Tooltip("How many marked spots lie on the floor")]
            [Range(2, 10)]
            [SerializeField]
            private int spotCount = 6;

            [Tooltip("Seconds into the round before the first spot is announced; the box springs up a warning later")]
            [Min(0f)]
            [SerializeField]
            private float delay = 4f;

            [Tooltip("Seconds from one announcement to the next")]
            [Min(1f)]
            [SerializeField]
            private float interval = 5f;

            [Tooltip("Random seconds added to or taken off each interval")]
            [Min(0f)]
            [SerializeField]
            private float jitter = 1f;

            [Tooltip("Chance that a second spot opens with the first")]
            [Range(0f, 1f)]
            [SerializeField]
            private float doubleChance = 0.4f;

            [Tooltip("Seconds a spot rattles and glows before the box springs up")]
            [Min(0.5f)]
            [SerializeField]
            private float warning = 1.2f;

            [Tooltip("Seconds the jack stays up before it folds back down")]
            [Min(0.2f)]
            [SerializeField]
            private float hold = 1.5f;

            [Tooltip("Cars this close to the spot, in metres, are tossed when the box springs up")]
            [Min(0.5f)]
            [SerializeField]
            private float hitRadius = 1.8f;

            [Tooltip("Speed, in metres per second, the box throws a car away from itself")]
            [Min(0f)]
            [SerializeField]
            private float push = 7f;

            [Tooltip("Upward speed, in metres per second, the box gives a car")]
            [Min(0f)]
            [SerializeField]
            private float lift = 4.7f;

            [Tooltip("Seconds a toss knocks the car off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.5f;

            [Tooltip("The player's share of the push and stagger on the easiest round; the hardest takes all of it")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("Metres a spot lies at least from the floor's edge (and so from the pits)")]
            [Min(0f)]
            [SerializeField]
            private float edgeClearance = 3.5f;

            [Tooltip("Metres between the middles of two spots at least")]
            [Min(0f)]
            [SerializeField]
            private float spotSpacing = 4.5f;

            [Tooltip("Metres from a car's starting place a spot lies at least")]
            [Min(0f)]
            [SerializeField]
            private float spawnClearance = 4f;

            [Tooltip("A car above this height, in metres, is not tossed")]
            [Min(0f)]
            [SerializeField]
            private float hitHeight = 1.2f;

            public int SpotCount => spotCount;
            public float Delay => delay;
            public float Interval => interval;
            public float Jitter => jitter;
            public float DoubleChance => doubleChance;
            public float Warning => warning;
            public float Hold => hold;
            public float HitRadius => hitRadius;
            public float Push => push;
            public float Lift => lift;
            public float Stagger => stagger;
            public float EasyPlayerShare => easyPlayerShare;
            public float EdgeClearance => edgeClearance;
            public float SpotSpacing => spotSpacing;
            public float SpawnClearance => spawnClearance;
            public float HitHeight => hitHeight;
        }
    }
}
