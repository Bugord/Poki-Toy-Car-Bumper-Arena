namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Where the player is in this visit to the game, kept across rounds although each round is a freshly loaded
    /// scene. The bots read it to start soft and grow harder, and to ease off after a run of losses, so a new player
    /// wins early and keeps pressing "play again".
    /// </summary>
    public interface ISessionProgress
    {
        /// <summary>
        /// The round being played, counting from 1 for the first round of the visit.
        /// </summary>
        int Round { get; }

        /// <summary>
        /// Rounds in a row the player has lost (fallen off) up to this one; 0 after a win or a round survived.
        /// </summary>
        int LosingStreak { get; }
    }
}
