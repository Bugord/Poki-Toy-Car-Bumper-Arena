using System;
using Bugord.Core.Storage;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The game's <see cref="ISessionProgress"/>: plain counters for this visit, and the one saved number, the rounds
    /// the player has finished on this device, so a returning player does not get the newcomer's easy rounds again.
    /// It lives in the app scope, which outlives the Game scene reloaded every round; the Game scene opened on its own
    /// keeps one in <see cref="StandaloneRound.Session"/> instead, with nothing saved.
    /// <para>
    /// It moves on when a round is decided, not when the next one begins: the bots read it as the next round's scene is
    /// built, before that round begins, so between rounds <see cref="Round"/> is already the round to come.
    /// </para>
    /// </summary>
    public sealed class SessionProgress : ISessionProgress
    {
        /// <summary>The rounds finished on this device, saved after each one.</summary>
        public const string RoundsKey = "rounds_played";

        private readonly IKeyValueStorage storage;
        private readonly GameTuning tuning;
        private int roundsBefore = -1;

        /// <param name="storage">Where the rounds finished are kept; null keeps nothing, as a new player.</param>
        /// <param name="tuning">Kept for the scope's registration; the count no longer needs it.</param>
        public SessionProgress(IKeyValueStorage storage = null, GameTuning tuning = null)
        {
            this.storage = storage;
            this.tuning = tuning;
        }

        public int Round { get; private set; } = 1;

        /// <summary>
        /// Read on first use rather than when the scope is built, as the other saved progress is, and kept for the
        /// visit. A player from before the count (experience, but no count saved) starts the onboarding again, as a new
        /// player, and keeps their experience, cars and coins: the game is not released, so they can only be playtesters
        /// of a build weeks older, with a different game. Until v0.18.2 they were taken to be past every ramp, and a
        /// v0.18.1 playtester with three rounds on a build from before coins got a veteran's warm-up and no safe round.
        /// </summary>
        public int RoundsBefore
        {
            get
            {
                if (roundsBefore < 0) {
                    roundsBefore = storage == null ? 0 :
                        storage.HasKey(RoundsKey) ? Math.Max(0, storage.GetInt(RoundsKey)) : 0;
                }

                return roundsBefore;
            }
        }

        public int LosingStreak { get; private set; }

        public int Nemesis { get; private set; } = RoundResult.Nobody;

        public int WinStreak { get; private set; }

        /// <summary>Set by the menu's CHOOSE MODE; QUICK MATCH until then, and every visit starts on it again.</summary>
        public EventMode Mode { get; set; }

        /// <summary>
        /// Counts the round just decided: a loss (a fall, or a match placed low) adds to the losing streak, anything else
        /// ends it. A win adds to the win streak; REVENGE counts as a win and starts a streak of <paramref name=
        /// "streakStart"/> by itself, even in a round the player then fell from; a fall without it ends the streak, and a
        /// round survived to the time leaves it as it was. The rounds finished on this device are saved at once.
        /// </summary>
        public void Record(RoundResult result, int streakStart = 2)
        {
            // A new knockout gives the next round a new rival, even if the old one was beaten earlier this round.
            if (result.Outcome == RoundOutcome.Out && result.KnockedOutBy != RoundResult.Nobody) {
                Nemesis = result.KnockedOutBy;
            }
            else if (result.Outcome == RoundOutcome.Won || result.RevengeTaken) {
                Nemesis = RoundResult.Nobody;
            }

            if (result.RevengeTaken) {
                WinStreak = Math.Max(WinStreak + 1, streakStart);
            }
            else if (result.Outcome == RoundOutcome.Won) {
                WinStreak++;
            }
            else if (result.IsLoss) {
                WinStreak = 0;
            }

            this.LosingStreak = result.IsLoss ? LosingStreak + 1 : 0;
            // Read the count from before this visit first: from here on the saved one includes this visit's rounds.
            var before = RoundsBefore;
            this.Round = Round + 1;
            storage?.SetInt(RoundsKey, before + Round - 1);
        }
    }
}
