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
            public const int KindCount = 9;

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

            [Tooltip("How likely each item is for a car level with the leader (or leading), in this order: mega, " +
                "shockwave, shield, turbo, bomb, glove, bowling, balloon, oil. A round, which keeps no score, always " +
                "rolls this row. Shield, turbo and glove are weighted out since v0.7.1: the players did not take to them. " +
                "A list shorter than the kinds gives the missing ones a weight of 1")]
            [SerializeField]
            private float[] leaderWeights = { 1f, 1f, 0f, 0f, 1f, 0f, 1f, 0.7f, 0.8f };

            [Tooltip("How likely each item is for a car far behind the leader, same order: the items that knock cars " +
                "off (shockwave, bomb, bowling) and the balloon that saves a fall come up more, to catch up")]
            [SerializeField]
            private float[] trailingWeights = { 0.5f, 1.3f, 0f, 0f, 1.5f, 0f, 1.4f, 1.1f, 0.6f };

            [Tooltip("Share of an item's push and stagger the player takes on the easiest match (blended to all of it " +
                "by the difficulty), as for the arena's hazards")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.6f;

            [Header("Items")]
            [SerializeField]
            private MegaSettings mega = new();

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

            [SerializeField]
            private BowlingSettings bowling = new();

            [SerializeField]
            private BalloonSettings balloon = new();

            [SerializeField]
            private OilSettings oil = new();

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
            public MegaSettings Mega => mega;
            public ShockwaveSettings Shockwave => shockwave;
            public ShieldSettings Shield => shield;
            public TurboSettings Turbo => turbo;
            public BombSettings Bomb => bomb;
            public GloveSettings Glove => glove;
            public BowlingSettings Bowling => bowling;
            public BalloonSettings Balloon => balloon;
            public OilSettings Oil => oil;
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
                    ItemKind.Mega => mega.Duration,
                    ItemKind.Shield => shield.Duration,
                    ItemKind.Turbo => turbo.Duration,
                    ItemKind.Balloon => balloon.Duration,
                    _ => 0f,
                };
            }

            private static float Row(float[] weights, int index)
            {
                return weights != null && index >= 0 && index < weights.Length ? Mathf.Max(0f, weights[index]) : 1f;
            }
        }

        /// <summary>
        /// The car grows big and heavy: its model and its collider grow by the size scale, so it is hard to miss and
        /// covers more of the floor, and its mass, which the push of its hits goes by, by the mass scale.
        /// </summary>
        [Serializable]
        public sealed class MegaSettings
        {
            [Tooltip("What the car's mass is multiplied by: in the push of its hits, which goes by the ratio of the masses, " +
                "and in the physics")]
            [Min(1f)]
            [SerializeField]
            private float massScale = 3f;

            [Tooltip("What the car's size is multiplied by, the model and the collider alike")]
            [Range(1f, 2.5f)]
            [SerializeField]
            private float sizeScale = 1.6f;

            [Tooltip("Seconds the car takes to grow, and to shrink back: growing at once into a car beside it would " +
                "fling that car away")]
            [Min(0.01f)]
            [SerializeField]
            private float growTime = 0.3f;

            [Tooltip("Seconds it lasts")]
            [Min(0.5f)]
            [SerializeField]
            private float duration = 6f;

            public float MassScale => massScale;
            public float SizeScale => sizeScale;
            public float GrowTime => growTime;
            public float Duration => duration;
        }

        /// <summary>
        /// A bowling ball rolled out of the car's nose straight along its heading: it rolls across the floor, knocks aside
        /// every car on its line once (credited to the roller) and keeps rolling, until it rolls off the floor and falls.
        /// </summary>
        [Serializable]
        public sealed class BowlingSettings
        {
            [Tooltip("Radius of the ball in metres")]
            [Min(0.2f)]
            [SerializeField]
            private float radius = 0.9f;

            [Tooltip("Speed it rolls at, in metres per second")]
            [Min(1f)]
            [SerializeField]
            private float speed = 17f;

            [Tooltip("Metres it rolls at most before it fades, if it never leaves the floor")]
            [Min(1f)]
            [SerializeField]
            private float range = 40f;

            [Tooltip("Metres from the ball's line a car's middle may be and still be hit, besides the ball's radius")]
            [Min(0f)]
            [SerializeField]
            private float hitReach = 1f;

            [Tooltip("Speed, in metres per second, a hit adds to a car")]
            [Min(0f)]
            [SerializeField]
            private float push = 15f;

            [Tooltip("Share of the push that goes out to the side of the ball's line the car is on; the rest goes along " +
                "the line, so a car is bowled aside and not only carried ahead")]
            [Range(0f, 1f)]
            [SerializeField]
            private float sideShare = 0.55f;

            [Tooltip("The most a hit may leave a car going, as a share of Car.MaxBumpSpeed")]
            [Min(0.5f)]
            [SerializeField]
            private float maxSpeedShare = 1.3f;

            [Tooltip("Speed a hit throws a car up at, in metres per second")]
            [Min(0f)]
            [SerializeField]
            private float lift = 3.4f;

            [Tooltip("Seconds a car the ball hits is off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.7f;

            public float Radius => radius;
            public float Speed => speed;
            public float Range => range;
            public float HitReach => hitReach;
            public float Push => push;
            public float SideShare => sideShare;
            public float MaxSpeedShare => maxSpeedShare;
            public float Lift => lift;
            public float Stagger => stagger;
        }

        /// <summary>
        /// A balloon tied to the car for a few seconds: a fall off the edge while it lasts is caught, and the balloon
        /// carries the car up and back over safe floor, drops it there and pops.
        /// </summary>
        [Serializable]
        public sealed class BalloonSettings
        {
            [Tooltip("Seconds it lasts if the car does not fall")]
            [Min(0.5f)]
            [SerializeField]
            private float duration = 10f;

            [Tooltip("Metres below the top of the floor a car off the floor must sink to before the balloon catches it; " +
                "well above Round.FallHeight, so the round never takes it out first")]
            [Min(0.05f)]
            [SerializeField]
            private float catchDepth = 0.4f;

            [Tooltip("Seconds the balloon takes to carry the car back")]
            [Min(0.2f)]
            [SerializeField]
            private float carryTime = 1.2f;

            [Tooltip("Metres above the floor the car is carried at, over the other cars")]
            [Min(1f)]
            [SerializeField]
            private float carryHeight = 4.5f;

            [Tooltip("Metres above the floor the balloon lets the car go")]
            [Min(0f)]
            [SerializeField]
            private float dropHeight = 1.5f;

            [Tooltip("Metres inside the safe edge the car is set down")]
            [Min(0f)]
            [SerializeField]
            private float landMargin = 3f;

            [Tooltip("Metres from every other car the car is set down")]
            [Min(0f)]
            [SerializeField]
            private float landClearance = 3f;

            public float Duration => duration;
            public float CatchDepth => catchDepth;
            public float CarryTime => carryTime;
            public float CarryHeight => carryHeight;
            public float DropHeight => dropHeight;
            public float LandMargin => landMargin;
            public float LandClearance => landClearance;
        }

        /// <summary>
        /// A slick of oil spilled on the floor behind the car: every car on it slides (little grip) and a car that crosses
        /// it counts as hit by the spiller, so a slide off the edge is the spiller's knock-out. A car that drove through it
        /// leaves oily tyre tracks for a while.
        /// </summary>
        [Serializable]
        public sealed class OilSettings
        {
            [Tooltip("Metres behind the car's middle the slick is spilled")]
            [Min(0f)]
            [SerializeField]
            private float dropBehind = 2.6f;

            [Tooltip("Metres from the car within which another car in play gets the slick under its nose instead")]
            [Min(0f)]
            [SerializeField]
            private float targetRange = 12f;

            [Tooltip("Metres ahead of the targeted car, along where it drives, the slick is spilled")]
            [Min(0f)]
            [SerializeField]
            private float dropAhead = 2.5f;

            [Tooltip("Radius of the slick in metres")]
            [Min(0.5f)]
            [SerializeField]
            private float radius = 2.6f;

            [Tooltip("Seconds the slick lies there")]
            [Min(1f)]
            [SerializeField]
            private float lifetime = 9f;

            [Tooltip("Metres over which the slick's feel fades in at its rim")]
            [Min(0.01f)]
            [SerializeField]
            private float blendWidth = 0.5f;

            [Tooltip("What a car's grip is multiplied by on the slick")]
            [Range(0.02f, 1f)]
            [SerializeField]
            private float grip = 0.12f;

            [Tooltip("What a car's top speed is multiplied by on the slick")]
            [Range(0.2f, 2f)]
            [SerializeField]
            private float speed = 1.1f;

            [Tooltip("What a car's acceleration is multiplied by on the slick: the wheels spin")]
            [Range(0.05f, 2f)]
            [SerializeField]
            private float acceleration = 0.45f;

            [Tooltip("What the push of a hit taken on the slick is multiplied by")]
            [Range(0.2f, 3f)]
            [SerializeField]
            private float push = 1.3f;

            [Tooltip("Seconds a car's tyres stay oily after it leaves the slick, leaving tracks")]
            [Min(0f)]
            [SerializeField]
            private float oilyTime = 3.5f;

            public float DropBehind => dropBehind;
            public float TargetRange => targetRange;
            public float DropAhead => dropAhead;
            public float Radius => radius;
            public float Lifetime => lifetime;
            public float BlendWidth => blendWidth;
            public float Grip => grip;
            public float Speed => speed;
            public float Acceleration => acceleration;
            public float Push => push;
            public float OilyTime => oilyTime;
        }

        /// <summary>A ring from the user pushes every other car on the floor near it outward, credited to the user.</summary>
        [Serializable]
        public sealed class ShockwaveSettings
        {
            [Tooltip("Metres from the user the ring reaches")]
            [Min(0.5f)]
            [SerializeField]
            private float radius = 9f;

            [Tooltip("Speed, in metres per second, the ring pushes a car at the user's side; it falls to the share below " +
                "at the edge of the ring, steeper with the falloff")]
            [Min(0f)]
            [SerializeField]
            private float push = 22f;

            [Tooltip("Share of the push a car at the edge of the ring gets")]
            [Range(0f, 1f)]
            [SerializeField]
            private float edgeShare = 0.35f;

            [Tooltip("How steeply the push falls from the middle to the edge: 1 falls evenly, more keeps the full push " +
                "only right by the middle")]
            [Min(1f)]
            [SerializeField]
            private float falloff = 1.5f;

            [Tooltip("The most the push may leave a car going, as a share of Car.MaxBumpSpeed: a blast throws harder " +
                "than a hit")]
            [Min(0.5f)]
            [SerializeField]
            private float maxSpeedShare = 1.4f;

            [Tooltip("Speed the ring throws a car up at in the middle, in metres per second; it eases like the push")]
            [Min(0f)]
            [SerializeField]
            private float lift = 4.7f;

            [Tooltip("Seconds a car the ring hits is off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.6f;

            public float Radius => radius;
            public float Push => push;
            public float EdgeShare => edgeShare;
            public float Falloff => falloff;
            public float MaxSpeedShare => maxSpeedShare;
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

            [Tooltip("A bomb landing this close to another car in play goes off at once, without its fuse: a hit is " +
                "a hit, not a warning to drive away from")]
            [Min(0f)]
            [SerializeField]
            private float impactRadius = 2.5f;

            [Tooltip("Metres from the bomb the blast reaches")]
            [Min(0.5f)]
            [SerializeField]
            private float radius = 4.5f;

            [Tooltip("Speed, in metres per second, the blast pushes a car right by the bomb; it falls to the share below " +
                "at the edge, steeper with the falloff")]
            [Min(0f)]
            [SerializeField]
            private float push = 24f;

            [Tooltip("Share of the push a car at the edge of the blast gets")]
            [Range(0f, 1f)]
            [SerializeField]
            private float edgeShare = 0.35f;

            [Tooltip("How steeply the push falls from the middle to the edge: 1 falls evenly, more keeps the full push " +
                "only right by the middle")]
            [Min(1f)]
            [SerializeField]
            private float falloff = 1.5f;

            [Tooltip("The most the push may leave a car going, as a share of Car.MaxBumpSpeed: a blast throws harder " +
                "than a hit")]
            [Min(0.5f)]
            [SerializeField]
            private float maxSpeedShare = 1.4f;

            [Tooltip("Speed the blast throws a car up at in the middle, in metres per second; it eases like the push")]
            [Min(0f)]
            [SerializeField]
            private float lift = 6.8f;

            [Tooltip("Seconds a car the blast hits is off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.7f;

            public float ThrowDistance => throwDistance;
            public float FlightTime => flightTime;
            public float ArcHeight => arcHeight;
            public float Fuse => fuse;
            public float ImpactRadius => impactRadius;
            public float Radius => radius;
            public float Push => push;
            public float EdgeShare => edgeShare;
            public float Falloff => falloff;
            public float MaxSpeedShare => maxSpeedShare;
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
            private float lift = 2f;

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

            [Tooltip("Metres ahead within which a target makes a bot use a mega or a turbo")]
            [Min(0f)]
            [SerializeField]
            private float pushRange = 6f;

            [Tooltip("Degrees off the nose a target may be and still count as ahead, for the mega, the turbo and the bomb")]
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

            [Tooltip("Metres, least and most, ahead a target must be for a bot to roll the bowling ball at it")]
            [SerializeField]
            private Vector2 bowlingRange = new(3f, 16f);

            [Tooltip("Degrees off the nose a target may be for a bot to roll the bowling ball at it")]
            [Range(0f, 45f)]
            [SerializeField]
            private float bowlingAngle = 10f;

            [Tooltip("Metres behind a bot within which a car chasing it makes it spill its oil")]
            [Min(0f)]
            [SerializeField]
            private float oilChaser = 9f;

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

            [Tooltip("Seconds after a bot's item went at the player before another bot's may: one at a time, so the items " +
                "that waited out the round's grace do not all go off at once. Longer than a bomb's flight and fuse, so the " +
                "next one does not land on a player still staggered by the last")]
            [Min(0f)]
            [SerializeField]
            private float playerItemGap = 4f;

            public float Weight => weight;
            public float TrailingBonus => trailingBonus;
            public float CrowdPenalty => crowdPenalty;
            public float Range => range;
            public Vector2 UseDelay => useDelay;
            public float PushRange => pushRange;
            public float AheadAngle => aheadAngle;
            public float PunchAngle => punchAngle;
            public Vector2 BombRange => bombRange;
            public Vector2 BowlingRange => bowlingRange;
            public float BowlingAngle => bowlingAngle;
            public float OilChaser => oilChaser;
            public int ShockwaveCars => shockwaveCars;
            public float ShieldThreat => shieldThreat;
            public float ShieldThreatSpeed => shieldThreatSpeed;
            public float PlayerItemGap => playerItemGap;
        }
    }
}
