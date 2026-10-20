using System;
using System.Runtime.InteropServices;
using ToyCarArena.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The HUD's controls as the gameplay input sees them. The HUD is an app screen and the input lives in the arena's
    /// scope, which is loaded anew every round, so this sits in the app scope between the two: the HUD hands it its
    /// boost button and its presses, and the arena's <see cref="InputService"/> resolves it from the parent scope as its
    /// <see cref="IOnScreenControls"/>, so a finger on the button boosts and does not steer. The other way, the player's
    /// driver hands it the joystick a finger steers with, and the HUD draws it from here.
    /// </summary>
    public sealed class HudControls : IOnScreenControls
    {
        public event Action BoostPressed;

        // Read once: whether the browser's main pointer is a finger does not change while the game runs.
        private readonly bool isTouchDevice = IsTouchDevice();

        private VisualElement boostButton;
        private bool isTouchSeen;

        /// <summary>
        /// Whether a finger steers with the joystick now, and so the HUD draws it.
        /// </summary>
        public bool IsJoystickShown { get; private set; }

        /// <summary>
        /// The joystick's centre, its knob and how far the knob goes, in screen pixels from the bottom-left corner;
        /// kept from the last frame it was shown.
        /// </summary>
        public Vector2 JoystickCenter { get; private set; }

        public Vector2 JoystickKnob { get; private set; }

        public float JoystickRadius { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int JS_TouchScreen_IsPrimary();
#endif

        /// <summary>
        /// Whether the player plays with a finger, so the screens show touch hints and the HUD its buttons: on a phone
        /// or a tablet, or once a finger has touched the screen in this session. A keyboard and a mouse have their own
        /// keys for everything, so a desktop keeps the HUD clean. Polled by the HUD, which is how a touch is noticed.
        /// </summary>
        public bool CheckTouchScreen()
        {
            if (!isTouchSeen && Touchscreen.current != null && Touchscreen.current.press.isPressed) {
                isTouchSeen = true;
            }

            return isTouchSeen || isTouchDevice;
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

        /// <summary>
        /// Whether the device plays with a finger first. In the browser the page's primary pointer says so
        /// (TouchScreen.jslib), which also catches an iPad, whose Safari tells the user agent it is a Mac.
        /// </summary>
        private static bool IsTouchDevice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return JS_TouchScreen_IsPrimary() != 0 || Application.isMobilePlatform;
#else
            return Application.isMobilePlatform;
#endif
        }

        public void PressBoost()
        {
            BoostPressed?.Invoke();
        }

        public void ShowJoystick(Vector2 center, Vector2 knob, float radius)
        {
            IsJoystickShown = true;
            JoystickCenter = center;
            JoystickKnob = knob;
            JoystickRadius = radius;
        }

        public void HideJoystick()
        {
            IsJoystickShown = false;
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
