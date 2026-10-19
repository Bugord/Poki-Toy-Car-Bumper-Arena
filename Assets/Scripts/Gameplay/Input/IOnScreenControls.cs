using UnityEngine;

namespace ToyCarArena.Gameplay.Input
{
    /// <summary>
    /// Buttons drawn over the game, like the boost button on a phone. A press on one belongs to the button: it does not
    /// steer the car.
    /// </summary>
    public interface IOnScreenControls
    {
        /// <param name="screenPosition">In screen pixels from the bottom-left corner, as the Input System reports them.</param>
        bool IsControlAt(Vector2 screenPosition);
    }
}
