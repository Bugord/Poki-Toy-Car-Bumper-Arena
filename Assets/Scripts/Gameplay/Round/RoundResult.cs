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
    /// is placed behind every car still on the arena) and how many cars started.
    /// </summary>
    public readonly struct RoundResult
    {
        public readonly RoundOutcome Outcome;
        public readonly int Place;
        public readonly int CarCount;
        public readonly float Seconds;

        public RoundResult(RoundOutcome outcome, int place, int carCount, float seconds)
        {
            Outcome = outcome;
            Place = place;
            CarCount = carCount;
            Seconds = seconds;
        }
    }
}
