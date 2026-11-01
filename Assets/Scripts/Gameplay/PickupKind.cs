namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The abilities a car picks up off the floor of a long match. They act the moment they are picked up; a kind that
    /// would wait for a button of its own can be added at the end later. Kept by number in the asset's lists, so new
    /// kinds go at the end.
    /// </summary>
    public enum PickupKind
    {
        /// <summary>No ability: what a car holds when nothing is running.</summary>
        None,

        /// <summary>The car weighs several times as much for a few seconds.</summary>
        Heavy,

        /// <summary>A ring pushes every other car near the picker outward at once.</summary>
        Shockwave,

        /// <summary>The next hit of any kind is absorbed.</summary>
        Shield,

        /// <summary>The boost has no cooldown for a few seconds.</summary>
        Turbo,
    }
}
