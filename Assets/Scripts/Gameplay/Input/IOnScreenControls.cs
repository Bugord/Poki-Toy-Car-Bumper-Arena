using System;
using UnityEngine;

namespace ToyCarArena.Gameplay.Input
{
    /// <summary>
    /// Controls drawn over the game: buttons, like the boost button on a phone, whose press belongs to the button and
    /// does not steer the car, and the joystick under a steering finger, drawn where the driver says it is.
    /// </summary>
    public interface IOnScreenControls
    {
        /// <summary>
        /// Raised when the on-screen boost button is pressed.
        /// </summary>
        event Action BoostPressed;

        /// <summary>
        /// Raised when the on-screen item button is clicked with a mouse; a touch on it is read by the input service.
        /// </summary>
        event Action ItemPressed;

        /// <summary>
        /// Whether the on-screen item button is shown at this point: a finger that comes down here uses the item and
        /// never steers or boosts.
        /// </summary>
        /// <param name="screenPosition">In screen pixels from the bottom-left corner, as the Input System reports them.</param>
        bool IsItemAt(Vector2 screenPosition);

        /// <param name="screenPosition">In screen pixels from the bottom-left corner, as the Input System reports them.</param>
        bool IsControlAt(Vector2 screenPosition);

        /// <summary>
        /// Whether a press here belongs to the screens over the game rather than to the car: a button other than boost,
        /// or anywhere while a popup is open over the round. Such a press neither steers nor boosts until released.
        /// </summary>
        /// <param name="screenPosition">In screen pixels from the bottom-left corner, as the Input System reports them.</param>
        bool IsUiAt(Vector2 screenPosition);

        /// <summary>
        /// Chooses the boost button's side opposite a new steering touch, once per drag, also in FollowFinger mode.
        /// </summary>
        void BeginTouchSteering(Vector2 screenPosition);

        /// <summary>
        /// Draws the joystick a finger steers with, every frame it steers: its centre, where its knob is (the finger,
        /// at most <paramref name="radius"/> from the centre) and how far the knob goes, all in screen pixels from the
        /// bottom-left corner.
        /// </summary>
        void ShowJoystick(Vector2 center, Vector2 knob, float radius);

        /// <summary>
        /// Takes the joystick away once no finger steers with it.
        /// </summary>
        void HideJoystick();
    }
}
