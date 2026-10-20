using System;
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
    /// A drag wins over the keys while it lasts. Boost (Space, the right mouse button, a gamepad's south button or the
    /// boost button on a touch screen) fires the car's boost towards where it is steered. Once the round is over the
    /// car rolls to a stop, so a click on the result screen does not steer it, and boost does nothing.
    /// Before the round begins it only watches for the player's first steer or boost (<see cref="HasActed"/>), which the
    /// first round of a visit waits for.
    /// It also notes how the player plays the round (<see cref="FirstInput"/>, <see cref="FirstMoveTime"/>,
    /// <see cref="Boosts"/>), so the analytics can tell a player who never found the controls from one who was bored.
    /// </summary>
    public sealed class PlayerDriver : ITickable, IInitializable, IDisposable
    {
        private readonly IInputService input;
        private readonly GameTuning tuning;
        private readonly ArenaCamera arenaCamera;
        private readonly ArenaRound round;

        // The joystick's centre: where the finger touched, pulled along when the finger goes past the radius.
        private int joystickDragId;
        private Vector2 joystickCenter;

        private bool isDriving;

        // Seconds the cars have stood waiting for the player before the round began.
        private float waitTime;

        /// <summary>
        /// Whether the player has steered or pressed boost since the cars were placed: the round can begin, the player
        /// is there.
        /// </summary>
        public bool HasActed { get; private set; }

        /// <summary>
        /// What the player first steered with this round, null until they steer.
        /// </summary>
        public InputMethod? FirstInput { get; private set; }

        /// <summary>
        /// Seconds from the cars standing on the arena to the player's first steer, or a negative number until they
        /// steer: into the round, plus the wait for the player before a round that waits for one.
        /// </summary>
        public float FirstMoveTime { get; private set; } = -1f;

        /// <summary>
        /// Boosts the player's car fired this round.
        /// </summary>
        public int Boosts { get; private set; }

        public PlayerDriver(IInputService input, GameTuning tuning, ArenaCamera arenaCamera, ArenaRound round)
        {
            this.input = input;
            this.tuning = tuning;
            this.arenaCamera = arenaCamera;
            this.round = round;
        }

        public void Initialize()
        {
            input.BoostPressed += OnBoostPressed;
            input.MoveUsed += OnMoveUsed;
        }

        public void Dispose()
        {
            input.MoveUsed -= OnMoveUsed;
            input.BoostPressed -= OnBoostPressed;
        }

        public void Tick()
        {
            if (IsWaiting) {
                WatchForFirstInput();
                return;
            }

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

        // A press while the boost cools down is dropped, not kept for later: a boost that fires on its own a moment
        // after the press would throw the car somewhere the player no longer means.
        // The press that starts the round only starts it: a boost before the round would throw the car off its place.
        private void OnBoostPressed()
        {
            if (IsWaiting) {
                HasActed = true;
                return;
            }

            var car = round.Player;
            if (round.IsRunning && !car.IsOut && car.Movement.TryBoost()) {
                Boosts++;
            }
        }

        // A steer that starts the round counts as the round's first, at its very start.
        private void OnMoveUsed(InputMethod method)
        {
            if (!round.IsPlaced || round.IsFinished || round.Player.IsOut) {
                return;
            }

            HasActed = true;
            if (FirstInput == null) {
                FirstInput = method;
                FirstMoveTime = waitTime + round.Elapsed;
            }
        }

        private bool IsWaiting => round.IsPlaced && !round.HasBegun;

        /// <summary>
        /// Notices the player's first steer while the cars wait: a key or stick held, or a finger or the mouse that
        /// comes down (which also reports it as <see cref="IInputService.MoveUsed"/>). The car is not driven yet.
        /// </summary>
        private void WatchForFirstInput()
        {
            waitTime += Time.deltaTime;
            if (input.TryGetDrag(out _) || input.CurrentMoveInput != Vector2.zero) {
                HasActed = true;
            }
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
