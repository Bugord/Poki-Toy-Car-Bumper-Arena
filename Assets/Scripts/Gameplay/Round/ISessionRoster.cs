using ToyCarArena.Gameplay.Cars;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Keeps each colour's handle and cosmetic car stable across the freshly loaded rounds of one visit.
    /// </summary>
    public interface ISessionRoster
    {
        /// <summary>
        /// The handle for a car's spawn index: YOU for 0, a bot's handle for 1..N.
        /// </summary>
        string NameOf(int carIndex);

        /// <summary>
        /// A bot's cosmetic car for the whole visit, drawn from the entire catalog, including locked entries.
        /// </summary>
        CarCatalog.Entry CarOf(int carIndex);
    }
}
