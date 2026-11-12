using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// A round that changes one rule (<see cref="SpecialRoundSettings"/>). Stored in the asset by number, so new ones go
    /// at the end.
    /// </summary>
    public enum SpecialRound
    {
        /// <summary>A plain round.</summary>
        None,

        /// <summary>Every car is giant and heavy for the whole round.</summary>
        Mega,

        /// <summary>
        /// The HOT POTATO mode of the menu, and a round of the rotation too: a bomb sits on one car and passes on a bump;
        /// when its fuse runs out its holder is out of the round. The floor is fenced, so nobody falls.
        /// </summary>
        HotPotato,

        /// <summary>One bot is a giant monster truck with a crown; knocking it off pays several knock-outs.</summary>
        Boss,

        /// <summary>The floor is ice: cars slide and take longer to get going.</summary>
        Ice,

        /// <summary>Gravity is weak: hits throw cars high and far.</summary>
        LowGravity,
    }

    /// <summary>
    /// The special rounds of a visit, kept apart from the main file: from the fourth round on (v0.13.5 lost 13–15% of
    /// the visits after each round from the third, and rounds read alike by then), every third round changes one rule
    /// and the lobby names it. Only in a build of last-car-standing rounds; a match has its own variety.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Special rounds")]
        [Tooltip("Rounds that change one rule, from a round of the visit on: which, how often and their numbers")]
        [SerializeField]
        private SpecialRoundSettings specialRounds = new();

        public SpecialRoundSettings SpecialRounds => specialRounds;

        [Serializable]
        public sealed class SpecialRoundSettings
        {
            [Tooltip("Whether a build of rounds plays special rounds at all")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("The visit's first round that is special; the first round is never")]
            [Min(2)]
            [SerializeField]
            private int firstRound = 4;

            [Tooltip("Every this many rounds from the first one is special")]
            [Min(1)]
            [SerializeField]
            private int every = 3;

            // Low gravity and the hot potato (after v0.17.9): the hot potato became a mode of the menu (EventMode) after
            // v0.17.4, but on v0.17.9 only 6 of 557 visits found it there (the first visit has no menu), so it is back in
            // the rotation as well, after low gravity: round 4 stays as v0.17.9 had it, round 7 is the hot potato. The
            // user took mega, boss and ice out; their code stays, reachable by Force.
            [Tooltip("The special rounds in the order they come, then again from the start")]
            [SerializeField]
            private SpecialRound[] order = { SpecialRound.LowGravity, SpecialRound.HotPotato };

            [Header("Mega")]
            [Tooltip("How many times its own size every car is in a mega round")]
            [Min(1f)]
            [SerializeField]
            private float megaSize = 1.5f;

            [Tooltip("How many times its own mass every car is in a mega round: all alike, so a hit pushes as before")]
            [Min(0.1f)]
            [SerializeField]
            private float megaMass = 2f;

            [Header("Ice")]
            [Tooltip("Grip on ice, times the car's own: a car slides on, turned or hit")]
            [Range(0.02f, 1f)]
            [SerializeField]
            private float iceGrip = 0.15f;

            [Tooltip("Acceleration on ice, times the car's own")]
            [Range(0.1f, 1f)]
            [SerializeField]
            private float iceAcceleration = 0.6f;

            [Tooltip("The extra push of a hit on ice, times the usual")]
            [Min(0f)]
            [SerializeField]
            private float icePush = 1.2f;

            [Header("Low gravity")]
            [Tooltip("Gravity in a low-gravity round, times the usual")]
            [Range(0.1f, 1f)]
            [SerializeField]
            private float gravityScale = 0.45f;

            [Header("Boss")]
            [Tooltip("The car the boss drives, by catalog id")]
            [SerializeField]
            private string bossCar = "monster-truck";

            [Tooltip("The topper the boss wears, by id; empty for none")]
            [SerializeField]
            private string bossTopper = "crown";

            [Tooltip("How many times its own size the boss is")]
            [Min(1f)]
            [SerializeField]
            private float bossSize = 1.8f;

            [Tooltip("How many times its own mass the boss is: a hit pushes it this much less")]
            [Min(0.1f)]
            [SerializeField]
            private float bossMass = 3f;

            [Tooltip("How many knock-outs the player's knock-out of the boss pays in XP and coins")]
            [Min(1)]
            [SerializeField]
            private int bossReward = 3;

            [Header("Hot potato")]
            [SerializeField]
            private HotPotatoSettings hotPotato = new();

            // A test or the dev console plays this kind in every round instead of the schedule; null for the schedule.
            [NonSerialized]
            private SpecialRound? forced;

            public bool Enabled => enabled;
            public int FirstRound => firstRound;
            public int Every => every;
            public float MegaSize => megaSize;
            public float MegaMass => megaMass;
            public float IceGrip => iceGrip;
            public float IceAcceleration => iceAcceleration;
            public float IcePush => icePush;
            public float GravityScale => gravityScale;
            public string BossCar => bossCar;
            public string BossTopper => bossTopper;
            public float BossSize => bossSize;
            public float BossMass => bossMass;
            public int BossReward => bossReward;
            public HotPotatoSettings HotPotato => hotPotato;

            /// <summary>
            /// Plays <paramref name="kind"/> in every round of the scenes built after this (<see cref="SpecialRound.None"/>
            /// for no special rounds at all), or the schedule again with null: in memory only, for the tests and the dev
            /// console.
            /// </summary>
            public void Force(SpecialRound? kind) => forced = kind;

            /// <summary>The special round of the visit's round <paramref name="round"/> (from 1).</summary>
            public SpecialRound For(int round)
            {
                if (forced.HasValue) {
                    return forced.Value;
                }

                if (!enabled || order == null || order.Length == 0 || round < firstRound
                    || (round - firstRound) % every != 0) {
                    return SpecialRound.None;
                }

                return order[(round - firstRound) / every % order.Length];
            }
        }

        /// <summary>
        /// The hot potato, the HOT POTATO mode of the menu since it left the special rounds: a bomb rides on one car and
        /// jumps to any car it bumps (never straight back to the car that passed it); when the fuse runs out its holder
        /// is out of the round (a knock-out for the car that passed it) and the cars beside it are nudged, and a new bomb
        /// lands on another car a moment later. The floor is fenced, so nobody falls; the last car left wins.
        /// </summary>
        [Serializable]
        public sealed class HotPotatoSettings
        {
            [Tooltip("Seconds from the round's start to the first bomb")]
            [Min(0f)]
            [SerializeField]
            private float firstDelay = 3f;

            [Tooltip("Seconds a bomb burns before it goes off, however often it passes")]
            [Min(1f)]
            [SerializeField]
            private float fuse = 8f;

            [Tooltip("Seconds from a blast to the next bomb")]
            [Min(0f)]
            [SerializeField]
            private float nextDelay = 2.5f;

            [Tooltip("Seconds a car that just got the bomb holds it before a bump can pass it on, so two cars in a " +
                "shove do not pass it back and forth every frame")]
            [Min(0f)]
            [SerializeField]
            private float passCooldown = 0.6f;

            [Tooltip("Metres per second the cars must close at for a touch to pass the bomb")]
            [Min(0f)]
            [SerializeField]
            private float passStrength = 1f;

            [Tooltip("Metres round the holder the blast nudges the other cars")]
            [Min(0f)]
            [SerializeField]
            private float radius = 4f;

            [Tooltip("Metres per second the blast pushes a car beside the holder at its middle, less towards its rim")]
            [Min(0f)]
            [SerializeField]
            private float push = 10f;

            [Tooltip("Metres per second the blast throws a car beside the holder up")]
            [Min(0f)]
            [SerializeField]
            private float lift = 3.5f;

            [Tooltip("Seconds the blast knocks a car beside the holder off balance")]
            [Min(0f)]
            [SerializeField]
            private float stagger = 0.6f;

            [Tooltip("Seconds of the fuse left from which the bomb flashes fast and the bots run for it")]
            [Min(0f)]
            [SerializeField]
            private float hurry = 3f;

            [Tooltip("Metres from the holder within which a bot without the bomb drives away from it")]
            [Min(0f)]
            [SerializeField]
            private float fleeRadius = 7f;

            [Header("Hot potato mode")]
            [Tooltip("Seconds the bomb cannot go back to the car that just passed it: no tag-backs")]
            [Min(0f)]
            [SerializeField]
            private float passBackTime = 1.5f;

            [Tooltip("Metres per second the blast throws its holder up as it leaves the round")]
            [Min(0f)]
            [SerializeField]
            private float outLift = 11f;

            [Tooltip("Seconds the blown-out holder flies before it is gone")]
            [Min(0.1f)]
            [SerializeField]
            private float outTime = 0.9f;

            [Tooltip("Metres high the red and white bumper round the floor stands")]
            [Min(0.1f)]
            [SerializeField]
            private float fenceHeight = 0.55f;

            [Tooltip("Metres thick the bumper and the wall on it are")]
            [Min(0.1f)]
            [SerializeField]
            private float fenceThickness = 0.4f;

            [Tooltip("Metres high the unseen wall on the bumper stands, so no hit lifts a car over")]
            [Min(0.5f)]
            [SerializeField]
            private float wallHeight = 3f;

            [Tooltip("Metres from the fence a fleeing bot starts to turn along it instead of into it")]
            [Min(0f)]
            [SerializeField]
            private float fenceMargin = 3f;

            public float FirstDelay => firstDelay;
            public float Fuse => fuse;
            public float NextDelay => nextDelay;
            public float PassCooldown => passCooldown;
            public float PassStrength => passStrength;
            public float Radius => radius;
            public float Push => push;
            public float Lift => lift;
            public float Stagger => stagger;
            public float Hurry => hurry;
            public float FleeRadius => fleeRadius;
            public float PassBackTime => passBackTime;
            public float OutLift => outLift;
            public float OutTime => outTime;
            public float FenceHeight => fenceHeight;
            public float FenceThickness => fenceThickness;
            public float WallHeight => wallHeight;
            public float FenceMargin => fenceMargin;
        }
    }
}
