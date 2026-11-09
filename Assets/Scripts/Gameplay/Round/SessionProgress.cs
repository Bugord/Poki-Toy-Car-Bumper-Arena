using System;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The game's <see cref="ISessionProgress"/>: plain counters for this visit, nothing saved, since a new visit should
    /// start soft again. It lives in the app scope, which outlives the Game scene reloaded every round; the Game scene
    /// opened on its own keeps one in <see cref="StandaloneRound.Session"/> instead.
    /// <para>
    /// It moves on when a round is decided, not when the next one begins: the bots read it as the next round's scene is
    /// built, before that round begins, so between rounds <see cref="Round"/> is already the round to come.
    /// </para>
    /// </summary>
    public sealed class SessionProgress : ISessionProgress
    {
        public int Round { get; private set; } = 1;

        public int LosingStreak { get; private set; }

        public int Nemesis { get; private set; } = RoundResult.Nobody;

        public int WinStreak { get; private set; }

        /// <summary>Set by the menu's CHOOSE MODE; QUICK MATCH until then, and every visit starts on it again.</summary>
        public EventMode Mode { get; set; }

        /// <summary>
        /// Counts the round just decided: a loss (a fall, or a match placed low) adds to the losing streak, anything else
        /// ends it. A win adds to the win streak; REVENGE counts as a win and starts a streak of <paramref name=
        /// "streakStart"/> by itself, even in a round the player then fell from; a fall without it ends the streak, and a
        /// round survived to the time leaves it as it was.
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
            this.Round = Round + 1;
        }
    }
}
