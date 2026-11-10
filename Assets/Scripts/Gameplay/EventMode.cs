namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The mode the player picked in the menu's CHOOSE MODE: none for QUICK MATCH (last car standing, with the
    /// rotation's special rounds), or an event mode that changes the whole round. Kept for the visit only
    /// (<see cref="Round.ISessionProgress.Mode"/>): a new visit opens on QUICK MATCH, and the first visit, which has no
    /// menu, plays only that.
    /// </summary>
    public enum EventMode
    {
        /// <summary>QUICK MATCH: the plain round.</summary>
        None,

        /// <summary>
        /// HOT POTATO: a fenced floor nobody can fall from, a bomb that passes on a bump and knocks its holder out of the
        /// round when its fuse runs out; the last car left wins.
        /// </summary>
        HotPotato,
    }
}
