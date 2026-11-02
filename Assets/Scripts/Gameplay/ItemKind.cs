namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The items a car gets from a mystery box on the floor of a long match. A car holds one at a time and uses it on a
    /// button; the timed ones (mega, shield, turbo, balloon) then run for a few seconds, the others act at once. Kept by
    /// number in the tuning's weight lists, so new kinds go at the end.
    /// </summary>
    public enum ItemKind
    {
        /// <summary>No item: an empty slot, or nothing running.</summary>
        None,

        /// <summary>
        /// The car grows big and weighs several times as much for a few seconds; the heavy item of v0.6 to v0.8, in the
        /// same place, made visible.
        /// </summary>
        Mega,

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

        /// <summary>A bowling ball rolled out of the nose that knocks aside every car on its line across the floor.</summary>
        Bowling,

        /// <summary>For a few seconds a balloon floats over the car, and a fall off the edge is carried back to the floor.</summary>
        Balloon,

        /// <summary>A slick of oil spilled behind the car: cars on it slide, and leave oily tyre tracks after.</summary>
        Oil,
    }
}
