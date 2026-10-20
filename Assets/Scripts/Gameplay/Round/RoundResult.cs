namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// How a round ended for the player.
    /// </summary>
    public enum RoundOutcome
    {
        /// <summary>The player is the last car on the arena.</summary>
        Won,

        /// <summary>The player fell off.</summary>
        Out,

        /// <summary>Time ran out with the player still on the arena, beside other cars.</summary>
        Survived,
    }

    /// <summary>
    /// The end of a round as the result screen shows it: how it ended, the player's place (1 is the winner; a car out
    /// is placed behind every car still on the arena), how many cars started, how many of them the player knocked out
    /// and which car knocked the player out, if one did.
    /// </summary>
    public readonly struct RoundResult
    {
        /// <summary>
        /// <see cref="KnockedOutBy"/> when no car gets the credit: the player is still on the arena, or fell off alone.
        /// </summary>
        public const int Nobody = -1;

        public readonly RoundOutcome Outcome;
        public readonly int Place;
        public readonly int CarCount;
        public readonly float Seconds;

        /// <summary>
        /// Cars the player knocked out (see <see cref="ArenaRound.KnockedOut"/>).
        /// </summary>
        public readonly int Knockouts;

        /// <summary>
        /// The spawn index of the car that knocked the player out (1..N, a bot; see <see cref="ArenaRound.IndexOf"/>),
        /// or <see cref="Nobody"/>. An index rather than the car, because the car goes with its scene and the result
        /// outlives it; the index is also what tells the car's colour.
        /// </summary>
        public readonly int KnockedOutBy;

        public RoundResult(RoundOutcome outcome, int place, int carCount, float seconds, int knockouts, int knockedOutBy)
        {
            Outcome = outcome;
            Place = place;
            CarCount = carCount;
            Seconds = seconds;
            Knockouts = knockouts;
            KnockedOutBy = knockedOutBy;
        }
    }
}
