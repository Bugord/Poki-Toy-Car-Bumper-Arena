using System;
using UnityEngine;

namespace ToyCarArena.Gameplay.Input
{
    /// <summary>
    /// The Game scene played on its own, without Boot and its HUD: nothing is drawn over the game. Played from Boot,
    /// the HUD's controls come from the app scope instead.
    /// </summary>
    public sealed class NoOnScreenControls : IOnScreenControls
    {
        // No button, so nothing to raise: the empty accessors keep the compiler from warning about an unused event.
        public event Action BoostPressed
        {
            add { }
            remove { }
        }

        public bool IsControlAt(Vector2 screenPosition)
        {
            return false;
        }

        public void ShowJoystick(Vector2 center, Vector2 knob, float radius)
        {
        }

        public void HideJoystick()
        {
        }
    }
}
