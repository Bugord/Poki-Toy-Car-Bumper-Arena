using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The USS class that paints an element in a car's body colour (car-color--N in ui_screens), so the HUD's chips and
    /// the result screen's PUSHED OFF BY badge show the car the player sees on the arena.
    /// </summary>
    public static class CarColor
    {
        private const string ClassPrefix = "car-color--";

        // As many colours as ui_screens has (car-color--0 to 7) and CarLook has swatches on the car prefab.
        private const int Count = 8;

        /// <summary>
        /// The class of the car with this index in the round: 0 the player, 1..N the bots. Past the last colour the bots
        /// start over from the first bot's, as <see cref="Gameplay.Cars.CarLook"/> paints them, so no bot ever wears
        /// the player's colour.
        /// </summary>
        public static string ClassOf(int index)
        {
            var color = index < Count ? Mathf.Max(0, index) : 1 + (index - 1) % (Count - 1);
            return ClassPrefix + color;
        }
    }
}
