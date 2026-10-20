namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// A round's result beside the player's personal bests from before it, as <see cref="PersonalBests.Record"/> made it:
    /// what the result screen needs to say "NEW BEST!", or what the best to beat is. The brief's "almost": a lost round
    /// still shows a goal for the next one.
    /// </summary>
    public readonly struct RoundRecord
    {
        public readonly RoundResult Result;

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

        public RoundRecord(RoundResult result, int bestPlace, int bestKnockouts, bool isBestPlace, bool isBestKnockouts)
        {
            this.Result = result;
            this.BestPlace = bestPlace;
            this.BestKnockouts = bestKnockouts;
            this.IsBestPlace = isBestPlace;
            this.IsBestKnockouts = isBestKnockouts;
        }
    }
}
