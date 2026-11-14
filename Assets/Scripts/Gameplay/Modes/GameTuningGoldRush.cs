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
        /// GOLD RUSH: the car with the most coins wins; a bump shakes a rival's coins out.
        /// </summary>
        [Serializable]
        public sealed class GoldRushSettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled;

            [Tooltip("Seconds a round of the mode lasts; the car with the most coins wins")]
            [Min(10f)]
            [SerializeField]
            private float duration = 60f;

            [Tooltip("The most knock-outs a round pays XP and coins for: cars come back, so there are many")]
            [Min(0)]
            [SerializeField]
            private int knockoutRewardCap = 5;

            public bool Enabled => enabled;
            public float Duration => duration;
            public int KnockoutRewardCap => knockoutRewardCap;

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(fall: FallRule.Respawn, floor: FloorRule.Still, mechanics: false, pickups: PickupRule.None,
                duration: duration, knockoutRewardCap: knockoutRewardCap);
        }
    }
}
