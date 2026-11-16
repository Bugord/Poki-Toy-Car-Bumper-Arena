using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The GOLD RUSH event mode's numbers, kept in a file of its own so the mode's work touches only its own files. Not in
    /// the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("GOLD RUSH")]
        [Tooltip("The GOLD RUSH event mode: whether it is on and its numbers")]
        [SerializeField]
        private GoldRushSettings goldRush = new();

        public GoldRushSettings GoldRush => goldRush;

        /// <summary>
        /// GOLD RUSH: coins fall on the floor in bursts and a car takes one by driving over it, a point each; a bump
        /// shakes some of a rival's coins out, a fall loses more. The car with the most coins at the end wins.
        /// </summary>
        [Serializable]
        public sealed class GoldRushSettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("Seconds a round of the mode lasts at most; the car with the most coins wins")]
            [Min(10f)]
            [SerializeField]
            private float duration = 45f;

            [Tooltip("Coins a car carries that end the round at once, before its time; 0 for none")]
            [Min(0)]
            [SerializeField]
            private int pointsToWin = 25;

            [Tooltip("The most knock-outs a round pays XP and coins for: cars come back, so there are many")]
            [Min(0)]
            [SerializeField]
            private int knockoutRewardCap = 5;

            [Header("Coins on the floor")]
            [Tooltip("Seconds after GO! the first coins drop: one in front of every car and a heap in the middle")]
            [Min(0f)]
            [SerializeField]
            private float firstDelay = 0.4f;

            [Tooltip("Metres in front of each car, toward the middle, the coin of its own lands at the start")]
            [Min(0f)]
            [SerializeField]
            private float openingAhead = 4f;

            [Tooltip("Coins in the heap in the middle at the start")]
            [Min(0)]
            [SerializeField]
            private int openingMiddle = 5;

            [Tooltip("Seconds between two bursts of coins, give or take the jitter")]
            [Min(0.5f)]
            [SerializeField]
            private float burstInterval = 3.2f;

            [Tooltip("Seconds a burst comes earlier or later at most")]
            [Min(0f)]
            [SerializeField]
            private float burstJitter = 1f;

            [Tooltip("The fewest and the most coins of one burst")]
            [SerializeField]
            private Vector2Int burstSize = new(3, 5);

            [Tooltip("Metres from a burst's middle its coins land at most")]
            [Min(0f)]
            [SerializeField]
            private float burstSpread = 2.6f;

            [Tooltip("Metres a coin keeps from another as it lands, when there is room")]
            [Min(0f)]
            [SerializeField]
            private float coinSpacing = 1.3f;

            [Tooltip("The most coins of the bursts on the floor at once: a burst waits until there is room (shaken out " +
                "and spilled coins do not count)")]
            [Min(1)]
            [SerializeField]
            private int maxCoins = 18;

            [Tooltip("Metres a coin keeps from the edge and from the pits: one is never taken by driving off")]
            [Min(0f)]
            [SerializeField]
            private float edgeMargin = 2.5f;

            [Tooltip("Metres a burst's middle keeps from every car, so a burst is driven to, not dropped on a car")]
            [Min(0f)]
            [SerializeField]
            private float carClearance = 3f;

            [Tooltip("Seconds a coin of a burst lies on the floor before it fades; it blinks the last seconds")]
            [Min(1f)]
            [SerializeField]
            private float lifetime = 14f;

            [Tooltip("Seconds a shaken out or spilled coin lies on the floor: a short scramble, not a hoard")]
            [Min(1f)]
            [SerializeField]
            private float spillLifetime = 7f;

            [Tooltip("Seconds before a coin fades that it blinks")]
            [Min(0f)]
            [SerializeField]
            private float blinkTime = 3f;

            [Tooltip("The chance a burst lands near the player, on Easy and on Normal: a new player gets coins to drive to")]
            [SerializeField]
            private Vector2 playerLean = new(0.55f, 0.15f);

            [Tooltip("Metres from the player a burst leaning to them lands, nearest and farthest")]
            [SerializeField]
            private Vector2 leanRange = new(4f, 9f);

            [Header("Taking coins")]
            [Tooltip("Metres from a car's middle a coin is taken at")]
            [Min(0.1f)]
            [SerializeField]
            private float pickRadius = 1.6f;

            [Tooltip("Metres from the player's car a coin slides to it: driving near is enough")]
            [Min(0f)]
            [SerializeField]
            private float playerMagnet = 2.8f;

            [Tooltip("Metres a second a coin slides to the player's car")]
            [Min(0f)]
            [SerializeField]
            private float magnetSpeed = 9f;

            [Tooltip("Seconds two coins may be apart to count as one run: each coin of a run rings a little higher")]
            [Min(0f)]
            [SerializeField]
            private float runWindow = 1.2f;

            [Header("Bumps and falls")]
            [Tooltip("Metres a second a rival must drive in for the bump to shake coins out")]
            [Min(0f)]
            [SerializeField]
            private float shakeStrength = 4f;

            [Tooltip("The share of a car's coins a bump shakes out, from a bump at the least strength to one at full speed")]
            [SerializeField]
            private Vector2 shakeShare = new(0.2f, 0.4f);

            [Tooltip("The most coins one bump shakes out")]
            [Min(1)]
            [SerializeField]
            private int shakeMax = 6;

            [Tooltip("How many times more coins a bump by the player shakes out")]
            [Min(0f)]
            [SerializeField]
            private float fromPlayer = 1.5f;

            [Tooltip("The share of the coins the player loses to a bump, on Easy and on Normal")]
            [SerializeField]
            private Vector2 playerShake = new(0.5f, 0.85f);

            [Tooltip("Seconds after losing coins a car loses none to another bump and cannot take its own back")]
            [Min(0f)]
            [SerializeField]
            private float shakeCooldown = 1.2f;

            [Tooltip("Metres from the car shaken out coins land, nearest and farthest")]
            [SerializeField]
            private Vector2 spillDistance = new(2f, 4.2f);

            [Tooltip("Seconds a coin flies from the car to the floor; nobody takes it in the air")]
            [Min(0.05f)]
            [SerializeField]
            private float spillTime = 0.55f;

            [Tooltip("Metres a flying coin rises at most")]
            [Min(0f)]
            [SerializeField]
            private float spillHeight = 2.2f;

            [Tooltip("The share of a car's coins a fall loses, spilled on the floor where it went off")]
            [Range(0f, 1f)]
            [SerializeField]
            private float fallShare = 0.5f;

            [Tooltip("The share of the coins the player loses to a fall, on Easy and on Normal")]
            [SerializeField]
            private Vector2 playerFall = new(0.5f, 0.9f);

            [Tooltip("The most coins one fall spills on the floor: the rest of the loss is lost for good")]
            [Min(0)]
            [SerializeField]
            private int fallSpillMax = 8;

            [Header("Bots")]
            [Tooltip("Metres a bot looks for a coin within")]
            [Min(0f)]
            [SerializeField]
            private float botRange = 18f;

            [Tooltip("A bot leaves a coin to another bot this many times nearer to it, so they spread over the coins")]
            [Min(1f)]
            [SerializeField]
            private float botYield = 1.3f;

            [Tooltip("The throttle a bot drives to a coin at, on Easy and on Normal")]
            [SerializeField]
            private Vector2 botPace = new(0.78f, 1f);

            [Tooltip("Coins a car must carry for the bots to go after it")]
            [Min(1)]
            [SerializeField]
            private int huntFrom = 5;

            [Tooltip("How many more coins than its own a car must carry for a bot to go after it")]
            [Min(0)]
            [SerializeField]
            private int huntLead = 2;

            [Tooltip("How many bots at most go after a rich car, on Easy and on Normal")]
            [SerializeField]
            private Vector2 botHunters = new(1f, 2f);

            [Tooltip("The most bots going after the player at once, however rich")]
            [Min(0)]
            [SerializeField]
            private int maxPlayerHunters = 1;

            [Tooltip("Score a coin a car carries adds to a bot's wish to bump it, when the bot picks a target the usual way")]
            [Min(0f)]
            [SerializeField]
            private float targetWeight = 0.06f;

            [Tooltip("The most the coins a car carries add to a bot's wish to bump it")]
            [Min(0f)]
            [SerializeField]
            private float targetCap = 0.7f;

            [Tooltip("Seconds between a bot's choices of a coin or a rich car")]
            [Min(0.05f)]
            [SerializeField]
            private float botThink = 0.45f;

            public bool Enabled => enabled;
            public float Duration => duration;
            public int PointsToWin => pointsToWin;
            public int KnockoutRewardCap => knockoutRewardCap;
            public float FirstDelay => firstDelay;
            public float OpeningAhead => openingAhead;
            public int OpeningMiddle => openingMiddle;
            public float BurstInterval => burstInterval;
            public float BurstJitter => burstJitter;
            public Vector2Int BurstSize => burstSize;
            public float BurstSpread => burstSpread;
            public float CoinSpacing => coinSpacing;
            public int MaxCoins => maxCoins;
            public float EdgeMargin => edgeMargin;
            public float CarClearance => carClearance;
            public float Lifetime => lifetime;
            public float SpillLifetime => spillLifetime;
            public float BlinkTime => blinkTime;
            public Vector2 LeanRange => leanRange;
            public float PickRadius => pickRadius;
            public float PlayerMagnet => playerMagnet;
            public float MagnetSpeed => magnetSpeed;
            public float RunWindow => runWindow;
            public float ShakeStrength => shakeStrength;
            public int ShakeMax => shakeMax;
            public float FromPlayer => fromPlayer;
            public float ShakeCooldown => shakeCooldown;
            public Vector2 SpillDistance => spillDistance;
            public float SpillTime => spillTime;
            public float SpillHeight => spillHeight;
            public float FallShare => fallShare;
            public int FallSpillMax => fallSpillMax;
            public float BotRange => botRange;
            public float BotYield => botYield;
            public int HuntFrom => huntFrom;
            public int HuntLead => huntLead;
            public int MaxPlayerHunters => maxPlayerHunters;
            public float TargetWeight => targetWeight;
            public float TargetCap => targetCap;
            public float BotThink => botThink;

            /// <summary>The chance a burst leans to the player at a difficulty level (0 Easy, 1 Normal).</summary>
            public float PlayerLeanAt(float level) => Mathf.Lerp(playerLean.x, playerLean.y, level);

            /// <summary>The share of a car's coins a bump at <paramref name="strength"/> shakes out, up to full speed.</summary>
            public float ShakeShareAt(float strength, float fullSpeed) =>
                Mathf.Lerp(shakeShare.x, shakeShare.y, Mathf.InverseLerp(shakeStrength, Mathf.Max(shakeStrength + 0.01f,
                    fullSpeed), strength));

            /// <summary>The share of the player's coins a bump takes at a difficulty level.</summary>
            public float PlayerShakeAt(float level) => Mathf.Lerp(playerShake.x, playerShake.y, level);

            /// <summary>The share of the player's fall loss at a difficulty level.</summary>
            public float PlayerFallAt(float level) => Mathf.Lerp(playerFall.x, playerFall.y, level);

            /// <summary>A bot's throttle to a coin at a difficulty level.</summary>
            public float BotPaceAt(float level) => Mathf.Lerp(botPace.x, botPace.y, level);

            /// <summary>How many bots go after a rich car at a difficulty level.</summary>
            public int BotHuntersAt(float level) => Mathf.RoundToInt(Mathf.Lerp(botHunters.x, botHunters.y, level));

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(fall: FallRule.Respawn, floor: FloorRule.Still,
                mechanics: false, pickups: PickupRule.None, duration: duration, knockoutRewardCap: knockoutRewardCap,
                pointsToWin: pointsToWin);
        }
    }
}
