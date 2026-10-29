namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// A round's result beside the player's personal bests from before it, as <see cref="PersonalBests.Record"/> made it:
    /// what the result screen needs to say "NEW BEST!", or what the best to beat is. RoundState attaches the saved XP
    /// reward too, so the result can replay it without being responsible for saving progress.
    /// </summary>
    public readonly struct RoundRecord
    {
        public readonly RoundResult Result;

        /// <summary>The reward is already saved before the result screen starts animating it.</summary>
        public readonly XpGain XpGain;

        /// <summary>
        /// The best place before this round, 1 being a win; 0 when no round with a place was played yet.
        /// </summary>
        public readonly int BestPlace;

        /// <summary>
        /// The most cars knocked out in one round before this one.
        /// </summary>
        public readonly int BestKnockouts;

        /// <summary>
        /// Whether this round's place beat <see cref="BestPlace"/>. The very first round sets the best without beating
        /// it: any place would be a "best" then, which says nothing.
        /// </summary>
        public readonly bool IsBestPlace;

        /// <summary>
        /// Whether this round's knock-outs beat <see cref="BestKnockouts"/>; never for none.
        /// </summary>
        public readonly bool IsBestKnockouts;

        public RoundRecord(RoundResult result, int bestPlace, int bestKnockouts, bool isBestPlace, bool isBestKnockouts,
            XpGain xpGain = default)
        {
            this.Result = result;
            this.BestPlace = bestPlace;
            this.BestKnockouts = bestKnockouts;
            this.IsBestPlace = isBestPlace;
            this.IsBestKnockouts = isBestKnockouts;
            this.XpGain = xpGain;
        }

        /// <summary>Attaches the saved XP reward without changing the bests captured before this round.</summary>
        public RoundRecord WithXp(XpGain gain) =>
            new(Result, BestPlace, BestKnockouts, IsBestPlace, IsBestKnockouts, gain);
    }
}
