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
        /// Rounds the player had finished on this device before this visit, 0 for a new player: what the player has
        /// learned is the player's, not the visit's, so the easy first rounds and the teaching hints count from here.
        /// Fixed for the visit; the player's round counting every visit is this plus <see cref="Round"/>.
        /// </summary>
        int RoundsBefore { get; }

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

        /// <summary>
        /// Whether the player had never played when the visit began: no experience and no round finished on this
        /// device. Their first round is the tutorial (<see cref="GameTuning.TutorialSettings"/>). False where nothing is
        /// saved (the Game scene on its own, the simulation), so only the app's visits get the tutorial.
        /// </summary>
        bool IsNewPlayer => false;

        /// <summary>
        /// How badly the visit is going: each round lost adds one, each round not lost takes one away, never below 0. It
        /// eases the next round like <see cref="LosingStreak"/> (the larger of the two counts), but a lone win between
        /// losses no longer wipes it out, so a player who loses most rounds keeps the easier bots. The losing streak by
        /// default, where nothing tracks it.
        /// </summary>
        int Struggle => LosingStreak;

        /// <summary>
        /// The player's falls off the floor on their own in this visit (out with no car to credit, not by the arena's
        /// ball), less one for each round won: each adds to the help against driving off the edge
        /// (<see cref="GameTuning.BotSettings.SelfFallAssist"/>). 0 where nothing tracks it.
        /// </summary>
        int SelfFalls => 0;

        /// <summary>
        /// Rounds of the event mode <paramref name="kind"/> the player has finished on this device before the round to
        /// come: the first ones of a mode are easier (<see cref="GameTuning.BotSettings.ModeEase"/>). Where nothing is
        /// saved (the simulation) the player knows every mode, so a measured round plays as it always did.
        /// </summary>
        int ModeRoundsOf(SpecialRound kind) => int.MaxValue;
    }
}
