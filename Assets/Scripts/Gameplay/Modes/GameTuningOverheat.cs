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
        /// OVERHEAT: every hit heats a car up, and the hotter it is the farther a hit throws it; the last car standing wins.
        /// </summary>
        [Serializable]
        public sealed class OverheatSettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled;

            public bool Enabled => enabled;

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(pickups: PickupRule.None);
        }
    }
}
