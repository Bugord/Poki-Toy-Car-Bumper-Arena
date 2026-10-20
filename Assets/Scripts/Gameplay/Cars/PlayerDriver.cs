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
    /// (<see cref="GameTuning.ControlSettings.TouchSteering"/>). The joystick is drawn under the finger while it
    /// steers (<see cref="IOnScreenControls.ShowJoystick"/>), so the player sees where its middle is and how hard they
    /// push.</item>
    /// </list>
    /// A drag wins over the keys while it lasts. Boost (Space, the right mouse button, a gamepad's south button or the
    /// boost button on a touch screen) fires the car's boost towards where it is steered. Once the round is over the
    /// car rolls to a stop, so a click on the result screen does not steer it, and boost does nothing.
    /// Before the round begins it only watches for the player's first steer or boost (<see cref="HasActed"/>), which the
    /// first round of a visit waits for.
    /// Near the edge it takes away the part of the steer that points off the arena, as much as the round's
    /// <see cref="RoundDifficulty.EdgeAssist"/> says, so a new player who steers too far stops at the edge instead of
    /// driving off it, and a boost there does not throw it off either; a hit still pushes the car off.
    /// It also notes how the player plays the round (<see cref="FirstInput"/>, <see cref="FirstMoveTime"/>,
    /// <see cref="Boosts"/>), so the analytics can tell a player who never found the controls from one who was bored.
    /// </summary>
    public sealed class PlayerDriver : ITickable, IInitializable, IDisposable
    {
        private readonly IInputService input;
        private readonly GameTuning tuning;
        private readonly ArenaCamera arenaCamera;
        private readonly ArenaRound round;
        private readonly IOnScreenControls onScreenControls;
        private readonly RoundDifficulty difficulty;

        // The joystick's centre: where the finger touched, pulled along when the finger goes past the radius.
        private int joystickDragId;
        private Vector2 joystickCenter;

        // Where the player last steered, before the edge assist; zero when they did not.
        private Vector3 steer;

        private bool isDriving;
        private bool isJoystickShown;

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

        public PlayerDriver(IInputService input, GameTuning tuning, ArenaCamera arenaCamera, ArenaRound round,
            IOnScreenControls onScreenControls, RoundDifficulty difficulty)
        {
            this.input = input;
            this.tuning = tuning;
            this.arenaCamera = arenaCamera;
            this.round = round;
            this.onScreenControls = onScreenControls;
            this.difficulty = difficulty;
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
            // The HUD outlives the arena: a joystick left shown would stay on the next round's screen.
            SetJoystickShown(false);
        }

        public void Tick()
        {
            SetJoystickShown(Steer());
        }

        /// <returns>True while a finger steers by the joystick, which is drawn under it then.</returns>
        private bool Steer()
        {
            if (IsWaiting) {
                WatchForFirstInput();
                return false;
            }

            var car = round.Player;
            if (!round.IsRunning || car.IsOut) {
                if (isDriving) {
                    isDriving = false;
                    car.Movement.Drive(Vector3.zero, 0f);
                }

                return false;
            }

            isDriving = true;
            if (input.TryGetDrag(out var drag)) {
                if (drag.IsMouse || tuning.Controls.TouchSteering == GameTuning.TouchSteering.FollowFinger) {
                    FollowPointer(car, drag.Position);
                    return false;
                }

                SteerByJoystick(car, drag);
                return true;
            }

            var move = Vector2.ClampMagnitude(input.CurrentMoveInput, 1f);
            Drive(car, arenaCamera.ScreenToGroundDirection(move), move.magnitude);
            return false;
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
            if (round.IsRunning && !car.IsOut && TryBoost(car)) {
                Boosts++;
            }
        }

        /// <summary>
        /// Fires the car's boost, but near the edge without the part of it that points off the arena, by the round's
        /// edge assist: a boost carries the car far past where the steer could stop it, so one fired off the edge
        /// would undo the assist. One left pointing mostly off is not fired, and its cooldown is kept.
        /// </summary>
        private bool TryBoost(Car car)
        {
            var outward = Outward(car, out var share);
            if (difficulty.EdgeAssist > 0f && share > 0f) {
                // Where the car would boost: as it is driven, or along its nose when it is not.
                var way = steer != Vector3.zero ? steer : car.transform.forward;
                way.y = 0f;
                way.Normalize();
                var off = Vector3.Dot(way, outward);
                if (off > 0f) {
                    way -= outward * (off * difficulty.EdgeAssist);
                    if (way.magnitude < 0.3f) {
                        return false;
                    }

                    car.Movement.Drive(way, 1f);
                }
            }

            return car.Movement.TryBoost();
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
                Drive(car, Vector3.zero, 0f);
                return;
            }

            var controls = tuning.Controls;
            var offset = target - car.Position;
            offset.y = 0f;
            var throttle = Mathf.InverseLerp(controls.PointerStopRadius, controls.PointerSlowRadius, offset.magnitude);
            Drive(car, offset, throttle);
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
            Drive(car, arenaCamera.ScreenToGroundDirection(stick), throttle);
            onScreenControls.ShowJoystick(joystickCenter, joystickCenter + offset, radius);
        }

        /// <summary>
        /// Drives the car, less off the arena the closer it is to the edge, by the round's edge assist: what is left is
        /// the steer along the edge, at as much throttle as it keeps of the steer, so steering straight off stops the
        /// car and steering past the edge slides it along.
        /// </summary>
        private void Drive(Car car, Vector3 heading, float throttle)
        {
            heading.y = 0f;
            steer = throttle > 0f ? heading : Vector3.zero;
            if (difficulty.EdgeAssist > 0f && heading.sqrMagnitude > 0.0001f) {
                var outward = Outward(car, out var share);
                share *= difficulty.EdgeAssist;
                heading.Normalize();
                var off = Vector3.Dot(heading, outward);
                if (off > 0f && share > 0f) {
                    heading -= outward * (off * share);
                    throttle *= heading.magnitude;
                }
            }

            car.Movement.Drive(heading, throttle);
        }

        /// <returns>The way off the arena from the car, flat, or zero at the very middle.</returns>
        /// <param name="share">How near the edge the car is for the edge assist: 0 farther in than
        /// <see cref="GameTuning.ControlSettings.EdgeAssistMargin"/>, 1 at
        /// <see cref="GameTuning.ControlSettings.EdgeAssistStop"/> and beyond.</param>
        private Vector3 Outward(Car car, out float share)
        {
            var position = car.Position;
            position.y = 0f;
            var fromMiddle = position.magnitude;
            if (fromMiddle < 0.01f) {
                share = 0f;
                return Vector3.zero;
            }

            var radius = round.ArenaRadius;
            var controls = tuning.Controls;
            share = Mathf.InverseLerp(radius - controls.EdgeAssistMargin, radius - controls.EdgeAssistStop, fromMiddle);
            return position / fromMiddle;
        }

        private void SetJoystickShown(bool isShown)
        {
            if (!isShown && isJoystickShown) {
                onScreenControls.HideJoystick();
            }

            isJoystickShown = isShown;
        }
    }
}
