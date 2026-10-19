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
        /// The move keys (WASD, arrows) or a gamepad stick, up to 1 long; up is away from the camera.
        /// </summary>
        Vector2 CurrentMoveInput { get; }

        /// <summary>
        /// The finger that steers, or the mouse while its left button is held; false while neither is down. Only the
        /// finger that touched first counts until it lifts, so a second one cannot take over the car. A finger or a
        /// click that comes down on an on-screen button never steers. A phone's echo of a tap as a mouse click is left
        /// out.
        /// </summary>
        bool TryGetDrag(out PointerDrag drag);
    }
}
