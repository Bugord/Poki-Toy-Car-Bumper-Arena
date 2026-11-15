using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The player's first rounds, which cannot be lost (<see cref="Round.PlayerSafety"/>), kept in a file of its own.
    /// Not in the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Safety")]
        [Tooltip("The player's first rounds, which cannot be lost")]
        [SerializeField]
        private SafetySettings safety = new();

        public SafetySettings Safety => safety;

        /// <summary>
        /// In a new player's first rounds a fall off the floor is caught and the car carried back, and the player's hits
        /// grow harder the longer the round goes: the bots play as usual, the round is won sooner or later. On the
        /// v0.18.0 playtest a new player who drove into the bots at the edge was knocked out in 5 seconds and left.
        /// </summary>
        [Serializable]
        public sealed class SafetySettings
        {
            [Tooltip("The player's first rounds on this device (all visits) played safe; 0 for none")]
            [SerializeField]
            [Min(0)]
            private int rounds = 2;

            [Tooltip("Metres below the top of the floor, with no floor under it, at which the player's fall is caught")]
            [SerializeField]
            private float catchDepth = 0.4f;

            [Tooltip("Metres over the floor the car is carried back at")]
            [SerializeField]
            private float carryHeight = 4.5f;

            [Tooltip("Seconds the carry back to the floor takes")]
            [SerializeField]
            private float carryTime = 1.2f;

            [Tooltip("Metres over the landing spot the carry ends at, before the car drops onto the floor")]
            [SerializeField]
            private float dropHeight = 1f;

            [Tooltip("How much harder the player's hits get by the end of the growth: 1 doubles them")]
            [SerializeField]
            [Min(0f)]
            private float powerGrowth = 0.8f;

            [Tooltip("Seconds of the round over which the player's hits grow to their hardest")]
            [SerializeField]
            [Min(1f)]
            private float powerTime = 30f;

            private int? forcedRounds;

            public int Rounds => forcedRounds ?? rounds;
            public float CatchDepth => catchDepth;
            public float CarryHeight => carryHeight;
            public float CarryTime => carryTime;
            public float DropHeight => dropHeight;

            /// <summary>Times the player's hits are as hard as usual, <paramref name="seconds"/> into a safe round.</summary>
            public float PowerAt(float seconds) => 1f + powerGrowth * Mathf.Clamp01(seconds / powerTime);

            /// <summary>Plays the given number of safe rounds whatever the build ships, in memory; null puts it back.</summary>
            public void Force(int? count) => forcedRounds = count;
        }
    }
}
