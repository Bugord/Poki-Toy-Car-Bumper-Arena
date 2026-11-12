using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The TEAM PUSH event mode's numbers, kept in a file of its own so the mode's work touches only its own files. Not in
    /// the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("TEAM PUSH")]
        [Tooltip("The TEAM PUSH event mode: whether it is on and its numbers")]
        [SerializeField]
        private TeamPushSettings teamPush = new();

        public TeamPushSettings TeamPush => teamPush;

        /// <summary>
        /// TEAM PUSH: blue against red; the team that pushes the other off wins.
        /// </summary>
        [Serializable]
        public sealed class TeamPushSettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled;

            public bool Enabled => enabled;

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(pickups: PickupRule.None, teams: 2);
        }
    }
}
