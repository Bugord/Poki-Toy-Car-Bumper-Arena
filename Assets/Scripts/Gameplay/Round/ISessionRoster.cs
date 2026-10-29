namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Keeps each colour's handle stable across the freshly loaded rounds of one visit.
    /// </summary>
    public interface ISessionRoster
    {
        /// <summary>
        /// The handle for a car's spawn index: YOU for 0, a bot's handle for 1..N.
        /// </summary>
        string NameOf(int carIndex);
    }
}
