namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The items a car gets from a mystery box on the floor of a long match. A car holds one at a time and uses it on a
    /// button; the timed ones (heavy, shield, turbo) then run for a few seconds, the others act at once. Kept by number
    /// in the tuning's weight lists, so new kinds go at the end.
    /// </summary>
    public enum ItemKind
    {
        /// <summary>No item: an empty slot, or nothing running.</summary>
        None,

        /// <summary>The car weighs several times as much for a few seconds.</summary>
        Heavy,

        /// <summary>A ring pushes every other car near the user outward at once.</summary>
        Shockwave,

        /// <summary>The next hit of any kind is absorbed.</summary>
        Shield,

        /// <summary>The boost has almost no cooldown for a few seconds.</summary>
        Turbo,

        /// <summary>A bomb thrown ahead in an arc that blinks on the floor and blows the cars near it outward.</summary>
        Bomb,

        /// <summary>A boxing glove on a spring that shoots out of the nose and punches the first car it meets.</summary>
        Glove,
    }
}
