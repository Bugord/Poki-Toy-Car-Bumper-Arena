using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The FOOTBALL event mode's numbers, kept in a file of its own so the mode's work touches only its own files. Not in
    /// the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("FOOTBALL")]
        [Tooltip("The FOOTBALL event mode: whether it is on and its numbers")]
        [SerializeField]
        private FootballSettings football = new();

        public FootballSettings Football => football;

        /// <summary>
        /// FOOTBALL: two teams push a ball into each other's goal; the team with more goals wins when time runs out.
        /// </summary>
        [Serializable]
        public sealed class FootballSettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled;

            [Tooltip("Seconds a match of football lasts; the team with more goals wins")]
            [Min(10f)]
            [SerializeField]
            private float duration = 90f;

            [Tooltip("Bots on the pitch: with the player, two teams of two")]
            [Range(1, 6)]
            [SerializeField]
            private int bots = 3;

            public bool Enabled => enabled;
            public float Duration => duration;
            public int Bots => bots;

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(fall: FallRule.PutBack, fence: true,
                floor: FloorRule.Still, mechanics: false, pickups: PickupRule.None, duration: duration, teams: 2, bots: bots);
        }
    }
}
