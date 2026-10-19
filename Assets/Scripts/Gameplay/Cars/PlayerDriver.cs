using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Input;
using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Drives the player's car from the input, the same way whatever the player holds:
    /// <list type="bullet">
    /// <item>the move keys or a gamepad stick point where the car should go, up being away from the camera;</item>
    /// <item>the mouse, held down, drives the car towards the cursor and stops it there;</item>
    /// <item>a finger is a joystick that appears where it touched, or drives the car towards itself like the mouse
    /// (<see cref="GameTuning.ControlSettings.TouchSteering"/>).</item>
    /// </list>
    /// A drag wins over the keys while it lasts.
    /// </summary>
    [RequireComponent(typeof(CarMovement))]
    public sealed class PlayerDriver : MonoBehaviour
    {
        private IInputService input;
        private GameTuning tuning;
        private ArenaCamera arenaCamera;
        private CarMovement movement;

        // The joystick's centre: where the finger touched, pulled along when the finger goes past the radius.
        private int joystickDragId;
        private Vector2 joystickCenter;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        [Inject]
        private void Construct(IInputService input, GameTuning tuning, ArenaCamera arenaCamera)
        {
            this.input = input;
            this.tuning = tuning;
            this.arenaCamera = arenaCamera;
        }

        private void Awake()
        {
            movement = GetComponent<CarMovement>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            // Until the round (step 3 of the brief) takes a fallen car out, it comes back to where it started.
            if (transform.position.y < tuning.Round.FallHeight) {
                movement.Place(spawnPosition, spawnRotation);
            }

            if (input.TryGetDrag(out var drag)) {
                if (drag.IsMouse || tuning.Controls.TouchSteering == GameTuning.TouchSteering.FollowFinger) {
                    FollowPointer(drag.Position);
                }
                else {
                    SteerByJoystick(drag);
                }

                return;
            }

            var move = Vector2.ClampMagnitude(input.CurrentMoveInput, 1f);
            movement.Drive(arenaCamera.ScreenToGroundDirection(move), move.magnitude);
        }

        private void FollowPointer(Vector2 screenPosition)
        {
            if (!arenaCamera.TryScreenToGround(screenPosition, out var target)) {
                movement.Drive(Vector3.zero, 0f);
                return;
            }

            var controls = tuning.Controls;
            var offset = target - transform.position;
            offset.y = 0f;
            var throttle = Mathf.InverseLerp(controls.PointerStopRadius, controls.PointerSlowRadius, offset.magnitude);
            movement.Drive(offset, throttle);
        }

        private void SteerByJoystick(PointerDrag drag)
        {
            var controls = tuning.Controls;
            if (drag.Id != joystickDragId) {
                joystickDragId = drag.Id;
                joystickCenter = drag.Start;
            }

            var radius = controls.JoystickRadius * Mathf.Min(Screen.width, Screen.height);
            var offset = drag.Position - joystickCenter;
            if (offset.magnitude > radius) {
                joystickCenter = drag.Position - offset.normalized * radius;
                offset = drag.Position - joystickCenter;
            }

            var stick = offset / radius;
            var throttle = Mathf.InverseLerp(controls.JoystickDeadZone, 1f, stick.magnitude);
            movement.Drive(arenaCamera.ScreenToGroundDirection(stick), throttle);
        }
    }
}
