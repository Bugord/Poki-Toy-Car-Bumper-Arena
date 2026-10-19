using UnityEngine;

namespace ToyCarArena.Gameplay.Input
{
    /// <summary>
    /// While nothing is drawn over the game: no HUD yet, or the Game scene played on its own.
    /// </summary>
    public sealed class NoOnScreenControls : IOnScreenControls
    {
        public bool IsControlAt(Vector2 screenPosition)
        {
            return false;
        }
    }
}
