using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The section of the floor that comes back, apart from the main file so the match mode and the floor can change
    /// side by side: <see cref="FloorCycleSettings"/>.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Floor cycles")]
        [Tooltip("The floor crumbling and coming back in cycles over a long match, instead of crumbling once")]
        [SerializeField]
        private FloorCycleSettings floorCycles = new();

        public FloorCycleSettings FloorCycles => floorCycles;

        /// <summary>
        /// The floor in cycles: open (the whole floor), shrink (every wave of the arena falls, spread over the phase
        /// however many waves the arena has), peak (only the core) and return (the tiles drop back from above). The
        /// tension of a long match comes in waves instead of building once. Off, the edge crumbles once over the round
        /// as it always has.
        /// </summary>
        [Serializable]
        public sealed class FloorCycleSettings
        {
            [Tooltip("Whether the floor comes back in cycles; off, it crumbles once over the round")]
            [SerializeField]
            private bool enabled;

            [Tooltip("How many cycles the floor goes through; after the last one it stays whole. 0 cycles for ever")]
            [Min(0)]
            [SerializeField]
            private int cycles = 3;

            [Tooltip("Seconds of a cycle with the whole floor, at its start")]
            [Min(0f)]
            [SerializeField]
            private float open = 15f;

            [Tooltip("Seconds every wave of the arena takes to fall, from the first one shaking to the last one gone; " +
                "the waves are spread evenly over it")]
            [Min(0.1f)]
            [SerializeField]
            private float shrink = 25f;

            [Tooltip("Seconds only the core is left, after the last wave has fallen")]
            [Min(0f)]
            [SerializeField]
            private float peak = 7f;

            [Tooltip("Seconds from the first sign of the floor coming back to the last tile landing")]
            [Min(0.1f)]
            [SerializeField]
            private float returnTime = 3f;

            [Tooltip("Seconds the tiles hang over their places before the first one lands, so a car can see the floor " +
                "coming; part of the return time")]
            [Min(0f)]
            [SerializeField]
            private float returnWarning = 1.2f;

            public bool Enabled => enabled;
            public int Cycles => cycles;
            public float Open => open;
            public float Shrink => shrink;
            public float Peak => peak;
            public float ReturnTime => returnTime;
            public float ReturnWarning => returnWarning;
        }
    }
}
