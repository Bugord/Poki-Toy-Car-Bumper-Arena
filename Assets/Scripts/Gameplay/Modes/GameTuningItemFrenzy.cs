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
        /// ITEM FRENZY: a round of last car standing on a crumbling floor, but boxes lie everywhere from the first second,
        /// in the first round of a visit too, and come two and a half times as often, up to one for every car; a broken
        /// box lands on its item in half the usual time. A box waits in front of every car as the round starts, so the
        /// first thing anyone does is grab one. The items are the usual round's six (mega, shockwave, bomb, bowling,
        /// balloon, oil): the shield, the turbo and the glove stay out as they are in every round (no boost in the build,
        /// no drawn icon, and the players did not take to them). A slot is free again the moment its item is used, so the
        /// pace is the boxes'. The bots with an empty slot go for the nearest box first (<see cref="BotBoxRange"/>),
        /// and a bot that held a ready item for <see cref="BotHoldMax"/> seconds without its moment lets it go anyway.
        /// <para>
        /// The numbers: a usual round's box comes every 4 s (±1), three at most; here every 1.6 s (±0.4) and seven at most,
        /// so with cars taking them as they come, a car is rarely without an item for more than a couple of seconds. The
        /// roll is 0.35 s (0.7 s usually): long enough to see the slot spin, short enough not to wait for it. On the last
        /// disc boxes thin out on their own: a box needs safe floor 3.5 m from the edge and 4 m from every car.
        /// </para>
        /// </summary>
        [Serializable]
        public sealed class ItemFrenzySettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("How many times as often boxes come as in a usual round")]
            [Min(1f)]
            [SerializeField]
            private float pickupPace = 2.5f;

            [Tooltip("How many boxes may lie on the floor at once: one for each of the seven cars a round can have")]
            [Min(1)]
            [SerializeField]
            private int pickupMax = 7;

            [Tooltip("Seconds a broken box's slot spins before it lands on its item (0.7 in a usual round)")]
            [Min(0.05f)]
            [SerializeField]
            private float rollTime = 0.35f;

            [Header("The start")]
            [Tooltip("Metres in front of each car, towards the middle, the box waiting for it as the round starts lies; " +
                "less where the floor ends sooner")]
            [Min(1f)]
            [SerializeField]
            private float openingDistance = 3.5f;

            [Tooltip("Metres the opening boxes keep from each other and from the edge")]
            [Min(0.5f)]
            [SerializeField]
            private float openingSpacing = 2.5f;

            [Header("Bots")]
            [Tooltip("Metres within which a bot with an empty slot drives for the nearest box before anything else (the " +
                "edge and the arena still come first)")]
            [Min(0f)]
            [SerializeField]
            private float botBoxRange = 14f;

            [Tooltip("A bot leaves a box to another bot already going for it, unless it is this many times nearer")]
            [Min(1f)]
            [SerializeField]
            private float botBoxNearer = 1.5f;

            [Tooltip("Seconds a bot holds a ready item waiting for its moment before it lets it go at the first moment " +
                "that does it no harm, so the items keep going off")]
            [Min(0f)]
            [SerializeField]
            private float botHoldMax = 2.5f;

            [Tooltip("Below this level of the round's difficulty (0 Easy, 1 Normal) an item a bot lets go without its " +
                "moment never catches the player, not only in the first seconds")]
            [Range(0f, 1f)]
            [SerializeField]
            private float spareBelow = 0.5f;

            public bool Enabled => enabled;
            public float PickupPace => pickupPace;
            public int PickupMax => pickupMax;
            public float RollTime => rollTime;
            public float OpeningDistance => openingDistance;
            public float OpeningSpacing => openingSpacing;
            public float BotBoxRange => botBoxRange;
            public float BotBoxNearer => botBoxNearer;
            public float BotHoldMax => botHoldMax;
            public float SpareBelow => spareBelow;

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(mechanics: false, pickups: PickupRule.Always,
                pickupPace: pickupPace, pickupMax: pickupMax, pickupRollTime: rollTime);
        }
    }
}
