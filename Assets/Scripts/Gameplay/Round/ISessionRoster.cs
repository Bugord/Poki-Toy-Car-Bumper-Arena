using System.Collections.Generic;
using ToyCarArena.Gameplay.Cars;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Keeps the visit's players in slots, with arrivals and departures for the next round's lobby.
    /// </summary>
    public interface ISessionRoster
    {
        /// <summary>
        /// The handle for a car's spawn index: YOU for 0, a bot's handle for 1..N.
        /// </summary>
        string NameOf(int carIndex);

        /// <summary>
        /// The current occupant's cosmetic car, drawn from the entire catalog, including locked entries.
        /// </summary>
        CarCatalog.Entry CarOf(int carIndex);

        /// <summary>The slot's colour ID; 0 is the player's current selection, independently of seated names.</summary>
        int ColorOf(int carIndex);

        /// <summary>
        /// The level shown by a car's name: the player's own for 0, the occupant's for 1..N, fixed when it was seated.
        /// It is only a look: difficulty never reads it.
        /// </summary>
        int LevelOf(int carIndex);

        /// <summary>The win streak a seated bot joined on, 0 for none and for the player (whose own is the visit's).</summary>
        int StreakOf(int carIndex);

        bool Joined(int carIndex);
        string LeftNameOf(int carIndex);
        IReadOnlyList<SessionPlayer> ForRound(int botCount);

        /// <summary>Called after progress is recorded, with the updated revenge target and next round's size.</summary>
        void NextRound(int playedBots, int nextBots, int nemesis, GameTuning.PlayerSettings settings);
    }
}
