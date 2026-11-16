using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// A build made to playtest one event mode with new players (a Poki playtest of about ten), kept in a file of its
    /// own. Off (<see cref="EventMode.None"/>) in the builds that ship.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Playtest")]
        [Tooltip("A build that playtests one event mode with new players; None for the usual schedule")]
        [SerializeField]
        private PlaytestSettings playtest = new();

        public PlaytestSettings Playtest => playtest;

        /// <summary>
        /// With a mode set, the visit meets that mode early and often instead of the rotation's special rounds: the mode
        /// of the day is always it (the menu, the daily task, the rotation's slot), and from <see cref="FromRound"/> every
        /// <see cref="Every"/>-th round of the visit plays it, the others plain. After v0.19.0 the mode of the day came
        /// only in round 7, which a playtest of new players mostly does not reach, and every day showed another mode;
        /// one mode a build tells which mode keeps them. The mode plays even with its own <c>enabled</c> off, so a mode
        /// taken out of the rotation for rework is playtested the same way. The first round stays the tutorial.
        /// </summary>
        [Serializable]
        public sealed class PlaytestSettings
        {
            [Tooltip("The event mode this build playtests; None for the usual schedule of special rounds and modes")]
            [SerializeField]
            private EventMode mode = EventMode.None;

            [Tooltip("The visit's first round that plays the mode; the first round is the tutorial")]
            [Min(2)]
            [SerializeField]
            private int fromRound = 3;

            [Tooltip("Every this many rounds from the first one plays the mode, the rounds between are plain")]
            [Min(1)]
            [SerializeField]
            private int every = 2;

            // A test plays this mode instead of the field; null for the field.
            [NonSerialized]
            private EventMode? forced;

            public EventMode Mode => forced ?? mode;
            public int FromRound => fromRound;
            public int Every => every;

            /// <summary>Whether this build playtests a mode.</summary>
            public bool IsOn => Mode != EventMode.None;

            /// <summary>
            /// Plays <paramref name="playtested"/> as this build's playtest, or the field's again with null: in memory
            /// only, for the tests.
            /// </summary>
            public void Force(EventMode? playtested) => forced = playtested;

            /// <summary>The kind of the visit's round <paramref name="round"/> (from 1) in a playtest build.</summary>
            public SpecialRound For(int round) =>
                IsOn && round >= fromRound && (round - fromRound) % every == 0
                    ? EventModes.KindOf(Mode)
                    : SpecialRound.None;
        }
    }
}
