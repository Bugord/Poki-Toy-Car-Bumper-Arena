using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The abilities lying on the floor of a long match, apart from the main file so they can change side by side with
    /// the match and the floor: <see cref="PickupSettings"/>. A round of last-car-standing has none.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Pickups")]
        [Tooltip("The abilities lying on the floor of a long match: how often and where they appear, and what each does")]
        [SerializeField]
        private PickupSettings pickups = new();

        public PickupSettings Pickups => pickups;

        /// <summary>
        /// When and where pickups appear, how a car takes one, what each ability does and how the bots go for them. They
        /// lie on the floor only while it is whole or shrinking, never at the peak or as it comes back.
        /// </summary>
        [Serializable]
        public sealed class PickupSettings
        {
            [Tooltip("Whether a long match has pickups at all; a round of last-car-standing never does")]
            [SerializeField]
            private bool enabled = true;

            [Header("Appearing")]
            [Tooltip("Seconds into the match, and into each cycle of the floor, before the first pickup appears")]
            [Min(0f)]
            [SerializeField]
            private float firstDelay = 3f;

            [Tooltip("Seconds between one pickup appearing and the next")]
            [Min(0.5f)]
            [SerializeField]
            private float interval = 6.5f;

            [Tooltip("Seconds the interval varies by, either way, at random")]
            [Min(0f)]
            [SerializeField]
            private float jitter = 1.5f;

            [Tooltip("Most pickups lying on the floor at once")]
            [Range(1, 4)]
            [SerializeField]
            private int maxCount = 2;

            [Tooltip("Seconds a pickup lies there before it fades away unclaimed")]
            [Min(1f)]
            [SerializeField]
            private float lifetime = 16f;

            [Tooltip("Metres a pickup lies inside the safe edge of the floor (the edge of shaking tiles, rims, pits and holes " +
                "alike), so a car taking it is not at the edge")]
            [Min(0f)]
            [SerializeField]
            private float edgeMargin = 3.5f;

            [Tooltip("A pickup lying closer than this, in metres, to the safe edge fades away: a wave has begun to shake " +
                "under it")]
            [Min(0f)]
            [SerializeField]
            private float vanishMargin = 1f;

            [Tooltip("Metres a pickup lies from every car on the floor, so it does not appear under one")]
            [Min(0f)]
            [SerializeField]
            private float carClearance = 4f;

            [Tooltip("Metres between two pickups lying at once")]
            [Min(0f)]
            [SerializeField]
            private float spacing = 7f;

            [Tooltip("How close, in metres, a car on the floor must come to take a pickup")]
            [Min(0.5f)]
            [SerializeField]
            private float pickRadius = 1.9f;

            [Header("Helping the losing")]
            [Tooltip("Chance, for each knock-out the leader is ahead of the car furthest behind, that a pickup appears " +
                "beside a car behind instead of anywhere; the car is picked by how far behind it is")]
            [Range(0f, 1f)]
            [SerializeField]
            private float leanPerKnockout = 0.2f;

            [Tooltip("Most that chance can come to")]
            [Range(0f, 1f)]
            [SerializeField]
            private float leanMax = 0.8f;

            [Tooltip("Metres from the car it leans to, nearest and furthest, a pickup appears")]
            [SerializeField]
            private Vector2 leanRange = new(4f, 9f);

            [Header("Kinds")]
            [Tooltip("How likely each kind is, against the others, in this order: heavy, shockwave, shield, turbo")]
            [SerializeField]
            private float[] kindWeights = { 1f, 1f, 1f, 1f };

            [SerializeField]
            private HeavySettings heavy = new();

            [SerializeField]
            private ShockwaveSettings shockwave = new();

            [SerializeField]
            private ShieldSettings shield = new();

            [SerializeField]
            private TurboSettings turbo = new();

            [Header("Bots")]
            [SerializeField]
            private PickupBotSettings bots = new();

            public bool Enabled => enabled;
            public float FirstDelay => firstDelay;
            public float Interval => interval;
            public float Jitter => jitter;
            public int MaxCount => maxCount;
            public float Lifetime => lifetime;
            public float EdgeMargin => edgeMargin;
            public float VanishMargin => vanishMargin;
            public float CarClearance => carClearance;
            public float Spacing => spacing;
            public float PickRadius => pickRadius;
            public float LeanPerKnockout => leanPerKnockout;
            public float LeanMax => leanMax;
            public Vector2 LeanRange => leanRange;
            public HeavySettings Heavy => heavy;
            public ShockwaveSettings Shockwave => shockwave;
            public ShieldSettings Shield => shield;
            public TurboSettings Turbo => turbo;
            public PickupBotSettings Bots => bots;

            /// <summary>How likely a kind is against the others; 1 for a kind the list does not reach.</summary>
            public float WeightOf(PickupKind kind)
            {
                var index = (int)kind - 1;
                return kindWeights != null && index >= 0 && index < kindWeights.Length ? Mathf.Max(0f, kindWeights[index]) : 1f;
            }

            /// <summary>How long the kind lasts, in seconds; 0 for one that acts once and leaves nothing running.</summary>
            public float DurationOf(PickupKind kind)
            {
                return kind switch
                {
                    PickupKind.Heavy => heavy.Duration,
                    PickupKind.Shield => shield.Duration,
                    PickupKind.Turbo => turbo.Duration,
                    _ => 0f,
                };
            }
        }

        /// <summary>The car weighs several times as much: it moves others, and is moved, as a heavy car does.</summary>
        [Serializable]
        public sealed class HeavySettings
        {
            [Tooltip("What the car's mass is multiplied by: in the push of its hits, which goes by the ratio of the masses, " +
                "and in the physics")]
            [Min(1f)]
            [SerializeField]
            private float massScale = 3f;

            [Tooltip("Seconds it lasts")]
            [Min(0.5f)]
            [SerializeField]
            private float duration = 6f;

            public float MassScale => massScale;
            public float Duration => duration;
        }

        /// <summary>A ring from the picker pushes every other car on the floor near it outward, credited to the picker.</summary>
        [Serializable]
        public sealed class ShockwaveSettings
        {
            [Tooltip("Metres from the picker the ring reaches")]
            [Min(0.5f)]
            [SerializeField]
            private float radius = 6f;

            [Tooltip("Speed, in metres per second, the ring pushes a car at the picker's side; it eases to the share below " +
                "at the edge of the ring. Never past a hit's most, Car.MaxBumpSpeed")]
            [Min(0f)]
            [SerializeField]
            private float push = 12f;

            [Tooltip("Share of the push a car at the edge of the ring gets")]
            [Range(0f, 1f)]
            [SerializeField]
            private float edgeShare = 0.5f;

            [Tooltip("Speed the ring throws a car up at, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float lift = 2f;

            [Tooltip("Seconds a car the ring hits is off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.6f;

            public float Radius => radius;
            public float Push => push;
            public float EdgeShare => edgeShare;
            public float Lift => lift;
            public float Stagger => stagger;
        }

        /// <summary>The next hit, of a car or of the arena, is absorbed: no push, no stagger, no knock-out credit.</summary>
        [Serializable]
        public sealed class ShieldSettings
        {
            [Tooltip("Seconds it lasts if nothing hits the car")]
            [Min(0.5f)]
            [SerializeField]
            private float duration = 10f;

            [Tooltip("Closing speed, in metres per second, a car must hit at for the shield to take it; a touch below is " +
                "let through without a mark and keeps the shield")]
            [Min(0f)]
            [SerializeField]
            private float minStrength = 1.5f;

            [Tooltip("Seconds after the shield takes a hit during which the car that made it, still pressing, is not " +
                "credited with a knock-out")]
            [Min(0f)]
            [SerializeField]
            private float grace = 0.5f;

            public float Duration => duration;
            public float MinStrength => minStrength;
            public float Grace => grace;
        }

        /// <summary>The boost can be fired again almost as soon as the last one ends.</summary>
        [Serializable]
        public sealed class TurboSettings
        {
            [Tooltip("Seconds it lasts")]
            [Min(0.5f)]
            [SerializeField]
            private float duration = 5f;

            [Tooltip("Seconds between boosts while it lasts, in place of the boost's cooldown")]
            [Min(0f)]
            [SerializeField]
            private float boostCooldown = 0.3f;

            public float Duration => duration;
            public float BoostCooldown => boostCooldown;
        }

        /// <summary>How the bots value a pickup against a car to hit.</summary>
        [Serializable]
        public sealed class PickupBotSettings
        {
            [Tooltip("What a pickup counts for in a bot's pick, against the cars to hit, before distance")]
            [SerializeField]
            private float weight = 0.6f;

            [Tooltip("What a pickup counts for more, times how far behind the leader in knock-outs the bot is (a share of " +
                "the leader's, 0 to 1)")]
            [SerializeField]
            private float trailingBonus = 0.6f;

            [Tooltip("What a pickup counts against it for each other bot already going for it")]
            [SerializeField]
            private float crowdPenalty = 0.5f;

            [Tooltip("Metres beyond which a bot does not go for a pickup at all")]
            [Min(0f)]
            [SerializeField]
            private float range = 20f;

            public float Weight => weight;
            public float TrailingBonus => trailingBonus;
            public float CrowdPenalty => crowdPenalty;
            public float Range => range;
        }
    }
}
