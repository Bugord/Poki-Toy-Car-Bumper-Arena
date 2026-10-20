using System;
using ToyCarArena.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The HUD's buttons as the gameplay input sees them. The HUD is an app screen and the input lives in the arena's
    /// scope, which is loaded anew every round, so this sits in the app scope between the two: the HUD hands it its
    /// boost button and its presses, and the arena's <see cref="InputService"/> resolves it from the parent scope as its
    /// <see cref="IOnScreenControls"/>, so a finger on the button boosts and does not steer.
    /// </summary>
    public sealed class HudControls : IOnScreenControls
    {
        public event Action BoostPressed;

        private VisualElement boostButton;
        private bool isTouchSeen;

        /// <summary>
        /// Whether the HUD's buttons belong on the screen: on a phone or a tablet, or once a finger has touched the
        /// screen in this session (a tablet browser may say it is a desktop). A keyboard and a mouse have their own
        /// keys for everything, so a desktop keeps the HUD clean. Polled by the HUD, which is how a touch is noticed.
        /// </summary>
        public bool CheckTouchScreen()
        {
            if (!isTouchSeen && Touchscreen.current != null && Touchscreen.current.press.isPressed) {
                isTouchSeen = true;
            }

            return isTouchSeen || Application.isMobilePlatform;
        }

        /// <summary>
        /// The boost button of the HUD on the screen now, null while there is none.
        /// </summary>
        public void SetBoostButton(VisualElement button)
        {
            boostButton = button;
        }

        /// <summary>
        /// Lets go of a HUD's button as the HUD goes away, unless the next HUD has already handed in its own.
        /// </summary>
        public void ReleaseBoostButton(VisualElement button)
        {
            if (boostButton == button) {
                boostButton = null;
            }
        }

        public void PressBoost()
        {
            BoostPressed?.Invoke();
        }

        public bool IsControlAt(Vector2 screenPosition)
        {
            var panel = boostButton?.panel;
            if (panel == null) {
                return false;
            }

            // The panel's coordinates start at the top left and are scaled to the reference resolution; picking also
            // leaves out a hidden button and one under anything drawn over it.
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = panel.Pick(panelPosition);
            return picked != null && (picked == boostButton || boostButton.Contains(picked));
        }
    }
}
