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

        /// <param name="screenPosition">In screen pixels from the bottom-left corner, as the Input System reports them.</param>
        bool IsControlAt(Vector2 screenPosition);

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
