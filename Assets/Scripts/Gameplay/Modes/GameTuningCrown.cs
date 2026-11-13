using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The CROWN event mode's numbers, kept in a file of its own so the mode's work touches only its own files. Not in
    /// the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("CROWN")]
        [Tooltip("The CROWN event mode: whether it is on and its numbers")]
        [SerializeField]
        private CrownSettings crown = new();

        public CrownSettings Crown => crown;

        /// <summary>
        /// CROWN: a crown lands in the middle of a fenced floor a moment after GO; the first car to reach it takes it,
        /// a bump on its holder steals it, and every whole second held is a point. The car with the most points when the
        /// time runs out wins. Nobody falls (as in the hot potato), so the crown is all there is to play for.
        /// </summary>
        [Serializable]
        public sealed class CrownSettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("Seconds a round of the mode lasts; the car that held the crown longest wins")]
            [Min(10f)]
            [SerializeField]
            private float duration = 60f;

            [Tooltip("The most knock-outs a round pays XP and coins for (nobody falls, so there are none; kept for the " +
                "framework's cap)")]
            [Min(0)]
            [SerializeField]
            private int knockoutRewardCap = 5;

            [Header("Crown")]
            [Tooltip("Seconds after GO the crown lands in the middle of the floor: long enough for the cars to see the " +
                "round start, short enough that the race to it is the round's first beat")]
            [Min(0f)]
            [SerializeField]
            private float firstDelay = 2f;

            [Tooltip("Metres from a car's middle to the loose crown's within which the car takes it")]
            [Min(0.5f)]
            [SerializeField]
            private float pickRadius = 2f;

            [Tooltip("Metres of floor the crown's spot keeps from the fence and the holes")]
            [Min(0f)]
            [SerializeField]
            private float spotMargin = 3f;

            [Header("Steal")]
            [Tooltip("Metres per second two cars must close in at for a bump to steal the crown from its holder: a bump, " +
                "not a graze")]
            [Min(0f)]
            [SerializeField]
            private float stealStrength = 1.5f;

            [Tooltip("Metres per second the thief must be driving at the holder just before the bump: a ram with a " +
                "run-up, from behind as well, not a nudge in a crowd")]
            [Min(0f)]
            [SerializeField]
            private float stealRunUp = 7f;

            [Tooltip("The run-up the player's ram needs, in metres per second: a little less than a bot's, so a player " +
                "who drives into the holder takes the crown")]
            [Min(0f)]
            [SerializeField]
            private float playerRunUp = 3f;

            [Tooltip("Seconds a car must keep pushing on the holder for the push to steal the crown: a long shove, not a " +
                "touch, so a holder that keeps moving keeps it")]
            [Min(0f)]
            [SerializeField]
            private float pressTime = 1.2f;

            [Tooltip("Seconds a new holder keeps the crown whatever hits it, so a pile-up does not pass it round and the " +
                "thief gets a start")]
            [Min(0f)]
            [SerializeField]
            private float stealCooldown = 2.5f;

            [Tooltip("Seconds after a bot takes the crown the player may steal it: sooner than another bot may, so the " +
                "player who lost it is in the chase at once")]
            [Min(0f)]
            [SerializeField]
            private float playerStealAfter = 1.5f;

            [Tooltip("Seconds the player keeps the crown it took whatever hits it, Easy to Normal: longer than a bot's " +
                "moment, so the player sees it has it and can drive off with it")]
            [SerializeField]
            private Vector2 playerKeep = new(4f, 3f);

            [Tooltip("Seconds the car the crown was just stolen from cannot steal it straight back")]
            [Min(0f)]
            [SerializeField]
            private float stealBackTime = 5f;

            [Tooltip("Seconds the player cannot steal the crown straight back after a bot took it: shorter than a bot's, " +
                "so losing it is the start of a chase")]
            [Min(0f)]
            [SerializeField]
            private float playerStealBack = 1.5f;

            [Tooltip("Metres per second the car robbed of the crown is knocked away from the thief, so the two part")]
            [Min(0f)]
            [SerializeField]
            private float stealPush = 6f;

            [Tooltip("Seconds the car robbed of the crown is off balance: the thief's head start")]
            [Min(0f)]
            [SerializeField]
            private float stealStagger = 0.6f;

            [Header("Holder")]
            [Tooltip("The holder's top speed and acceleration as a share of its own: the crown is heavy, so the others " +
                "catch it and the crown changes hands several times a round")]
            [Range(0.5f, 1f)]
            [SerializeField]
            private float holderSpeed = 0.85f;

            [Range(0.5f, 1f)]
            [SerializeField]
            private float holderAcceleration = 0.85f;

            [Tooltip("The player's top speed and acceleration as a share of its own while it holds the crown: as fast " +
                "as its hunters drive at it, so driving away keeps the crown and only a corner or a cut-off loses it")]
            [Range(0.5f, 1.2f)]
            [SerializeField]
            private float playerHolderSpeed = 1f;

            [Tooltip("The share of their pace the hunters drive at the holder while it cannot lose the crown yet: they " +
                "close in, they do not ram it round the floor")]
            [Range(0f, 1f)]
            [SerializeField]
            private float protectedPace = 0.6f;

            [Tooltip("How much harder the holder is pushed by a bump: it is easy to bowl over")]
            [Range(1f, 2f)]
            [SerializeField]
            private float holderPush = 1.2f;

            [Header("Bots")]
            [Tooltip("How many bots hunt the player while it holds the crown, Easy to Normal; the others keep their " +
                "distance on free floor round it")]
            [SerializeField]
            private Vector2Int playerHunters = new(1, 2);

            [Tooltip("How many bots hunt a bot that holds the crown, Easy to Normal; the others keep their distance on " +
                "free floor round it, so the crown does not change hands every second in a swarm")]
            [SerializeField]
            private Vector2Int botHunters = new(1, 3);

            [Tooltip("The pace the hunters drive at the player holding the crown, Easy to Normal: under the player's own, " +
                "so a player who drives away with it keeps it and only a corner or a cut-off loses it")]
            [SerializeField]
            private Vector2 hunterPlayerPace = new(0.7f, 0.95f);

            [Tooltip("Seconds ahead of the holder a bot that chases it aims, and one that cuts it off aims; the bots " +
                "take turns at the two, so they do not all drive at one point")]
            [SerializeField]
            private Vector2 lead = new(0.35f, 1.2f);

            [Tooltip("Metres from the holder a bot that does not hunt it keeps, on free floor away from the other cars: " +
                "near enough to cut in when the crown changes hands by it")]
            [Min(0f)]
            [SerializeField]
            private float shadowDistance = 9f;

            [Tooltip("The pace a bot holding the crown runs from the player at, Easy to Normal: under the player's own, so " +
                "a player who drives at it catches it")]
            [SerializeField]
            private Vector2 fleePlayerPace = new(0.7f, 1f);

            [Tooltip("Metres within which a bot holding the crown runs from a car")]
            [Min(0f)]
            [SerializeField]
            private float fleeRadius = 11f;

            [Tooltip("Metres of floor a bot holding the crown wants ahead before it runs that way: it slides along the " +
                "fence rather than into it")]
            [Min(0.5f)]
            [SerializeField]
            private float fenceMargin = 3f;

            public bool Enabled => enabled;
            public float Duration => duration;
            public int KnockoutRewardCap => knockoutRewardCap;
            public float FirstDelay => firstDelay;
            public float PickRadius => pickRadius;
            public float SpotMargin => spotMargin;
            public float StealStrength => stealStrength;
            public float StealRunUp => stealRunUp;
            public float PlayerRunUp => playerRunUp;
            public float PressTime => pressTime;
            public float StealCooldown => stealCooldown;
            public float PlayerStealAfter => playerStealAfter;
            public float StealBackTime => stealBackTime;
            public Vector2 PlayerKeep => playerKeep;
            public float PlayerStealBack => playerStealBack;
            public float StealPush => stealPush;
            public float StealStagger => stealStagger;
            public float HolderSpeed => holderSpeed;
            public float HolderAcceleration => holderAcceleration;
            public float PlayerHolderSpeed => playerHolderSpeed;
            public float ProtectedPace => protectedPace;
            public float HolderPush => holderPush;
            public Vector2Int PlayerHunters => playerHunters;
            public Vector2Int BotHunters => botHunters;
            public Vector2 HunterPlayerPace => hunterPlayerPace;
            public Vector2 Lead => lead;
            public float ShadowDistance => shadowDistance;
            public Vector2 FleePlayerPace => fleePlayerPace;
            public float FleeRadius => fleeRadius;
            public float FenceMargin => fenceMargin;

            /// <summary>
            /// What the mode changes in the shared round (<see cref="ModeRules"/>): timed and scored by the crown's
            /// seconds, fenced with nobody falling (a car past the fence is put back), the floor still, no mechanics and no
            /// boxes, and no edge assist (the fence is the edge; the assist would keep the player off it).
            /// </summary>
            public ModeRules Rules => ModeRules.Plain.With(fall: FallRule.PutBack, fence: true, floor: FloorRule.Still,
                mechanics: false, pickups: PickupRule.None, edgeAssist: false, duration: duration,
                knockoutRewardCap: knockoutRewardCap);
        }
    }
}
