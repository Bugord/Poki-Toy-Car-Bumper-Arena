using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Drives the player's car from the input while the round runs, the same way whatever the player holds:
    /// <list type="bullet">
    /// <item>the move keys or a gamepad stick point where the car should go, up being away from the camera;</item>
    /// <item>the mouse, held down, drives the car towards the cursor and stops it there;</item>
    /// <item>a finger is a joystick that appears where it touched, or drives the car towards itself like the mouse
    /// (<see cref="GameTuning.ControlSettings.TouchSteering"/>).</item>
    /// </list>
    /// A drag wins over the keys while it lasts. Once the round is over the car rolls to a stop, so a click on the
    /// result screen does not steer it.
    /// </summary>
    public sealed class PlayerDriver : ITickable
    {
        private readonly IInputService input;
        private readonly GameTuning tuning;
        private readonly ArenaCamera arenaCamera;
        private readonly ArenaRound round;

        // The joystick's centre: where the finger touched, pulled along when the finger goes past the radius.
        private int joystickDragId;
        private Vector2 joystickCenter;

        private bool isDriving;

        public PlayerDriver(IInputService input, GameTuning tuning, ArenaCamera arenaCamera, ArenaRound round)
        {
            this.input = input;
            this.tuning = tuning;
            this.arenaCamera = arenaCamera;
            this.round = round;
        }

        public void Tick()
        {
            var car = round.Player;
            if (!round.IsRunning || car.IsOut) {
                if (isDriving) {
                    isDriving = false;
                    car.Movement.Drive(Vector3.zero, 0f);
                }

                return;
            }

            isDriving = true;
            if (input.TryGetDrag(out var drag)) {
                if (drag.IsMouse || tuning.Controls.TouchSteering == GameTuning.TouchSteering.FollowFinger) {
                    FollowPointer(car, drag.Position);
                }
                else {
                    SteerByJoystick(car, drag);
                }

                return;
            }

            var move = Vector2.ClampMagnitude(input.CurrentMoveInput, 1f);
            car.Movement.Drive(arenaCamera.ScreenToGroundDirection(move), move.magnitude);
        }

        private void FollowPointer(Car car, Vector2 screenPosition)
        {
            if (!arenaCamera.TryScreenToGround(screenPosition, out var target)) {
                car.Movement.Drive(Vector3.zero, 0f);
                return;
            }

            var controls = tuning.Controls;
            var offset = target - car.Position;
            offset.y = 0f;
            var throttle = Mathf.InverseLerp(controls.PointerStopRadius, controls.PointerSlowRadius, offset.magnitude);
            car.Movement.Drive(offset, throttle);
        }

        private void SteerByJoystick(Car car, PointerDrag drag)
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
            car.Movement.Drive(arenaCamera.ScreenToGroundDirection(stick), throttle);
        }
    }
}
