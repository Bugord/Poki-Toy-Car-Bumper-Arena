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

        bool Joined(int carIndex);
        string LeftNameOf(int carIndex);
        IReadOnlyList<SessionPlayer> ForRound(int botCount);

        /// <summary>Called after progress is recorded, with the updated revenge target and next round's size.</summary>
        void NextRound(int playedBots, int nextBots, int nemesis, GameTuning.PlayerSettings settings);
    }
}
