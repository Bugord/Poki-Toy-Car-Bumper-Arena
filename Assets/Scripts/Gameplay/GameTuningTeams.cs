using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// How the bots play a round in teams (<see cref="ModeRules.Teams"/>), for every mode played in teams (TEAM PUSH,
    /// FOOTBALL), kept in a file of its own. Not in the tuning asset: the values here are the ones played.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Teams")]
        [Tooltip("How the bots play a round in teams")]
        [SerializeField]
        private TeamSettings teams = new();

        public TeamSettings Teams => teams;

        /// <summary>
        /// The bots of a round in teams leave their teammates alone, go to the help of one under attack and do not all go
        /// after the same car; on the easiest rounds the other team's bots are softer, so a new player's team wins.
        /// </summary>
        [Serializable]
        public sealed class TeamSettings
        {
            [Tooltip("Score a car of the other team gets from a bot while it is pushing, or has just hit, one of the bot's " +
                "teammates: the bot comes to the teammate's help")]
            [SerializeField]
            private float coverWeight = 0.45f;

            [Tooltip("Seconds after a car hit a teammate that it still counts as pushing it")]
            [SerializeField]
            private float coverTime = 1.5f;

            [Tooltip("Metres from a teammate within which a car of the other team after it counts as pushing it")]
            [SerializeField]
            private float coverRange = 5f;

            [Tooltip("Score taken off a car for each teammate of the bot already after it, on top of the usual crowd " +
                "penalty: a team spreads over the other team instead of piling on one car")]
            [SerializeField]
            private float teamCrowdPenalty = 0.3f;

            [Tooltip("Times the throttle the other team's bots attack at on the easiest round, rising to 1 on Normal")]
            [SerializeField]
            [Range(0.3f, 1f)]
            private float easyEnemyPace = 0.8f;

            public float CoverWeight => coverWeight;
            public float CoverTime => coverTime;
            public float CoverRange => coverRange;
            public float TeamCrowdPenalty => teamCrowdPenalty;
            public float EasyEnemyPace => easyEnemyPace;

            /// <summary>The other team's attack throttle at difficulty <paramref name="level"/> (0 Easy, 1 Normal).</summary>
            public float EnemyPaceAt(float level) => Mathf.Lerp(easyEnemyPace, 1f, Mathf.Clamp01(level));
        }
    }
}
