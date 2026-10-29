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

        /// <summary>
        /// Counts the round just decided: a fall adds to the losing streak, a win or a round survived ends it.
        /// </summary>
        public void Record(RoundResult result)
        {
            // A new knockout gives the next round a new rival, even if the old one was beaten earlier this round.
            if (result.Outcome == RoundOutcome.Out && result.KnockedOutBy != RoundResult.Nobody) {
                Nemesis = result.KnockedOutBy;
            }
            else if (result.Outcome == RoundOutcome.Won || result.RevengeTaken) {
                Nemesis = RoundResult.Nobody;
            }

            this.LosingStreak = result.Outcome == RoundOutcome.Out ? LosingStreak + 1 : 0;
            this.Round = Round + 1;
        }
    }
}
