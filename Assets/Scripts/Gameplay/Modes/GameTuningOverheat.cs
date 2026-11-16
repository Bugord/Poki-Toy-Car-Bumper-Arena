using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The OVERHEAT event mode's numbers, kept in a file of its own so the mode's work touches only its own files. Not in
    /// the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("OVERHEAT")]
        [Tooltip("The OVERHEAT event mode: whether it is on and its numbers")]
        [SerializeField]
        private OverheatSettings overheat = new();

        public OverheatSettings Overheat => overheat;

        /// <summary>
        /// OVERHEAT: every car for itself for a minute, a point for each knock-out, the cars come back after a fall. Every
        /// hit a car takes heats it up, by how hard it was; the hotter a car, the farther the next hit throws it and the
        /// longer it is off balance, so a red-hot car flies off from almost anywhere. A fall cools it down: it comes back
        /// cold.
        /// </summary>
        [Serializable]
        public sealed class OverheatSettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("Seconds a round of the mode lasts at most; the most points wins")]
            [Min(10f)]
            [SerializeField]
            private float duration = 45f;

            [Tooltip("A car's points (knock-outs) that end the round at once, before its time; 0 for none")]
            [Min(0)]
            [SerializeField]
            private int pointsToWin = 5;

            [Tooltip("What the floor does: Still stays whole, Cycles shrinks and comes back as in the long match")]
            [SerializeField]
            private FloorRule floor = FloorRule.Cycles;

            [Tooltip("The most knock-outs a round pays XP and coins for: cars come back, so there are many")]
            [Min(0)]
            [SerializeField]
            private int knockoutRewardCap = 5;

            [Header("Heat")]
            [Tooltip("Heat (0 cold, 1 red-hot through) a hit at the car's full speed gives the car that took it")]
            [Min(0f)]
            [SerializeField]
            private float heatPerHit = 0.24f;

            [Tooltip("Heat any hit that counts gives at least, on top of its strength: a tap still warms")]
            [Min(0f)]
            [SerializeField]
            private float heatFloor = 0.03f;

            [Tooltip("Metres a second the two cars must close in for a hit to heat at all: contact and nudges don't")]
            [Min(0f)]
            [SerializeField]
            private float minStrength = 2f;

            [Tooltip("Share of the heat the car that did the hitting takes too: ramming heats a little")]
            [Range(0f, 1f)]
            [SerializeField]
            private float hitterShare = 0.2f;

            [Tooltip("Times the heat a hit by the player gives: the player's rams heat the bots faster")]
            [Min(0f)]
            [SerializeField]
            private float fromPlayer = 1.8f;

            [Tooltip("Share of the heat the player takes from a hit on Easy")]
            [Range(0f, 1f)]
            [SerializeField]
            private float easyPlayerShare = 0.5f;

            [Tooltip("Share of the heat the player takes from a hit on Normal (the shares mix by the round's level): a " +
                "little under the bots', so the player stays the strongest car of a later round")]
            [Range(0f, 1f)]
            [SerializeField]
            private float normalPlayerShare = 0.65f;

            [Tooltip("Heat from which a car is warm: it steams")]
            [Range(0f, 1f)]
            [SerializeField]
            private float warm = 0.35f;

            [Tooltip("Heat from which a car is red-hot: it burns, the HUD calls it out, and knocking it off is a meltdown")]
            [Range(0f, 1f)]
            [SerializeField]
            private float hot = 0.7f;

            [Tooltip("Power of the heat in its effect: above 1 the first hits barely change a thing and the last ones a lot")]
            [Min(1f)]
            [SerializeField]
            private float curve = 2.2f;

            [Header("What heat does")]
            [Tooltip("Times the extra push of a hit a red-hot-through car takes, against 1 cold")]
            [Min(1f)]
            [SerializeField]
            private float pushAtMax = 3.4f;

            [Tooltip("Times the loss of balance a red-hot-through car takes from a hit")]
            [Min(1f)]
            [SerializeField]
            private float staggerAtMax = 2.4f;

            [Tooltip("Times the most a hit may leave a red-hot-through car going (Car.MaxBumpSpeed)")]
            [Min(1f)]
            [SerializeField]
            private float speedCapAtMax = 1.6f;

            [Tooltip("Metres a second a hit throws a red-hot-through car up from the floor, by its heat effect")]
            [Min(0f)]
            [SerializeField]
            private float liftAtMax = 4.5f;

            [Tooltip("Degrees a second a hit spins a red-hot-through car round as it flies")]
            [Min(0f)]
            [SerializeField]
            private float spinAtMax = 540f;

            [Tooltip("Share of a hit at full speed from which a hot car is thrown up: a nudge does not lift it")]
            [Range(0f, 1f)]
            [SerializeField]
            private float liftFrom = 0.3f;

            [Header("Bots")]
            [Tooltip("Added to a car's worth as a bots' target at red-hot through, by its heat effect: hot cars are hunted")]
            [Min(0f)]
            [SerializeField]
            private float targetWeight = 0.9f;

            [Tooltip("The most bots after the player at once, however hot the player is")]
            [Min(0)]
            [SerializeField]
            private int maxPlayerHunters = 1;

            [Tooltip("Metres a red-hot-through bot keeps from the edge beyond the usual margin: a hot bot is careful")]
            [Min(0f)]
            [SerializeField]
            private float hotEdgeMargin = 3f;

            public bool Enabled => enabled;
            public float Duration => duration;
            public int PointsToWin => pointsToWin;
            public FloorRule Floor => floor;
            public int KnockoutRewardCap => knockoutRewardCap;
            public float HeatPerHit => heatPerHit;
            public float HeatFloor => heatFloor;
            public float MinStrength => minStrength;
            public float HitterShare => hitterShare;
            public float FromPlayer => fromPlayer;
            public float EasyPlayerShare => easyPlayerShare;
            public float NormalPlayerShare => normalPlayerShare;

            /// <summary>The share of a hit's heat the player takes at the round's level of difficulty, 0 (Easy) to 1.</summary>
            public float PlayerShareAt(float level) => Mathf.Lerp(easyPlayerShare, normalPlayerShare, level);
            public float Warm => warm;
            public float Hot => hot;
            public float Curve => curve;
            public float PushAtMax => pushAtMax;
            public float StaggerAtMax => staggerAtMax;
            public float SpeedCapAtMax => speedCapAtMax;
            public float LiftAtMax => liftAtMax;
            public float SpinAtMax => spinAtMax;
            public float LiftFrom => liftFrom;
            public float TargetWeight => targetWeight;
            public int MaxPlayerHunters => maxPlayerHunters;
            public float HotEdgeMargin => hotEdgeMargin;

            /// <summary>
            /// How much a car's heat does, 0 cold to 1 red-hot through: the heat to <see cref="Curve"/>, so the first
            /// two or three hits feel like plain ones.
            /// </summary>
            public float EffectOf(float heat) => Mathf.Pow(Mathf.Clamp01(heat), curve);

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(fall: FallRule.Respawn, floor: floor, mechanics: false,
                pickups: PickupRule.None, duration: duration, pointsPerKnockout: 1,
                knockoutRewardCap: knockoutRewardCap, pointsToWin: pointsToWin);
        }
    }
}
