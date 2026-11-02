using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The mystery boxes lying on the floor and the items they give, apart from the main file so they can change side by
    /// side with the match and the floor: <see cref="PickupSettings"/>. A long match has them from the start, a round of
    /// last-car-standing from <see cref="PickupSettings.FirstRound"/> of a visit, so the first round stays plain.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Pickups")]
        [Tooltip("The mystery boxes lying on the floor of a long match: how often and where they appear, which item " +
            "they give and what each item does")]
        [SerializeField]
        private PickupSettings pickups = new();

        public PickupSettings Pickups => pickups;

        /// <summary>
        /// When and where boxes appear, how a car takes one, how the item in it is rolled, what each item does once used
        /// and how the bots go for boxes and use items. Boxes lie on the floor only while it is whole or shrinking, never
        /// at the peak or as it comes back. The defaults are for a match of a minute in two floor cycles of 30 s: the
        /// first boxes a few seconds in, then two or three on the floor at a time.
        /// </summary>
        [Serializable]
        public sealed class PickupSettings
        {
            /// <summary>The kinds an item can be, in the order of the weight lists.</summary>
            public const int KindCount = 6;

            [Tooltip("Whether there are boxes at all")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("Round of a visit from which a round of last-car-standing has boxes, so a new player learns to " +
                "drive and push first; a long match always has them. 0 never in a round")]
            [Min(0)]
            [SerializeField]
            private int firstRound = 2;

            [Header("Appearing")]
            [Tooltip("Seconds into the match, and into each cycle of the floor, before the first boxes appear")]
            [Min(0f)]
            [SerializeField]
            private float firstDelay = 3f;

            [Tooltip("Seconds between one box appearing and the next")]
            [Min(0.5f)]
            [SerializeField]
            private float interval = 4f;

            [Tooltip("Seconds the interval varies by, either way, at random")]
            [Min(0f)]
            [SerializeField]
            private float jitter = 1f;

            [Tooltip("Most boxes lying on the floor at once")]
            [Range(1, 4)]
            [SerializeField]
            private int maxCount = 3;

            [Tooltip("Seconds a box lies there before it fades away unclaimed")]
            [Min(1f)]
            [SerializeField]
            private float lifetime = 12f;

            [Tooltip("Metres a box lies inside the safe edge of the floor (the edge of shaking tiles, rims, pits and holes " +
                "alike), so a car taking it is not at the edge")]
            [Min(0f)]
            [SerializeField]
            private float edgeMargin = 3.5f;

            [Tooltip("A box lying closer than this, in metres, to the safe edge fades away: a wave has begun to shake " +
                "under it")]
            [Min(0f)]
            [SerializeField]
            private float vanishMargin = 1f;

            [Tooltip("Metres a box lies from every car on the floor, so it does not appear under one")]
            [Min(0f)]
            [SerializeField]
            private float carClearance = 4f;

            [Tooltip("Metres between two boxes lying at once")]
            [Min(0f)]
            [SerializeField]
            private float spacing = 6f;

            [Tooltip("How close, in metres, a car on the floor must come to break a box")]
            [Min(0.5f)]
            [SerializeField]
            private float pickRadius = 1.9f;

            [Header("Helping the losing")]
            [Tooltip("Chance, for each knock-out the leader is ahead of the car furthest behind, that a box appears " +
                "beside a car behind instead of anywhere; the car is picked by how far behind it is")]
            [Range(0f, 1f)]
            [SerializeField]
            private float leanPerKnockout = 0.2f;

            [Tooltip("Most that chance can come to")]
            [Range(0f, 1f)]
            [SerializeField]
            private float leanMax = 0.8f;

            [Tooltip("Metres from the car it leans to, nearest and furthest, a box appears")]
            [SerializeField]
            private Vector2 leanRange = new(4f, 9f);

            [Header("The roll")]
            [Tooltip("Seconds the item slot spins before it lands on the item; it cannot be used before")]
            [Min(0f)]
            [SerializeField]
            private float rollTime = 0.7f;

            [Tooltip("Knock-outs behind the leader at which a car rolls with the trailing weights alone; between level " +
                "and this, the two rows are blended")]
            [Min(1f)]
            [SerializeField]
            private float trailingGap = 3f;

            [Tooltip("How likely each item is for a car level with the leader (or leading), in this order: heavy, " +
                "shockwave, shield, turbo, bomb, glove. A round, which keeps no score, always rolls this row. Shield, " +
                "turbo and glove are weighted out since v0.7.1: the players did not take to them")]
            [SerializeField]
            private float[] leaderWeights = { 1f, 1f, 0f, 0f, 1f, 0f };

            [Tooltip("How likely each item is for a car far behind the leader, same order: the items that knock cars " +
                "off (shockwave, bomb, glove) come up more, to catch up")]
            [SerializeField]
            private float[] trailingWeights = { 0.7f, 1.3f, 0f, 0f, 1.5f, 0f };

            [Tooltip("Share of an item's push and stagger the player takes on the easiest match (blended to all of it " +
                "by the difficulty), as for the arena's hazards")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.6f;

            [Header("Items")]
            [SerializeField]
            private HeavySettings heavy = new();

            [SerializeField]
            private ShockwaveSettings shockwave = new();

            [SerializeField]
            private ShieldSettings shield = new();

            [SerializeField]
            private TurboSettings turbo = new();

            [SerializeField]
            private BombSettings bomb = new();

            [SerializeField]
            private GloveSettings glove = new();

            [Header("Bots")]
            [SerializeField]
            private PickupBotSettings bots = new();

            public bool Enabled => enabled;
            public int FirstRound => firstRound;
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
            public float RollTime => rollTime;
            public float TrailingGap => trailingGap;
            public float EasyPlayerShare => easyPlayerShare;
            public HeavySettings Heavy => heavy;
            public ShockwaveSettings Shockwave => shockwave;
            public ShieldSettings Shield => shield;
            public TurboSettings Turbo => turbo;
            public BombSettings Bomb => bomb;
            public GloveSettings Glove => glove;
            public PickupBotSettings Bots => bots;

            /// <summary>
            /// How likely an item of <paramref name="kind"/> is for a car <paramref name="trailing"/> of the way from level
            /// with the leader (0) to far behind (1): the two rows blended. 1 for a kind a list does not reach.
            /// </summary>
            public float WeightOf(ItemKind kind, float trailing)
            {
                var index = (int)kind - 1;
                return Mathf.Lerp(Row(leaderWeights, index), Row(trailingWeights, index), Mathf.Clamp01(trailing));
            }

            /// <summary>How long the kind runs once used, in seconds; 0 for one that acts once and leaves nothing running.</summary>
            public float DurationOf(ItemKind kind)
            {
                return kind switch
                {
                    ItemKind.Heavy => heavy.Duration,
                    ItemKind.Shield => shield.Duration,
                    ItemKind.Turbo => turbo.Duration,
                    _ => 0f,
                };
            }

            private static float Row(float[] weights, int index)
            {
                return weights != null && index >= 0 && index < weights.Length ? Mathf.Max(0f, weights[index]) : 1f;
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

        /// <summary>A ring from the user pushes every other car on the floor near it outward, credited to the user.</summary>
        [Serializable]
        public sealed class ShockwaveSettings
        {
            [Tooltip("Metres from the user the ring reaches")]
            [Min(0.5f)]
            [SerializeField]
            private float radius = 9f;

            [Tooltip("Speed, in metres per second, the ring pushes a car at the user's side; it eases to the share below " +
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

        /// <summary>The next hit, of a car, an item or the arena, is absorbed: no push, no stagger, no knock-out credit.</summary>
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

        /// <summary>
        /// A bomb thrown ahead of the car in an arc: it lands on the floor (or falls off it), blinks over a red ring that
        /// grows to its reach, and blows every car in the ring outward, credited to the thrower.
        /// </summary>
        [Serializable]
        public sealed class BombSettings
        {
            [Tooltip("Metres ahead of the car, along its nose, the bomb lands")]
            [Min(1f)]
            [SerializeField]
            private float throwDistance = 9f;

            [Tooltip("Seconds the bomb flies before it lands")]
            [Min(0.1f)]
            [SerializeField]
            private float flightTime = 0.6f;

            [Tooltip("Metres the arc rises above the line from the car to where it lands")]
            [Min(0f)]
            [SerializeField]
            private float arcHeight = 2.5f;

            [Tooltip("Seconds the bomb blinks on the floor before it goes off; the red ring on the floor grows meanwhile")]
            [Min(0.2f)]
            [SerializeField]
            private float fuse = 1f;

            [Tooltip("Metres from the bomb the blast reaches")]
            [Min(0.5f)]
            [SerializeField]
            private float radius = 4.5f;

            [Tooltip("Speed, in metres per second, the blast pushes a car right by the bomb; it eases to the share below " +
                "at the edge. Never past a hit's most, Car.MaxBumpSpeed")]
            [Min(0f)]
            [SerializeField]
            private float push = 13f;

            [Tooltip("Share of the push a car at the edge of the blast gets")]
            [Range(0f, 1f)]
            [SerializeField]
            private float edgeShare = 0.55f;

            [Tooltip("Speed the blast throws a car up at, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float lift = 3f;

            [Tooltip("Seconds a car the blast hits is off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.7f;

            public float ThrowDistance => throwDistance;
            public float FlightTime => flightTime;
            public float ArcHeight => arcHeight;
            public float Fuse => fuse;
            public float Radius => radius;
            public float Push => push;
            public float EdgeShare => edgeShare;
            public float Lift => lift;
            public float Stagger => stagger;
        }

        /// <summary>
        /// A boxing glove on a spring shoots out of the car's nose and snaps back; the first car it meets is punched
        /// along the punch, credited to the puncher.
        /// </summary>
        [Serializable]
        public sealed class GloveSettings
        {
            [Tooltip("Metres in front of the nose the glove reaches")]
            [Min(1f)]
            [SerializeField]
            private float reach = 6f;

            [Tooltip("Seconds the glove takes to shoot out to its reach")]
            [Min(0.02f)]
            [SerializeField]
            private float extendTime = 0.15f;

            [Tooltip("Seconds it stays out before it snaps back")]
            [Min(0f)]
            [SerializeField]
            private float holdTime = 0.06f;

            [Tooltip("Seconds it takes to snap back")]
            [Min(0.02f)]
            [SerializeField]
            private float retractTime = 0.2f;

            [Tooltip("Metres from the glove's line a car's middle may be and still be punched")]
            [Min(0.1f)]
            [SerializeField]
            private float hitRadius = 1.4f;

            [Tooltip("Speed, in metres per second, a punch adds to a car along the punch")]
            [Min(0f)]
            [SerializeField]
            private float push = 15f;

            [Tooltip("The most a punch may leave a car going, as a share of Car.MaxBumpSpeed: a punch is stronger than a " +
                "hit, but never flings a car across the arena")]
            [Min(0.5f)]
            [SerializeField]
            private float maxSpeedShare = 1.2f;

            [Tooltip("Speed the punch throws a car up at, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float lift = 1.5f;

            [Tooltip("Seconds a punched car is off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.8f;

            public float Reach => reach;
            public float ExtendTime => extendTime;
            public float HoldTime => holdTime;
            public float RetractTime => retractTime;
            public float HitRadius => hitRadius;
            public float Push => push;
            public float MaxSpeedShare => maxSpeedShare;
            public float Lift => lift;
            public float Stagger => stagger;

            /// <summary>Seconds from the punch to the glove back at the nose.</summary>
            public float Length => extendTime + holdTime + retractTime;
        }

        /// <summary>How the bots value a box against a car to hit, and when they use the item they hold.</summary>
        [Serializable]
        public sealed class PickupBotSettings
        {
            [Tooltip("What a box counts for in a bot's pick, against the cars to hit, before distance")]
            [SerializeField]
            private float weight = 0.6f;

            [Tooltip("What a box counts for more, times how far behind the leader in knock-outs the bot is (a share of " +
                "the leader's, 0 to 1)")]
            [SerializeField]
            private float trailingBonus = 0.6f;

            [Tooltip("What a box counts against it for each other bot already going for it")]
            [SerializeField]
            private float crowdPenalty = 0.5f;

            [Tooltip("Metres beyond which a bot does not go for a box at all")]
            [Min(0f)]
            [SerializeField]
            private float range = 20f;

            [Header("Using items")]
            [Tooltip("Seconds, least and most, a bot waits once the moment to use its item has come, so it does not use " +
                "it the very frame a machine would; the moment has to last that long")]
            [SerializeField]
            private Vector2 useDelay = new(0.2f, 0.7f);

            [Tooltip("Metres ahead within which a target makes a bot use a heavy or a turbo")]
            [Min(0f)]
            [SerializeField]
            private float pushRange = 6f;

            [Tooltip("Degrees off the nose a target may be and still count as ahead, for the heavy, the turbo and the bomb")]
            [Range(0f, 90f)]
            [SerializeField]
            private float aheadAngle = 30f;

            [Tooltip("Degrees off the nose a target may be for a bot to punch it with the glove")]
            [Range(0f, 45f)]
            [SerializeField]
            private float punchAngle = 14f;

            [Tooltip("Metres, least and most, ahead a target must be for a bot to throw a bomb at it")]
            [SerializeField]
            private Vector2 bombRange = new(7f, 11f);

            [Tooltip("Other cars in play within the shockwave's reach that make a bot use it")]
            [Min(1)]
            [SerializeField]
            private int shockwaveCars = 2;

            [Tooltip("Metres within which another car driving at a bot makes it raise its shield")]
            [Min(0f)]
            [SerializeField]
            private float shieldThreat = 4f;

            [Tooltip("Speed, in metres per second, that car must close in at")]
            [Min(0f)]
            [SerializeField]
            private float shieldThreatSpeed = 4f;

            public float Weight => weight;
            public float TrailingBonus => trailingBonus;
            public float CrowdPenalty => crowdPenalty;
            public float Range => range;
            public Vector2 UseDelay => useDelay;
            public float PushRange => pushRange;
            public float AheadAngle => aheadAngle;
            public float PunchAngle => punchAngle;
            public Vector2 BombRange => bombRange;
            public int ShockwaveCars => shockwaveCars;
            public float ShieldThreat => shieldThreat;
            public float ShieldThreatSpeed => shieldThreatSpeed;
        }
    }
}
