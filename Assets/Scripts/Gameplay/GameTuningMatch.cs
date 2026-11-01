using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// What the rounds of this build play, and the numbers of the long match, kept apart from the main file because a
    /// build plays one mode or the other: the mode is chosen here, per build, never by the player, so each version on
    /// Poki measures one mode and compares fairly with the versions before it.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Mode")]
        [Tooltip("What every round of this build plays: the last car standing, or a timed match where a car that " +
            "falls comes back and knock-outs score. Chosen per build, never by the player")]
        [SerializeField]
        private GameMode mode = GameMode.LastStanding;

        [Header("Match")]
        [Tooltip("The long match: how long it lasts, how soon a fallen car comes back and how long it is safe then")]
        [SerializeField]
        private MatchSettings match = new();

        public GameMode Mode => mode;
        public MatchSettings Match => match;

        /// <summary>
        /// Whether the rounds of this build are long matches with respawns.
        /// </summary>
        public bool IsMatch => mode == GameMode.Match;

        /// <summary>
        /// Seconds a round of this build's mode lasts at most: the round's own length, or the match's.
        /// </summary>
        public float PlayDuration => IsMatch ? match.Duration : round.Duration;

        /// <summary>
        /// The game's modes. Stored in the asset by number, so new ones go at the end.
        /// </summary>
        public enum GameMode
        {
            /// <summary>The round ends when the player falls, is the last car left or time runs out.</summary>
            LastStanding,

            /// <summary>A timed match: fallen cars come back, the most knock-outs wins when time runs out.</summary>
            Match,
        }

        /// <summary>
        /// The long match: it ends only when time runs out, a car that falls drops back onto the safe floor a moment
        /// later, away from the others, and cannot be pushed or push for a moment after, or it would be knocked off
        /// again where it lands.
        /// </summary>
        [Serializable]
        public sealed class MatchSettings
        {
            [Tooltip("Seconds a match lasts; it ends only when time runs out")]
            [Min(10f)]
            [SerializeField]
            private float duration = 150f;

            [Tooltip("Seconds between a car falling below the round's fall height and it dropping back onto the floor")]
            [Min(0f)]
            [SerializeField]
            private float respawnDelay = 2f;

            [Tooltip("Seconds a car that came back stays safe after it lands: half seen through, it neither pushes nor " +
                "is pushed by cars, gives no knock-out credit, hazards pass it by and bots do not pick it")]
            [Min(0f)]
            [SerializeField]
            private float invulnerability = 1.5f;

            [Tooltip("Metres above the floor a car that comes back is dropped from, so the player sees it arrive")]
            [Min(0f)]
            [SerializeField]
            private float dropHeight = 3f;

            [Tooltip("Seconds a dropped car may take to land before its safe time counts down anyway (a car that misses " +
                "the floor would otherwise stay safe while it falls)")]
            [Min(0f)]
            [SerializeField]
            private float maxDropTime = 2f;

            [Tooltip("Metres a car comes back inside the safe edge of the floor (the edge of shaking tiles, rims, pits " +
                "and holes alike), so it does not land where the next wave falls or slide off as it lands")]
            [Min(0f)]
            [SerializeField]
            private float spawnMargin = 2.5f;

            [Tooltip("Metres from the nearest other car beyond which a comeback spot is far enough; past it a spot " +
                "deeper inside the floor is preferred")]
            [Min(0f)]
            [SerializeField]
            private float spawnSpread = 8f;

            [Tooltip("Metres between the points of the grid a comeback spot is chosen from")]
            [Min(0.25f)]
            [SerializeField]
            private float spawnGridStep = 1f;

            [Tooltip("A match placed this high or better is no loss for the visit's losing streak, which eases the bots; " +
                "a new player's first-match goal is a place among the top three")]
            [Min(1)]
            [SerializeField]
            private int topPlaces = 3;

            public float Duration => duration;
            public float RespawnDelay => respawnDelay;
            public float Invulnerability => invulnerability;
            public float DropHeight => dropHeight;
            public float MaxDropTime => maxDropTime;
            public float SpawnMargin => spawnMargin;
            public float SpawnSpread => spawnSpread;
            public float SpawnGridStep => spawnGridStep;
            public int TopPlaces => topPlaces;
        }
    }
}
