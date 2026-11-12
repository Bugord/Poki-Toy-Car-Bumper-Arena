using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The TEAM PUSH event mode's numbers, kept in a file of its own so the mode's work touches only its own files. Not in
    /// the tuning asset: the values here are the ones played. How the bots play in teams is shared with every mode in
    /// teams (<see cref="GameTuning.Teams"/>).
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("TEAM PUSH")]
        [Tooltip("The TEAM PUSH event mode: whether it is on and its numbers")]
        [SerializeField]
        private TeamPushSettings teamPush = new();

        public TeamPushSettings TeamPush => teamPush;

        /// <summary>
        /// TEAM PUSH: blue (the player's team) against red, three against three, for a minute. Every car that falls is a
        /// point for the other team, whoever pushed it, and comes back a moment later; the team with more points wins,
        /// level is a draw.
        /// </summary>
        [Serializable]
        public sealed class TeamPushSettings
        {
            [Tooltip("Whether the build plays the mode: the menu, the mode of the day and the rotation offer only modes " +
                "that are on")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("Seconds a round of the mode lasts; the team with more points wins")]
            [Min(10f)]
            [SerializeField]
            private float duration = 60f;

            [Tooltip("Bots in the round beside the player: three against three")]
            [Min(1)]
            [SerializeField]
            private int bots = 5;

            [Tooltip("What the floor does: Still stays whole, Cycles shrinks and comes back as in the long match")]
            [SerializeField]
            private FloorRule floor = FloorRule.Cycles;

            [Tooltip("Points a fall gives the other team, whoever pushed the car (a fall of its own too)")]
            [Min(1)]
            [SerializeField]
            private int pointsPerFall = 1;

            [Tooltip("The most knock-outs a round pays XP and coins for: cars come back, so there are many")]
            [Min(0)]
            [SerializeField]
            private int knockoutRewardCap = 5;

            public bool Enabled => enabled;
            public float Duration => duration;
            public int Bots => bots;
            public FloorRule Floor => floor;
            public int PointsPerFall => pointsPerFall;
            public int KnockoutRewardCap => knockoutRewardCap;

            /// <summary>What the mode changes in the shared round (<see cref="ModeRules"/>).</summary>
            public ModeRules Rules => ModeRules.Plain.With(fall: FallRule.Respawn, floor: floor, mechanics: false,
                pickups: PickupRule.None, duration: duration, pointsPerKnockout: pointsPerFall, teams: 2, bots: bots,
                knockoutRewardCap: knockoutRewardCap);
        }
    }
}
