using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The ITEM FRENZY event mode's numbers, kept in a file of its own so the mode's work touches only its own files. Not in
    /// the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("ITEM FRENZY")]
        [Tooltip("The ITEM FRENZY event mode: whether it is on and its numbers")]
        [SerializeField]
        private ItemFrenzySettings itemFrenzy = new();

        public ItemFrenzySettings ItemFrenzy => itemFrenzy;

        /// <summary>
        /// ITEM FRENZY: boxes everywhere, from the first round of the visit; the last car standing wins.
        /// </summary>
        [Serializable]
        public sealed class ItemFrenzySettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled;

            [Tooltip("How many times as often boxes come as in a usual round")]
            [Min(1f)]
            [SerializeField]
            private float pickupPace = 2.5f;

            [Tooltip("How many boxes may lie on the floor at once")]
            [Min(1)]
            [SerializeField]
            private int pickupMax = 6;

            public bool Enabled => enabled;
            public float PickupPace => pickupPace;
            public int PickupMax => pickupMax;

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(mechanics: false, pickups: PickupRule.Always, pickupPace: pickupPace, pickupMax: pickupMax);
        }
    }
}
