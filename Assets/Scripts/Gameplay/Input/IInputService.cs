using System;
using UnityEngine;

namespace ToyCarArena.Gameplay.Input
{
    public interface IInputService
    {
        /// <summary>
        /// Raised when the player steers with a key, a gamepad stick, or a finger or the mouse that starts a drag.
        /// </summary>
        event Action<InputMethod> MoveUsed;

        /// <summary>
        /// Raised when the player presses boost: Space, the right mouse button (the left one steers), a gamepad's south
        /// button, or a touch button, a short tap on release, or another finger on press. Carries the way it was pressed.
        /// </summary>
        event Action<BoostInputMethod> BoostPressed;

        /// <summary>
        /// Raised when the player uses the held item: E or Left Shift, a gamepad's west button, or the on-screen item
        /// button (touched or clicked). One press is one use, and a finger on the button never steers.
        /// </summary>
        event Action ItemPressed;

        /// <summary>
        /// Raised when the steering finger lifts after a quick stroke (<see cref="GameTuning.ControlSettings.SwipeTime"/>,
        /// <see cref="GameTuning.ControlSettings.SwipeDistance"/>): some players flick the screen the way they want the
        /// car to go instead of holding it. Carries the stroke, from where it touched to where it lifted, in screen
        /// pixels. A mouse drag never swipes.
        /// </summary>
        event Action<Vector2> Swiped;

        /// <summary>
        /// Prevents the current steering touch from boosting on release: a touch that starts the round only starts it.
        /// </summary>
        void CancelTapBoost();

        /// <summary>
        /// The move keys (WASD, arrows) or a gamepad stick, up to 1 long; up is away from the camera.
        /// </summary>
        Vector2 CurrentMoveInput { get; }

        /// <summary>
        /// The finger that steers, or the mouse while its left button is held; false while neither is down. Only the
        /// finger that touched first counts until it lifts; extra fingers boost and never take over, even after it
        /// lifts. A finger or a click that comes down on an on-screen button never steers. A phone's echo of a tap as
        /// a mouse click is left out.
        /// </summary>
        bool TryGetDrag(out PointerDrag drag);
    }
}
