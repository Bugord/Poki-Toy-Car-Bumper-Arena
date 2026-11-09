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

        /// <summary>
        /// The player's win streak going into this round (<see cref="GameTuning.StreakSettings"/>): wins in a row, a
        /// REVENGE taken counting as one; 0 after a fall without REVENGE.
        /// </summary>
        int WinStreak { get; }

        /// <summary>
        /// The colour index of the rival to take revenge on next round, or <see cref="RoundResult.Nobody"/>.
        /// </summary>
        int Nemesis { get; }

        /// <summary>
        /// The mode the player picked in the menu for the rounds to come: QUICK MATCH (none) or an event mode. The next
        /// scene is built for it (<see cref="RoundRules"/>), as for the round's number.
        /// </summary>
        EventMode Mode { get; }
    }
}
