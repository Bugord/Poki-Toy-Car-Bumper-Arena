using System;
using System.Runtime.InteropServices;
using Bugord.Core.Storage;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Pickups;
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
    public sealed class HudControls : IOnScreenControls, IItemLesson
    {
        public event Action BoostPressed;
        public event Action ItemPressed;

        // Saved once the player has used an item: the item lesson is over, in any visit. (Until v0.17.9 the hint showed
        // once for a moment under "hint_item"; that key is no longer read, so a player who only saw it learns again.)
        private const string ItemUsedKey = "item_used";

        // Read once: whether the browser's main pointer is a finger does not change while the game runs.
        private readonly bool isTouchDevice = IsTouchDevice();
        private readonly IKeyValueStorage storage;

        private VisualElement boostButton;
        private VisualElement itemButton;
        private VisualElement settingsButton;
        private bool isTouchSeen;
        private bool hasUsedItem;

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

        /// <summary>
        /// Whether a finger has steered in this visit, so <see cref="JoystickCenter"/> is where the player's thumb
        /// rests; kept between rounds, as the HUD is.
        /// </summary>
        public bool HasJoystickPlace { get; private set; }

        /// <summary>
        /// The button starts on the right and changes sides only when a new steering finger comes down. The choice
        /// lives here, in the app's scope, so the next round keeps the side the player plays on.
        /// </summary>
        public bool IsBoostOnLeft { get; private set; }

        /// <param name="storage">Where the hints already shown are kept; null keeps them for this run only.</param>
        public HudControls(IKeyValueStorage storage = null)
        {
            this.storage = storage;
        }

        /// <summary>
        /// Whether the player has used an item, for good: until then each round with boxes lays one in front of them and
        /// the HUD keeps its TAP TO USE hint up whenever an item is ready. Saved, and kept here, in the app's scope, beside
        /// the HUD; settable for the tests and screenshots.
        /// </summary>
        public bool HasUsedItem
        {
            get
            {
                // Once known, kept: the HUD asks every frame an item is ready.
                if (!hasUsedItem && storage != null && storage.HasKey(ItemUsedKey)) {
                    hasUsedItem = true;
                }

                return hasUsedItem;
            }
            set
            {
                hasUsedItem = value;
                if (value) {
                    storage?.SetInt(ItemUsedKey, 1);
                }
                else {
                    storage?.DeleteKey(ItemUsedKey);
                }
            }
        }

        public void MarkItemUsed()
        {
            if (!HasUsedItem) {
                HasUsedItem = true;
            }
        }

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
        /// The item button of the HUD on the screen now, null while there is none (or the round has no items).
        /// </summary>
        public void SetItemButton(VisualElement button)
        {
            itemButton = button;
        }

        public void ReleaseItemButton(VisualElement button)
        {
            if (itemButton == button) {
                itemButton = null;
            }
        }

        /// <summary>A mouse click on the item button; a touch on it is read by the input service.</summary>
        public void PressItem()
        {
            ItemPressed?.Invoke();
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

        public void BeginTouchSteering(Vector2 screenPosition)
        {
            // A tap can begin and end between HUD refreshes, so polling the touchscreen alone could miss it.
            isTouchSeen = true;
            IsBoostOnLeft = screenPosition.x > Screen.width * 0.5f;
        }

        public void ShowJoystick(Vector2 center, Vector2 knob, float radius)
        {
            IsJoystickShown = true;
            HasJoystickPlace = true;
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
            return IsPicked(boostButton, screenPosition);
        }

        public bool IsItemAt(Vector2 screenPosition)
        {
            return !IsPopupOpen && IsPicked(itemButton, screenPosition);
        }

        public bool IsUiAt(Vector2 screenPosition)
        {
            return IsPopupOpen || IsPicked(settingsButton, screenPosition);
        }

        /// <summary>
        /// A popup is open over the running round: the round goes on, so the keys still drive, but every press belongs
        /// to the popup. The HUD clears it as the popup closes, whatever closes it, since a flag left set here would keep
        /// every later round from taking a finger.
        /// </summary>
        public bool IsPopupOpen { get; set; }

        /// <summary>
        /// The HUD's settings button on the screen now, null while there is none.
        /// </summary>
        public void SetSettingsButton(VisualElement button)
        {
            settingsButton = button;
        }

        public void ReleaseSettingsButton(VisualElement button)
        {
            if (settingsButton == button) {
                settingsButton = null;
            }
        }

        private static bool IsPicked(VisualElement button, Vector2 screenPosition)
        {
            var panel = button?.panel;
            if (panel == null) {
                return false;
            }

            // The panel's coordinates start at the top left and are scaled to the reference resolution; picking also
            // leaves out a hidden button and one under anything drawn over it.
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = panel.Pick(panelPosition);
            return picked != null && (picked == button || button.Contains(picked));
        }
    }
}
